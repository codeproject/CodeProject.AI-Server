import os
import io
import sys
import base64
import time
import json
from datetime import datetime

# Get the Python interpreter directory, and add to the package search path the path of the packages
#  within our local virtual environment
if sys.platform.startswith('linux'):
    currentPythonDir = os.path.normpath(os.path.join(os.getcwd(), "../../bin/linux/python37"))
    sys.path.insert(0, currentPythonDir + "/venv/lib/python3.7/site-packages")
elif sys.platform.startswith('darwin'):
    currentPythonDir = os.path.normpath(os.path.join(os.getcwd(), "../../bin/osx/python37"))
    sys.path.insert(0, currentPythonDir + "/venv/lib/python3.7/site-packages")
elif sys.platform.startswith('win'):
    currentPythonDir = os.path.normpath(os.path.join(os.getcwd(), "..\\..\\bin\\win\\python37"))
    sys.path.insert(0, currentPythonDir + "\\venv\\lib\\site-packages")
else:
    currentPythonDir = ""

import requests
from PIL import Image

class SenseAIBackend:

    pythonDir     = currentPythonDir
    virtualEnv    = os.getenv("VIRTUAL_ENV",   f"{pythonDir}/venv")
    errLog_APIkey = os.getenv("ERRLOG_APIKEY", "")
    port          = os.getenv("PORT",          "5000")
    
    errorPause    = 1.0

    BaseQueueUrl  = f"http://localhost:{port}/v1/queue/"
    BaseLogUrl    = f"http://localhost:{port}/v1/log/"


    requestSession = requests.Session()

    # Performance timer ===========================================================================

    def startTimer(self, desc:str) :
        return (desc, time.perf_counter())

    def endTimer(self, timer : tuple) :
        (desc, startTime) = timer
        elapsedSeconds = time.perf_counter() - startTime
        # log(f"{desc} took {elapsedSeconds:.3} seconds")


    # Service Commands and Responses ==============================================================

    def getCommand(self, queueName : str):
        success = False
        try:
            cmdTimer = self.startTimer(f"Getting Command from {queueName}")
            response = self.requestSession.get(
                self.BaseQueueUrl + queueName,
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
            time.sleep(self.errorPause)
            return []

        finally:
            if success:
                self.endTimer(cmdTimer)
        
    def sendResponse(self, req_id : str, body : str):
        # self.log(f"Sending response for id: {req_id}")

        success = False
        respTimer = self.startTimer("Sending Response")

        try:
            self.requestSession.post(
                self.BaseQueueUrl + req_id,
                data = body,
                timeout=1,
                verify=False)

            success = True

        except Exception as ex:
            time.sleep(self.errorPause)
            print(f"Error sending response: {str(ex)}")
            # print(f"Error sending response: Is the API Server running?")

        finally:
            if success:
                self.endTimer(respTimer)


    # Logging and Error Reporting =================================================================

    def sendLog(self, entry : str):

        payload = { "entry" : entry }

        try:
            self.requestSession.post(
                self.BaseLogUrl, 
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


    def errLog(self, method : str, file:str, message : str, exceptionType: str):
        """
        Logs an error to our remote logging server (errLog.io)
        """

        url = 'https://relay.errlog.io/api/v1/log'

        obj = {
            'message' : message,
            'apikey' : self.errLog_APIkey,
            'applicationname' : 'CodeProject SenseAI',
            'type' : exceptionType,
            'errordate' : datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S"),
            'filename' : file,
            'method' : method,
            'lineno' : 0,
            'colno' : 0
        }

        data = json.dumps(obj)

        # If you want to see the data you're sending.
        # print "Json Data: ", data

        headers = {'Content-Type': 'application/json','Accept': 'application/json'}
        r = requests.post(url, data = data, headers = headers)

        # print("Response:", r)
        # print("Text: " , r.text)

        return r

    def getImageFromRequest(self, req_data, index : int):
        """
        Gets an image from the requests 'files' array.
        """
        payload     = req_data["payload"]
        files       = payload["files"]
        img_file    = files[index]
        img_dataB64 = img_file["data"]
        img_bytes   = base64.b64decode(img_dataB64)
        img_stream  = io.BytesIO(img_bytes)
        img         = Image.open(img_stream).convert("RGB")

        return img

    def getRequestImageCount(self, req_data):
        payload = req_data["payload"]
        files   = payload["files"]
        return len(files)

    def getRequestValue(self, req_data, key : str):
        payload = req_data["payload"]
        values  = payload["values"]

        for value in values:
            if value["key"] == key :
                return value["value"][0]

        return None

