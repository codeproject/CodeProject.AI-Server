# Lint as: python3
# pylint:disable=g-doc-args,g-short-docstring-punctuation,invalid-name,missing-class-docstring
# Copyright 2019 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     https://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
"""A weight imprinting engine that performs low-shot transfer-learning for image classification models.

For more information about how to use this API and how to create the type of
model required, see
`Retrain a classification model on-device with weight imprinting
</docs/edgetpu/retrain-classification-ondevice/>`_.
"""

from pycoral.pybind import _pywrap_coral


class ImprintingEngine:

  def __init__(self, model_path, keep_classes=False):
    """Performs weight imprinting (transfer learning) with the given model.

    Args:
      model_path (str): Path to the ``.tflite`` model you want to retrain.
        This must be a model that's specially-designed for this API. You
        can use our `weight imprinting model
        <https://coral.ai/models/image-classification/#training-models>`_ that
        has a pre-trained base model, or you can train the base model yourself
        by following our guide to `Retrain the base MobileNet model
        <https://coral.ai/docs/edgetpu/retrain-classification-ondevice/#retrain-the-base-mobilenet-model>`_.
      keep_classes (bool): If True, keep the existing classes from the
        pre-trained model (and use training to add additional classes). If
        False, drop the existing classes and train the model to include new
        classes only.
    """
    self._engine = _pywrap_coral.ImprintingEnginePythonWrapper(
        model_path, keep_classes)

  @property
  def embedding_dim(self):
    """Returns number of embedding dimensions."""
    return self._engine.EmbeddingDim()

  @property
  def num_classes(self):
    """Returns number of currently trained classes."""
    return self._engine.NumClasses()

  def serialize_extractor_model(self):
    """Returns embedding extractor model as `bytes` object."""
    return self._engine.SerializeExtractorModel()

  def serialize_model(self):
    """Returns newly trained model as `bytes` object."""
    return self._engine.SerializeModel()

  def train(self, embedding, class_id):
    """Trains the model with the given embedding for specified class.

    You can use this to add new classes to the model or retrain classes that you
    previously added using this imprinting API.

    Args:
      embedding (:obj:`numpy.array`): The embedding vector for training
        specified single class.
      class_id (int): The label id for this class. The index must be either the
        number of existing classes (to add a new class to the model) or the
        index of an existing class that was trained using this imprinting API
        (you can't retrain classes from the pre-trained model).
    """
    self._engine.Train(embedding, class_id)
