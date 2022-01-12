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

    APPDIR          = getEnvVariable("APPDIR", os.path.join(os.getcwd(), ".."))
    PROFILE         = getEnvVariable("PROFILE", "desktop_cpu")

    CUDA_MODE       = getEnvVariable("CUDA_MODE", "False")
    TEMP_PATH       = getEnvVariable("TEMP_PATH",  f"{APPDIR}\tempstore")
    DATA_DIR        = getEnvVariable("DATA_DIR",   f"{APPDIR}\datastore")
    MODELS_DIR      = getEnvVariable("MODELS_DIR", f"{APPDIR}\assets")
    PORT            = getEnvVariable("PORT", "5000")

    if CUDA_MODE == "True":
        CUDA_MODE = True
    else:
        CUDA_MODE = False

    SLEEP_TIME      = 0.01
    ERROR_PAUSE     = 1.0

    MODE            = "Medium"
    if "MODE" in os.environ:
        MODE = os.environ["MODE"]
    
    SHARED_APP_DIR  = os.path.join(APPDIR, MODELS_DIR)

    BaseQueueUrl = f"http://localhost:{PORT}/v1/queue/"
    BaseLogUrl   = f"http://localhost:{PORT}/v1/log/"

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
    
    if CUDA_MODE:
        SETTINGS.PLATFORM_PKGS = "gpufiles"
    # elif PROFILE != "windows_native":
    #    SETTINGS.PLATFORM_PKGS = "cpufiles"

    # print(f"APPDIR:       {APPDIR}")
    # print(f"PROFILE:      {PROFILE}")
    # print(f"CUDA_MODE:    {CUDA_MODE}")
    # print(f"TEMP_PATH:    {TEMP_PATH}")
    # print(f"DATA_DIR:     {DATA_DIR}")
    # print(f"MODELS_DIR:   {MODELS_DIR}")
    # print(f"PORT:         {PORT}")
    # print(f"BaseQueueUrl: {BaseQueueUrl}")

class FrontendClient:
    requestSession = requests.Session()

    # TODO: Wrap these into a Timer class
    def startTimer(self, desc:str) :
        return (desc, time.perf_counter())

    def endTimer(self, timer : tuple) :
        (desc, startTime) = timer
        elapsedSeconds = time.perf_counter() - startTime
        # log(f"{desc} took {elapsedSeconds:.3} seconds")

    # TODO: Wrap these into a Command class    
    def getCommand(self, queueName : str):
        success = False
        try:
            cmdTimer = self.startTimer(f"Getting Command from {queueName}")
            response = self.requestSession.get(
                SharedOptions.BaseQueueUrl + queueName,
                timeout=30,
                verify=False
            )
            if (response.ok and len(response.content) > 2):
                success = True
                content = response.text
                return [content]
            else:
                return []

        except Exception as ex:
            # print(f"Error retrieving command: {str(ex)}")
            print(f"Error retrieving command: Is the API Server running?")
            time.sleep(SharedOptions.ERROR_PAUSE)
            return []

        finally:
            if success:
                self.endTimer(cmdTimer)
        
    def sendResponse(self, req_id : str, body : str):
        self.log(f"Sending response for id: {req_id}")

        success = False
        respTimer = self.startTimer("Sending Response")

        try:
            self.requestSession.post(
                SharedOptions.BaseQueueUrl + req_id,
                data = body,
                timeout=1,
                verify=False)

            success = True

        except Exception as ex:
            time.sleep(SharedOptions.ERROR_PAUSE)
            print(f"Error sending response: {str(ex)}")
            # print(f"Error sending response: Is the API Server running?")

        finally:
            if success:
                self.endTimer(respTimer)

    def sendLog(self, entry : str):

        payload = { "entry" : entry }

        try:
            self.requestSession.put(
                SharedOptions.BaseLogUrl, 
                data = payload, 
                timeout = 1, 
                verify = False)

        except Exception as ex:
            # print(f"Error posting log: {str(ex)}")
            print(f"Error posting log: Is the API Server running?")
            return

    def log(self, entry : str, is_error : bool = False):
        if is_error:
            print(entry, file=sys.stderr, flush=True)
        else:
            print(entry, file=sys.stdout, flush=True)
        self.sendLog(entry)
