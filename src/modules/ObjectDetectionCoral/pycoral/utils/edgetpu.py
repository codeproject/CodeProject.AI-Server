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
"""Utilities for using the TensorFlow Lite Interpreter with Edge TPU."""

import contextlib
import ctypes
import ctypes.util
import numpy as np

# pylint:disable=unused-import
# We're trying to support 5 different platforms with 3 different libs across 2
# different packages using libraries that are tied to out of date OSs and things
# just are not consistent. We do what we can where we can.
try:
  from pycoral.pybind._pywrap_coral import GetRuntimeVersion as get_runtime_version
  from pycoral.pybind._pywrap_coral import InvokeWithBytes as invoke_with_bytes
  from pycoral.pybind._pywrap_coral import InvokeWithDmaBuffer as invoke_with_dmabuffer
  from pycoral.pybind._pywrap_coral import InvokeWithMemBuffer as invoke_with_membuffer
  from pycoral.pybind._pywrap_coral import ListEdgeTpus as list_edge_tpus
  from pycoral.pybind._pywrap_coral import SetVerbosity as set_verbosity
  from pycoral.pybind._pywrap_coral import SupportsDmabuf as supports_dmabuf
except: pass

import platform

# First determine if we have TensorFlow-Lite runtime installed, or the whole Tensorflow
# In either case we're looking to load TFLite models
try:
  from tflite_runtime.interpreter import Interpreter, load_delegate
except ImportError as ex:
  # "/lib/aarch64-linux-gnu/libm.so.6: version `GLIBC_2.29' not found (required by 
  # site-packages/tflite_runtime/_pywrap_tensorflow_interpreter_wrapper.cpython-38-aarch64-linux-gnu.so)"
  import tensorflow as tf
  Interpreter, load_delegate = tf.lite.Interpreter, tf.lite.experimental.load_delegate

_EDGETPU_SHARED_LIB = {
  'Linux': 'libedgetpu.so.1',
  'Darwin': 'libedgetpu.1.dylib',
  'Windows': 'edgetpu.dll'
}[platform.system()]


def load_edgetpu_delegate(options=None):
  """Loads the Edge TPU delegate with the given options.

  Args:
    options (dict): Options that are passed to the Edge TPU delegate, via
      ``tf.lite.load_delegate``. The only option you should use is
      "device", which defines the Edge TPU to use. Supported values are the same
      as `device` in :func:`make_interpreter`.
  Returns:
    The Edge TPU delegate object.
  """
  return load_delegate(_EDGETPU_SHARED_LIB, options or {})


def make_interpreter(model_path_or_content, device=None, delegate=None):
  """Creates a new ``tf.lite.Interpreter`` instance using the given model.

  **Note:** If you have multiple Edge TPUs, you should always specify the
  ``device`` argument.

  Args:
     model_path_or_content (str or bytes): `str` object is interpreted as
       model path, `bytes` object is interpreted as model content.
     device (str): The Edge TPU device you want:

       + "cpu"     -- use the CPU
       + None      -- use any Edge TPU (this is the default)
       + ":<N>"    -- use N-th Edge TPU (this corresponds to the enumerated
         index position from :func:`list_edge_tpus`)
       + "usb"     -- use any USB Edge TPU
       + "usb:<N>" -- use N-th USB Edge TPU
       + "pci"     -- use any PCIe Edge TPU
       + "pci:<N>" -- use N-th PCIe Edge TPU

       If left as None, you cannot reliably predict which device you'll get.
       So if you have multiple Edge TPUs and want to run a specific model on
       each one, then you must specify the device.
     delegate: A pre-loaded Edge TPU delegate object, as provided by
       :func:`load_edgetpu_delegate`. If provided, the `device` argument
       is ignored.

  Returns:
     New ``tf.lite.Interpreter`` instance.
  """
  if device == "cpu":
      return Interpreter(model_path=model_path_or_content)
    
  try:
    if delegate:
      delegates = [delegate]
    else:
      delegates = [load_edgetpu_delegate({'device': device} if device else {})]

    if isinstance(model_path_or_content, bytes):
      return Interpreter(
          model_content=model_path_or_content, experimental_delegates=delegates)
    else:
      return Interpreter(
          model_path=model_path_or_content, experimental_delegates=delegates)
  except:
      return None
    

