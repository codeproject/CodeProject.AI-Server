# Import our general libraries
import io
import math
import time
from typing import Tuple

import utils.tools as tool
from utils.cartesian import *

from module_runner import ModuleRunner
from module_logging import LogVerbosity
from common import JSON
import ast

from PIL import Image, UnidentifiedImageError
import cv2
import numpy as np

from options import Options
import fastdeploy as fd

# Constants
debug_image     = False
debug_log       = False
no_plate_found  = 'Characters Not Found'

# Globals
ocr                  = None
previous_label       = None
prev_avg_char_height = None
prev_avg_char_width  = None
resize_width_factor  = None
resize_height_factor = None


def init_detect_platenumber(opts: Options) -> None:

    global max_size, resize_width_factor, resize_height_factor, license_plate_model, det_model, rec_model
    
    max_size = 640
    
    resize_width_factor  = opts.OCR_rescale_factor
    resize_height_factor = opts.OCR_rescale_factor

    runtime_option = fd.RuntimeOption()
    runtime_option.use_rknpu2()

    # Detection model, detection text box
    det_model_file = opts.det_model_path
    det_params_file = ""
        
    # Recognition model, text recognition model
    rec_model_file = opts.rec_model_path
    rec_params_file = ""
    rec_label_file = opts.rec_label_file

    license_plate_model_file = opts.license_plate_model_path

    det_model = fd.vision.ocr.DBDetector(
        det_model_file,
        det_params_file,
        runtime_option=runtime_option,
        model_format=fd.ModelFormat.RKNN)

    rec_model = fd.vision.ocr.Recognizer(
        rec_model_file,
        rec_params_file,
        rec_label_file,
        runtime_option=runtime_option,
        model_format=fd.ModelFormat.RKNN)

    license_plate_model = fd.vision.detection.RKYOLOV5(
        license_plate_model_file,
        runtime_option=runtime_option,
        model_format=fd.ModelFormat.RKNN)

    license_plate_model.postprocessor.class_num = 2

    # Det, Rec model enables static shape reasoning
    det_model.preprocessor.static_shape_infer = True
    rec_model.preprocessor.static_shape_infer = True

    det_model.preprocessor.disable_normalize()
    det_model.preprocessor.disable_permute()
    rec_model.preprocessor.disable_normalize()
    rec_model.preprocessor.disable_permute()


