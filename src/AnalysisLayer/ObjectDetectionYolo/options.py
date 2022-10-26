import os
import sys
import torch
import cpuinfo

class Settings:

    def __init__(self, RESOLUTION, STD_MODEL_NAME):
        self.RESOLUTION     = RESOLUTION
        self.STD_MODEL_NAME = STD_MODEL_NAME


class Options:

    def get_env_variable(varName: str, default: str):
        value = os.getenv(varName, "")
        if not value and default != "": # Allow a None value to be a default
            value = default
            print(f"{varName} not found. Setting to default {default}")

        return value

    # -------------------------------------------------------------------------
    # Setup constants

    # see https://github.com/ultralytics/yolov5 for resolution data
    MODEL_SETTINGS = {
        "tiny":   Settings(STD_MODEL_NAME = "yolov5n", RESOLUTION = 256), # 640
        "small":  Settings(STD_MODEL_NAME = "yolov5s", RESOLUTION = 256),
        "medium": Settings(STD_MODEL_NAME = "yolov5m", RESOLUTION = 416),
        "large":  Settings(STD_MODEL_NAME = "yolov5l", RESOLUTION = 640),
        "huge":   Settings(STD_MODEL_NAME = "yolov5x", RESOLUTION = 640)  # Not yet included
    }

    # -------------------------------------------------------------------------
    # Setup values

    _show_env_variables = True

    app_dir           = os.path.normpath(get_env_variable("APPDIR", os.getcwd()))
    models_dir        = os.path.normpath(get_env_variable("MODELS_DIR", f"{app_dir}/assets"))
    custom_models_dir = os.path.normpath(get_env_variable("CUSTOM_MODELS_DIR", f"{app_dir}/custom-models"))

    sleep_time        = 0.01

    port              = get_env_variable("CPAI_PORT",  "32168")
    support_GPU       = get_env_variable("CPAI_MODULE_SUPPORT_GPU", "True")
    model_size        = get_env_variable("MODEL_SIZE", "Medium")   # small, medium, large //, nano, x-large
    use_CUDA          = get_env_variable("USE_CUDA",   "True")     # True / False
    cuda_device_num   = get_env_variable("CPAI_CUDA_DEVICE_NUM", "0")
    half_precision    = get_env_variable("CPAI_HALF_PRECISION", "Enable")

    # Normalise input
    model_size        = model_size.lower()
    support_GPU       = support_GPU.lower() == "true"
    use_CUDA          = use_CUDA.lower() == "true"
    use_CUDA          = support_GPU and use_CUDA and torch.cuda.is_available()
    cuda_device_num   = int(cuda_device_num)
    half_precision    = half_precision.lower()
    
    if half_precision not in [ "force", "enable", "disable" ]:
        half_precision = "enable"

    use_MPS           = False
    manufacturer      = cpuinfo.get_cpu_info().get('brand_raw')
    if manufacturer and manufacturer.startswith("Apple M"):
        use_MPS = hasattr(torch.backends, "mps") and torch.backends.mps.is_available()

    if model_size not in [ "tiny", "small", "medium", "large" ]:
        model_size = "medium"

    # Get settings
    settings = MODEL_SETTINGS[model_size]   
    resolution_pixels = settings.RESOLUTION
    std_model_name    = settings.STD_MODEL_NAME

    # -------------------------------------------------------------------------
    # dump the important variables

    if _show_env_variables:
        print(f"APPDIR:      {app_dir}")
        print(f"CPAI_PORT:   {port}")
        print(f"MODEL_SIZE:  {model_size}")
        print(f"MODELS_DIR:  {models_dir}")
        print(f"support_GPU: {support_GPU}")
        print(f"use_CUDA:    {use_CUDA}")
