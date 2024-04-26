import argparse
import os
from os.path import exists
import time
from threading import Lock

import torch
from PIL import Image, ImageDraw, UnidentifiedImageError

from ultralytics import YOLO

# Setup a global bucket of YOLO detectors. One for each model
detectors   = {}  # We'll use this to cache the detectors based on models
models_lock = Lock()

def get_detector(models_dir: str, model_name: str, resolution: int,
                 use_Cuda: bool, accel_device_name: int, use_MPS: bool,
                 use_DirectML: bool, half_precision: str) -> any:

    """
    We have a detector for each custom model. Lookup the detector, or if it's 
    not found, create a new one and add it to our lookup.
    """

    detector = detectors.get(model_name, None)
    if detector is None:
        with models_lock:
            detector = detectors.get(model_name, None)
            half     = False

            if detector is None:
                model_path = os.path.join(models_dir, model_name + ".pt")

                if use_Cuda:
                    print("Using CUDA")
                    device_type = "cuda"
                    if accel_device_name:
                        device = torch.device(accel_device_name)
                    else:
                        device = torch.device("cuda")
                    device_name = torch.cuda.get_device_name(device)

                    print(f"GPU compute capability is {torch.cuda.get_device_capability()[0]}.{torch.cuda.get_device_capability()[1]}")

                    # Use half-precision if possible. There's a bunch of NVIDIA cards where
                    # this won't work
                    half = half_precision != 'disable'
                    if half:
                        print(f"Using half-precision for the device '{device_name}'")
                    else:
                        print(f"Not using half-precision for the device '{device_name}'")
                
                elif use_MPS:
                    print("Using MPS")
                    device_type = "mps"
                    device_name = "Apple Silicon GPU"
                    device      = torch.device(device_type)

                elif use_DirectML:
                    print("Using DirectML")
                    device_type = "cpu"
                    device_name = "DirectML"                    
                    # Torch-DirectlML throws "Cannot set version_counter for inference tensor"
                    import torch_directml
                    device = torch_directml.device()

                else:
                    print("Using CPU")
                    device_type = "cpu"
                    device_name = "CPU"
                    device = torch.device(device_type)

                print(f"Inference processing will occur on device '{device_name}'")

                if exists(model_path):
                    try:
                        detector = YOLO(model_path)
                        detectors[model_name] = detector
                        print(f"Model Path is {model_path}")

                    except Exception as ex:
                        print(f"Unable to load model at {model_path} ({str(ex)})")
                        detector = None

                else:
                    print(f"{model_path} does not exist")

    return detector

def do_detection(img: any, threshold: float = 0.4, models_dir: str = "assets",
                 model_name: str = "yolov8m", resolution: int = 640,
                 use_Cuda: bool = False, accel_device_name: int = 0,
                 use_MPS: bool = False, use_DirectML: bool = False,
                 half_precision: str = "enable"):
    
    create_err_msg     = f"Unable to create YOLO detector for model {model_name}"
    start_process_time = time.perf_counter()

    # Lookup the detector, or if it's not found, create a new one and add it to our lookup.
    detector = None
    try:
        detector = get_detector(models_dir, model_name, resolution, use_Cuda,
                                accel_device_name, use_MPS, use_DirectML,
                                half_precision)
    except Exception as ex:
        create_err_msg = f"{create_err_msg} ({str(ex)})"
    if detector is None:
        print(create_err_msg)
        return { "success": False, "error": create_err_msg }
    
    # We have a detector for this model, so let's go ahead and detect
    try:
        start_inference_time = time.perf_counter()

        use_half    = half_precision == "true"
        results     = detector.predict(img, imgsz=int(resolution), half=use_half)
        inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

        outputs = []

        # Process results list
        for result in results:           
            boxes = result.boxes        # Boxes object for bbox outputs
            for i in range(len(boxes.conf)):
                score = boxes.conf[i].item()
                if score >= threshold:
                    x_min = boxes.xyxy[i][0].item()
                    y_min = boxes.xyxy[i][1].item()
                    x_max = boxes.xyxy[i][2].item()
                    y_max = boxes.xyxy[i][3].item()

                    label = detector.names[int(boxes.cls[i].item())]

                    detection = {
                        "confidence": score,
                        "label": label,
                        "x_min": int(x_min),
                        "y_min": int(y_min),
                        "x_max": int(x_max),
                        "y_max": int(y_max),
                    }

                    outputs.append(detection)

        if len(outputs) > 3:
            message = 'Found ' + (', '.join(prediction["label"] for prediction in outputs[0:3])) + "..."
        elif len(outputs) > 0:
            message = 'Found ' + (', '.join(prediction["label"] for prediction in outputs))
        else:
            message = "No objects found"

        return {
            "message"     : message,
            "count"       : len(outputs),
            "predictions" : outputs,
            "success"     : True,
            "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
            "inferenceMs" : inferenceMs
        }

    except UnidentifiedImageError as img_ex:
        print("The image provided was of an unknown type")
        return { "success": False, "error": "invalid image file"}

    except Exception as ex:
        print("Exception: " + str(ex))
        return { "success": False, "error": "Error occurred on the server" }


def draw_predictions(draw, predictions):
  """Draws the bounding box and label for each object."""
  for prediction in predictions:
    draw.rectangle([(prediction['x_min'], prediction['y_min']), (prediction['x_max'], prediction['y_max'])],
                   outline='yellow', width=3)
    draw.text((prediction['x_min'] + 10, prediction['y_min'] + 10),
              '%s %.2f%%' % (prediction['label'], int(prediction['confidence']*100)),
              fill='yellow')

def main():
  parser = argparse.ArgumentParser(formatter_class=argparse.ArgumentDefaultsHelpFormatter)
  parser.add_argument('-i', '--input', required=True, help='File path of image to process')
  parser.add_argument('-m', '--model', type=str, default="yolov8m", help='File name of .pt model file (no extension)')
  parser.add_argument('-a', '--assets', type=str, default="assets", help='Path to model files')
  parser.add_argument('-d', '--device', type=str, default="cpu", help='Device to use. cpu, cuda:0 etc')
  parser.add_argument('-p', '--half-precision', type=str, default="enable", help='File path of .pt file')
  parser.add_argument('-r', '--resolution', type=int, default=640, help='Image resolution')
  parser.add_argument('-t', '--threshold', type=float, default=0.4, help='Score threshold for detected objects')
  parser.add_argument('-o', '--output', type=str, default="yolov8m_result.jpg", help='File path for the result image with annotations')
  args = parser.parse_args()

  image = Image.open(args.input)

  results = do_detection(image, args.threshold, args.assets, args.model, args.resolution,
                         use_Cuda=False, accel_device_name=0, use_MPS=False, use_DirectML=False,
                         half_precision=args.half_precision)
  print(results)

  if not results or not results["success"]:
    print('Operation failed')
  elif "predictions" not in results or not results["predictions"]:
    print('No objects detected')
  else:
    print(f"Label        confidence    bounding box")
    print(f"====================================================")
    for prediction in results["predictions"]:
      box = f"({prediction['x_min']}, {prediction['y_min']})".ljust(12) + " - " \
          + f"({prediction['x_max']}, {prediction['y_max']})".ljust(12)
      print(f"{prediction['label'].ljust(15)} {int(prediction['confidence'] * 100)}%        {box}")

    if args.output:
        image = image.convert('RGB')
        draw_predictions(ImageDraw.Draw(image), results["predictions"])
    image.save(args.output)
    image.show()
  
if __name__ == '__main__':
  main()
