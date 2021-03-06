"""Contains full preprocessing pipelines for PTA-stained µCT reconstructions."""

import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as patches
import cv2

from tqdm.auto import tqdm

from components.utilities.misc import print_orthogonal, otsu_threshold
from components.utilities.load_write import load_bbox, save
from components.utilities.VTKFunctions import render_volume
from components.processing.rotations import orient
from components.processing.segmentation_pipelines import segmentation_cntk, segmentation_kmeans, segmentation_pytorch, \
    segmentation_unet
from components.processing.extract_volume import get_interface, deep_depth, mean_std


def pipeline_subvolume_mean_std(args, sample):
    """Calculates volume and calls subvolume or mean + std pipeline

    1. Loads µCT stack

    2. Orients sample

    3. Crops sample edges

    4. Calls mean+std pipeline (or processes subvolumes individually).

    Parameters
    ----------
    sample : str
        Sample name.
    args : Namespace
        Namespace containing processing arguments:
        data_path = Directory containing µCT datasets.
        save_image_path = Path for saving processed datasets.
        rotation = Selected rotation method. See rotations/orient.
        size = Dictionary including saved VOI dimensions. See extract_volume.
        size_wide = Different width for edge crop. Used for Test set 1.
        crop_method = Method for finding sample center.
        n_jobs = Number of parallel workers.
    render : bool
        Choice whether to save render images of the processed sample.

    """

    # 1. Load sample
    # Unpack paths
    save_path = args.save_image_path
    render = args.render
    print('Sample name: ' + sample)
    data, bounds = load_bbox(str(args.data_path), args.n_jobs)
    print_orthogonal(data, savepath=str(save_path / "Images" / (sample + "_large.png")))
    if render:
        render_volume(data, str(save_path / "Images" / (sample + "_render_large.png")))

    # 2. Orient array
    print('2. Orient sample')
    if data.shape[0] * data.shape[1] * data.shape[2] < 3e9:  # Orient samples > 3GB
        data, angles = orient(data, bounds, args.rotation)
        print_orthogonal(data, savepath=str(save_path / "Images" / (sample + "_orient.png")))

    # 3. Crop and flip volume
    print('3. Crop and flip center volume:')
    data, _ = crop_center(data, args.size['width'], args.size['width'], method=args.crop_method)  # crop data
    print_orthogonal(data, savepath=str(save_path / "Images" / (sample + "_input.png")))
    if render:
        render_volume(data, str(save_path / "Images" / (sample + "_render_input.png")))

    # Save crop data
    (save_path / 'Cropped').mkdir(exist_ok=True)
    save(str(save_path / 'Cropped' / sample), sample, data)

    # Different pipeline for large dataset
    if args.n_subvolumes > 1:  # Segment and calculate each subvolume individually
        create_subvolumes(data, sample, args)
    else:  # Calculate
        pipeline_mean_std(str(save_path / 'Cropped' / sample), args, sample, data=data)


def pipeline_mean_std(image_path, args, sample='', mask_path=None, data=None):
    """Runs full processing pipeline on single function. No possibility for subvolumes. Used in run_mean_std."""

    # 1. Load sample
    save_path = args.save_image_path
    save_path.mkdir(exist_ok=True)
    if data is None:
        print('1. Load sample')
        data, bounds = load_bbox(image_path, n_jobs=args.n_jobs)
        print_orthogonal(data, savepath=str(save_path / 'Images' / (sample + '_input.png')))
        render_volume(data, savepath=str(save_path / 'Images' / (sample + '_render_input.png')))
    if mask_path is not None:
        mask, _ = load_bbox(mask_path)
        print_orthogonal(mask)

    # 2. Segment BCI mask
    if args.segmentation is 'torch' or args.segmentation is 'kmeans':
        # Bottom offset
        if data.shape[2] < 1000:
            offset = 0
        elif 1000 <= data.shape[2] < 1600:
            offset = 20
        else:
            offset = 50
        # Pytorch segmentation
        if args.segmentation is 'torch':
            cropsize = 512
            mask = segmentation_pytorch(data, args.model_path, args.snapshots, cropsize, offset)
        # K-means segmentation
        elif args.segmentation is 'kmeans':
            mask = segmentation_kmeans(data, n_clusters=3, offset=offset, n_jobs=args.n_jobs)
        else:
            raise Exception('Invalid segmentation selection!')
    elif args.segmentation is 'unet':
        #args.mask_path.mkdir(exist_ok=True)
        mask = segmentation_unet(data, args, sample)
    # CNTK segmentation
    else:
        mask = segmentation_cntk(data, args.model_path)
    print_orthogonal(mask * data, savepath=str(save_path / 'Images' / (sample + '_mask.png')))
    render_volume((mask > args.threshold) * data, savepath=str(save_path / 'Images' / (sample + '_render_mask.png')))
    save(str(save_path / 'Masks' / sample), sample, mask)

    # Crop
    crop = args.size['crop']
    data = data[crop:-crop, crop:-crop, :]
    mask = mask[crop:-crop, crop:-crop, :]
    size_temp = args.size.copy()
    size_temp['width'] = args.size['width'] - 2 * crop

    # Calculate cartilage depth
    data = np.flip(data, 2)
    mask = np.flip(mask, 2)  # flip to begin indexing from surface
    dist = deep_depth(data, mask)
    size_temp['deep'] = (0.6 * dist).astype('int')
    print('Automatically setting deep voi depth to {0}'.format((0.6 * dist).astype('int')))
