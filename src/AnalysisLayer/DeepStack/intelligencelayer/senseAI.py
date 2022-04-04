import os
import sys

# Add to the package search path the path of the packages within our local virtual environment
if sys.platform.startswith('linux'):
    currentPythonDir = os.path.normpath(os.path.join(os.getcwd(), "../../bin/linux/python37"))
elif sys.platform.startswith('darwin'):
    currentPythonDir = os.path.normpath(os.path.join(os.getcwd(), "../../bin/osx/python37"))
elif sys.platform.startswith('win'):
    currentPythonDir = os.path.normpath(os.path.join(os.getcwd(), "..\\..\\bin\\win\\python37"))
else:
    currentPythonDir = ""

if currentPythonDir != "":
    sys.path.insert(0, currentPythonDir)

import time
import json
import requests
from datetime import datetime

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