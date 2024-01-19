from __future__ import print_function

import sys
sys.path.append('./vggish')
sys.path.append('./audio')

import os

import tensorflow as tf
import numpy as np

from abc import ABC
from abc import abstractmethod

import vggish_slim
import vggish_input
import vggish_postprocess

from audio_records import encodes_example
from audio_util import maybe_create_directory
from audio_params import NUM_VGGISH_FEATURE_PER_EXAMPLE


class ExtractorBase(ABC):
    """Base class for Extractors"""
    def __init__(self):
        super(ExtractorBase, self).__init__()

    @abstractmethod
    def __enter__(self):
        return self

    @abstractmethod
    def __exit__(self, type, value, traceback):
        pass

    @abstractmethod
    def wavfile_to_features(self, wav_file):
        """Extract features from wav file."""
        pass

    def create_records(self, record_path, wav_files, wav_labels):
        """Create TF Records from wav files and corresponding labels."""
        record_dir = os.path.dirname(record_path)
        maybe_create_directory(record_dir)
        writer = tf.python_io.TFRecordWriter(record_path)
        N = len(wav_labels)
        for n, (wav_file, wav_label) in enumerate(zip(wav_files, wav_labels)):
            tf.logging.info('[{}/{}] Extracting VGGish feature:'
                ' label: {} - {}'.format(n, N, wav_label, wav_file))

            features = self.wavfile_to_features(wav_file)

            if NUM_VGGISH_FEATURE_PER_EXAMPLE > 1:
                if NUM_VGGISH_FEATURE_PER_EXAMPLE != num_features:
                    tf.logging.warning('Invalid vggish features length:'
                        ' label: {} - {}'.format(wav_label, wav_file))
                    continue
                f = features.reshape(-1)
                example = encodes_example(np.float64(f), np.int64(l))
                writer.write(example.SerializeToString())
            else:
                num_features = features.shape[0] # one feature for one second
                if num_features == 0:
                    tf.logging.warning('No vggish features:'
                        ' label: {} - {}'.format(wav_label, wav_file))
                    continue
                cur_wav_labels = [wav_label] * num_features
                for (f, l) in zip(features, cur_wav_labels):
                    example = encodes_example(np.float64(f), np.int64(l))
                    writer.write(example.SerializeToString())
        writer.close()


class MelExtractor(ExtractorBase):
    """Feature Extractor that extract mel feature from wav."""
    def __init__(self):
        super(MelExtractor, self).__init__()

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        pass

    @staticmethod
    def wavfile_to_features(wav_file):
        assert os.path.exists(wav_file), '{} not exists!'.format(wav_file)
        mel_features = vggish_input.wavfile_to_examples(wav_file)
        return mel_features

    @staticmethod
    def waveform_to_features(wave_form, sample_rate):
        """ Added chris.maunder@codeproject.com 27Dec2023 """
        assert wave_form is not None, 'wave data is None'
        mel_features = vggish_input.waveform_to_examples(wave_form, sample_rate)
        return mel_features


class VGGishExtractor(ExtractorBase):
    """Feature Extractor use VGGish model from wav."""
    def __init__(self, checkpoint, pca_params, input_tensor_name, output_tensor_name):
        """Create a new Graph and a new Session for every VGGishExtractor object."""
        super(VGGishExtractor, self).__init__()
        
        self.graph = tf.Graph()
        with self.graph.as_default():
            vggish_slim.define_vggish_slim(training=False)

        sess_config = tf.compat.v1.ConfigProto(allow_soft_placement=True)
        sess_config.gpu_options.allow_growth = True
        self.sess = tf.compat.v1.Session(graph=self.graph, config=sess_config)
        vggish_slim.load_defined_vggish_slim_checkpoint(self.sess, checkpoint)
        
        # use the self.sess to init others
        self.input_tensor = self.graph.get_tensor_by_name(input_tensor_name)
        self.output_tensor = self.graph.get_tensor_by_name(output_tensor_name)

        # postprocessor
        self.postprocess = vggish_postprocess.Postprocessor(pca_params)

    def __enter__(self):
        return self

    def __exit__(self, type, value, traceback):
        self.close()

    def mel_to_vggish(self, mel_features):
        """Converting mel features to VGGish features."""
        assert mel_features is not None, 'mel_features is None'
        # mel_features shape is 0, skip
        if mel_features.shape[0]==0:
            return mel_features
        # Run inference and postprocessing.
        [embedding_batch] = self.sess.run([self.output_tensor],
                                     feed_dict={self.input_tensor: mel_features})
        vggish_features = self.postprocess.postprocess(embedding_batch)
        return vggish_features

    def wavfile_to_features(self, wav_file):
        """Extract VGGish feature from wav file."""
        assert os.path.exists(wav_file), '{} not exists!'.format(wav_file)
        mel_features = MelExtractor.wavfile_to_features(wav_file)
        return self.mel_to_vggish(mel_features)

    def waveform_to_features(self, wave_form, sample_rate):
        """ Extract VGGish feature from wav data. Added chris.maunder@codeproject.com 27Dec2023 """
        assert wave_form is not None, 'Wave form is null'
        mel_features = MelExtractor.waveform_to_features(wave_form, sample_rate)
        return self.mel_to_vggish(mel_features)
    
    def close(self):
        self.sess.close()

