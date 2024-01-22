from module_options import ModuleOptions

class Options:

    def __init__(self):

        self.log_verbosity         = ModuleOptions.log_verbosity
        
        # confidence threshold for plate detections       
        self.plate_confidence      = float(ModuleOptions.getEnvVariable("PLATE_CONFIDENCE", 0.5))

        # positive integer for counterclockwise rotation and negative integer for clockwise rotation
        self.plate_rotate_deg      = int(ModuleOptions.getEnvVariable("PLATE_ROTATE_DEG", 0))

        # positive integer for counterclockwise rotation and negative interger for clockwise rotation
        self.auto_plate_rotate     = str(ModuleOptions.getEnvVariable("AUTO_PLATE_ROTATE", "True")).lower() == "true"

        # increase size of plate 2X before attempting OCR
        self.OCR_rescale_factor    = float(ModuleOptions.getEnvVariable("PLATE_RESCALE_FACTOR", 2.0))

        # OCR optimization
        self.OCR_optimization             = str(ModuleOptions.getEnvVariable("OCR_OPTIMIZATION", "True")).lower() == "true"
        self.OCR_optimal_character_height = int(ModuleOptions.getEnvVariable("OCR_OPTIMAL_CHARACTER_HEIGHT", 60))
        self.OCR_optimal_character_width  = int(ModuleOptions.getEnvVariable("OCR_OPTIMAL_CHARACTER_WIDTH", 36))

        # PaddleOCR settings
        self.det_model_path             = 'paddleocr/en_PP-OCRv3_det_infer/en_PP-OCRv3_det_infer_rk3588_unquantized.rknn'
        self.rec_model_path             = 'paddleocr/en_PP-OCRv3_rec_infer/en_PP-OCRv3_rec_infer_rk3588_unquantized.rknn'
        self.rec_label_file             = 'paddleocr/en_PP-OCRv3_rec_infer/en_dict.txt'
        self.license_plate_model_path   = 'paddleocr/license-plate-nano.rknn'