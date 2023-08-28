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
r"""Example using PyCoral to classify a given image using an Edge TPU.

To run this code, you must attach an Edge TPU attached to the host and
install the Edge TPU runtime (`libedgetpu.so`) and `tflite_runtime`. For
device setup instructions, see coral.ai/docs/setup.

Example usage:
```
bash examples/install_requirements.sh classify_image.py

python3 examples/classify_image.py \
  --model test_data/mobilenet_v2_1.0_224_inat_bird_quant_edgetpu.tflite  \
  --labels test_data/inat_bird_labels.txt \
  --input test_data/parrot.jpg
```

Running this directly in Windows from src\runtimes\bin\windows\python37:

    cd <root>\src\runtimes\bin\windows\python37
    python.exe coral\pycoral\examples\classify_image.py --model coral\pycoral\test_data\mobilenet_v2_1.0_224_inat_bird_quant.tflite --labels coral\pycoral\test_data\inat_bird_labels.txt --input coral\pycoral\test_data\parrot.jpg

"""

import argparse
from datetime import datetime
import time

import numpy as np
from PIL import Image

from pycoral.adapters import common
from pycoral.adapters import classify
from pycoral.utils.dataset import read_label_file
from pycoral.utils.edgetpu import make_interpreter

interpreter_lifespan_secs = 3600  # Refresh the interpreter once an hour

interpreter         = None  # The model interpreter
interpreter_created = None  # When was the interpreter created?
labels              = None  # set of labels for this model


from options import Options

def init_classify(options: Options):

    global interpreter
    global interpreter_created
    global labels

    # edge_tpu   = options.support_GPU # Assuming this correctly tests for Coral TPU
    # model_file = options.model_tpu_file if edge_tpu else options.model_cpu_file
   
    # Read labels
    labels = read_label_file(options.label_file) if options.label_file else {}

    # Initialize TF-Lite interpreter.
    try:
        interpreter = make_interpreter(options.model_tpu_file, device=None, delegate=None)
    except Exception as ex:
        print("Error creating interpreter: " + str(ex))
        interpreter = None
        return;

    interpreter.allocate_tensors()

    interpreter_created = datetime.now()

    # Model must be uint8 quantized
    if common.input_details(interpreter, 'dtype') != np.uint8:
        raise ValueError('Only support uint8 input type.')

    # Get input and output tensors.
    input_details  = interpreter.get_input_details()
    output_details = interpreter.get_output_details()

    print(f"Debug: Input details: {input_details[0]}\n")
    print(f"Debug: Output details: {output_details[0]}\n")


def do_classify(options: Options, img: Image, score_threshold: float = 0.5):

    global interpreter
    global interpreter_created

    mean  = 128 # args.input_mean
    std   = 128 # args.input_std
    top_k = 1

    # Once an hour, refresh the interpreter
    if interpreter != None:
        seconds_since_created = (datetime.now() - interpreter_created).total_seconds()
        if seconds_since_created > interpreter_lifespan_secs:
            print("Info: Refreshing the Tensorflow Interpreter")
            interpreter = None

    if interpreter == None:
        init_detect(options)

    if interpreter == None:
        return {
            "success"     : False,
            "error"       : "Unable to create interpreter",
            "count"       : 0,
            "predictions" : [],
            "inferenceMs" : 0
        }

    w,h = img.size
    print("Debug: Input(height, width): ", h, w)

    size = common.input_size(interpreter)
    resize_im = img.convert('RGB').resize(size, Image.ANTIALIAS)

    # numpy_image = np.array(img)
    # input_im = cv2.cvtColor(numpy_image, cv2.COLOR_BGR2RGB)
    # resize_im = cv2.resize(input_im, size)

    # Image data must go through two transforms before running inference:
    #   1. normalization: f = (input - mean) / std
    #   2. quantization: q = f / scale + zero_point
    # The following code combines the two steps as such:
    #   q = (input - mean) / (std * scale) + zero_point
    # However, if std * scale equals 1, and mean - zero_point equals 0, the input
    # does not need any preprocessing (but in practice, even if the results are
    # very close to 1 and 0, it is probably okay to skip preprocessing for better
    # efficiency; we use 1e-5 below instead of absolute zero).

    params     = common.input_details(interpreter, 'quantization_parameters')
    scale      = params['scales']
    zero_point = params['zero_points']

    if abs(scale * std - 1) < 1e-5 and abs(mean - zero_point) < 1e-5:
        # Input data does not require preprocessing.
        common.set_input(interpreter, resize_im)
    else:
        # Input data requires preprocessing
        normalized_input = (np.asarray(resize_im) - mean) / (std * scale) + zero_point
        np.clip(normalized_input, 0, 255, out=normalized_input)
        common.set_input(interpreter, normalized_input.astype(np.uint8))

    # Run inference
    start_inference_time = time.perf_counter()
    interpreter.invoke()
    inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

    # Get output
    classes = classify.get_classes(interpreter, top_k, score_threshold)
    objs = []
    for c in classes:
        detection = {
          "class_id": c.id,
          "score": c.score,
          "bounding_box": (0,0,0,0)
        }
        objs.append(detection)

    # Generate results
    outputs = []
    for i, obj in enumerate(objs):
        class_id = int(obj["class_id"])
        caption  = labels.get(class_id, class_id) if class_id in labels else class_id
        score    = float(obj["score"])

        if score >= score_threshold:
            detection = {
                "confidence": score,
                "label": caption
            }

            outputs.append(detection)

    return {
        "success"     : True,
        "count"       : len(outputs),
        "predictions" : outputs,
        "inferenceMs" : inferenceMs
    }


