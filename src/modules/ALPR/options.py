import os
from module_options import ModuleOptions

class Options:

    def __init__(self):

        self.log_verbosity         = ModuleOptions.log_verbosity
        
        # confidence threshold for plate detections       
        self.plate_confidence      = float(ModuleOptions.getEnvVariable("PLATE_CONFIDENCE", 0.7))

        # positive integer for counterclockwise rotation and negative integer for clockwise rotation
        self.plate_rotate_deg      = int(ModuleOptions.getEnvVariable("PLATE_ROTATE_DEG", 0))

        # positive integer for counterclockwise rotation and negative integer for clockwise rotation
        self.auto_plate_rotate     = str(ModuleOptions.getEnvVariable("AUTO_PLATE_ROTATE", "True")).lower() == "true"

        # increase size of plate 2X before attempting OCR
        self.OCR_rescale_factor    = float(ModuleOptions.getEnvVariable("PLATE_RESCALE_FACTOR", 2.0))

        # For checking if this system can support Paddle GPU
        self.min_compute_capability = float(ModuleOptions.getEnvVariable("MIN_COMPUTE_CAPABILITY", 6))
        self.min_cuDNN_version      = float(ModuleOptions.getEnvVariable("MIN_CUDNN_VERSION",      6))

        # OCR optimization
        self.OCR_optimization             = str(ModuleOptions.getEnvVariable("OCR_OPTIMIZATION", "True")).lower() == "true"
        self.OCR_optimal_character_height = int(ModuleOptions.getEnvVariable("OCR_OPTIMAL_CHARACTER_HEIGHT", 60))
        self.OCR_optimal_character_width  = int(ModuleOptions.getEnvVariable("OCR_OPTIMAL_CHARACTER_WIDTH", 36))
        self.remove_spaces                = str(ModuleOptions.getEnvVariable("REMOVE_SPACES", "False")).lower() == "true"
        self.save_cropped_plate           = str(ModuleOptions.getEnvVariable("SAVE_CROPPED_PLATE", "False")).lower() == "true"
        self.root_path                    = os.path.normpath(ModuleOptions.getEnvVariable("ROOT_PATH"))
        self.cropped_plate_dir            = os.path.normpath(ModuleOptions.getEnvVariable("CROPPED_PLATE_DIR", f"{self.root_path}/custom-models"))
        
        # PaddleOCR settings
        self.use_gpu               = ModuleOptions.enable_GPU  # We'll disable this if we can't find GPU libraries
        self.box_detect_threshold  = 0.40  # confidence threshold for text box detection
        self.char_detect_threshold = 0.40  # confidence threshold for character detection
        self.det_db_unclip_ratio   = 2.0   # Differentiable Binarization expand ratio for output box
        self.language              = 'en'
        self.algorithm             = 'CRNN'
        self.cls_model_dir         = 'paddleocr/ch_ppocr_mobile_v2.0_cls_infer'
        self.det_model_dir         = 'paddleocr/en_PP-OCRv3_det_infer'
        self.rec_model_dir         = 'paddleocr/en_PP-OCRv4_rec_infer'