def detect_platenumber(module_runner: ModuleRunner, opts: Options, image: Image) -> JSON:

    """
    Performs the plate number detection
    Returns a tuple containing the Json description of what was found 
    """

    global previous_label, prev_avg_char_width, prev_avg_char_height
    global resize_width_factor, resize_height_factor
    
    outputs      = []
    # pillow_image = image
    numpy_image = np.array(image)
    
    numpy_image = cv2.cvtColor(numpy_image, cv2.COLOR_BGR2RGB)
    
    if debug_image:
        filename = "alpr.jpg"
        cv2.imwrite(filename, numpy_image)
        
    inferenceMs: int = 0
    
    start_time = time.perf_counter()
    detect_plate_response = do_detect(module_runner, numpy_image, opts.plate_confidence) # rknn
    inferenceMs += int((time.perf_counter() - start_time) * 1000)
    
    if debug_log:
        with open("log.txt", "a") as text_file:
            text_file.write(f"{detect_plate_response}\n\n")

    if not detect_plate_response["success"]:
        return { "error": detect_plate_response["error"], "inferenceMs": inferenceMs }

    # Note: we will only get plates that have at least opts.plate_confidence 
    # confidence. 
    if not detect_plate_response["predictions"]:
        return { "predictions": [], "inferenceMs": inferenceMs }
    
    # We have a plate (or plates) detected, so let's prep the original image for some work
    # numpy_image = np.array(pillow_image)

    # Remember: numpy is left handed when it comes to indexes
    orig_image_size: Size = Size(width = numpy_image.shape[1], height = numpy_image.shape[0])

    # Correct the colour space
    # numpy_image = cv2.cvtColor(numpy_image, cv2.COLOR_BGR2RGB)
    # numpy_image = cv2.cvtColor(numpy_image, cv2.COLOR_RGB2GRAY)

    # If a plate is found we'll pass this onto OCR
    for plate_detection in detect_plate_response["predictions"]:

        # Pull out just the detected plate.
        # The coordinates... (relative to the original image)
        plate_rect  = Rect(plate_detection["x_min"], plate_detection["y_min"],
                           plate_detection["x_max"], plate_detection["y_max"])
        # The image itself... (Its coordinates are now relative to itself)
        numpy_plate = numpy_image[plate_rect.top:plate_rect.bottom, plate_rect.left:plate_rect.right]

        # Store the size of the scaled plate before we start rotating it
        plate_size = Size(numpy_plate.shape[1], numpy_plate.shape[0])

        # Work out the angle we need to rotate the image to de-skew it (or use the manual override).
        if opts.auto_plate_rotate:
            plate_rotate_deg = tool.compute_skew(numpy_plate)
        else:
            plate_rotate_deg = opts.plate_rotate_deg

        # If we need to rotate, then check to ensure that rotating the image won't chop off
        # important parts of the plate. Note that this will only happen to plates that are at,
        # or very close to, the edge of the image.
        if plate_rotate_deg:
        
            # We start with the assumption that we have a plate that is not displayed level. Once
            # the plate is de-skewed, the bounding box of the rotated plate will be *smaller* than
            # the skewed plate.
            
            # Calculate the corrected (smaller) bounding box if we were to rotate the image
            rotated_size: Size = tool.largest_rotated_rect(plate_size, math.radians(plate_rotate_deg))

            # Calculate the space between the corresponding edges of the (smaller) rotated plate
            # and the original plate.
            # buffer: Size = (plate_size - rotated_size) / 2.0
            buffer: Size = (plate_size - rotated_size).__div__(2.0)

            # 'buffer' represents the width/height of an area along the edges of the original image.
            #
            #         =============================
            #         |       buffer              |   
            #         |  ----------------------   |
            #         |  |                    |   |
            #         |  |    (safe area)     |   |
            #         |  |                    |   |
            #         |  ----------------------   |
            #         |       (unsafe area)       |
            #         =============================
            #
            # If a plate is detected in the original image, and part of that plate extends into 
            # (or over) the buffer area then it means that, on rotation, part of the image could 
            # be cut off. We should avoid that since it could cut off characters.

            # Calculate the region inside the extracted plate that the rotated plate image must fit
            # inside for us to go ahead and do the rotation (left, top, right, bottom)
            safe_rotation_area: Rect = Rect(buffer.width,  buffer.height,
                                            orig_image_size.width  - buffer.width,
                                            orig_image_size.height - buffer.height)

            # If the plate image isn't completely contained within the 'safe' area then do not
            # rotate
            if not safe_rotation_area.contains(plate_rect):
                plate_rotate_deg = 0

        # Rotate if we've determined we have a valid rotation angle to apply
        if plate_rotate_deg:
            numpy_plate = tool.rotate_image(numpy_plate, plate_rotate_deg, rotated_size)

        if opts.OCR_optimization:
            # Based on the previous observation we'll adjust the resize factor so that we get
            # closer and closer to an "optimal" factor that produces images whose characters
            # match our optimum character size. 
            # Assumptions:
            #  1. Most plates we detect will be more or less the same size. Obviously an issue if
            #     you are scanning plates both near and far
            #  2. The aspect ratio of the license plate text (width:height) is around 3:5
            if previous_label and previous_label != no_plate_found: 
                
                if prev_avg_char_width < opts.OCR_optimal_character_width and resize_width_factor < 50:
                    resize_width_factor += 0.02

                if prev_avg_char_width > opts.OCR_optimal_character_width and resize_width_factor > 0.03:
                    resize_width_factor -= 0.02
                    
                if prev_avg_char_height < opts.OCR_optimal_character_height and resize_height_factor < 50:
                    resize_height_factor += 0.02

                if prev_avg_char_height > opts.OCR_optimal_character_height and resize_height_factor > 0.03:
                    resize_height_factor -= 0.02

            numpy_plate = cv2.resize(numpy_plate, None, fx = resize_width_factor, 
                                     fy = resize_height_factor, interpolation = cv2.INTER_CUBIC)
        else:
            if opts.OCR_rescale_factor != 1:
                numpy_plate = tool.resize_image(numpy_plate, opts.OCR_rescale_factor * 100)

        if debug_log:
            with open("log.txt", "a") as text_file:
                text_file.write(f"{resize_height_factor}x{resize_width_factor} - {prev_avg_char_height}x{prev_avg_char_width}\n\n")

        """
        dimensions: Size = Size(numpy_plate.shape[1], numpy_plate.shape[0])
        dimensions.integerize()

        # Exaggerate the width to make line detection more prominent
        dimensions.width *= 1.5
        """
   
        # Pre-processing of the extracted plate to give the OCR a better chance of success
        
        # Run it through a super-resolution module to improve readability (TBD, but could be slow)
        # numpy_plate = super_resolution(numpy_plate)

        # resize image if required
        # if opts.OCR_rescale_factor != 1:
            # numpy_plate = tool.resize_image(numpy_plate, opts.OCR_rescale_factor * 100)

        # perform otsu thresh (best to use binary inverse since opencv contours work better with white text)
        # ret, numpy_plate = cv2.threshold(numpy_plate, 0, 255, cv2.THRESH_OTSU   | cv2.THRESH_BINARY_INV)
        # ret, numpy_plate = cv2.threshold(numpy_plate, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        # numpy_plate = cv2.GaussianBlur(numpy_plate, (5,5), 0)
        # numpy_plate = cv2.medianBlur(numpy_plate, 3)
        # numpy_plate = cv2.bilateralFilter(numpy_plate,9,75,75)
        
        if debug_image:
            filename = "alpr1.jpg"
            cv2.imwrite(filename, numpy_plate)

 
        # Read plate
        (label, confidence, avg_char_width, avg_char_height, plateInferenceMs) = \
                            read_plate_chars_PaddleOCR(module_runner, numpy_plate)
        inferenceMs += plateInferenceMs

        # Read the plate. This may require multiple attempts
        # If we had no success reading the original plate, apply some image enhancement
        # and try again
        """
        if label == no_plate_found:
            # If characters are not found try gamma correction and equalize
            # numpy_plate = cv2.fastNlMeansDenoisingColored(numpy_plate, None, 10, 10, 7, 21)
            numpy_plate = tool.gamma_correction(numpy_plate)
            # numpy_plate = tool.equalize(numpy_plate)
            # numpy_plate = cv2.cvtColor(numpy_plate, cv2.COLOR_BGR2GRAY)
            # cv2.imwrite("alpr-enhanced.jpg", numpy_plate)

            # Read plate, 2nd attempt
            (label, confidence, avg_char_width, avg_char_height, plateInferenceMs) = \
                             await read_plate_chars_PaddleOCR(module_runner, numpy_plate)
            inferenceMs += plateInferenceMs
        """

        if label and confidence:
            # Store to help with adjusting for next detection
            previous_label       = label
            prev_avg_char_width  = avg_char_width
            prev_avg_char_height = avg_char_height

            # return what we found
            detection = {
                "confidence": confidence,
                "label": "Plate: " + label,
                "plate": label,
                "x_min": plate_rect.left,
                "y_min": plate_rect.top,
                "x_max": plate_rect.right,
                "y_max": plate_rect.bottom
            }
            outputs.append(detection)
        else:
            # Next loop around we don't want to needlessly adjust resize factors
            previous_label = no_plate_found

    return { "predictions": outputs, "inferenceMs": inferenceMs }