def main():
    parser = argparse.ArgumentParser(
        formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    parser.add_argument(
        '-m', '--model', required=True, help='File path of .tflite file.')
    parser.add_argument(
        '-i', '--input', required=True, help='Image to be classified.')
    parser.add_argument(
        '-l', '--labels', help='File path of labels file.')
    parser.add_argument(
        '-k', '--top_k', type=int, default=1,
        help='Max number of classification results')
    parser.add_argument(
        '-t', '--threshold', type=float, default=0.0,
        help='Classification score threshold')
    parser.add_argument(
        '-c', '--count', type=int, default=5,
        help='Number of times to run inference')
    parser.add_argument(
        '-a', '--input_mean', type=float, default=128.0,
        help='Mean value for input normalization')
    parser.add_argument(
        '-s', '--input_std', type=float, default=128.0,
        help='STD value for input normalization')
    args = parser.parse_args()

    labels = read_label_file(args.labels) if args.labels else {}

    interpreter = make_interpreter(*args.model.split('@'))
    interpreter.allocate_tensors()

    # Model must be uint8 quantized
    if common.input_details(interpreter, 'dtype') != np.uint8:
        raise ValueError('Only support uint8 input type.')

    size = common.input_size(interpreter)
    image = Image.open(args.input).convert('RGB').resize(size, Image.ANTIALIAS)

    # Image data must go through two transforms before running inference:
    # 1. normalization: f = (input - mean) / std
    # 2. quantization: q = f / scale + zero_point
    # The following code combines the two steps as such:
    # q = (input - mean) / (std * scale) + zero_point
    # However, if std * scale equals 1, and mean - zero_point equals 0, the input
    # does not need any preprocessing (but in practice, even if the results are
    # very close to 1 and 0, it is probably okay to skip preprocessing for better
    # efficiency; we use 1e-5 below instead of absolute zero).
    params = common.input_details(interpreter, 'quantization_parameters')
    scale = params['scales']
    zero_point = params['zero_points']
    mean = args.input_mean
    std = args.input_std
    if abs(scale * std - 1) < 1e-5 and abs(mean - zero_point) < 1e-5:
        # Input data does not require preprocessing.
        common.set_input(interpreter, image)
    else:
        # Input data requires preprocessing
        normalized_input = (np.asarray(image) - mean) / (std * scale) + zero_point
        np.clip(normalized_input, 0, 255, out=normalized_input)
        common.set_input(interpreter, normalized_input.astype(np.uint8))

    # Run inference
    print('----INFERENCE TIME----')
    print('Note: The first inference on Edge TPU is slow because it includes',
          'loading the model into Edge TPU memory.')
    for _ in range(args.count):
        start = time.perf_counter()
        interpreter.invoke()
        inference_time = time.perf_counter() - start
        classes = classify.get_classes(interpreter, args.top_k, args.threshold)
        print('%.1fms' % (inference_time * 1000))

    print('-------RESULTS--------')
    for c in classes:
        print('%s: %.5f' % (labels.get(c.id, c.id), c.score))

if __name__ == '__main__':
  main()
