# coding: utf-8
# author: luuil@outlook.com

r"""Records related functions."""

from __future__ import print_function

import sys
sys.path.append('..')

import tensorflow as tf

from audio_params import AUDIO_FEATURE_NAME
from audio_params import AUDIO_LABEL_NAME


def encodes_example(feature, label):
    """Encodes to TF Example
    
    Args:
      feature: feature to encode
      label: label corresponding to feature
      
    Returns:
      tf.Example object
    """
    def _bytes_feature(value):
        """Creates a TensorFlow Record Feature with value as a byte array.
        """
        return tf.train.Feature(bytes_list=tf.train.BytesList(value=[value]))

    def _int64_feature(value):
        """Creates a TensorFlow Record Feature with value as a 64 bit integer.
        """
        return tf.train.Feature(int64_list=tf.train.Int64List(value=[value]))

    features = {AUDIO_FEATURE_NAME: _bytes_feature(feature.tobytes()),
                AUDIO_LABEL_NAME: _int64_feature(label)}
    return tf.train.Example(features=tf.train.Features(feature=features))


def parse_example(example, shape=None):
    """Parse TF Example"""
    keys_to_feature = { AUDIO_FEATURE_NAME: tf.FixedLenFeature([], tf.string),
                        AUDIO_LABEL_NAME: tf.FixedLenFeature([], tf.int64)}
    raw_parsed_example = tf.parse_single_example(example, features=keys_to_feature)
    feature = tf.decode_raw(raw_parsed_example[AUDIO_FEATURE_NAME], tf.float64)
    label = tf.cast(raw_parsed_example[AUDIO_LABEL_NAME], tf.int32)
    feature = tf.cast(feature, tf.float32)
    if shape is not None:
        feature = tf.reshape(feature, shape)
    return feature, label


class RecordsParser(object):
    """Parse TF Records and return Iterator."""
    def __init__(self, records_files, num_classes, feature_shape):
        super(RecordsParser, self).__init__()
        self.dataset = tf.data.TFRecordDataset(filenames=records_files)
        self.shape = feature_shape
        self.num_classes = num_classes

    def iterator(self, is_onehot=True, is_shuffle=False, batch_size=64, buffer_size=512):
        parse_func = lambda example: parse_example(example, shape=self.shape)
        dataset = self.dataset.map(parse_func) # Parse the record into tensors.
        # Only go through the data once with no repeat.
        num_repeats = 1
        if is_shuffle:
            # If training then read a buffer of the given size and randomly shuffle it.
            dataset = dataset.shuffle(buffer_size=buffer_size)
        dataset = dataset.repeat(num_repeats)        # Repeat the input indefinitely.
        if is_onehot:
            onehot_func = lambda feature, label: (feature,
                                                  tf.one_hot(label, self.num_classes))
            dataset = dataset.map(onehot_func)

        dataset = dataset.batch(batch_size)
        iterator = dataset.make_initializable_iterator()
        batch = iterator.get_next()
        return iterator, batch


if __name__ == '__main__':
    from audio_params import TF_RECORDS_VAL
    from audio_params import NUM_CLASSES
    from os.path import join as pjoin

    rp = RecordsParser([pjoin('..', TF_RECORDS_VAL)], NUM_CLASSES, feature_shape=None)
    iterator, data_batch = rp.iterator(is_onehot=True, batch_size=64)

    with tf.compat.v1.Session() as sess:
        sess.run(iterator.initializer)
        predicted = []
        groundtruth = []
        while True:
            try:
                features, labels = sess.run(data_batch)
            except tf.errors.OutOfRangeError:
                break
            print(features, features.shape)
            print(labels, labels.shape)