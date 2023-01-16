import os

class Options:

    def __init__(self):
        self.use_OpenCV            = True

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

