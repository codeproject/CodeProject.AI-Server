# coding: utf-8
# author: luuil@outlook.com

r"""Test on audio model."""

from __future__ import print_function
import time

# import sys
# sys.path.append('./audio')

#import tensorflow as tf
# https://stackoverflow.com/a/58473210
import tensorflow.compat.v1 as tf
tf.compat.v1.disable_v2_behavior()
tf.compat.v1.logging.set_verbosity(tf.compat.v1.logging.ERROR)

import numpy as np
from os.path import join as pjoin
# from sklearn.metrics import accuracy_score

import audio_params
import audio.audio_model
import audio.audio_util as util
from audio.audio_feature_extractor import VGGishExtractor
from audio.audio_records import RecordsParser

NUM_VGGISH_FEATURE_PER_EXAMPLE = audio_params.NUM_VGGISH_FEATURE_PER_EXAMPLE

CKPT_DIR  = audio_params.AUDIO_CHECKPOINT_DIR
CKPT_NAME = audio_params.AUDIO_CHECKPOINT_NAME

META = audio_params.AUDIO_META       # pjoin(CKPT_DIR, audio_params.AUDIO_TRAIN_NAME, '{ckpt}.meta'.format(ckpt=CKPT_NAME))
CKPT = audio_params.AUDIO_CHECKPOINT # pjoin(CKPT_DIR, audio_params.AUDIO_TRAIN_NAME, CKPT_NAME)

VGGISH_CKPT = audio_params.VGGISH_CHECKPOINT
VGGISH_PCA = audio_params.VGGISH_PCA_PARAMS

SESS_CONFIG = tf.compat.v1.ConfigProto(allow_soft_placement=True)
SESS_CONFIG.gpu_options.allow_growth = True

def _restore_from_meta_and_ckpt(sess, meta, ckpt):
    """Restore graph from meta file and variables from ckpt file."""
    saver = tf.train.import_meta_graph(meta)
    saver.restore(sess, ckpt)


def _restore_from_defined_and_ckpt(sess, ckpt):
    """Restore graph from define and variables from ckpt file."""
    with sess.graph.as_default():
        audio.audio_model.define_audio_slim(training=False)
        audio.audio_model.load_audio_slim_checkpoint(sess, ckpt)

def inference_waveform(wave_form: any, sample_rate):
    """Test audio model on a wav form. Added by chris.maunder@codeproject.com 27Dec2023"""
    graph = tf.Graph()
    with tf.Session(graph=graph, config=SESS_CONFIG) as sess:
        with VGGishExtractor(VGGISH_CKPT, VGGISH_PCA, audio_params.VGGISH_INPUT_TENSOR_NAME, \
                             audio_params.VGGISH_OUTPUT_TENSOR_NAME) as ve:
            vggish_features = ve.waveform_to_features(wave_form, sample_rate)

        assert vggish_features is not None

        if NUM_VGGISH_FEATURE_PER_EXAMPLE > 1:
            vggish_features = vggish_features.reshape(1, -1)
                
        # restore graph
        # _restore_from_meta_and_ckpt(sess, META, CKPT)
        _restore_from_defined_and_ckpt(sess, CKPT)

        # get input and output tensor
        # graph = tf.get_default_graph()
        inputs = graph.get_tensor_by_name(audio_params.AUDIO_INPUT_TENSOR_NAME)
        outputs = graph.get_tensor_by_name(audio_params.AUDIO_OUTPUT_TENSOR_NAME)
        
        start_inference_time = time.perf_counter()
        predictions = sess.run(outputs, feed_dict={inputs: vggish_features}) # [num_features, num_class]
        inference_time = int((time.perf_counter() - start_inference_time) * 1000)

        # voting
        predictions = np.mean(predictions, axis=0)
        label_pred  = int(np.argmax(predictions))
        prob        = float(predictions[label_pred])
        
        print(f'{dict(zip(range(len(predictions)), predictions))}')
        print(f'predict label index: {label_pred} ({int(prob*100)}%)')

        return (predictions, label_pred, prob, inference_time)


def inference_wav(wav_file: str, label: int):
    """Test audio model on a wav file."""
    graph = tf.Graph()
    with tf.Session(graph=graph, config=SESS_CONFIG) as sess:
        with VGGishExtractor(VGGISH_CKPT, VGGISH_PCA, audio_params.VGGISH_INPUT_TENSOR_NAME, \
                             audio_params.VGGISH_OUTPUT_TENSOR_NAME) as ve:
            vggish_features = ve.wavfile_to_features(wav_file)

        assert vggish_features is not None

        if NUM_VGGISH_FEATURE_PER_EXAMPLE > 1:
            vggish_features = vggish_features.reshape(1, -1)
                
        # restore graph
        # _restore_from_meta_and_ckpt(sess, META, CKPT)
        _restore_from_defined_and_ckpt(sess, CKPT)

        # get input and output tensor
        # graph = tf.get_default_graph()
        inputs = graph.get_tensor_by_name(audio_params.AUDIO_INPUT_TENSOR_NAME)
        outputs = graph.get_tensor_by_name(audio_params.AUDIO_OUTPUT_TENSOR_NAME)
        
        predictions = sess.run(outputs, feed_dict={inputs: vggish_features}) # [num_features, num_class]

        # voting
        predictions = np.mean(predictions, axis=0)
        label_pred = np.argmax(predictions)
        prob = predictions[label_pred] * 100
       
        print('\n'*3)
        print(f'{dict(zip(range(len(predictions)), predictions))}')
        print(f'true label: {label}')
        print(f'predict label: {label_pred}({prob:.03f}%)')
        print('\n'*3)

"""
def inference_on_test():
    "" "Test audio model on test dataset."" "
    graph = tf.Graph()
    with tf.Session(graph=graph, config=SESS_CONFIG) as sess:
        rp = RecordsParser([audio_params.TF_RECORDS_TEST], 
            audio_params.NUM_CLASSES, feature_shape=None)
        test_iterator, test_batch = rp.iterator(is_onehot=True, batch_size=1)


        # restore graph: 2 ways to restore, both will working
        # _restore_from_meta_and_ckpt(sess, META, CKPT)
        _restore_from_defined_and_ckpt(sess, CKPT)

        # get input and output tensor
        # graph = tf.get_default_graph()
        inputs = graph.get_tensor_by_name(audio_params.AUDIO_INPUT_TENSOR_NAME)
        outputs = graph.get_tensor_by_name(audio_params.AUDIO_OUTPUT_TENSOR_NAME)

        sess.run(test_iterator.initializer)
        predicted = []
        groundtruth = []
        while True:
            try:
                # feature: [batch_size, num_features]
                # label: [batch_size, num_classes]
                te_features, te_labels = sess.run(test_batch)
            except tf.errors.OutOfRangeError:
                break
            predictions = sess.run(outputs, feed_dict={inputs: te_features})
            predicted.extend(np.argmax(predictions, 1))
            groundtruth.extend(np.argmax(te_labels, 1))
            # print(te_features.shape, te_labels, te_labels.shape)

        right = accuracy_score(groundtruth, predicted, normalize=False) # True: return prob
        print('all: {}, right: {}, wrong: {}, acc: {}'.format(
            len(predicted), right, len(predicted) - right, right/(len(predicted))))
"""

if __name__ == '__main__':
    tf.logging.set_verbosity(tf.logging.INFO)
    inference_wav('./data/wav/16772-8-0-0.wav', 8)
    # inference_on_test()

