import os
import sys
import requests
import time

from enum import Enum

# This should be inside Settings as a "private" static method.
def getEnvVariable(varName: str, default: str):
    value = os.getenv(varName, "")
    if value == "" and default != "":
        value = default
        print(f"{varName} not found. Setting to default {default}")
    return value


class Settings:
    def __init__(
        self,
        DETECTION_HIGH,
        DETECTION_MEDIUM,
        DETECTION_LOW,
        DETECTION_MODEL,
        FACE_HIGH,
        FACE_MEDIUM,
        FACE_LOW,
        FACE_MODEL,
    ):
        self.DETECTION_HIGH   = DETECTION_HIGH
        self.DETECTION_MEDIUM = DETECTION_MEDIUM
        self.DETECTION_LOW    = DETECTION_LOW
        self.DETECTION_MODEL  = DETECTION_MODEL
        self.FACE_HIGH        = FACE_HIGH
        self.FACE_MEDIUM      = FACE_MEDIUM
        self.FACE_LOW         = FACE_LOW
        self.FACE_MODEL       = FACE_MODEL


class SharedOptions:

    APPDIR    = getEnvVariable("APPDIR", "C:\\CodeProject.SenseAI\\AnalysisLayer\\DeepStack")
    PROFILE   = getEnvVariable("PROFILE", "desktop_cpu")
    if PROFILE == "windows_native":
        sys.path.append(os.path.join(APPDIR,"python_packages"))

    # from redis import RedisError, StrictRedis

    CUDA_MODE       = getEnvVariable("CUDA_MODE", "False")
    TEMP_PATH       = getEnvVariable("TEMP_PATH", "")
    DATA_DIR        = getEnvVariable("DATA_DIR", "/datastore")
    MODELS_DIR      = getEnvVariable("MODELS_DIR", "assets")
    PORT            = getEnvVariable("PORT", "5000")

    SLEEP_TIME      = 0.01
    ERROR_PAUSE     = 1.0

    SHARED_APP_DIR  = os.path.join(APPDIR, MODELS_DIR)
    GPU_APP_DIR     = os.path.join(APPDIR, "gpufiles")
    CPU_APP_DIR     = os.path.join(APPDIR, "cpufiles")

    # print("APPDIR: " + APPDIR)
    # print("DATA_DIR: " + DATA_DIR)
    # print("TEMP_PATH: " + DATA_DIR)

    if CUDA_MODE == "True":
        APP_DIR   = GPU_APP_DIR
        CUDA_MODE = True
    else:
        APP_DIR   = CPU_APP_DIR
        CUDA_MODE = False

    MODE = "Medium"

    if "MODE" in os.environ:
        MODE = os.environ["MODE"]

    BaseUrl = f"http://localhost:{PORT}/v1/queue/"
    # This will be going
    # db = StrictRedis(host="localhost", db=0, decode_responses=True)

    # TB_EMBEDDINGS = "TB_EMBEDDINGS"

    PROFILE_SETTINGS = {
        "desktop_cpu": Settings(
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
    
    # print(f"APPDIR:     {APPDIR}")
    # print(f"PROFILE:    {PROFILE}")
    # print(f"CUDA_MODE:  {CUDA_MODE}")
    # print(f"TEMP_PATH:  {TEMP_PATH}")
    # print(f"DATA_DIR:   {DATA_DIR}")
    # print(f"MODELS_DIR: {MODELS_DIR}")
    # print(f"PORT:       {PORT}")
    # print(f"BaseUrl:    {BaseUrl}")

# These should be in a APICommand class class methods.
def getCommand(queueName : str):
    try:
        response = requests.get(
            SharedOptions.BaseUrl + queueName,
            verify=False
        )
        if (response.ok and len(response.content) > 2):
            content = response.text
            return [content]
        else:
            return []
    except:
        time.sleep(SharedOptions.ERROR_PAUSE)
        return []
        
def sendResponse(req_id : str, body : str):
    requests.post(
        SharedOptions.BaseUrl + req_id,
        data = body,
        verify=False)
