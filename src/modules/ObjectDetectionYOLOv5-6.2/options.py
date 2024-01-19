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

        # see https://github.com/ultralytics/yolov5 for resolution data
        self.MODEL_SETTINGS = {
            "tiny":   Settings(STD_MODEL_NAME = "yolov5n", RESOLUTION = 256), # 640
            "small":  Settings(STD_MODEL_NAME = "yolov5s", RESOLUTION = 256),
            "medium": Settings(STD_MODEL_NAME = "yolov5m", RESOLUTION = 416),
            "large":  Settings(STD_MODEL_NAME = "yolov5l", RESOLUTION = 640),
            "huge":   Settings(STD_MODEL_NAME = "yolov5x", RESOLUTION = 640)  # Not yet included
        }

        # -------------------------------------------------------------------------
        # Setup values

        self._show_env_variables = True

        self.app_dir            = os.path.normpath(ModuleOptions.getEnvVariable("APPDIR", os.getcwd()))
        self.models_dir         = os.path.normpath(ModuleOptions.getEnvVariable("MODELS_DIR", f"{self.app_dir}/assets"))
        self.custom_models_dir  = os.path.normpath(ModuleOptions.getEnvVariable("CUSTOM_MODELS_DIR", f"{self.app_dir}/custom-models"))

        self.sleep_time         = 0.01

        self.model_size         = ModuleOptions.getEnvVariable("MODEL_SIZE", "Medium")   # tiny, small, medium, large //, x-large
        self.use_CUDA           = ModuleOptions.getEnvVariable("USE_CUDA",   "True")     # True / False
        self.use_MPS            = True          # only if available...
        self.use_DirectML       = True          # only if available...

        # Normalise input
        self.model_size         = self.model_size.lower()
        self.use_CUDA           = ModuleOptions.enable_GPU and self.use_CUDA.lower() == "true"

        if self.model_size not in [ "tiny", "small", "medium", "large" ]:
            self.model_size = "medium"

        # Get settings
        settings = self.MODEL_SETTINGS[self.model_size]   
        self.resolution_pixels = settings.RESOLUTION
        self.std_model_name    = settings.STD_MODEL_NAME

        # -------------------------------------------------------------------------
        # dump the important variables

        if self._show_env_variables:
            print(f"Debug: APPDIR:      {self.app_dir}")
            print(f"Debug: MODEL_SIZE:  {self.model_size}")
            print(f"Debug: MODELS_DIR:  {self.models_dir}")
