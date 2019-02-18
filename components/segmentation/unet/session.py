import numpy as np
import time
import torch
from torch import optim
import os

from torch import nn
from functools import partial
from torch.utils.data import DataLoader
from tqdm import tqdm
from torchvision import transforms
from termcolor import colored

from mctseg.utils import GlobalKVS, git_info
from mctseg.unet.args import parse_args_train
from mctseg.unet.loss import BinaryDiceLoss, CombinedLoss, BCEWithLogitsLoss2d
from mctseg.unet.model import UNet
from mctseg.unet.dataset import init_train_augmentation_pipeline
from mctseg.unet.dataset import SegmentationDataset, gs2tens, apply_by_index, \
    read_gs_ocv, read_gs_mask_ocv


device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
if not torch.cuda.is_available():
    raise EnvironmentError('The code must be run on GPU.')


def init_session():
    kvs = GlobalKVS()

    # Getting the arguments
    args = parse_args_train()
    # Initializing the seeds
    torch.manual_seed(args.seed)
    torch.cuda.manual_seed(args.seed)
    np.random.seed(args.seed)
    # Creating the snapshot
    snapshot_name = time.strftime('%Y_%m_%d_%H_%M')
    os.makedirs(os.path.join(args.snapshots, snapshot_name), exist_ok=True)

    res = git_info()
    if res is not None:
        kvs.update('git branch name', res[0])
        kvs.update('git commit id', res[1])
    else:
        kvs.update('git branch name', None)
        kvs.update('git commit id', None)

    kvs.update('pytorch_version', torch.__version__)

    if torch.cuda.is_available():
        kvs.update('cuda', torch.version.cuda)
        kvs.update('gpus', torch.cuda.device_count())
    else:
        kvs.update('cuda', None)
        kvs.update('gpus', None)

    kvs.update('snapshot_name', snapshot_name)
    kvs.update('args', args)
    kvs.save_pkl(os.path.join(args.snapshots, snapshot_name, 'session.pkl'))

    return args, snapshot_name


def init_loss():
    kvs = GlobalKVS()
    class_weights = kvs['class_weights']
    if kvs['args'].n_classes == 2:
        if kvs['args'].loss == 'combined':
            return CombinedLoss([BCEWithLogitsLoss2d(), BinaryDiceLoss()])
        elif kvs['args'].loss == 'bce':
            return BCEWithLogitsLoss2d()
        elif kvs['args'].loss == 'dice':
            return BinaryDiceLoss()
        elif kvs['args'].loss == 'wbce':
            return BCEWithLogitsLoss2d(weight=class_weights)
        else:
            raise NotImplementedError
    else:
        raise NotImplementedError


def init_optimizer(net):
    kvs = GlobalKVS()
    if kvs['args'].optimizer == 'adam':
        return optim.Adam(net.parameters(), lr=kvs['args'].lr, weight_decay=kvs['args'].wd)
    elif kvs['args'].optimizer == 'sgd':
        return optim.SGD(net.parameters(), lr=kvs['args'].lr, weight_decay=kvs['args'].wd, momentum=0.9)
    else:
        raise NotImplementedError


def init_model():
    kvs = GlobalKVS()
    if kvs['args'].model == 'unet':
        net = UNet(bw=kvs['args'].bw, depth=kvs['args'].depth,
                   center_depth=kvs['args'].cdepth,
                   n_inputs=kvs['args'].n_inputs,
                   n_classes=kvs['args'].n_classes - 1,
                   activation='relu')
        if kvs['gpus'] > 1:
            net = nn.DataParallel(net).to('cuda')

        net = net.to('cuda')
    else:
        raise NotImplementedError

    return net


def init_data_processing():
    kvs = GlobalKVS()
    train_augs = init_train_augmentation_pipeline()

    dataset = SegmentationDataset(split=kvs['metadata_train'],
                                  trf=train_augs,
                                  read_img=read_gs_ocv,
                                  read_mask=read_gs_mask_ocv)

    mean_vector, std_vector, class_weights = init_mean_std(snapshots_dir=kvs['args'].snapshots,
                                                           dataset=dataset,
                                                           batch_size=kvs['args'].bs,
                                                           n_threads=kvs['args'].n_threads,
                                                           n_classes=kvs['args'].n_classes)

    norm_trf = transforms.Normalize(torch.from_numpy(mean_vector).float(),
                                    torch.from_numpy(std_vector).float())
    train_trf = transforms.Compose([
        train_augs,
        partial(apply_by_index, transform=norm_trf, idx=0)
    ])

    val_trf = transforms.Compose([
        partial(apply_by_index, transform=gs2tens, idx=[0, 1]),
        partial(apply_by_index, transform=norm_trf, idx=0)
    ])
    kvs.update('class_weights', class_weights)
    kvs.update('train_trf', train_trf)
    kvs.update('val_trf', val_trf)
    kvs.save_pkl(os.path.join(kvs['args'].snapshots, kvs['snapshot_name'], 'session.pkl'))


def init_loaders(x_train, x_val):
    kvs = GlobalKVS()
    train_dataset = SegmentationDataset(split=x_train,
                                        trf=kvs['train_trf'],
                                        read_img=read_gs_ocv,
                                        read_mask=read_gs_mask_ocv)

    val_dataset = SegmentationDataset(split=x_val,
                                      trf=kvs['val_trf'],
                                      read_img=read_gs_ocv,
                                      read_mask=read_gs_mask_ocv)

    train_loader = DataLoader(train_dataset, batch_size=kvs['args'].bs,
                              num_workers=kvs['args'].n_threads, shuffle=True,
                              drop_last=True,
                              worker_init_fn=lambda wid: np.random.seed(np.uint32(torch.initial_seed() + wid)))

    val_loader = DataLoader(val_dataset, batch_size=kvs['args'].val_bs,
                            num_workers=kvs['args'].n_threads)

    return train_loader, val_loader


def init_mean_std(snapshots_dir, dataset, batch_size, n_threads, n_classes):
    if os.path.isfile(os.path.join(snapshots_dir, 'mean_std_weights.npy')):
        tmp = np.load(os.path.join(snapshots_dir, 'mean_std_weights.npy'))
        mean_vector, std_vector, class_weights = tmp
    else:
        tmp_loader = DataLoader(dataset, batch_size=batch_size, num_workers=n_threads)
        mean_vector = None
        std_vector = None
        num_pixels = 0
        class_weights = np.zeros(n_classes)
        print(colored('==> ', 'green') + 'Calculating mean and std')
        for batch in tqdm(tmp_loader, total=len(tmp_loader)):
            imgs = batch['img']
            masks = batch['mask']
            if mean_vector is None:
                mean_vector = np.zeros(imgs.size(1))
                std_vector = np.zeros(imgs.size(1))
            for j in range(mean_vector.shape[0]):
                mean_vector[j] += imgs[:, j, :, :].mean()
                std_vector[j] += imgs[:, j, :, :].std()

            for j in range(class_weights.shape[0]):
                class_weights[j] += np.sum(masks.numpy() == j)
            num_pixels += np.prod(masks.size())

        mean_vector /= len(tmp_loader)
        std_vector /= len(tmp_loader)
        class_weights /= num_pixels
        class_weights = 1 / class_weights
        class_weights /= class_weights.max()
        np.save(os.path.join(snapshots_dir, 'mean_std_weights.npy'), [mean_vector.astype(np.float32),
                                                                      std_vector.astype(np.float32),
                                                                      class_weights.astype(np.float32)])

    return mean_vector, std_vector, class_weights