def do_detect(module_runner, img, score_threshold):# rknn

    start_process_time = time.perf_counter()

    # We have a detector for this model, so let's go ahead and detect
    try:
        # Predicting Image Results
        
        # Resize the image to a maximum size of 640
        resized_image, x_scaling_factor, y_scaling_factor = tool.resize_image_rknn(img, max_size)

        start_inference_time = time.perf_counter()
        result = license_plate_model.predict(resized_image, conf_threshold=score_threshold, nms_iou_threshold=0.45)
        inferenceMs = int((time.perf_counter() - start_inference_time) * 1000)
        
        result = str(result)
        lines = result.strip().split("\n")

        outputs = []

        for line in lines[1:]:
            # Split the line by comma to get a list of values
            values = line.split(",")
            values = [x.strip(' ') for x in values]

            box = values[0], values[1], values[2], values[3]
            
            box = tool.convert_bounding_boxes(box, x_scaling_factor, y_scaling_factor)
            
            # Convert the values to appropriate data types
            xmin        = int(float(box[0]))
            ymin        = int(float(box[1]))
            xmax        = int(float(box[2]))
            ymax        = int(float(box[3]))
            score       = float(values[4])
            label       = str(values[5])

            detection = {
                "confidence": score,
                "label": label,
                "x_min": xmin,
                "y_min": ymin,
                "x_max": xmax,
                "y_max": ymax,
            }

            outputs.append(detection)

        if len(outputs) > 3:
            message = 'Found ' + (', '.join(det["label"] for det in outputs[0:3])) + "..."
        elif len(outputs) > 0:
            message = 'Found ' + (', '.join(det["label"] for det in outputs))
        else:
            message = "No objects found"

        return {
            "success"     : True,
            "count"       : len(outputs),
            "predictions" : outputs,
            "message"     : message,
            "processMs"   : int((time.perf_counter() - start_process_time) * 1000),
            "inferenceMs" : inferenceMs
        }
    
    except UnidentifiedImageError as img_ex:
        module_runner.report_error(img_ex, __file__, "The image provided was of an unknown type")
        return { "success": False, "error": "invalid image file"}
    
    except Exception as ex:
        module_runner.report_error(ex, __file__)
        return { "success": False, "error": "Error occurred on the server" }