def main_test():
    import audio_params
    from vggish import vggish_params
    import timeit
    from audio_util import urban_labels

    tf.get_logger().setLevel('INFO')

    wav_file = 'F:/3rd-datasets/UrbanSound8K-16bit/audio-classified/siren/90014-8-0-1.wav'
    wav_dir = 'F:/3rd-datasets/UrbanSound8K-16bit/audio-classified/siren'
    wav_filenames = os.listdir(wav_dir)
    wav_files = [os.path.join(wav_dir, wav_filename) for wav_filename in wav_filenames]
    wav_labels = urban_labels(wav_files)

    # test VGGishExtractor
    time_start = timeit.default_timer()
    with VGGishExtractor(audio_params.VGGISH_CHECKPOINT,
                         audio_params.VGGISH_PCA_PARAMS,
                         vggish_params.INPUT_TENSOR_NAME,
                         vggish_params.OUTPUT_TENSOR_NAME) as ve:
        
        vggish_features = ve.wavfile_to_features(wav_file)
        print(vggish_features, vggish_features.shape)

        ve.create_records('./vggish_test.records', wav_files[:10], wav_labels[:10])

    time_end = timeit.default_timer()
    # print('cost time: {}s, {}s/wav'.format((time_end-time_start), (time_end-time_start)/(i+1)))

    # test MelExtractor
    with MelExtractor() as me:
        mel_features = me.wavfile_to_features(wav_file)
        print(mel_features, mel_features.shape)
        me.create_records('./mel_test.records', wav_files[:10], wav_labels[:10])


def main_create_urban_tfr():
    import timeit
    import natsort
    import audio_params
    import vggish_params
    from audio_util import train_test_val_split

    tf.get_logger().setLevel('INFO')
    
    def _listdir(d):
      return [os.path.join(d, f) for f in natsort.natsorted(os.listdir(d))]
    
    wav_dir = r"path/to/UrbanSound8K-16bit/audio-classified"
    tfr_dir = r"./data/tfrecords"

    wav_files = list()
    wav_labels = list()
    class_dict = dict()
    for idx, folder in enumerate(_listdir(wav_dir)):
      wavs = list(filter(lambda x: x.endswith('.wav'), _listdir(folder)))
      wav_files.extend(wavs)
      wav_labels.extend([idx] * len(wavs))
      class_dict[idx] = os.path.basename(folder)
    print(f'class-id pair: {class_dict}')

    wav_file = wav_files[0]

    (X_train, Y_train), (X_test, Y_test), (X_val, Y_val) = train_test_val_split(wav_files, wav_labels, split=(.2, .1), shuffle=True)

    time_start = timeit.default_timer()
    with VGGishExtractor(audio_params.VGGISH_CHECKPOINT,
                         audio_params.VGGISH_PCA_PARAMS,
                         vggish_params.INPUT_TENSOR_NAME,
                         vggish_params.OUTPUT_TENSOR_NAME) as ve:
        
        vggish_features = ve.wavfile_to_features(wav_file)
        print(vggish_features, vggish_features.shape)

        ve.create_records(os.path.join(tfr_dir, 'vggish.train.records'), X_train, Y_train)
        ve.create_records(os.path.join(tfr_dir, 'vggish.test.records'), X_test, Y_test)
        ve.create_records(os.path.join(tfr_dir, 'vggish.val.records'), X_val, Y_val)
    
    time_end = timeit.default_timer()
    print('cost time: {}s, {}s/wav'.format((time_end-time_start), (time_end-time_start)/len(wav_files)))

if __name__ == '__main__':
    main_test()
    # main_create_urban_tfr()
    pass