# coding: utf-8
# author: luuil@outlook.com

"""Converting wav file's bits.

Such as, convert `PCM_24` to `PCM_16`
"""

import os
import soundfile # for convert wav file
# import urban_sound_params
from shutil import copyfile
from audio_util import maybe_create_directory
from audio_util import urban_labels

                
def maybe_copy_file(src, dst):
    if not os.path.exists(dst):
        print('{} => {}'.format(src, dst))
        copyfile(src, dst)



def convert_wav(src_wav, dst_wav, subtype='PCM_16'):
    """Converting wav file's bits.

    Such as, convert `PCM_24` to `PCM_16`
    """
    assert os.path.exists(src_wav), "{} not exists!".format(src_wav)
    data, sr = soundfile.read(src_wav)
    soundfile.write(dst_wav, data, sr, subtype=subtype)

def convert_urban_pcm24_to_pcm16():
    """Convert urban sound codec from PCM_24 to PCM_16."""
    src_dir = ['/data1/data/UrbanSound8K/audio/fold{:d}'.format(i+1) for i in range(10)]
    dst_dir = ['/data1/data/UrbanSound8K-16bit/audio/fold{:d}'.format(i+1) for i in range(10)]
    converted_wav_paths = []
    for dsrc, ddst in zip(src_dir, dst_dir):
        maybe_create_directory(ddst)
        wav_files = filter(lambda FP: FP if FP.endswith('.wav') else None, 
                           [FP for FP in os.listdir(dsrc)])
        for wav_file in wav_files:
            src_wav, dst_wav = os.path.join(dsrc, wav_file), os.path.join(ddst, wav_file)
            convert_wav(src_wav, dst_wav, subtype='PCM_16')
            converted_wav_paths.append(dst_wav)
            print('converted count:', len(converted_wav_paths))
    print(converted_wav_paths, len(converted_wav_paths))


def arange_urban_sound_file_by_class():
    """Arange urban sound file by it's class."""
    def _listdir(d):
      return [os.path.join(d, f) for f in sorted(os.listdir(d))]
    
    src_path = '/data1/data/UrbanSound8K-16bit/audio'
    dst_dir = '/data1/data/UrbanSound8K-16bit/audio-classfied'
    
    src_paths = list()
    for d in _listdir(src_path):
      wavs = filter(lambda x: x.endswith('.wav'), _listdir(d))
      src_paths.extend(list(wavs))
    
    CLASSES = [
        'air conditioner',
        'car horn',
        'children playing',
        'dog bark',
        'drilling',
        'engine idling',
        'gun shot',
        'jackhammer',
        'siren',
        'street music']
    CLASSES_STRIPED = [c.replace(' ', '_') for c in CLASSES]
    for src in src_paths:
        lbl = urban_labels([src])[0]
        dst = '{dir}/{label}'.format(dir=dst_dir, label=CLASSES_STRIPED[lbl])
        maybe_create_directory(dst)
        maybe_copy_file(src, '{dst}/{name}'.format(dst=dst, name=os.path.split(src)[-1]))


if __name__ == '__main__':
    convert_urban_pcm24_to_pcm16()
    arange_urban_sound_file_by_class()
    pass
        
    

