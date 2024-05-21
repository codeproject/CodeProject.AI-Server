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

Running this directly from src\runtimes\bin\windows\python37:

cd \src\runtimes\bin\windows\python37
python.exe coral\pycoral\examples\classify_image.py --model coral\pycoral\test_data\mobilenet_v2_1.0_224_inat_bird_quant.tflite --labels coral\pycoral\test_data\inat_bird_labels.txt --input coral\pycoral\test_data\parrot.jpg



"""

import argparse
from datetime import datetime
import fnmatch
import os
import threading
import time

import numpy as np
from PIL import Image

# Make sure we can find the coral libraries
import platform
if platform.system() == "Darwin": # or platform.system() == "Linux"
    search_path = ''
    if platform.uname()[4] == 'x86_64' and platform.release()[:2] != '20':   # macOS 11 / Big Sur on Intel can install pycoral PIP
       search_path = f"./pycoral_simplified/"    # macOS will use the simplified library
    elif platform.uname()[4] == 'arm64' and platform.release()[:2] != '21':  # macOS 12 / Monterey on arm64 can install pycoral PIP
       search_path = f"./pycoral_simplified/"    # macOS will use the simplified library
    if search_path:
        import sys
        sys.path.insert(0, search_path)

from pycoral.adapters import common
from pycoral.adapters import detect
from pycoral.utils.dataset import read_label_file
from pycoral.utils.edgetpu import make_interpreter

interpreter         = None  # The model interpreter
interpreter_created = None  # When was the interpreter created?
last_check_time     = None  # When we last checked the health of the interpreter
labels              = None  # set of labels for this model
model_name          = None
model_size          = None
inference_device    = None
last_model_check    = None  # When were the models last checked?
model_list          = None
model_list_lock     = threading.Lock()

from options import Options

def init_detect(options: Options) -> str:

    global interpreter
    global interpreter_created
    global labels
    global model_name
    global model_size

    error = ""

    model_name  = options.model_name
    model_size  = options.model_size

    # SANITY CHECK
    if model_name not in [ "mobilenet ssd", "efficientdet-lite" ]: # "yolov5", "yolov8" aren't yet supported
        model_name = "mobilenet ssd"
        options.set_model(model_name)

    # edge_tpu   = options.enable_GPU # Assuming this correctly tests for Coral TPU
    # model_file = options.model_tpu_file if edge_tpu else options.model_cpu_file
   
    # Read labels
    try:
        labels = read_label_file(options.label_file) if options.label_file else {}
    except:
        labels = {}

    # Initialize TF-Lite interpreter.
    device = ""
    try:
        device = "TPU"
        if os.path.exists(options.model_tpu_file):
            interpreter = make_interpreter(options.model_tpu_file, device=None, delegate=None)
        if not interpreter:
            print("Info: Unable to use TPU (make_interpreter faileD). Falling back to CPU")
            device = "CPU"
            if os.path.exists(options.model_cpu_file):
                interpreter = make_interpreter(options.model_cpu_file, device="cpu", delegate=None)
            else:
                error = "Supplied CPU model doesn't exist"

    except Exception as ex:
        try:
            print("Info: Unable to find or initialise the Coral TPU. Falling back to CPU-only.")
            device = "CPU"

            # We can't use the EdgeTPU libraries for making an interpreter because we don't have an
            # edge TPU device. So, fallback to plain TFLite
            # interpreter = make_interpreter(options.model_cpu_file, device=None, delegate=None)
            if os.path.exists(options.model_cpu_file):
                import tflite_runtime.interpreter as tflite
                interpreter = tflite.Interpreter(options.model_cpu_file, None)
            else:
                error = "Supplied CPU model doesn't exist"
                
        except Exception as ex:
            error = "Error creating interpreter (Coral issue)"
            print("Error creating interpreter: " + str(ex))
            try:
                interpreter = None
            except: 
                # __del__ can throw here.
                pass

    if not interpreter:
        device = ""
    else:
        interpreter.allocate_tensors()
        interpreter_created = datetime.now()

        """
        # Model must be uint8 quantized
        if common.input_details(interpreter, 'dtype') != np.uint8:
            raise ValueError('Only support uint8 input type.')
        """

        # Get input and output tensors.
        input_details  = interpreter.get_input_details()
        output_details = interpreter.get_output_details()

        print(f"Debug: Input details: {input_details[0]}\n")
        print(f"Debug: Output details: {output_details[0]}\n")

    return device, error


def list_models(options:Options):
    
    global last_model_check
    global model_list

    supported_models = [ 'MobileNet SSD', 'EfficientDet-Lite' ] 

    # Check to make sure we aren't checking too often
    now_ts = datetime.now()
    if not model_list or not last_model_check or \
       (now_ts - last_model_check).total_seconds() > 30:

        last_model_check = now_ts

        with model_list_lock:
            model_list = []
            for model_name in supported_models:
                model_index = model_name.lower()
                pattern     = options.MODEL_SETTINGS[model_index][options.model_size].model_name_pattern
                if os.path.exists(options.models_dir):
                    for file in os.listdir(options.models_dir):
                        if fnmatch.fnmatch(file, '*' + pattern + '*'):
                            model_list.append(model_name)
                            break

    return {
        "success": True,
        "models":  model_list
    }


def reset_detector():
    global interpreter

    print("Info: Refreshing the Tensorflow Interpreter")
    interpreter = None


def periodic_check(options: Options, force: bool = False,
                   check_refresh: bool = True) -> tuple[bool, str]:
    """
    Run a periodic check to ensure the interpreters are good and we don't need
    to (re)initialize the interpreters. The system is setup to refresh the TF
    interpreters once an hour.

    @param options       - options for creating interpreters
    @param force         - force the recreation of interpreters
    @param check_refresh - check for, and refresh, old interpreters
    
    I suspect that many of the problems reported with the use of the Coral
    TPUs were due to overheating chips. There were a few comments along the
    lines of: "Works great, but after running for a bit it became unstable
    and crashed. I had to back way off and it works fine now" This seems
    symptomatic of the TPU throttling itself as it heats up, reducing its
    own workload, and giving unexpected results to the end user.
    """
    global interpreter
    global interpreter_created
    global last_check_time
    global model_name
    global model_size
    global inference_device

    now_ts = datetime.now()
    
    if not interpreter:
        print("No interpreter found. Recreating.")
        force = True

    # Force if we've changed the model
    if options.model_name != model_name or \
        options.model_size != model_size:
        print("Model change detected. Forcing model reload.")
        force = True

    # Check to make sure we aren't checking too often
    if interpreter and last_check_time != None and \
        not force and (now_ts - last_check_time).total_seconds() < 10:
        return True, None

    last_check_time = now_ts
    
    # Once an hour, refresh the interpreter
    if (force or check_refresh) and interpreter:
        current_age_sec = (now_ts - interpreter_created).total_seconds()
        if force or current_age_sec > options.interpreter_lifespan_secs:
            reset_detector()

    # (Re)start them if needed
    error = None
    if not interpreter:
        (inference_device, error) = init_detect(options)

    return bool(interpreter), error


def do_detect(options: Options, img: Image, score_threshold: float = 0.5):

    global interpreter
    global interpreter_created

    mean  = 128 # args.input_mean
    std   = 128 # args.input_std
    top_k = 1

    # Once in a while refresh the interpreter
    (device, error) = periodic_check(options)

    if not interpreter:
        return {
            "success"     : False,
            "error"       : error,
            "count"       : 0,
            "predictions" : [],
            "inferenceMs" : 0
        }

    w,h = img.size
    # print("Debug: Input(height, width): ", h, w)

    _, scale = common.set_resized_input(
        interpreter, img.size, lambda size: img.resize(size, Image.Resampling.LANCZOS))

    """
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
    """

    # Run inference
    start_inference_time = time.perf_counter()
    try:
        interpreter.invoke()
    except Exception as ex:
        return {
            "success"     : False,
            "count"       : 0,
            "error"       : "Unable to run inference: " + str(ex),
            "predictions" : [],
            "inferenceMs" : 0
        }

    inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

    # Get output
    outputs = []
    objs = detect.get_objects(interpreter, score_threshold, scale)
    for obj in objs:
        class_id = obj.id
        caption  = labels.get(class_id, class_id)
        score    = float(obj.score)
        # ymin, xmin, ymax, xmax = obj.bbox
        xmin, ymin, xmax, ymax = obj.bbox

        if score >= score_threshold:
            detection = {
                "confidence": score,
                "label": caption,
                "x_min": xmin,
                "y_min": ymin,
                "x_max": xmax,
                "y_max": ymax,
            }

            outputs.append(detection)

    return {
        "success"         : True,
        "count"           : len(outputs),
        "predictions"     : outputs,
        "inferenceMs"     : inferenceMs,
        "inferenceDevice" : inference_device
    }


# ------------------------------------------------------------------------------
# For Debug / command line calls

from PIL import Image
from PIL import ImageDraw

def draw_objects(draw, objs, labels):
  """Draws the bounding box and label for each object."""
  for obj in objs:
    bbox = obj.bbox
    draw.rectangle([(bbox.xmin, bbox.ymin), (bbox.xmax, bbox.ymax)],
                   outline='red')
    draw.text((bbox.xmin + 10, bbox.ymin + 10),
              '%s\n%.2f' % (labels.get(obj.id, obj.id), obj.score),
              fill='red')

def main():
  parser = argparse.ArgumentParser(
      formatter_class=argparse.ArgumentDefaultsHelpFormatter)
  parser.add_argument('-m', '--model', required=True,
                      help='File path of .tflite file')
  parser.add_argument('-i', '--input', required=True,
                      help='File path of image to process')
  parser.add_argument('-l', '--labels', help='File path of labels file')
  parser.add_argument('-t', '--threshold', type=float, default=0.4,
                      help='Score threshold for detected objects')
  parser.add_argument('-o', '--output',
                      help='File path for the result image with annotations')
  parser.add_argument('-c', '--count', type=int, default=5,
                      help='Number of times to run inference')
  args = parser.parse_args()

  labels = read_label_file(args.labels) if args.labels else {}
  interpreter = make_interpreter(args.model)
  interpreter.allocate_tensors()

  image = Image.open(args.input)
  _, scale = common.set_resized_input(
      interpreter, image.size, lambda size: image.resize(size, Image.Resampling.LANCZOS)) # replaces deprecated Image.ANTIALIAS

  print('----INFERENCE TIME----')
  print('Note: The first inference is slow because it includes',
        'loading the model into Edge TPU memory.')
  for _ in range(args.count):
    start = time.perf_counter()
    interpreter.invoke()
    inference_time = time.perf_counter() - start
    objs = detect.get_objects(interpreter, args.threshold, scale)
    print('%.2f ms' % (inference_time * 1000))

  print('-------RESULTS--------')
  if not objs:
    print('No objects detected')

  for obj in objs:
    print(labels.get(obj.id, obj.id))
    print('  id:    ', obj.id)
    print('  score: ', obj.score)
    print('  bbox:  ', obj.bbox)

  if args.output:
    image = image.convert('RGB')
    draw_objects(ImageDraw.Draw(image), objs, labels)
    image.save(args.output)
    image.show()

if __name__ == '__main__':
  main()