#
    # 4. Get VOIs
    print('4. Get interface coordinates:')
    surf_voi, deep_voi, calc_voi, otsu_thresh = get_interface(data, size_temp, (mask > args.threshold), n_jobs=args.n_jobs)
    # Show and save results
    print_orthogonal(surf_voi, savepath=str(save_path / "Images" / (sample + "_surface.png")))
    print_orthogonal(deep_voi, savepath=str(save_path / "Images" / (sample + "_deep.png")))
    print_orthogonal(calc_voi, savepath=str(save_path / "Images" / (sample + "_cc.png")))
    render_volume(np.flip(surf_voi, 2), str(save_path / "Images" / (sample + "_render_surface.png")))
    render_volume(np.flip(deep_voi, 2), str(save_path / "Images" / (sample + "_render_deep.png")))
    render_volume(calc_voi, str(save_path / "Images" / (sample + "_render_cc.png")))

    # 5. Calculate mean and std
    print('5. Save mean and std images')
    mean_std(surf_voi, str(save_path), sample, deep_voi, calc_voi, otsu_thresh)
    if size_temp['surface'] > 25:
        mean_std(surf_voi[:, :, :surf_voi.shape[2] // 2], str(save_path), sample + '_25', deep_voi,
                 calc_voi[:, :, :calc_voi.shape[2] // 2], otsu_thresh)
        mean_std(surf_voi[:, :, surf_voi.shape[2] // 2:], str(save_path), sample + '_25_backup', deep_voi,
                 calc_voi[:, :, calc_voi.shape[2] // 2:], otsu_thresh)


def pipeline_subvolume(args, sample, individual=False, save_data=True, render=False, use_wide=False):
    """Pipeline for saving subvolumes. Used in run_subvolume script."""
    # 1. Load sample
    # Unpack paths
    save_path = args.save_image_path
    print('Sample name: ' + sample)
    data, bounds = load_bbox(str(args.data_path / sample), args.n_jobs)
    print_orthogonal(data, savepath=str(save_path / "Images" / (sample + "_input.png")))
    if render:
        render_volume(data, str(save_path / "Images" / (sample + "_input_render.png")))

    # 2. Orient array
    data, angles = orient(data, bounds, args.rotation)
    print_orthogonal(data, savepath=str(save_path / "Images" / (sample + "_orient.png")))

    # 3. Crop and flip volume
    if use_wide:
        wide = args.size_wide
    else:
        wide = args.size['width']
    data, crop = crop_center(data, args.size['width'], wide, method=args.crop_method)  # crop data
    print_orthogonal(data , savepath=str(save_path / "Images" / (sample + "_orient_cropped.png")))
    if render:
        render_volume(data, str(save_path / "Images" / (sample + "_orient_cropped_render.png")))

    # Different pipeline for large dataset
    if data.shape[0] > 799 and data.shape[1] > 799 and save_data:
        create_subvolumes(data, sample, args)
        return

    # Save crop data
    if data.shape[1] > args.size['width'] and save_data:
        save(save_path + '/' + sample + '_sub1', sample + '_sub1_', data[:, :args.size['width'], :])
        save(save_path + '/' + sample + '_sub2', sample + '_sub2_', data[:, -args.size['width']:, :])
    elif save_data:
        save(save_path + '/' + sample, sample, data)
    else:
        return data


def create_subvolumes(data, sample, args, method='calculate', show=False):
    """Either saves subvolumes or calculates mean + std from them. Takes edge cropped sample as input.
    Not necessary, since subimages can be calculated from mean+std images."""

    dims = [448, data.shape[2] // 2]
    print_orthogonal(data)

    # Loop for 9 subvolumes
    for n in range(3):
        for nn in range(3):
            # Selection
            x1 = n * 200
            y1 = nn * 200

            # Plot selection
            if show:
                fig, ax = plt.subplots(1)
                ax.imshow(data[:, :, dims[1]])
                rect = patches.Rectangle((x1, y1), dims[0], dims[0], linewidth=3, edgecolor='r', facecolor='none')
                ax.add_patch(rect)
                plt.show()

            # Crop subvolume
            subdata = data[x1:x1 + dims[0], y1:y1 + dims[0], :]

            # Save data
            subsample = sample + "_sub" + str(n) + str(nn) + '_'
            if method == 'save':
                subpath = str(args.save_image_path / (sample + "_sub" + str(n) + str(nn)))
                save(subpath, subsample, subdata)
            else:
                pipeline_mean_std(subdata, subsample, args)


def crop_center(data, sizex=400, sizey=400, method='cm'):
    """Performs edge crop for input data.

    Parameters
    ----------
    data : ndarray (3-dimensional)
        Input data for edge cropping.
    sizex : int
        Width of edge cropped sample.
    sizey : int
        Height of edge cropped sample.
    method : str
        Method for finding sample center. Choices = "cm", "mass".
        Defaults to center moment but center of mass can be also used.

    Returns
    -------
    Edge cropped data, cropping coordinates.
    """
    dims = np.shape(data)
    center = np.zeros(2)

    # Calculate center moment
    crop = dims[2] // 2
    sumarray = data[:, :, :crop].sum(2).astype(float)
    # sumarray = data.sum(2).astype(float)
    sumarray -= sumarray.min()
    sumarray /= sumarray.max()
    sumarray = sumarray > 0.1
    sumarray = sumarray.astype(np.uint8) * 255
    cnts, _ = cv2.findContours(sumarray, 1, 2)
    cnts.sort(key=cv2.contourArea)
    center_moment = cv2.moments(cnts[-1])
    cy = int(center_moment["m10"] / center_moment["m00"])
    cx = int(center_moment["m01"] / center_moment["m00"])

    # Calculate center pixel
    mask, val = otsu_threshold(data[:, :, :crop])
    # mask, val = otsuThreshold(data)
    sumarray = mask.sum(2)
    n = 0
    for i in range(dims[0]):
        for j in range(dims[1]):
            if sumarray[i, j] > 0:
                center[0] += i * sumarray[i, j]
                center[1] += j * sumarray[i, j]
                n += 1

    # Large dataset (at least 4mm sample with 2.75µm res)
    if dims[0] > 1300 and dims[1] > 1300:
        print('Large sample')
        sizex = 848  # set larger size
        sizey = 848  # set larger size

    # Cropping coordinates
    center[0] = np.uint(center[0] / np.sum(sumarray))
    center[1] = np.uint(center[1] / np.sum(sumarray))
    x1 = np.uint(center[0] - sizex / 2)
    x2 = np.uint(center[0] + sizex / 2)
    y1 = np.uint(center[1] - sizey / 2)
    y2 = np.uint(center[1] + sizey / 2)
    xx1 = np.uint(cx - sizex / 2)
    xx2 = np.uint(cx + sizex / 2)
    yy1 = np.uint(cy - sizey / 2)
    yy2 = np.uint(cy + sizey / 2)

    # Visualize crops
    fig, ax = plt.subplots(1)
    ax.imshow(sumarray, cmap='bone')
    fig.suptitle('Sum image along z-axis\nMoment (green): x = {0}, y = {1}\nCenter of mass (red): x = {2}, y = {3}'
                 .format(cx, cy, center[0], center[1]))
    rect = patches.Rectangle((y1, x1), sizey, sizex, linewidth=3, edgecolor='r', facecolor='none')
    rect2 = patches.Rectangle((yy1, xx1), sizey, sizex, linewidth=3, edgecolor='g', facecolor='none')
    ax.add_patch(rect)
    ax.add_patch(rect2)
    plt.show()

    # Select method
    if method == 'mass':
        return data[x1:x2, y1:y2, :], (x1, x2, y1, y2)
    else:
        return data[xx1:xx2, yy1:yy2, :], (xx1, xx2, yy1, yy2)
