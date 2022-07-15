import os
import torch

class Settings:

    def __init__(self, RESOLUTION_HIGH, RESOLUTION_MEDIUM, RESOLUTION_LOW, MODEL_NAME):
        self.RESOLUTION_HIGH   = RESOLUTION_HIGH
        self.RESOLUTION_MEDIUM = RESOLUTION_MEDIUM
        self.RESOLUTION_LOW    = RESOLUTION_LOW
        self.MODEL_NAME        = MODEL_NAME


class Options:

    MODEL_SETTINGS = {

        "small": Settings(
            RESOLUTION_HIGH   = 416,
            RESOLUTION_MEDIUM = 320,
            RESOLUTION_LOW    = 256,
            MODEL_NAME        = "ipcam-general.pt" # "yolov5s.pt",
        ),

        "medium": Settings(
            RESOLUTION_HIGH   = 640,
            RESOLUTION_MEDIUM = 416,
            RESOLUTION_LOW    = 256,
            MODEL_NAME        = "ipcam-general.pt" # "yolov5m.pt",
        ),

        "large": Settings(
            RESOLUTION_HIGH   = 640,
            RESOLUTION_MEDIUM = 416,
            RESOLUTION_LOW    = 256,
            MODEL_NAME        = "ipcam-general.pt" # "yolov5l.pt",
        ),

        "x-large": Settings(
            RESOLUTION_HIGH   = 640,
            RESOLUTION_MEDIUM = 416,
            RESOLUTION_LOW    = 256,
            MODEL_NAME        = "ipcam-general.pt" # "yolov5x.pt",
        ),
    }

    def get_env_variable(varName: str, default: str):
        value = os.getenv(varName, "")
        if value == "" and default != "":
            value = default
            print(f"{varName} not found. Setting to default {default}")

        return value

    _show_env_variables = False

    print(f"Custom Object detection services setup: Retrieving environment variables...")

    app_dir          = os.path.normpath(get_env_variable("APPDIR", os.getcwd()))
    models_dir      = os.path.normpath(get_env_variable("MODELS_DIR", f"{app_dir}/assets"))
    port            = get_env_variable("PORT",       "5000")

    sleep_time      = 0.01

    model_size      = get_env_variable("MODEL_SIZE", "Medium")   # small, medium, large, x-large
    use_CUDA        = get_env_variable("USE_CUDA", "False")      # True / False
    resolution      = get_env_variable("RESOLUTION", "Medium")   # low, medium, high

    # Normalise input
    use_CUDA   = use_CUDA.lower() == "true" and torch.cuda.is_available()
    model_size = model_size.lower()

    # Get settings
    settings   = MODEL_SETTINGS[model_size]
    
    resolution_pizels = settings.RESOLUTION_MEDIUM
    if resolution.lower() == 'low':
        resolution_pizels = settings.RESOLUTION_LOW
    elif resolution.lower() == 'high':
        resolution_pizels = settings.RESOLUTION_HIGH

    model_name = settings.MODEL_NAME

    # dump the important variables
    if _show_env_variables:
        print(f"USE_CUDA:     {use_CUDA}")
        print(f"MODEL_SIZE:   {model_size}")
        print(f"MODELS_DIR:   {models_dir}")
        print(f"PORT:         {port}")
        print(f"APPDIR:       {app_dir}")
