"""Loading/writing functions

Contains functions used to writing and reading image stacks, images, h5 datasets, excel tables and binary .dat files.
"""

import numpy as np
from struct import pack, unpack  # Binary writing
import pandas as pd
import os
import cv2
import h5py
from tqdm import tqdm
from joblib import Parallel, delayed
from components.utilities.misc import bounding_box


def find_image_paths(path, files):
    """Finds image paths from given path."""
    # Loop for all files
    file_paths = []
    for k in range(len(files)):
        # Data path
        try:
            os.listdir(path + "\\" + files[k] + "\\" + "Registration")
            file_paths.append(path + "\\" + files[k] + "\\" + "Registration")
        except FileNotFoundError:  # Case: Unusable folder
            try:
                os.listdir(path + "\\" + files[k] + "\\" + files[k] + "\\" + "Registration")
                file_paths.append(path + "\\" + files[k] + "\\" + files[k] + "\\" + "Registration")
            except FileNotFoundError:
                try:
                    os.listdir(path + "\\" + files[k] + '\\' + files[k] + '_Rec')
                    file_paths.append(path + "\\" + files[k] + '\\' + files[k] + '_Rec')
                except FileNotFoundError:
                    try:
                        pth = os.path.basename(path + '\\' + files[k])
                        file_paths.append(pth)
                    except FileNotFoundError:  # Case: Unusable folder
                        print('Skipping folder {0}'.format(files[k]))
                        continue
    return file_paths


def load(path, axis=(1, 2, 0), n_jobs=12):
    """
    Loads an image stack as numpy array.

    Parameters
    ----------
    path : str
        Path to image stack.
    axis : tuple
        Order of loaded sample axes.
    n_jobs : int
        Number of parallel workers. Check N of CPU cores.
    Returns
    -------
    Loaded stack as 3D numpy array.
    """
    files = os.listdir(path)
    files.sort()
    # Exclude extra files
    newlist = []
    for file in files:
        if file.endswith('.png') or file.endswith('.bmp') or file.endswith('.tif'):
            try:
                int(file[-7:-4])
                newlist.append(file)
            except ValueError:
                continue
    files = newlist[:]  # replace list
    # Load data and get bounding box
    data = Parallel(n_jobs=n_jobs)(delayed(read_image)(path, file) for file in tqdm(files, 'Loading'))
    if axis != (0, 1, 2):
        return np.transpose(np.array(data), axis)

    return np.array(data)


def load_bbox(path, n_jobs=12):
    """
    Loads an image stack as numpy array. Calculates bounding box (rectangle) for each loaded image.

    Parameters
    ----------
    path : str
        Path to image stack.
    n_jobs : int
        Number of parallel workers. Check N of CPU cores.
    Returns
    -------
    Loaded stack as 3D numpy array. Coordinates of image bounding boxes.
    """
    files = os.listdir(path)
    files.sort()
    # Exclude extra files
    newlist = []
    for file in files:
        if file.endswith('.png') or file.endswith('.bmp') or file.endswith('.tif'):
            try:
                int(file[-7:-4])
                newlist.append(file)
            except ValueError:
                continue
    files = newlist[:]  # replace list
    # Load data and get bounding box
    data = Parallel(n_jobs=n_jobs)(delayed(read_image)(path, file) for file in files)
    data = np.transpose(np.array(data), (1, 2, 0))
    angles = Parallel(n_jobs=n_jobs)(delayed(read_image_bbox)(path, file) for file in files)
    angles = np.array(angles)

    return data, (angles[:, 0], angles[:, 1], angles[:, 2], angles[:, 3])


def read_image(path, file):
    """Reads image from given path."""
    # Image
    f = os.path.join(path, file)
    image = cv2.imread(f, cv2.IMREAD_GRAYSCALE)
    return image


def read_image_bbox(path, file):
    """Reads image from given path and calculates bounding box for the sample."""
    # Image
    f = os.path.join(path, file)
    image = cv2.imread(f, cv2.IMREAD_GRAYSCALE)

    # Bounding box
    x1, x2, y1, y2 = bounding_box(image)
    return [x1, x2, y1, y2]


def save(path, file_name, data, n_jobs=12):
    """
    Save a volumetric 3D dataset in given directory.

    Parameters
    ----------
    path : str
        Directory for dataset.
    file_name : str
        Prefix for the image filenames.
    data : 3D numpy array
        Volumetric data to be saved.
    n_jobs : int
        Number of parallel workers. Check N of CPU cores.
    """
    if not os.path.exists(path):
        os.makedirs(path, exist_ok=True)
    nfiles = np.shape(data)[2]

    if data[0, 0, 0].dtype is bool:
        data = data * 255

    # Parallel saving (nonparallel if n_jobs = 1)
    Parallel(n_jobs=n_jobs)(delayed(cv2.imwrite)
                            (path + '/' + file_name + str(k).zfill(8) + '.png', data[:, :, k].astype(np.uint8))
                            for k in tqdm(range(nfiles), 'Saving dataset'))