def read_plate_chars_PaddleOCR(module_runner: ModuleRunner, img) -> Tuple[str, float, int, int, float]:
    
    """
    This uses PaddleOCR for reading the plates. Note that the image being passed
    in should be a tightly cropped licence plate, so we're looking for the largest
    text box and will assume that's the plate number.
    Returns (plate label, confidence, avg char height (px), avg char width (px), inference time (ms))
    """
    
    # Create PP-OCR and connect 3 models in series, where cls_model is optional, if not required, it can be set to None
    ppocr_v3 = fd.vision.ocr.PPOCRv3(det_model=det_model, cls_model=None, rec_model=rec_model)

    # The batch size of Rec model must be set to 1 to enable static shape reasoning
    ppocr_v3.rec_batch_size = 1

    inferenceTimeMs: int = 0
        
    resized_image = tool.plate_resize_image(img, 480)
    
    if debug_image:
        filename = "alpr2.jpg"
        cv2.imwrite(filename, resized_image)        
            
    start_time = time.perf_counter()
    ocr_response = ppocr_v3.predict(resized_image)
    inferenceTimeMs = int((time.perf_counter() - start_time) * 1000)
    
    if debug_log:
        with open("log.txt", "a") as text_file:
            text_file.write(str(ocr_response) + "\n")
        
    ocr_response = str(ocr_response)
    
    lines = ocr_response.split('\n')  # Split the string into individual lines
    
    modified_lines = [line for line in lines if "0.000000" not in line]
        
    # Find lines that contain all three substrings and do not contain "0.000000" and keep them
    # modified_lines = [line for line in lines if all(substring in line for substring in ['det boxes:', 'rec text:', 'rec score:']) and '0.000000' not in line]

    # Reconstruct the modified string
    ocr_response = '\n'.join(modified_lines)
    
    if debug_log:
        with open("log.txt", "a") as text_file:
            text_file.write(str(ocr_response) + "\n")

    
    ocr_response = ocr_response.replace("'", "")  # Remove single quotes
    ocr_response = ocr_response.replace('"', '')  # Remove double quotes
    ocr_response = ocr_response.replace("det boxes: ", "[")
    ocr_response = ocr_response.replace("rec text: ", ", ('")
    ocr_response = ocr_response.replace(" rec score:", "', ")
    ocr_response = ocr_response.replace(" \n", ")], ")
    ocr_response = ocr_response.strip("\n")
    ocr_response = ocr_response.rstrip(", ")
    ocr_response = "[" + ocr_response + "]"
    
    if debug_log:
        with open("log.txt", "a") as text_file:
            text_file.write(str(ocr_response) + "\n")
    
    ocr_response = ast.literal_eval(ocr_response)
    
    # Note that ocr_response[0][0][0][0] could be a float with value 0 ('false'), or in some
    # other universe maybe it's a string. To be really careful we would have a test like
    # if hasattr(ocr_response[0][0][0][0], '__len__') and (not isinstance(ocr_response[0][0][0][0], str))
    if not ocr_response or not ocr_response[0] or not ocr_response[0][0] or not ocr_response[0][0][0]:
        return no_plate_found, 0, 0, 0, inferenceTimeMs

    # Seems that different versions of paddle return different structures, OR
    # paddle returns different structures depending on its mood. We're expecting
    # ocr_response = array of single set of detections
    #  -> detections = array of detection
    #    -> detection = array of bounding box, classification
    #       -> bounding box = array of [x,y], classification = array of label, confidence.
    # so ocr_response[0][0][0][0][0] = first 'x' of the bounding boxes, which is a float
    # However, the first "array of single set of detections" isn't always there, so first
    # check to see if ocr_response[0][0][0][0] is a float

    # detections = ocr_response if isinstance(ocr_response[0][0][0][0], float) else ocr_response[0]

    # Find the biggest textbox and assume that's the plate number
    plate_label, plate_confidence, avg_char_width, avg_char_height = tool.merge_text_detections(ocr_response)

    if not plate_label:
        return no_plate_found, 0, 0, 0, inferenceTimeMs

    return plate_label, plate_confidence, avg_char_height, avg_char_width, inferenceTimeMs
