#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
    TensorFlow Lite Utils.

    Copyright (c) 2020 Nobuo Tsukamoto
    Modified 2023 Chris Maunder @ CodeProject

    This software is released under the MIT License.
    See the LICENSE file in the project root for more information.
"""

from ctypes import *
from typing import Tuple
import numpy as np

def make_interpreter(tpu_model_file: str, cpu_model_file: str = None, 
                     num_of_threads: int = 1) -> Tuple[any, bool]:
    """ make tf-lite interpreter.

    If tpu_model_file is provided, but no cpu_model_file, then we assume the 
    caller has determined the libraries and hardware that is available and has 
    supplied a suitable file. Otherwise, this method will assume the model file 
    is an edgetpu model but will sniff libraries and hardware and fallback to 
    cpu_model_file if edge TPU support isn't available.

    Args:
        tpu_model_file: Model file path for TPUs. 
        cpu_model_file: Model file path for CPUs. 
        num_of_threads: Num of threads.

    Return:
        tf-lite interpreter.
    """

    # First determine if we have TensorFlow-Lite runtime installed, or the whole Tensorflow
    # In either case we're looking to load TFLite models
    try:
        from tflite_runtime.interpreter import Interpreter, load_delegate
    except ImportError:
        import tensorflow as tf
        Interpreter, load_delegate = tf.lite.Interpreter, tf.lite.experimental.load_delegate

    # Initially try loading EdgeTPU delegates for the Coral TPU. If this fails fallback.
    # For Coral edge TPU you load up a delegate that will handle the TPU computations, and
    # pass that to the Interpreter constructor. Everything else is vanilla TFLite.
    # https://coral.ai/docs/edgetpu/tflite-python/#update-existing-tf-lite-code-for-the-edge-tpu
    delegates = None

    # Only try and load delegates if we're trying to use a TPU
    if tpu_model_file:
        try:
            import platform
            delegate = {
                'Linux': 'libedgetpu.so.1',
                'Darwin': 'libedgetpu.1.dylib',
                'Windows': 'edgetpu.dll'}[platform.system()]
            delegates = [load_delegate(delegate)]
        except Exception as ex:
            pass

    interpreter = None
    edge_tpu    = False

    if delegates and tpu_model_file:
        try:
            # TensorFlow-Lite loading a TF-Lite TPU model
            # CRASH: On Windows, the interpreter.__init__ method accepts experimental
            # delegates. These are used in self._interpreter.ModifyGraphWithDelegate, 
            # which fails on Windows
            interpreter = Interpreter(model_path=tpu_model_file, experimental_delegates=delegates)
            edge_tpu = True
        except Exception as ex:
            # Fall back
            if cpu_model_file:
                interpreter = Interpreter(model_path=cpu_model_file)
    else:
        # TensorFlow loading a TF-Lite CPU model
        if cpu_model_file:
            interpreter = Interpreter(model_path=cpu_model_file)
    
    return (interpreter, edge_tpu)

    """
    if "edgetpu.tflite" in model_file and EDGETPU_SHARED_LIB:
        print("EdgeTpu delegate")
        return tflite.Interpreter(
            model_path=model_file,
            experimental_delegates=[tflite.load_delegate(EDGETPU_SHARED_LIB)],
        )
    elif delegate_library is not None:
        print("{} delegate".format(os.path.splitext(os.path.basename(delegate_library))[0]))
        option = {"backends": "CpuAcc",
                  "logging-severity": "info",
                  "number-of-threads": str(num_of_threads),
                  "enable-fast-math":"true"}
        print(option)
        return tflite.Interpreter(
            model_path=model_file,
            experimental_delegates=[
                tflite.load_delegate(delegate_library, options=option)
            ],
        )
    else:
        return tflite.Interpreter(model_path=model_file, num_threads=num_of_threads)
    """

def set_input_tensor(interpreter, image):
    """ Sets the input tensor.

    Args:
        interpreter: Interpreter object.
        image: a function that takes a (width, height) tuple, 
        and returns an RGB image resized to those dimensions.
    """
    tensor_index = interpreter.get_input_details()[0]["index"]
    input_tensor = interpreter.tensor(tensor_index)()[0]
    input_tensor[:, :] = image.copy()

def get_output_tensor(interpreter, index):
    """ Returns the output tensor at the given index.

    Args:
        interpreter
        index

    Returns:
        tensor
    """
    output_details = interpreter.get_output_details()[index]
    tensor = np.squeeze(interpreter.get_tensor(output_details["index"]))
    return tensor

def get_output_results(interpreter, field: str):
    """ Returns the output tensor at the given index.

    Args:
        interpreter
        index

    Returns:
        tensor
    """
    tensor = None

    for index in range(4):
        output_details = interpreter.get_output_details()[index]
        tensor = interpreter.get_tensor(output_details["index"])
        dimensions = np.ndim(tensor)

        if dimensions == 3 and field == "boxes":
            break
        
        if dimensions == 1 and field == "count":
            break

        if dimensions == 2:
            if tensor.max() > 1.0 and field == "classes":
                break
            if tensor.max() <= 1.0 and field == "scores":
                break

    return np.squeeze(tensor)

