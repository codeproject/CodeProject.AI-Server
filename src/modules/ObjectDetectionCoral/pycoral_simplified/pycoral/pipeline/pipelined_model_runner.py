# Lint as: python3
# Copyright 2020 Google LLC
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
"""The pipeline API allows you to run a segmented model across multiple Edge TPUs.

For more information, see `Pipeline a model with multiple Edge
TPUs </docs/edgetpu/pipeline/>`_.
"""

import numpy as np

from pycoral.pybind import _pywrap_coral


def _get_names(details):
  """Returns a set of names given input/output tensor details."""
  return {d['name'] for d in details}


class PipelinedModelRunner:
  """Manages the model pipeline.

  To create an instance::

    interpreter_a = tflite.Interpreter(model_path=model_segment_a,
                                       experimental_delegates=delegate_a)
    interpreter_a.allocate_tensors()
    interpreter_b = tflite.Interpreter(model_path=model_segment_b,
                                       experimental_delegates=delegate_b)
    interpreter_b.allocate_tensors()
    interpreters = [interpreter_a, interpreter_b]
    runner = PipelinedModelRunner(interpreters)
  """

  def __init__(self, interpreters):
    """Be sure you first call ``allocate_tensors()`` on each interpreter.

    Args:
      interpreters: A list of ``tf.lite.Interpreter`` objects, one for each
        segment in the pipeline.
    """
    self._runner = None

    if not interpreters:
      raise ValueError('At least one interpreter expected')

    # It requires that the inputs of interpreter[i] is a subset of outputs of
    # interpreter[j], where j=0,...,i-1.
    prev_outputs = _get_names(interpreters[0].get_input_details())
    for index, interpreter in enumerate(interpreters):
      inputs = _get_names(interpreter.get_input_details())
      if not inputs.issubset(prev_outputs):
        raise ValueError(
            'Interpreter {} can not get its input tensors'.format(index))
      prev_outputs.update(_get_names(interpreter.get_output_details()))

    self._interpreters = interpreters
    self._runner = _pywrap_coral.PipelinedModelRunnerWrapper(
        [i._native_handle() for i in interpreters])

    self._input_types = {}
    for d in self._interpreters[0].get_input_details():
      self._input_types[d['name']] = d['dtype']

    self._output_shapes = {}
    for d in self._interpreters[-1].get_output_details():
      self._output_shapes[d['name']] = d['shape']

  def __del__(self):
    if self._runner:
      # Push empty request to stop the pipeline in case user forgot.
      self.push({})
      num_unconsumed = 0
      # Release any unconsumed tensors if any.
      while self.pop():
        num_unconsumed += 1
      if num_unconsumed:
        print(
            'WARNING: {} unconsumed results in the pipeline during destruction!'
            .format(num_unconsumed))

  def set_input_queue_size(self, size):
    """Sets the maximum number of inputs that may be queued for inference.

    By default, input queue size is unlimited.

    Note: It's OK to change the queue size max when PipelinedModelRunner is
    active. If the new max is smaller than current queue size, pushes to
    the queue are blocked until the current queue size drops below the new max.

    Args:
      size (int): The input queue size max
    """
    self._runner.SetInputQueueSize(size)

  def set_output_queue_size(self, size):
    """Sets the maximum number of outputs that may be unconsumed.

    By default, output queue size is unlimited.

    Note: It's OK to change the queue size max when PipelinedModelRunner is
    active. If the new max is smaller than current queue size, pushes to the
    queue are blocked until the current queue size drops below the new max.

    Args:
      size (int): The output queue size max
    """
    self._runner.SetOutputQueueSize(size)

  def push(self, input_tensors):
    """Pushes input tensors to trigger inference.

    Pushing an empty dict is allowed, which signals the class that no more
    inputs will be added (the function will return false if inputs were pushed
    after this special push). This special push allows the ``pop()`` consumer to
    properly drain unconsumed output tensors.

    Caller will be blocked if the current input queue size is greater than the
    queue size max (use ``set_input_queue_size()``). By default, input queue
    size threshold is unlimited, in this case, call to push() is non-blocking.

    Args:
      input_tensors: A dictionary with key of type string, and value of type
        :obj:`numpy.array` representing the model's input tensors, where keys
        are the tensor names.

    Raises:
      RuntimeError: error during pushing pipelined model inference request.
    """
    if input_tensors and len(input_tensors) != len(self._input_types):
      raise ValueError('Expected input of length {}, but got {}'.format(
          len(self._input_types), len(input_tensors)))

    for key, tensor in input_tensors.items():
      input_type = self._input_types[key]
      if not isinstance(tensor, np.ndarray) or tensor.dtype != input_type:
        raise ValueError(
            'Input should be a list of numpy array of type {}'.format(
                input_type))

    self._runner.Push(input_tensors)

  def pop(self):
    """Returns a single inference result.

    This function blocks the calling thread until a result is returned.

    Returns:
      Dictionary with key of type string, and value of type :obj:`numpy.array`
      representing the model's output tensors, where keys are the tensor names.
      Returns None when a ``push()`` receives an empty dict input, indicating
      there are no more output tensors available.

    Raises:
      RuntimeError: error during retrieving pipelined model inference results.
    """
    result = self._runner.Pop()
    if result:
      result = {k: v.reshape(self._output_shapes[k]) for k, v in result.items()}
    return result

  def interpreters(self):
    """Returns list of interpreters that constructed PipelinedModelRunner."""
    return self._interpreters
