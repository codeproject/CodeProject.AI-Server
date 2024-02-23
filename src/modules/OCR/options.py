from module_options import ModuleOptions

class Options:

    def __init__(self):
        
        self.log_verbosity          = ModuleOptions.log_verbosity
        
        # PaddleOCR settings
        self.use_gpu                = ModuleOptions.enable_GPU  # We'll disable this if we can't find GPU libraries

        # For checking if this system can support Paddle GPU
        self.min_compute_capability = float(ModuleOptions.getEnvVariable("MIN_COMPUTE_CAPABILITY", 6))
        self.min_cuDNN_version      = float(ModuleOptions.getEnvVariable("MIN_CUDNN_VERSION",      6))

        self.box_detect_threshold   = 0.40  # confidence threshold for text box detection
        self.char_detect_threshold  = 0.40  # confidence threshold for character detection
        self.det_db_unclip_ratio    = 2.0   # Differentiable Binarization expand ratio for output box

        self.language               = 'en'
        self.algorithm              = 'CRNN'
        self.cls_model_dir          = 'paddleocr/ch_ppocr_mobile_v2.0_cls_infer'
        self.det_model_dir          = 'paddleocr/en_PP-OCRv3_det_infer'
        self.rec_model_dir          = 'paddleocr/en_PP-OCRv4_rec_infer'