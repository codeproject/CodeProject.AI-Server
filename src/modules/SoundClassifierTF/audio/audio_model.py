# coding: utf-8
# author: luuil@outlook.com

r"""Defines the 'audio' model used to classify the VGGish features."""

from __future__ import print_function

import tensorflow as tf
tf.compat.v1.disable_eager_execution()

import audio_params as params

# https://github.com/google-research/tf-slim
# slim = tf.contrib.slim
import tf_slim as slim
            
def define_audio_slim(training=False):
    """Defines the audio TensorFlow model.

    All ops are created in the current default graph, under the scope 'audio/'.

    The input is a placeholder named 'audio/vggish_input' of type float32 and
    shape [batch_size, feature_size] where batch_size is variable and
    feature_size is constant, and feature_size represents a VGGish output feature.
    The output is an op named 'audio/prediction' which produces the activations of
    a NUM_CLASSES layer.

    Args:
        training: If true, all parameters are marked trainable.

    Returns:
        The op 'audio/logits'.
    """
    with slim.arg_scope([slim.fully_connected],
                        weights_initializer=tf.compat.v1.truncated_normal_initializer(  # tf.truncated_normal_initializer(
                          stddev=params.INIT_STDDEV),
                        biases_initializer=tf.zeros_initializer(),
                        trainable=training),\
         tf.compat.v1.variable_scope('audio'): # tf.variable_scope('audio'):
        vggish_input = tf.compat.v1.placeholder(tf.float32,
                                      shape=[None, params.NUM_FEATURES],
                                      name='vggish_input')
        # Add a fully connected layer with NUM_UNITS units
        fc = slim.fully_connected(vggish_input, params.NUM_UNITS)
        logits = slim.fully_connected(fc, params.NUM_CLASSES, 
            activation_fn=None, scope='logits')
        tf.nn.softmax(logits, name='prediction')
        return logits
    
def load_audio_slim_checkpoint(session, checkpoint_path):
    """Loads a pre-trained audio-compatible checkpoint.
    
    This function can be used as an initialization function (referred to as
    init_fn in TensorFlow documentation) which is called in a Session after
    initializating all variables. When used as an init_fn, this will load
    a pre-trained checkpoint that is compatible with the audio model
    definition. Only variables defined by audio will be loaded.
    
    Args:
        session: an active TensorFlow session.
        checkpoint_path: path to a file containing a checkpoint that is
          compatible with the audio model definition.
    """

    # Get the list of names of all audio variables that exist in
    # the checkpoint (i.e., all inference-mode audio variables).
    with tf.Graph().as_default():
        define_audio_slim(training=False)
        audio_var_names = [v.name for v in tf.compat.v1.global_variables()]

    # Get list of variables from exist graph which passed by session
    with session.graph.as_default():
        global_variables = tf.compat.v1.global_variables()

    # Get the list of all currently existing variables that match
    # the list of variable names we just computed.
    audio_vars = [v for v in global_variables if v.name in audio_var_names]

    # Use a Saver to restore just the variables selected above.
    saver = tf.compat.v1.train.Saver(audio_vars, name='audio_load_pretrained',
                         write_version=1)
    saver.restore(session, checkpoint_path)