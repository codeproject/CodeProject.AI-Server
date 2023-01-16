import os
import torch


class Settings:
    def __init__(
        self,
        PLATFORM_PKGS,
        DETECTION_HIGH,
        DETECTION_MEDIUM,
        DETECTION_LOW,
        DETECTION_MODEL,
        FACE_HIGH,
        FACE_MEDIUM,
        FACE_LOW,
        FACE_MODEL,
    ):
        self.PLATFORM_PKGS    = PLATFORM_PKGS
        self.DETECTION_HIGH   = DETECTION_HIGH
        self.DETECTION_MEDIUM = DETECTION_MEDIUM
        self.DETECTION_LOW    = DETECTION_LOW
        self.DETECTION_MODEL  = DETECTION_MODEL
        self.FACE_HIGH        = FACE_HIGH
        self.FACE_MEDIUM      = FACE_MEDIUM
        self.FACE_LOW         = FACE_LOW
        self.FACE_MODEL       = FACE_MODEL


class SharedOptions:

    def getEnvVariable(varName: str, default: str):
        value = os.getenv(varName, "")
        if value == "" and default != "":
            value = default
            print(f"{varName} not found. Setting to default {default}")

        return value

    showEnvVariables = False

    print(f"Vision AI services setup: Retrieving environment variables...")

    APPDIR          = os.path.normpath(getEnvVariable("APPDIR", os.path.join(os.getcwd(), "..")))

    # We may add back the concept of profile
    PROFILE         = getEnvVariable("PROFILE", "desktop_gpu")

    SUPPORT_GPU     = getEnvVariable("CPAI_MODULE_SUPPORT_GPU", "True")
    USE_CUDA        = getEnvVariable("USE_CUDA", "True")
    DATA_DIR        = os.path.normpath(getEnvVariable("DATA_DIR",   f"{APPDIR}/datastore"))
    MODELS_DIR      = os.path.normpath(getEnvVariable("MODELS_DIR", f"{APPDIR}/assets"))
    PORT            = getEnvVariable("CPAI_PORT", "32168")

    SUPPORT_GPU     = SUPPORT_GPU.lower() == "true"
    USE_CUDA        = USE_CUDA.lower() == "true"
    USE_CUDA        = SUPPORT_GPU and USE_CUDA and torch.cuda.is_available()

    SLEEP_TIME      = 0.01

    MODE            = "Medium"

    if "MODE" in os.environ:
        MODE = os.environ["MODE"]
    
    SHARED_APP_DIR  = os.path.normpath(os.path.join(APPDIR, MODELS_DIR))

    PROFILE_SETTINGS = {
        "desktop_cpu": Settings(
            PLATFORM_PKGS    = "cpufiles",
            DETECTION_HIGH   = 640,
            DETECTION_MEDIUM = 416,
            DETECTION_LOW    = 256,
            DETECTION_MODEL  = "yolov5m.pt",
            FACE_HIGH        = 416,
            FACE_MEDIUM      = 320,
            FACE_LOW         = 256,
            FACE_MODEL       = "face.pt",
        ),

        "desktop_gpu": Settings(
            PLATFORM_PKGS    = "gpufiles",
            DETECTION_HIGH   = 640,
            DETECTION_MEDIUM = 416,
            DETECTION_LOW    = 256,
            DETECTION_MODEL  = "yolov5m.pt",
            FACE_HIGH        = 416,
            FACE_MEDIUM      = 320,
            FACE_LOW         = 256,
            FACE_MODEL       = "face.pt",
        ),

        "jetson": Settings(
            PLATFORM_PKGS    = "cpufiles",
            DETECTION_HIGH   = 416,
            DETECTION_MEDIUM = 320,
            DETECTION_LOW    = 256,
            DETECTION_MODEL  = "yolov5s.pt",
            FACE_HIGH        = 384,
            FACE_MEDIUM      = 256,
            FACE_LOW         = 192,
            FACE_MODEL       = "face_lite.pt",
        ),

        "windows_native": Settings(
            PLATFORM_PKGS    = "python_packages",
            DETECTION_HIGH   = 640,
            DETECTION_MEDIUM = 416,
            DETECTION_LOW    = 256,
            DETECTION_MODEL  = "yolov5m.pt",
            FACE_HIGH        = 416,
            FACE_MEDIUM      = 320,
            FACE_LOW         = 256,
            FACE_MODEL       = "face.pt",
        ),
    }

    SETTINGS = PROFILE_SETTINGS[PROFILE]
    
    if USE_CUDA:
        SETTINGS.PLATFORM_PKGS = "gpufiles"
    # elif PROFILE != "windows_native":
    #    SETTINGS.PLATFORM_PKGS = "cpufiles"

    # dump the important variables
    if showEnvVariables:
        print(f"APPDIR:       {APPDIR}")
        print(f"PROFILE:      {PROFILE}")
        print(f"USE_CUDA:     {USE_CUDA}")
        print(f"TEMP_PATH:    {TEMP_PATH}")
        print(f"DATA_DIR:     {DATA_DIR}")
        print(f"MODELS_DIR:   {MODELS_DIR}")
        print(f"PORT:         {PORT}")
        print(f"MODE:         {MODE}")

    if USE_CUDA:
        print(f"GPU in use: {torch.cuda.get_device_name(0)}")