# ctypes definition of GstMapInfo. This is a stable API, guaranteed to be
# ABI compatible for any past and future GStreamer 1.0 releases.
# Used to get the underlying memory pointer without any copies, and without
# native library linking against libgstreamer.
class _GstMapInfo(ctypes.Structure):
  _fields_ = [
      ('memory', ctypes.c_void_p),  # GstMemory *memory
      ('flags', ctypes.c_int),  # GstMapFlags flags
      ('data', ctypes.c_void_p),  # guint8 *data
      ('size', ctypes.c_size_t),  # gsize size
      ('maxsize', ctypes.c_size_t),  # gsize maxsize
      ('user_data', ctypes.c_void_p * 4),  # gpointer user_data[4]
      ('_gst_reserved', ctypes.c_void_p * 4)
  ]  # GST_PADDING


# Try to import GStreamer but don't fail if it's not available. If not available
# we're probably not getting GStreamer buffers as input anyway.
_libgst = None
try:
  # pylint:disable=g-import-not-at-top
  import gi
  gi.require_version('Gst', '1.0')
  gi.require_version('GstAllocators', '1.0')
  # pylint:disable=g-multiple-import
  from gi.repository import Gst, GstAllocators
  _libgst = ctypes.CDLL(ctypes.util.find_library('gstreamer-1.0'))
  _libgst.gst_buffer_map.argtypes = [
      ctypes.c_void_p,
      ctypes.POINTER(_GstMapInfo), ctypes.c_int
  ]
  _libgst.gst_buffer_map.restype = ctypes.c_int
  _libgst.gst_buffer_unmap.argtypes = [
      ctypes.c_void_p, ctypes.POINTER(_GstMapInfo)
  ]
  _libgst.gst_buffer_unmap.restype = None
except (ImportError, ValueError, OSError):
  pass


def _is_valid_ctypes_input(input_data):
  if not isinstance(input_data, tuple):
    return False
  pointer, size = input_data
  if not isinstance(pointer, ctypes.c_void_p):
    return False
  return isinstance(size, int)


@contextlib.contextmanager
def _gst_buffer_map(buffer):
  """Yields gst buffer map."""
  mapping = _GstMapInfo()
  ptr = hash(buffer)
  success = _libgst.gst_buffer_map(ptr, mapping, Gst.MapFlags.READ)
  if not success:
    raise RuntimeError('gst_buffer_map failed')
  try:
    yield ctypes.c_void_p(mapping.data), mapping.size
  finally:
    _libgst.gst_buffer_unmap(ptr, mapping)


def _check_input_size(input_size, expected_input_size):
  if input_size < expected_input_size:
    raise ValueError('input size={}, expected={}.'.format(
        input_size, expected_input_size))


def run_inference(interpreter, input_data):
  """Performs interpreter ``invoke()`` with a raw input tensor.

  Args:
    interpreter: The ``tf.lite.Interpreter`` to invoke.
    input_data: A 1-D array as the input tensor. Input data must be uint8
      format. Data may be ``Gst.Buffer`` or :obj:`numpy.ndarray`.
  """
  input_shape = interpreter.get_input_details()[0]['shape']
  expected_input_size = np.prod(input_shape)

  interpreter_handle = interpreter._native_handle()  # pylint:disable=protected-access
  if isinstance(input_data, bytes):
    _check_input_size(len(input_data), expected_input_size)
    invoke_with_bytes(interpreter_handle, input_data)
  elif _is_valid_ctypes_input(input_data):
    pointer, actual_size = input_data
    _check_input_size(actual_size, expected_input_size)
    invoke_with_membuffer(interpreter_handle, pointer.value,
                          expected_input_size)
  elif _libgst and isinstance(input_data, Gst.Buffer):
    memory = input_data.peek_memory(0)
    map_buffer = not GstAllocators.is_dmabuf_memory(
        memory) or not supports_dmabuf(interpreter_handle)
    if not map_buffer:
      _check_input_size(memory.size, expected_input_size)
      fd = GstAllocators.dmabuf_memory_get_fd(memory)
      try:
        invoke_with_dmabuffer(interpreter_handle, fd, expected_input_size)
      except RuntimeError:
        # dma-buf input didn't work, likely due to old kernel driver. This
        # situation can't be detected until one inference has been tried.
        map_buffer = True
    if map_buffer:
      with _gst_buffer_map(input_data) as (pointer, actual_size):
        assert actual_size >= expected_input_size
        invoke_with_membuffer(interpreter_handle, pointer.value,
                              expected_input_size)
  elif isinstance(input_data, np.ndarray):
    _check_input_size(len(input_data), expected_input_size)
    invoke_with_membuffer(interpreter_handle, input_data.ctypes.data,
                          expected_input_size)
  else:
    raise TypeError('input data type is not supported.')
