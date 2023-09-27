import io
import sys
import time
import traceback
from PIL import Image

from common import JSON
from module_runner import ModuleRunner
from module_logging import LogMethod, LogVerbosity

from options import Options
from paddleocr import PaddleOCR

ocr = None
no_text_found = 'Text Not Found'

def init_detect_ocr(opts: Options) -> None:

    global ocr
  
    # See notes at the end of this file for options.
    ocr = PaddleOCR(lang                = opts.language,
                    use_gpu             = opts.use_gpu,
                    show_log            = opts.log_verbosity == LogVerbosity.Loud,
                    det_db_unclip_ratio = opts.det_db_unclip_ratio,
                    det_db_box_thresh   = opts.box_detect_threshold,
                    drop_score          = opts.char_detect_threshold,
                    rec_algorithm       = opts.algorithm,
                    cls_model_dir       = opts.cls_model_dir,
                    det_model_dir       = opts.det_model_dir,
                    rec_model_dir       = opts.rec_model_dir)

def read_text(module_runner: ModuleRunner, image: Image, rotate_deg: int = 0) -> JSON:

    outputs = []

    # rotate image if needed
    working_image = image
    if rotate_deg != 0:
        working_image = image.rotate(rotate_deg, expand=True, resample=Image.BICUBIC)
       
    # Possibly run it through a super-resolution module to improve readability
    # working_image = enhance_image(working_image)

    # Read text
    inferenceTimeMs = 0
    try:
        # Convert the image to a bytes array
        with io.BytesIO() as image_buffer:
            working_image.save(image_buffer, format='JPEG')
            img_byte_arr = image_buffer.getvalue()

        start_time = time.perf_counter()
        ocr_response = ocr.ocr(img_byte_arr, cls=True)
        inferenceTimeMs = int((time.perf_counter() - start_time) * 1000)

        # Note that ocr_response[0][0][0][0] could be a float with value 0 ('false'), or in some
        # other universe maybe it's a string. To be really careful we would have a test like
        # if hasattr(ocr_response[0][0][0][0], '__len__') and (not isinstance(ocr_response[0][0][0][0], str))
        if not ocr_response or not ocr_response[0] or not ocr_response[0][0] or not ocr_response[0][0][0]:
            return { "success": False, "error": "No OCR response received", "inferenceMs" : inferenceTimeMs }

        # Seems that different versions of paddle return different structures, OR
        # paddle returns different structures depending on its mood. We're expecting
        # ocr_response = array of single set of detections
        #  -> detections = array of detection
        #    -> detection = array of bounding box, classification
        #       -> bounding box = array of [x,y], classification = array of label, confidence.
        # so ocr_response[0][0][0][0][0] = first 'x' of the bounding boxes, which is a float
        # However, the first "array of single set of detections" isn't always there, so first
        # check to see if ocr_response[0][0][0][0] is a float

        detections = ocr_response if isinstance(ocr_response[0][0][0][0], float) else ocr_response[0]

        for detection in detections:
            bounding_box   = detection[0] # [ topleft, topright, bottom right, bottom left ], each is [x,y]
            classification = detection[1]

            label      = classification[0]
            confidence = classification[1]

            if label and confidence:

                # Obviously some optimisation can be done here, but is it worth 
                # it? The trivial optimisation is to assume the order of the 
                # points, but that's dangerous.

                detection = {
                    "confidence": confidence,
                    "label": label,
                    "x_min": int(min(point[0] for point in bounding_box)),   # int(bounding_box[0][0]),
                    "y_min": int(min(point[1] for point in bounding_box)),   # int(bounding_box[0][1]),
                    "x_max": int(max(point[0] for point in bounding_box)),   # int(bounding_box[3][0]),
                    "y_max": int(max(point[1] for point in bounding_box)),   # int(bounding_box[3][1]),
                }
                outputs.append(detection)

        # The operation  was successfully completed. There just wasn't any text
        # if not outputs:
        #    return { "success": False, "predictions" : None, "inferenceMs" : inferenceTimeMs }

        return { "success": True, "predictions" : outputs, "inferenceMs" : inferenceTimeMs }

    except Exception as ex:
        module_runner.report_error(ex, __file__)

        message = "".join(traceback.TracebackException.from_exception(ex).format())
        return { "success": False, "error": message, "inferenceMs" : inferenceTimeMs }


"""
Options for the PaddleOCR object:

Parameter                  Default    Description
-------------------------------------------------------------------------------
use_gpu                     TRUE      use GPU or not
gpu_mem                     8000M     GPU memory size used for initialization    
image_dir                             The images path or folder path for predicting 
                                      when used by the command line    
det_algorithm               DB        Type of detection algorithm selected (DB = Differentiable Binarization)
det_model_dir               None      the text detection inference model folder. There
                                      are two ways to transfer parameters, 1. None: Automatically download
                                      the built-in model to ~/.paddleocr/det; 
                                      2. The path of the inference model converted by yourself, the model
                                      and params files must be included in the model path
det_max_side_len            960       The maximum size of the long side of the image. When the long side 
                                      exceeds this value, the long side will be resized to this size, and
                                      the short side will be scaled proportionally    
det_db_thresh               0.3       Binarization threshold value of DB output map
det_db_box_thresh           0.5       The threshold value of the DB output box. Boxes score lower than 
                                      this value will be discarded
det_db_unclip_ratio         2         The expanded ratio of DB output box
det_east_score_thresh       0.8       Binarization threshold value of EAST output map
det_east_cover_thresh       0.1       The threshold value of the EAST output box. Boxes score lower than 
                                      this value will be discarded
det_east_nms_thresh         0.2       The NMS threshold value of EAST model output box
rec_algorithm               CRNN      Type of recognition algorithm selected
rec_model_dir               None      the text recognition inference model folder. There are two ways to 
                                      transfer parameters, 1. None: Automatically download the built-in 
                                      model to ~/.paddleocr/rec; 2. The path of the inference model 
                                      converted by yourself, the model and params files must be included 
                                      in the model path
rec_image_shape             "3,32,320" image shape of recognition algorithm
rec_char_type               ch        Character type of recognition algorithm, Chinese (ch) or English (en)
rec_batch_num               30        When performing recognition, the batchsize of forward images
max_text_length             25        The maximum text length that the recognition algorithm can recognize
rec_char_dict_path          ./ppocr/utils/ppocr_keys_v1.txt the alphabet path which needs to be modified to 
                                      your own path when rec_model_Name use mode 2
use_space_char              TRUE      Whether to recognize spaces
use_angle_cls               FALSE     Whether to load classification model
cls_model_dir               None      the classification inference model folder. There are two ways to 
                                      transfer parameters, 1. None: Automatically download the built-in model 
                                      to ~/.paddleocr/cls; 2. The path of the inference model converted 
                                      by yourself, the model and params files must be included in the 
                                      model path
cls_image_shape              "3,48,192"    image shape of classification algorithm
label_list                  ['0','180']    label list of classification algorithm    
cls_batch_num               30        When performing classification, the batchsize of forward images
enable_mkldnn               FALSE     Whether to enable mkldnn
use_zero_copy_run           FALSE     Whether to forward by zero_copy_run
lang                        ch        The support language, Only Chinese (ch), English (en), French (french), 
                                      German (german), Korean (korean), Japanese (japan) are supported
det                         TRUE      Enable detection when ppocr.ocr func exec
rec                         TRUE      Enable recognition when ppocr.ocr func exec
cls                         FALSE     Enable classification when ppocr.ocr func exec. This parameter only 
                                      exists in code usage mode    
"""