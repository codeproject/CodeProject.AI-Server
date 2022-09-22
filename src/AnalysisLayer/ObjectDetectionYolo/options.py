import os
import torch
import cpuinfo

class Settings:

    def __init__(self, RESOLUTION_HIGH, RESOLUTION_MEDIUM, RESOLUTION_LOW, STD_MODEL_NAME):
        self.RESOLUTION_HIGH   = RESOLUTION_HIGH
        self.RESOLUTION_MEDIUM = RESOLUTION_MEDIUM
        self.RESOLUTION_LOW    = RESOLUTION_LOW
        self.STD_MODEL_NAME    = STD_MODEL_NAME


class Options:

    def get_env_variable(varName: str, default: str):
        value = os.getenv(varName, "")
        if not value and default != "": # Allow a None value to be a default
            value = default
            print(f"{varName} not found. Setting to default {default}")

        return value

    # -------------------------------------------------------------------------
    # Setup constants

    MODEL_SETTINGS = {

        "small": Settings(
            RESOLUTION_HIGH   = 416,
            RESOLUTION_MEDIUM = 320,
            RESOLUTION_LOW    = 256,
            STD_MODEL_NAME    = "yolov5s",
        ),

        "medium": Settings(
            RESOLUTION_HIGH   = 640,
            RESOLUTION_MEDIUM = 416,
            RESOLUTION_LOW    = 256,
            STD_MODEL_NAME    = "yolov5m",
        ),

        "large": Settings(
            RESOLUTION_HIGH   = 640,
            RESOLUTION_MEDIUM = 416,
            RESOLUTION_LOW    = 256,
            STD_MODEL_NAME    = "yolov5l",
        ),

        # Maybe soon. This is a big model
        #"x-large": Settings(
        #    RESOLUTION_HIGH   = 640,
        #    RESOLUTION_MEDIUM = 416,
        #    RESOLUTION_LOW    = 256,
        #    STD_MODEL_NAME    = "yolov5x",
        #)
    }

    # -------------------------------------------------------------------------
    # Setup values

    _show_env_variables = True

    # print(f"Object detection (YOLO) services setup: Retrieving environment variables...")

    app_dir           = os.path.normpath(get_env_variable("APPDIR", os.getcwd()))
    models_dir        = os.path.normpath(get_env_variable("MODELS_DIR", f"{app_dir}/assets"))
    custom_models_dir = os.path.normpath(get_env_variable("CUSTOM_MODELS_DIR", f"{app_dir}/custom-models"))

    sleep_time        = 0.01

    port              = get_env_variable("CPAI_PORT",  "32168")
    support_GPU       = get_env_variable("CPAI_MODULE_SUPPORT_GPU", "True")
    model_size        = get_env_variable("MODEL_SIZE", "Medium")   # small, medium, large //, x-large
    use_CUDA          = get_env_variable("USE_CUDA",   "True")     # True / False
    resolution        = get_env_variable("RESOLUTION", "Medium")   # low, medium, high

    # Normalise input
    model_size  = model_size.lower()
    support_GPU = support_GPU.lower() == "true"
    use_CUDA    = use_CUDA.lower() == "true"
    use_CUDA    = support_GPU and use_CUDA and torch.cuda.is_available()

    manufacturer = cpuinfo.get_cpu_info().get('brand_raw')
    if manufacturer.startswith("Apple M"):
        use_MPS = hasattr(torch.backends, "mps") and torch.backends.mps.is_available()
    else:
        use_MPS = False

    # Get settings
    settings    = MODEL_SETTINGS[model_size]
    
    resolution_pixels = settings.RESOLUTION_MEDIUM
    if resolution.lower() == 'low':
        resolution_pixels = settings.RESOLUTION_LOW
    elif resolution.lower() == 'high':
        resolution_pixels = settings.RESOLUTION_HIGH

    std_model_name = settings.STD_MODEL_NAME

    # -------------------------------------------------------------------------
    # dump the important variables

    if _show_env_variables:
        print(f"APPDIR:      {app_dir}")
        print(f"CPAI_PORT:   {port}")
        print(f"MODEL_SIZE:  {model_size}")
        print(f"MODELS_DIR:  {models_dir}")
        print(f"support_GPU: {support_GPU}")
        print(f"use_CUDA:    {use_CUDA}")

    if use_CUDA:
        try:
            print(f"GPU in use: {torch.cuda.get_device_name(0)}")
        except:
            print(f"GPU not actually in use: torch wasn't compiled for CUDA")
