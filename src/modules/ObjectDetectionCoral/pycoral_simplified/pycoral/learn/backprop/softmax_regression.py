# Lint as: python3
# pylint:disable=g-doc-args,g-short-docstring-punctuation,g-no-space-after-docstring-summary
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
"""A softmax regression model for on-device backpropagation of the last layer."""
from pycoral.pybind import _pywrap_coral


class SoftmaxRegression:
  """An implementation of the softmax regression function (multinominal logistic

  regression) that operates as the last layer of your classification model, and
  allows for on-device training with backpropagation (for this layer only).

  The input for this layer must be an image embedding, which should be the
  output of your embedding extractor (the backbone of your model). Once given
  here, the input is fed to a fully-connected layer where weights and bias are
  applied, and then passed to the softmax function to receive the final
  probability distribution based on the number of classes for your model:

  training/inference input (image embedding) --> fully-connected layer -->
  softmax function

  When you're conducting training with :func:`train_with_sgd`, the process uses
  a cross-entropy loss function to measure the error and then update the weights
  of the fully-connected layer (backpropagation).

  When you're satisfied with the inference accuracy, call
  :func:`serialize_model` to create a new model in `bytes` with this
  retrained layer appended to your embedding extractor. You can then run
  inferences with this new model as usual (using TensorFlow Lite interpreter
  API).

  .. note::

    This last layer (FC + softmax) in the retrained model always runs on the
    host CPU instead of the Edge TPU. As long as the rest of your embedding
    extractor model is compiled for the Edge TPU, then running this last layer
    on the CPU should not significantly affect the inference speed.


  """

  def __init__(self,
               feature_dim=None,
               num_classes=None,
               weight_scale=0.01,
               reg=0.0):
    """For more detail, see the `Stanford CS231 explanation of the softmax
    classifier <http://cs231n.github.io/linear-classify/#softmax>`_.

    Args:
      feature_dim (int): The dimension of the input feature (length of the
        feature vector).
      num_classes (int): The number of output classes.
      weight_scale (float): A weight factor for computing new weights. The
        backpropagated weights are drawn from standard normal distribution, then
        multiplied by this number to keep the scale small.
      reg (float): The regularization strength.
    """
    self.model = _pywrap_coral.SoftmaxRegressionModelWrapper(
        feature_dim, num_classes, weight_scale, reg)

  def serialize_model(self, in_model_path):
    """Appends learned weights to your TensorFlow Lite model and serializes it.

    Beware that learned weights and biases are quantized from float32 to uint8.

    Args:
      in_model_path (str): Path to the embedding extractor model (``.tflite``
        file).

    Returns:
       The TF Lite model with new weights, as a `bytes` object.
    """
    return self.model.AppendLayersToEmbeddingExtractor(in_model_path)

  def get_accuracy(self, mat_x, labels):
    """Calculates the model's accuracy (percentage correct).

    The calculation is on performing inferences on the given data and labels.

    Args:
      mat_x (:obj:`numpy.array`): The input data (image embeddings) to test,
        as a matrix of shape ``NxD``, where ``N`` is number of inputs to test
        and ``D`` is the dimension of the input feature (length of the feature
        vector).
      labels (:obj:`numpy.array`): An array of the correct label indices that
        correspond to the test data passed in ``mat_x`` (class label index in
        one-hot vector).

    Returns:
      The accuracy (the percent correct) as a float.
    """
    return self.model.GetAccuracy(mat_x, labels)

  def train_with_sgd(self,
                     data,
                     num_iter,
                     learning_rate,
                     batch_size=100,
                     print_every=100):
    """Trains your model using stochastic gradient descent (SGD).

    The training data must be structured in a dictionary as specified in the
    ``data`` argument below. Notably, the training/validation images must be
    passed as image embeddings, not as the original image input. That is, run
    the images through your embedding extractor (the backbone of your graph) and
    use the resulting image embeddings here.

    Args:
      data (dict): A dictionary that maps ``'data_train'`` to an array of
        training image embeddings, ``'labels_train'`` to an array of training
        labels, ``'data_val'`` to an array of validation image embeddings, and
        ``'labels_val'`` to an array of validation labels.
      num_iter (int): The number of iterations to train.
      learning_rate (float): The learning rate (step size) to use in training.
      batch_size (int): The number of training examples to use in each
        iteration.
      print_every (int): The number of iterations for which to print the loss,
        and training/validation accuracy. For example, ``20`` prints the stats
        for every 20 iterations. ``0`` disables printing.
    """
    train_config = _pywrap_coral.TrainConfigWrapper(num_iter, batch_size,
                                                    print_every)

    training_data = _pywrap_coral.TrainingDataWrapper(data['data_train'],
                                                      data['data_val'],
                                                      data['labels_train'],
                                                      data['labels_val'])

    self.model.Train(training_data, train_config, learning_rate)
