"""Contains arguments used for preprocessing pipelines."""

from argparse import ArgumentParser
from pathlib import Path


def return_args(root, choice):
    """Returns arguments needed in preprocessing pipeline.
    Sample rotation options: 1=Bounding box, 2=PCA, 3=Gradient descent, 4=Average of the methods, 0=No rotation.
    VOI options: width=ROI width, surface, deep, calcified=corresponding VOI depth, offset=VOI offset from BCI."""

    parser = ArgumentParser()
    parser.add_argument('--data_path', type=Path, default=root)
    parser.add_argument('--save_image_path', type=Path, default=Path(root) / ('MeanStd_' + choice + '_augmented'))
    parser.add_argument('--model_path', type=Path, default=root + r'/components/segmentation/unet/')
    parser.add_argument('--snapshots', type=Path, default=root + r'/components/segmentation/2018_12_03_15_25/')
    parser.add_argument('--rotation', choices=[0, 1, 2, 3, 4], type=int, default=1)
    parser.add_argument('--crop_method', choices=['moment', 'mass'], type=str, default='moment')
    parser.add_argument('--size', type=dict, default=dict(width=448, surface=25, deep=150, calcified=50, offset=10, crop=24))
    parser.add_argument('--size_wide', type=int, default=640)
    parser.add_argument('--segmentation', type=str, choices=['torch', 'kmeans', 'cntk'], default='kmeans')
    parser.add_argument('--n_subvolumes', type=int, default=1)
    parser.add_argument('--subvolumes_x', type=int, default=1)
    parser.add_argument('--subvolumes_y', type=int, default=1)
    parser.add_argument('--n_jobs', type=int, default=10)
    parser.add_argument('--render', type=bool, default=True)
    parser.add_argument('--GUI', type=bool, default=False)
    return parser.parse_args()
