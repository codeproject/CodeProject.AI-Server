# Lint as: python3
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
"""Functions to work with a classification model."""

import collections
import operator
import numpy as np


Class = collections.namedtuple('Class', ['id', 'score'])
"""Represents a single classification, with the following fields:

  .. py:attribute:: id

      The class id.

  .. py:attribute:: score

      The prediction score.
"""


def num_classes(interpreter):
  """Gets the number of classes output by a classification model.

  Args:
    interpreter: The ``tf.lite.Interpreter`` holding the model.

  Returns:
    The total number of classes output by the model.
  """
  return np.prod(interpreter.get_output_details()[0]['shape'])


def get_scores(interpreter):
  """Gets the output (all scores) from a classification model, dequantizing it if necessary.

  Args:
    interpreter: The ``tf.lite.Interpreter`` to query for output.

  Returns:
    The output tensor (flattened and dequantized) as :obj:`numpy.array`.
  """
  output_details = interpreter.get_output_details()[0]
  output_data = interpreter.tensor(output_details['index'])().flatten()

  if np.issubdtype(output_details['dtype'], np.integer):
    scale, zero_point = output_details['quantization']
    # Always convert to np.int64 to avoid overflow on subtraction.
    return scale * (output_data.astype(np.int64) - zero_point)

  return output_data.copy()


def get_classes_from_scores(scores,
                            top_k=float('inf'),
                            score_threshold=-float('inf')):
  """Gets results from a classification model as a list of ordered classes, based on given scores.

  Args:
    scores: The output from a classification model. Must be flattened and
      dequantized.
    top_k (int): The number of top results to return.
    score_threshold (float): The score threshold for results. All returned
      results have a score greater-than-or-equal-to this value.

  Returns:
    A list of :obj:`Class` objects representing the classification results,
    ordered by scores.
  """
  top_k = min(top_k, len(scores))
  classes = [
      Class(i, scores[i])
      for i in np.argpartition(scores, -top_k)[-top_k:]
      if scores[i] >= score_threshold
  ]
  return sorted(classes, key=operator.itemgetter(1), reverse=True)


def get_classes(interpreter, top_k=float('inf'), score_threshold=-float('inf')):
  """Gets results from a classification model as a list of ordered classes.

  Args:
    interpreter: The ``tf.lite.Interpreter`` to query for results.
    top_k (int): The number of top results to return.
    score_threshold (float): The score threshold for results. All returned
      results have a score greater-than-or-equal-to this value.

  Returns:
    A list of :obj:`Class` objects representing the classification results,
    ordered by scores.
  """
  return get_classes_from_scores(
      get_scores(interpreter), top_k, score_threshold)
