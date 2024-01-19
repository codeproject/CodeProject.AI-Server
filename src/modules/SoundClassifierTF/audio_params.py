# coding: utf-8
# author: luuil@outlook.com

r"""Global parameters for the audio model.

See audio_model.py for more information.
"""

import os

# Training
AUDIO_TRAIN_NAME = 'urbansounds8k'
NUM_EPOCHS       = 2000
BATCH_SIZE       = 128
TENSORBOARD_DIR  = './data/train/tensorboard'

# Path to UrbanSound8K files
WAV_FILE_PARENT_DIR            = './data/train/wavs/UrbanSound8K-16bit/audio-classfied'
NUM_VGGISH_FEATURE_PER_EXAMPLE = 1

# Architectural constants.
EMBEDDING_SIZE = 128 * NUM_VGGISH_FEATURE_PER_EXAMPLE # Size of embedding layer.
NUM_FEATURES   = EMBEDDING_SIZE
NUM_CLASSES    = 10


# Hyperparameters used in training.
INIT_STDDEV = 0.01      # Standard deviation used to initialize weights.
LEARNING_RATE = 1e-5    # Learning rate for the Adam optimizer.
ADAM_EPSILON = 1e-8     # Epsilon for the Adam optimizer.
NUM_UNITS = 10          # hidden units


# Names of ops, tensors, and features.
AUDIO_INPUT_OP_NAME     = 'audio/vggish_input'
AUDIO_INPUT_TENSOR_NAME = AUDIO_INPUT_OP_NAME + ':0'
AUDIO_OUTPUT_OP_NAME    = 'audio/prediction'
AUDIO_OUTPUT_TENSOR_NAME = AUDIO_OUTPUT_OP_NAME + ':0'


# Checkpoint
AUDIO_CHECKPOINT_DIR  = './data/train'
AUDIO_CHECKPOINT_NAME = 'audio_urban_model.ckpt'
AUDIO_CLASSES_NAME    = 'audio_urban_model.txt'

AUDIO_CHECKPOINT      = os.path.join(AUDIO_CHECKPOINT_DIR, AUDIO_TRAIN_NAME, AUDIO_CHECKPOINT_NAME)
AUDIO_META            = os.path.join(AUDIO_CHECKPOINT_DIR, AUDIO_TRAIN_NAME, f'{AUDIO_CHECKPOINT_NAME}.meta')
AUDIO_CLASSES         = os.path.join(AUDIO_CHECKPOINT_DIR, AUDIO_TRAIN_NAME, AUDIO_CLASSES_NAME)


# Records
AUDIO_FEATURE_NAME    = 'feature'
AUDIO_LABEL_NAME      = 'label'

TF_RECORDS_TRAIN_NAME = 'audio_urban_model_train.tfrecords'
TF_RECORDS_TEST_NAME  = 'audio_urban_model_test.tfrecords'
TF_RECORDS_VAL_NAME   = 'audio_urban_model_val.tfrecords'

TF_RECORDS_DIR        = './data/records'
TF_RECORDS_TRAIN      = os.path.join(TF_RECORDS_DIR, TF_RECORDS_TRAIN_NAME)
TF_RECORDS_TEST       = os.path.join(TF_RECORDS_DIR, TF_RECORDS_TEST_NAME)
TF_RECORDS_VAL        = os.path.join(TF_RECORDS_DIR, TF_RECORDS_VAL_NAME)


# Vggish
VGGISH_CHECKPOINT_DIR  = './data/models/vggish'
VGGISH_CHECKPOINT_NAME = 'vggish_model.ckpt'
VGGISH_PCA_PARAMS_NAME = 'vggish_pca_params.npz'
VGGISH_CHECKPOINT      = os.path.join(VGGISH_CHECKPOINT_DIR, VGGISH_CHECKPOINT_NAME)
VGGISH_PCA_PARAMS      = os.path.join(VGGISH_CHECKPOINT_DIR, VGGISH_PCA_PARAMS_NAME)

# Names of ops, tensors, and features.
VGGISH_INPUT_OP_NAME      = 'vggish/input_features'
VGGISH_INPUT_TENSOR_NAME  = VGGISH_INPUT_OP_NAME + ':0'
VGGISH_OUTPUT_OP_NAME     = 'vggish/embedding'
VGGISH_OUTPUT_TENSOR_NAME = VGGISH_OUTPUT_OP_NAME + ':0'