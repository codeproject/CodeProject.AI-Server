"""
    TensorFlow Lite Object detection benchmark with OpenCV.

    Copyright (c) 2020 Nobuo Tsukamoto

    This software is released under the MIT License.
    See the LICENSE file in the project root for more information.

    python3 deeplab_tflite_image_opencv.py --help
    usage: deeplab_tflite_image_opencv.py [-h] --model MODEL [--input_shape INPUT_SHAPE] [--thread THREAD] [--input INPUT] [--output OUTPUT]

    options:
    -h, --help            show this help message and exit
    --model MODEL         File path of Tflite model.
    --input_shape INPUT_SHAPE
                            Specify an input shape for inference.
    --thread THREAD       Num threads.
    --input INPUT         File path of image.
    --output OUTPUT       File path of result.    
"""

import argparse
import collections
import time

import cv2
import numpy as np

from label_util import read_label_file
from tflite_util import get_output_results, make_interpreter, set_input_tensor

from PIL import Image
from PIL import ImageDraw


interpreter = None
labels      = None
edge_tpu    = False

Object = collections.namedtuple('Object', ['label', 'score', 'bbox'])

from options import Options


def get_output(interpreter, score_threshold):
    """Returns list of detected objects.

    Args:
        interpreter
        score_threshold

    Returns: bounding_box, class_id, score
    """

    # Get all output details
    """
    if edge_tpu:
        class_ids = get_output_tensor(interpreter, 3)
        boxes     = get_output_tensor(interpreter, 1)
        count     = int(get_output_tensor(interpreter, 2))
        scores    = get_output_tensor(interpreter, 0) 
    else:
        scores    = get_output_tensor(interpreter, 0)
        boxes     = get_output_tensor(interpreter, 1)
        count     = int(get_output_tensor(interpreter, 2))
        class_ids = get_output_tensor(interpreter, 3)
    """
    count     = int(get_output_results(interpreter, "count"))
    boxes     = get_output_results(interpreter, "boxes")
    class_ids = get_output_results(interpreter, "classes")
    scores    = get_output_results(interpreter, "scores") 

    results = []
    for i in range(count):
        if scores[i] >= score_threshold:
            result = {
                "bounding_box": boxes[i],
                "class_id": int(class_ids[i]),
                "score": scores[i],
            }
            results.append(result)

    return results


def init_detect(options: Options):

    global interpreter
    global labels
    global edge_tpu

    # edge_tpu   = options.support_GPU # Assuming this correctly tests for Coral TPU
    # model_file = options.model_tpu_file if edge_tpu else options.model_cpu_file

    # Initialize TF-Lite interpreter.
    (interpreter, edge_tpu) = make_interpreter(options.model_tpu_file, options.model_cpu_file, options.num_threads)

    # Get input and output tensors.
    input_details = interpreter.get_input_details()
    output_details = interpreter.get_output_details()

    interpreter.allocate_tensors()

    print(f"Input details: {input_details[0]}\n")
    print(f"Output details: {output_details[0]}\n")

    # Read label and generate random colors.
    labels = read_label_file(options.label_file) if options.label_file else None


def do_detect(img: Image, score_threshold: float = 0.5):

    w,h = img.size
    print("Input(height, width): ", h, w)

    numpy_image = np.array(img)

    input_im = cv2.cvtColor(numpy_image, cv2.COLOR_BGR2RGB)

    _, height, width, channel = interpreter.get_input_details()[0]["shape"]
    resize_im = cv2.resize(input_im, (width, height))
    # resize_im = resize_im / 127.5 -1.

    set_input_tensor(interpreter, resize_im)

    start_inference_time = time.perf_counter()
    interpreter.invoke()
    inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)

    objs = get_output(interpreter, score_threshold)

    # Get results
    outputs = []
    for i, obj in enumerate(objs):
        class_id = int(obj["class_id"])
        caption  = labels[class_id] if class_id in labels else class_id
        score    = float(obj["score"])

        # Convert the bounding box figures from relative coordinates
        # to absolute coordinates based on the original resolution
        if edge_tpu:
            ymin, xmin, ymax, xmax = obj["bounding_box"]
        else:
            ymin, xmin, ymax, xmax = obj["bounding_box"]
        xmin = int(xmin * w)
        xmax = int(xmax * w)
        ymin = int(ymin * h)
        ymax = int(ymax * h)

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
        "success"     : True,
        "count"       : len(outputs),
        "predictions" : outputs,
        "inferenceMs" : inferenceMs
    }


def draw_object(draw, obj):
    """Draws detection candidate on the image.

    Args:
        draw: the PIL.ImageDraw object that draw on the image.
        obj: The detection candidate.
    """
    draw.rectangle(obj.bbox, outline='red')
    draw.text((obj.bbox[0], obj.bbox[3]), obj.label, fill='#0000')
    draw.text((obj.bbox[0], obj.bbox[3] + 10), str(obj.score), fill='#0000')

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", help="File path of Tflite model.", required=True)
    parser.add_argument("--image", help="File path of image file.", required=True)
    parser.add_argument("--thread", help="Num threads.", default=2, type=int)
    parser.add_argument("--count", help="Repeat count.", default=100, type=int)
    parser.add_argument("--threshold", help="threshold to filter results.", default=0.5, type=float)
    args = parser.parse_args()

    model_file      = args.model     # assets/tf2_ssd_mobilenet_v1_fpn_640x640_coco17_ptq_edgetpu.tflite
    label_file      = args.label     # assets/coco_labels.txt
    score_threshold = args.threshold # 0.5
    input_file      = args.image     # assets/pexels-tima-miroshnichenko-6694964.jpg
    output_file     = args.output    # object_detection_results.jpg

    # Open image.
    img = Image.open(input_file).convert('RGB')
    draw = ImageDraw.Draw(img)

    init_detect(model_file, label_file)
    result = do_detect(img, score_threshold)

    if result.success:
        for detection in result.detections:
            bbox = [detection['x_min'], detection['y_min'], detection['x_max'], detection['y_max']]
            obj = Object(detection['label'], detection['confidence'], bbox)
            draw_object(draw, obj)

        # img.show()
        if output_file:
            img.save(output_file)
            print('Done. Results saved at', output_file)

if __name__ == '__main__':
    main()