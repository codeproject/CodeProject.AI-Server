import os

class Options:

    def __init__(self):
        self.use_OpenCV            = True

        self.server_host           = "localhost"
        self.server_port           = int(self.get_env_variable("CPAI_PORT",  "32168"))
        self.server_url            = f"http://{self.server_host}:{self.server_port}/v1/"

        # confidence threshold for plate detections
        self.plate_confidence      = float(self.get_env_variable("PLATE_CONFIDENCE", 0.7))

        self.auto_deskew           = True

        # PaddleOCR settings
        self.box_detect_threshold  = 0.40  # confidence threshold for text box detection
        self.char_detect_threshold = 0.40  # confidence threshold for character detection
        self.det_db_unclip_ratio   = 2.0   # Differentiable Binarization expand ratio for output box
        self.language              = 'en'
        self.algorithm             = 'CRNN'
        self.cls_model_dir         = 'paddleocr/ch_ppocr_mobile_v2.0_cls_infer'
        self.det_model_dir         = 'paddleocr/en_PP-OCRv3_det_infer'
        self.rec_model_dir         = 'paddleocr/en_PP-OCRv3_rec_infer'
        self.use_GPU               = self.get_env_variable("CPAI_MODULE_SUPPORT_GPU", "True") == "True"
        self.showLog               = True

        """
        This was for YOLO character detection. No longer used

        # confidence threshold for character detections
        self.ocr_confidence        = float(get_env_variable("OCR_CONFIDENCE", 0.2))

        # How close (in px) the chars can be before the inferior one is ignored
        self.overlap_threshold_px  = int(get_env_variable("OVERLAP_THRESHOLD_PX", 4))

        # Min number of characters that must for found for a valid result
        self.min_plate_characters  = min(int(get_env_variable("MIN_PLATE_CHARACTERS", 1)), 1)

        # Max number of characters to return for a plate. If more found, least
        # confident results will be discarded
        self.max_plate_characters  = int(get_env_variable("MAX_PLATE_CHARACTERS", 8)) 
        """
        
        if self.use_GPU:
            try:
                import paddle
                self.use_GPU = self.use_GPU and paddle.device.get_device().startswith("gpu")
            except:
                self.use_GPU = False

    def get_env_variable(self, varName: str, default: str):
        value = os.getenv(varName, "")
        if value == "" and default != "":
            value = default
            print(f"{varName} not found. Setting to default {default}")

        return value

    def endpoint(self, route) -> str:
        return self.server_url + route

    def cleanDetectedDir(self) -> None:
        # make sure the detected directory exists
        if not os.path.exists(self.detectedDir):
            os.mkdir(self.detectedDir)

        # delete all the files in the output directory
        filelist = os.listdir(self.detectedDir)
        for filename in filelist:
            try:
                filepath = os.path.join(self.detectedDir, filename)
                os.remove(filepath)
            except:
                pass


