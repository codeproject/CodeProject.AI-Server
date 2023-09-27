import os
from module_options import ModuleOptions

class Settings:
    def __init__(self, RESOLUTION, STD_MODEL_NAME):
        self.RESOLUTION     = RESOLUTION
        self.STD_MODEL_NAME = STD_MODEL_NAME


class Options:

    def __init__(self):

        # -------------------------------------------------------------------------
        # Setup constants

        # Models at https://github.com/MikeLud/CodeProject.AI-Custom-IPcam-Models/tree/main/RKNN_Models/yolov5
        self.MODEL_SETTINGS = {
            # Large:    yolov5-large 80 objects,    COCO 640x640x3    RKNN-2
            "large":    Settings(640, 'yolov5-large'),
            # Medium:   yolov5-medium  80 objects,  COCO 640x640x3    RKNN-2
            "medium":   Settings(640, 'yolov5-medium'),
            # Small:    yolov5-small 80 objects,    COCO 640x640x3    RKNN-2
            "small":    Settings(640, 'yolov5-small'),
            # Tiny:     yolov5-tiny 80 objects,     COCO 640x640x3    RKNN-2
            "tiny":     Settings(640, 'yolov5-tiny')
        }

        self.NUM_THREADS    = 1
        self.MIN_CONFIDENCE = 0.30
        
        # -------------------------------------------------------------------------
        # Setup values

        self._show_env_variables = True

        self.module_path        = ModuleOptions.module_path
        self.models_dir         = os.path.normpath(ModuleOptions.getEnvVariable("MODELS_DIR", f"{self.module_path}/assets"))
        self.model_size         = ModuleOptions.getEnvVariable("MODEL_SIZE", "Small").lower()   # tiny, small, medium, large
        self.custom_models_dir  = os.path.normpath(ModuleOptions.getEnvVariable("CUSTOM_MODELS_DIR", f"{self.module_path}/custom-models"))

        self.num_threads        = int(ModuleOptions.getEnvVariable("NUM_THREADS",      self.NUM_THREADS))
        self.min_confidence     = float(ModuleOptions.getEnvVariable("MIN_CONFIDENCE", self.MIN_CONFIDENCE))

        self.sleep_time         = 0.01

        if self.model_size not in [ "tiny", "small", "medium", "large" ]:
            self.model_size = "small"

        # Get settings
        settings                = self.MODEL_SETTINGS[self.model_size]
        self.resolution         = settings.RESOLUTION
        self.std_model_name     = settings.STD_MODEL_NAME

        # -------------------------------------------------------------------------
        # dump the important variables

        if self._show_env_variables:
            print(f"MODULE_PATH:        {self.module_path}")
            print(f"MODELS_DIR:         {self.models_dir}")
            print(f"custom_models_dir:  {self.custom_models_dir}")
            print(f"MODEL_SIZE:         {self.model_size}")
            print(f"STD_MODEL_NAME:     {self.std_model_name}")