def load_binary(path, datatype=np.int32):
    """Loads binary .dat file including an array as given datatype."""
    if datatype == np.float64:
        byte_array = np.fromfile(path, dtype=np.int64)  # read everything as int32
    else:
        byte_array = np.fromfile(path, dtype=np.int32)  # read everything as int32
    w = byte_array[0]
    h = int((byte_array.size - 1) / w)
    with open(path, "rb") as f:  # open to read binary file
        if datatype == np.float64:
            f.seek(8)  # skip first integer (width)
        else:
            f.seek(4)  # skip first integer (width)
        array = np.zeros((w, h))
        for i in range(w):
            for j in range(h):
                if datatype == np.int32:
                    array[i, j] = unpack('<i', f.read(4))[0]
                    # when reading byte by byte (struct),
                    # data type can be defined with every byte
                elif datatype == np.float32:
                    array[i, j] = unpack('<f', f.read(4))[0]
                elif datatype == np.float64:
                    array[i, j] = unpack('<d', f.read(8))[0]
        return array


def load_binary_weights(path):
    """Loads linear regression weights and PCA variables from binary .dat file."""
    # bytesarray64 = np.fromfile(path, dtype=np.int64)  # read everything as int64
    bytesarray32 = np.fromfile(path, dtype=np.int32)  # read everything as int32
    w = bytesarray32[0]
    ncomp = bytesarray32[1]
    with open(path, "rb") as f:  # open to read binary file
        f.seek(8)  # skip first two integers (width)
        eigenvec = np.zeros((w, ncomp))
        for i in range(w):
            for j in range(ncomp):
                eigenvec[i, j] = unpack('<f', f.read(4))[0]
        singularvalues = np.zeros(ncomp)
        for i in range(ncomp):
            singularvalues[i] = unpack('<f', f.read(4))[0]
        weights = np.zeros(ncomp)
        for i in range(ncomp):
            weights[i] = unpack('<d', f.read(8))[0]
        weights_log = np.zeros(ncomp)
        for i in range(ncomp):
            weights_log[i] = unpack('<d', f.read(8))[0]
        mean = np.zeros(w)
        for i in range(w):
            mean[i] = unpack('<d', f.read(8))[0]
        intercept_lin = unpack('<d', f.read(8))[0]
        intercept_log = unpack('<d', f.read(8))[0]
        return w, ncomp, eigenvec, singularvalues, weights, weights_log, mean, [intercept_lin, intercept_log]


def write_binary_weights(path, ncomp, eigenvectors, singularvalues, weights, weights_log, mean, intercepts):
    """Saves linear regression weights and PCA variables into a binary .dat file."""
    # Input eigenvectors in shape: components, features
    with open(path, "wb") as f:
        f.write(pack('<i', eigenvectors.shape[1]))  # Width
        f.write(pack('<i', ncomp))  # Number of components
        # Eigenvectors
        for j in range(eigenvectors.shape[1]):
            for i in range(eigenvectors.shape[0]):  # Write row by row, component at a time
                f.write(pack('<f', eigenvectors[i, j]))
        # Singular values
        for i in range(singularvalues.shape[0]):
            f.write(pack('<f', singularvalues[i]))
        # Weights
        for i in range(weights.shape[0]):
            f.write(pack('<d', weights[i]))
        for i in range(weights_log.shape[0]):
            f.write(pack('<d', weights_log[i]))
        for i in range(mean.shape[0]):
            f.write(pack('<d', mean[i]))
        f.write(pack('<d', intercepts[0]))  # Linear intercept
        f.write(pack('<d', intercepts[1]))  # Logistic intercept


def write_binary_image(path, image, dtype='int'):
    """Saves a numpy array as binary .dat file in given datatype."""
    with open(path, "wb") as f:
        f.write(pack('<i', image.shape[0]))  # Width
        # Image values as float
        for i in range(image.shape[0]):
            for j in range(image.shape[1]):
                if dtype == 'float':
                    f.write(pack('<f', image[i, j]))
                if dtype == 'double':
                    f.write(pack('<d', image[i, j]))
                if dtype == 'int':
                    f.write(pack('<i', image[i, j]))


def load_vois_h5(pth, sample):
    """Loads results of Preprocess pipeline (mean + std images from each zone)."""
    # Image loading
    h5 = h5py.File(os.path.join(pth, sample), 'r')
    surf = h5['surf'][:]
    deep = h5['deep'][:]
    calc = h5['calc'][:]
    h5.close()
    return surf, deep, calc


def load_excel(path, titles=None):
    """Loads data from excel table. If list of titles is given, loads only selected columns."""
    # Load array and header
    array = []
    if titles is not None:
        file = pd.read_excel(path)
        for key in titles:
            if array is None:
                array = np.array(file[key])
            else:
                col = np.array(file[key])
                array.append(col)
        df = pd.DataFrame(file)
        header = df.values[:, 0].astype('str')
    else:
        file = pd.read_excel(path, skiprows=0, skipcolumns=0)
        df = pd.DataFrame(file)
        header = df.columns.values.astype('str')[1:]
        array = df.values.astype('float')[:, 1:]

    return np.array(array), header


def save_excel(array, save_path, files=None):
    """Save array as excel file."""
    if not os.path.exists(save_path.rsplit('/', 1)[0]):
        os.makedirs(save_path.rsplit('/', 1)[0], exist_ok=True)
    writer = pd.ExcelWriter(save_path)
    if files is not None:
        try:
            df1 = pd.DataFrame(array, columns=files)
        except ValueError:
            df1 = pd.DataFrame(array.T, columns=files)
    elif isinstance(array, dict):
        df1 = pd.DataFrame(array, index=[0])
    else:
        df1 = pd.DataFrame(array)
    df1.to_excel(writer)
    writer.save()
