## Audio Classification dependencies

Classify the audios

### Usage

- [`audio_feature_extractor.py`][i-fe]: Extracting VGGish or mel features from wav files. 
- [`audio_model.py`][i-mo]: Define classification model.
- [`audio_records.py`][i-rcd]: Parse records.
- [`audio_urban_preprocess.py`][i-prep]: Preprocess for [Urban Sound Dataset][data-urban].
- [`audio_util.py`][i-u]: Util functions.


[i-fe]: ./audio_feature_extractor.py
[i-mo]: ./audio_model.py
[i-rcd]: ./audio_records.py
[i-prep]: ./audio_urban_preprocess.py
[i-u]: ./audio_util.py
[data-urban]: https://serv.cusp.nyu.edu/projects/urbansounddataset/
