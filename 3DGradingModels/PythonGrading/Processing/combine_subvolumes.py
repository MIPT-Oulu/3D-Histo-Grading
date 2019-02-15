from time import time
import numpy as np
import matplotlib.pyplot as plt
import os
import h5py
from joblib import Parallel, delayed
from argparse import ArgumentParser
from tqdm import tqdm
from copy import deepcopy

from Utilities.load_write import load_vois_h5, save_h5
from Utilities import listbox


def pipeline(arg, selection, size=640-48, size_s=480-48):
    # Start time
    start_time = time()

    # List datasets
    files = os.listdir(arg.image_path)
    files.sort()
    # Exclude samples
    if selection is not None:
        files = [files[i] for i in selection]

    # Load and normalize images
    images_surf = (Parallel(n_jobs=args.n_jobs)(delayed(load_voi)  # Initialize
              (arg.image_path, files[i], 'surf')
              for i in tqdm(range(len(files)), desc='Loading and normalizing')))  # Iterable
    images_deep = (Parallel(n_jobs=args.n_jobs)(delayed(load_voi)  # Initialize
                                           (arg.image_path, files[i], 'deep')
                                           for i in
                                           tqdm(range(len(files)), desc='Loading and normalizing')))  # Iterable
    images_calc = (Parallel(n_jobs=args.n_jobs)(delayed(load_voi)  # Initialize
                                           (arg.image_path, files[i], 'calc')
                                           for i in
                                           tqdm(range(len(files)), desc='Loading and normalizing')))  # Iterable
    dims_l = images_surf[0].shape
    for i in range(0, len(files), 2):
        dims = images_surf[i].shape
        if dims[0] < dims_l[0]:
            s = deepcopy(size_s)
        else:
            s = deepcopy(size)

        im_surf = np.zeros((dims[0], s))
        im_surf[:, :dims[1]] = images_surf[i]
        im_surf[:, -dims[1]:] = images_surf[i + 1]
        im_deep = np.zeros((dims[0], s))
        im_deep[:, :dims[1]] = images_surf[i]
        im_deep[:, -dims[1]:] = images_surf[i + 1]
        im_calc = np.zeros((dims[0], s))
        im_calc[:, :dims[1]] = images_surf[i]
        im_calc[:, -dims[1]:] = images_surf[i + 1]

        plt.imshow(im_surf)
        plt.show()
        # Save .h5
        h5 = h5py.File(arg.save_path + "\\" + files[i][:-8] + '.h5', 'w')
        h5.create_dataset('surf', data=im_surf)
        h5.create_dataset('deep', data=im_surf)
        h5.create_dataset('calc', data=im_surf)
        h5.close()


def load_voi(path, file, grade, max_roi=400):
    # Load images
    image_surf, image_deep, image_calc = load_vois_h5(path, file)
    # Crop
    if np.shape(image_surf)[0] > max_roi:
        crop = (np.shape(image_surf)[0] - max_roi) // 2
        image_surf = image_surf[crop:-crop, crop:-crop]
    if np.shape(image_deep)[0] > max_roi:
        crop = (np.shape(image_deep)[0] - max_roi) // 2
        image_deep = image_deep[crop:-crop, crop:-crop]
    if np.shape(image_calc)[0] > max_roi:
        crop = (np.shape(image_calc)[0] - max_roi) // 2
        image_calc = image_calc[crop:-crop, crop:-crop]
    # Select VOI
    if grade[:4] == 'surf':
        image = image_surf[:]
    elif grade[:4] == 'deep':
        image = image_deep[:]
    elif grade[:4] == 'calc':
        image = image_calc[:]
    else:
        raise Exception('Check selected zone!')
    return image


if __name__ == '__main__':
    # Arguments
    parser = ArgumentParser()
    choice = 'Insaf'
    parser.add_argument('--image_path', type=str, default=r'Y:\3DHistoData\MeanStd_' + choice)  # + '_Python')
    parser.add_argument('--save_path', type=str, default=r'Y:\3DHistoData\MeanStd_' + choice + '_combined')
    parser.add_argument('--n_components', type=int, default=15)
    parser.add_argument('--n_jobs', type=int, default=12)
    parser.add_argument('--convolution', type=bool, default=False)
    parser.add_argument('--normalize_hist', type=bool, default=True)
    args = parser.parse_args()

    # Use listbox (Result is saved in listbox.file_list)
    listbox.GetFileSelection(args.image_path)

    # Call pipeline
    pipeline(args, listbox.file_list)
