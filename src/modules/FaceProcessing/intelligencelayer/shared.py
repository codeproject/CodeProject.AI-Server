import os

class Settings:
    def __init__(
        self,
        FACE_HIGH,
        FACE_MEDIUM,
        FACE_LOW,
        FACE_MODEL,
        FACE_RECOG_MODEL,
    ):
        self.FACE_HIGH        = FACE_HIGH
        self.FACE_MEDIUM      = FACE_MEDIUM
        self.FACE_LOW         = FACE_LOW
        self.FACE_MODEL       = FACE_MODEL
        self.FACE_RECOG_MODEL = FACE_RECOG_MODEL

# CodeProject.AI module options helper
from module_options import ModuleOptions

class SharedOptions:

    PROFILE_SETTINGS = {
        "desktop_cpu": Settings(
            FACE_HIGH        = 416,
            FACE_MEDIUM      = 320,
            FACE_LOW         = 256,
            FACE_MODEL       = "face.pt",
            FACE_RECOG_MODEL = "facerec-high.model"
        ),

        "desktop_gpu": Settings(
            FACE_HIGH        = 416,
            FACE_MEDIUM      = 320,
            FACE_LOW         = 256,
            FACE_MODEL       = "face.pt",
            FACE_RECOG_MODEL = "facerec-high.model"
        ),

        "jetson": Settings(
            FACE_HIGH        = 384,
            FACE_MEDIUM      = 256,
            FACE_LOW         = 192,
            FACE_MODEL       = "face_lite.pt",
            FACE_RECOG_MODEL = "facerec-high.model"
        ),

        "windows_native": Settings(
            FACE_HIGH        = 416,
            FACE_MEDIUM      = 320,
            FACE_LOW         = 256,
            FACE_MODEL       = "face.pt",
            FACE_RECOG_MODEL = "facerec-high.model"
        ),
    }

    showEnvVariables = True

    ENABLE_GPU      = ModuleOptions.enable_GPU
    PORT            = ModuleOptions.port

    print(f"Trace: Vision AI services setup: Retrieving environment variables...")

    default_app_dir = os.getcwd()
    if default_app_dir.endswith("intelligencelayer"):
        default_app_dir = os.path.join(default_app_dir, "..")

    APPDIR          = os.path.normpath(ModuleOptions.getEnvVariable("APPDIR", default_app_dir))

    # We may add back the concept of profile
    PROFILE         = ModuleOptions.getEnvVariable("PROFILE", "desktop_gpu")
    MODE            = ModuleOptions.getEnvVariable("MODE", "Medium")

    USE_CUDA        = ModuleOptions.getEnvVariable("USE_CUDA", "True")
    USE_MPS         = ModuleOptions.getEnvVariable("USE_MPS", "True")

    DATA_DIR        = os.path.normpath(ModuleOptions.getEnvVariable("DATA_DIR",   f"{APPDIR}/datastore"))
    MODELS_DIR      = os.path.normpath(ModuleOptions.getEnvVariable("MODELS_DIR", f"{APPDIR}/assets"))

    USE_CUDA        = USE_CUDA.lower() == "true" and ENABLE_GPU
    USE_MPS         = USE_MPS.lower() == "true"  and ENABLE_GPU
    SLEEP_TIME      = 0.01

    if USE_CUDA:
        try:
            import torch
            USE_CUDA = torch.cuda.is_available()
            print(f"GPU in use: {torch.cuda.get_device_name(0)}")
        except:
            USE_CUDA = False
   
    if USE_MPS:
        try:
            import torch
            USE_MPS = hasattr(torch.backends, "mps") and torch.backends.mps.is_available()
        except:
            USE_MPS = False

    SHARED_APP_DIR  = os.path.normpath(os.path.join(APPDIR, MODELS_DIR))

    if PROFILE == "desktop_gpu" and not USE_CUDA:
        PROFILE = "desktop_cpu"

    SETTINGS = PROFILE_SETTINGS[PROFILE]

    # dump the important variables
    if showEnvVariables:
        print(f"debug: APPDIR:       {APPDIR}")
        print(f"debug: PROFILE:      {PROFILE}")
        print(f"debug: USE_CUDA:     {USE_CUDA}")
        print(f"debug: DATA_DIR:     {DATA_DIR}")
        print(f"debug: MODELS_DIR:   {MODELS_DIR}")
        print(f"debug: MODE:         {MODE}")