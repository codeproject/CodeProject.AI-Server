## Audio Classification

Classify the audios. In this repo, I train a model on [UrbanSound8K][data-urban] dataset,
and achieve about `80%` accuracy on test dataset.

There is a pre-trained model in [urbansounds8k][i-ckpt], trained epoch is `1000`

### Usage

- [`audio_train.py`][i-train]: Train audio model from scratch or restore from checkpoint.
- [`audio_params.py`][i-params]: Configuration for training a model.
- [`audio_inference_demo.py`][i-demo]: Demo for test the trained model.
- [`./audio/*`][i-audio]: Dependencies of training, model and datasets.
- [`./vggish/*`][i-vggish]: Dependencies of [VGGish][tool-vggish] for feature extracting.


### Env setup

Conda are recommended, just need one line: `conda env create -f conda.env.yml`

### Train & Test

1. Config parameters: `audio_params.py`.
2. Train the model: `python audio_train.py`. (It will **create tfrecords automaticly** if not exists)
3. Check the training process from tensorboard: `tensorboard --logdir=./data/tensorboard`
4. Test the model: `python audio_inference_demo.py`.

### Tools

- [TensorFlow: VGGish][tool-vggish]
- [Google AudioSet][tool-as]
    - [VGGish model checkpoint][tool-as-ckpt]
    - [Embedding PCA parameters][tool-as-pca]
- [pyAudioAnalysis][tool-pyaa](Ref.)

### Dataset

- [urban sound dataset][data-urban]

### Ref. Blogs

- [AudioSet: An ontology and human-labelled dataset for audio events][blog-as]
- [CNN Architectures for Large-Scale Audio Classification][blog-accnn]

[i-train]: ./audio_train.py
[i-params]: ./audio_params.py
[i-demo]: ./audio_inference_demo.py
[i-audio]: ./audio
[i-vggish]: ./vggish
[i-ckpt]: ./data/train/urbansounds8k
[tool-vggish]: https://github.com/tensorflow/models/tree/master/research/audioset
[tool-pyaa]: https://github.com/tyiannak/pyAudioAnalysis
[tool-as]: https://research.google.com/audioset/index.html
[tool-as-ckpt]: https://storage.googleapis.com/audioset/vggish_model.ckpt
[tool-as-pca]: https://storage.googleapis.com/audioset/vggish_pca_params.npz
[data-urban]: https://serv.cusp.nyu.edu/projects/urbansounddataset/urbansound8k.html
[blog-as]: https://research.google.com/pubs/pub45857.html
[blog-accnn]: https://research.google.com/pubs/pub45611.html