# coding: utf-8
# author: luuil@outlook.com

r"""Util functions."""

from __future__ import print_function

import os
import numpy as np


def is_exists(path):
    if not os.path.exists(path):
        print('Not exists: {}'.format(path))
        return False
    return True


def maybe_create_directory(dirname):
    """Check directory exists or create it."""
    if not os.path.exists(dirname):
        os.makedirs(dirname)


def maybe_download(url, dst_dir):
    """Download file.

    If the file not exist then download it.

    Args:
        url: Web location of the file.

    Returns:
        path to downloaded file.
    """
    import urllib.request
    maybe_create_directory(dst_dir)
    filename = url.split('/')[-1]
    filepath = os.path.join(dst_dir, filename)
    if not os.path.exists(filepath):
        def _progress(count, block_size, total_size):
            sys.stdout.write('\r>> Downloading %s %.1f%%' %
                             (filename,
                              float(count * block_size) / float(total_size) * 100.0))
            sys.stdout.flush()

        filepath, _ = urllib.request.urlretrieve(url, filepath, _progress)
        print()
        statinfo = os.stat(filepath)
        print('Successfully downloaded:', filename, statinfo.st_size, 'bytes.')
    return filepath


def maybe_download_and_extract(url, dst_dir):
    """Download and extract model tar file.

    If the pretrained model we're using doesn't already exist, this function
    downloads it from the TensorFlow.org website and unpacks it into a directory.

    Args:
      url: Web location of the tar file containing the pretrained model.
      dst_dir: Destination directory to save downloaded and extracted file.

    Returns:
      None.
    """
    import tarfile
    filepath =maybe_download(url, dst_dir)
    tarfile.open(filepath, 'r:gz').extractall(dst_dir)


def urban_labels(fpaths):
    """urban sound dataset labels."""
    urban_label = lambda path: int(os.path.split(path)[-1].split('-')[1])
    return [urban_label(p) for p in fpaths]


def train_test_val_split(X, Y, split=(0.2, 0.1), shuffle=True):
    """Split dataset into train/val/test subsets by 70:20:10(default).
    
    Args:
      X: List of data.
      Y: List of labels corresponding to data.
      split: Tuple of split ratio in `test:val` order.
      shuffle: Bool of shuffle or not.
      
    Returns:
      Three dataset in `train:test:val` order.
    """
    from sklearn.model_selection import train_test_split
    assert len(X) == len(Y), 'The length of X and Y must be consistent.'
    X_train, X_test_val, Y_train, Y_test_val = train_test_split(X, Y, 
        test_size=(split[0]+split[1]), shuffle=shuffle)
    X_test, X_val, Y_test, Y_val = train_test_split(X_test_val, Y_test_val, 
        test_size=split[1]/(split[0]+split[1]), shuffle=False)
    return (X_train, Y_train), (X_test, Y_test), (X_val, Y_val)


def calculate_flops(graph):
    """Calculate floating point operations with specified `graph`.

    Print to stdout an analysis of the number of floating point operations in the
    model broken down by individual operations.
    """
    tf.profiler.profile(graph=graph,
        options=tf.profiler.ProfileOptionBuilder.float_operation(), cmd='scope')


if __name__ == '__main__':

    X, y = np.arange(20).reshape((10, 2)), np.arange(10)
    print(X)
    print(y)
    tr, te, vl = train_test_val_split(X, y, shuffle=True)
    print(tr)
    print(te)
    print(vl)

    import sys
    sys.path.append('..')
    sys.path.append('../vggish')
    from audio_model import define_audio_slim
    from vggish_slim import define_vggish_slim
    import tensorflow as tf
    with tf.Graph().as_default() as graph:
        # define_vggish_slim(training=False)
        define_audio_slim(training=False)
        calculate_flops(graph)
    pass