import os
import io
import sys
import base64
import time
from datetime import datetime
from enum import Flag, unique

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

import requests
from PIL import Image
from typing import Dict, List, Union

# Define a Json type to allow type hints to be sensible.
# See https://adamj.eu/tech/2021/06/14/python-type-hints-3-somewhat-unexpected-uses-of-typing-any-in-pythons-standard-library/
_PlainJSON = Union[
    None, bool, int, float, str, List["_PlainJSON"], Dict[str, "_PlainJSON"]
]
JSON = Union[_PlainJSON, Dict[str, "JSON"], List["JSON"]]


@unique
class LogMethod(Flag):
    """ The types of logging that can be done"""
    Unknown = 0
    Info    = 1   # Standard info output such as the console
    Error   = 2   # Standard error output
    Server  = 4   # Send the log to the front end API server
    Cloud   = 8   # Send the log to a cloud provider such as errlog.io
    All     = 15  # It's a job lot

class SenseAIBackend:
    """
    A thin abstraction + helper methods to allow python modules to communicate with the main API
    Server
    """

    pythonDir       = currentPythonDir
    virtualEnv      = os.getenv("VIRTUAL_ENV",   f"{pythonDir}/venv")
    errLog_APIkey   = os.getenv("ERRLOG_APIKEY", "")
    port            = os.getenv("PORT",          "5000")
    
    errorPause      = 1.0
    logTimingEvents = True

    BaseQueueUrl    = f"http://localhost:{port}/v1/queue/"
    BaseLogUrl      = f"http://localhost:{port}/v1/log/"


    requestSession = requests.Session()

    # Performance timer ===========================================================================

    def startTimer(self, desc: str) -> tuple:
        """
        Starts a timer and initializes the description string that will be associated with the time
        Param: desc - the description
        Returns a tuple containing the description and the timer itself
        """
        return (desc, time.perf_counter())

    def endTimer(self, timer : tuple) -> None:
        """
        Ends a timing session and logs the time taken along with the initial description if the
        variable logTimingEvents = True
        Param: timer - A tuple containing the initial description and the timer object
        """
        (desc, startTime) = timer
        elapsedSeconds = time.perf_counter() - startTime
    
        if (self.logTimingEvents):
            self.log(LogMethod.Info, {"message": f"{desc} took {elapsedSeconds:.3} seconds"})


    # Service Commands and Responses ==============================================================

    def getCommand(self, queueName : str) -> "list[str]":

        """
        Gets a command from the given queue. SenseAI works on the basis of having a client pass
        requests to the frontend server, which in turns places each request into various command
        queues. The backend analysis services continually pull requests from the queue that they
        can service. Each request for a queued command is done via a long poll HTTP request.
        Param: queueName - the name of the queue from which the command should be retrieved.
        Returns the Json package containing the raw request from the client that was sent to the
        server

        Remarks: The API server will currently only return a single command, not a list, so we
        could just as easily return a string instead of a list of strings. We return a list to
        maintain compatibility with the old legacy modules we started with, but also to future-
        proof the code in case we want to allow batch processing. Be aware that batch processing
        will mean less opportunity to load balance the requests.
        """

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
                self.log(LogMethod.Info, {"message": "retrieved TextSummary command"})

                return [content]
            else:
                return []

        except Exception as ex:
            self.log(LogMethod.Error|LogMethod.Cloud, {
                "message": "Error retrieving command: Is the API Server running?",
                "method": "getCommand",
                "process": queueName,
                "file": "senseAI.py",
                "exception_type": "Exception"
            })
            time.sleep(self.errorPause)
            return []

        finally:
            if success:
                self.endTimer(cmdTimer)

        
    def getImageFromRequest(self, req_data: JSON, index : int) -> Image:
        """
        Gets an image from the requests 'files' array that was passed in as part of a HTTP POST.
        Param: req_data - the request data from the HTTP form
        Param: index - the index of the image to return
        Returns: An image if succesful; None otherwise.
        """
        payload = req_data.get("payload", None)
        if payload is None:
            return None

        files = payload.get("files", None)
        if files is None:
            return None

        img_file = files.get(index, None)
        if img_file is None:
            return None

        img_dataB64 = img_file.get("data", None)
        if img_dataB64 is None:
            return None

        try:
            img_bytes   = base64.b64decode(img_dataB64)
            img_stream  = io.BytesIO(img_bytes)
            img         = Image.open(img_stream).convert("RGB")
            return img
        except:
            self.log(LogMethod.Error|LogMethod.Cloud, {
                "message": "Unable to get image from request",
                "method": "getImageFromRequest",
                "process": queueName,
                "file": "senseAI.py",
                "exception_type": "Exception"
            })


    def getRequestImageCount(self, req_data: JSON) -> int:
        """
        Returns the number of images included in the HTTP request Form from the client
        Param: req_data - the request data from the HTTP form
        Returns: The number of images if successful; 0 otherwise. 
        """

        # req_data is a dict
        payload = req_data.get("payload", None)
        if payload is None:
            return 0

        # payload is also a dict
        files = payload.get("files", None)
        if files is None:
            return 0

        # files is a list
        return len(files)


    def getRequestValue(self, req_data, key : str, defaultValue : str = None):
        """
        Gets a value from the HTTP request Form send by the client
        Param: req_data - the request data from the HTTP form
        Param: key - the name of the key holding the data in the form collection
        Returns: The data if successful; None otherwise.
        Remarks: Note that HTTP forms contain multiple values per key (a string array) to allow
        for situations like checkboxes, where a set of checkbox controls share a name but have 
        unique IDs. The form will contain an array of values for the shared name. WE ONLY RETURN
        THE FIRST VALUE HERE.
        """
        try:
            # req_data is a dict
            payload = req_data.get("payload", None)
            if payload is None:
                return defaultValue

            # payload is also a dict
            valueList = payload.get("values", None)
            if valueList is None:
                return defaultValue

            # valueList is a list. Note that in a HTML form, each element may have multiple values 
            for value in valueList:
                if value["key"] == key :
                    return value["value"][0]
        
            return defaultValue

        except:
            return defaultValue

    def sendResponse(self, req_id : str, body : str) -> bool:

        """
        Sends the result of a comment to the analysis services back to the API server who will
        then pass this result back to the original calling client. SenseAI works on the basis of 
        having a client pass requests to the frontend server, which in turns places each request 
        into various command queues. The backend analysis services continually pull requests from 
        the queue that they can service, process each request, and then send the results back to
        the server.
        Param: req_id - the ID of the request that was originally pulled from the command queue.
        Param: body: - the Json result (as a string) from the analysis of the request.
        Returns True on success; False otherwise
        """

        success       = False
        responseTimer = self.startTimer("Sending Response")

        try:
            # self.log(LogMethod.Info, {"message": f"Sending response for id: {req_id}"})

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
                self.endTimer(responseTimer)
            return success


    # Logging and Error Reporting =================================================================

    def log(self, logMethod: LogMethod, data: JSON) -> None:

        """
        Outputs a log entry to one or more logging providers
        Param: logMethod - can be Info (console), Error (err output), Server (sends the log to the
        API server), or Cloud (sends the log to a cloud provider such as errlog.io)
        Param: data - a Json object (really: a dictionary) of the form:
           { 
              "process": "Name of process", 
              "file": "filename.ext", 
              "message": "The message to log",
              "exception_type": "Exception Type"
           }

        Only "message" is required.
        """

        message = ""
        if data.get("process", "") != "":
            message += data["process"] + " "
        if data.get("file", "") != "":
            message += "(" + data["file"] + ")"

        if message != "":
            message += "\n"

        if data.get("exception_type", "") != "":
            message += data["exception_type"] + ": "
        if data.get("message", "") != "":
            message += message + data["message"]

        if logMethod & LogMethod.Error:
            print(message, file=sys.stderr, flush=True)

        if logMethod & LogMethod.Info:
            print(message, file=sys.stdout, flush=True)

        if logMethod & LogMethod.Server:
            self._serverLog(message)

        if logMethod & LogMethod.Cloud:
            self._cloudLog(data.get("process", ""),
                           data.get("method", ""),
                           data.get("file", ""),
                           data.get("message", ""),
                           data.get("exception_type", ""))
   
    def _serverLog(self, entry : str) -> bool:

        """
        Sends a log entry to the API server. Handy if you wish to send logging info to clients
        that are using the API server (eg any dashboard app you have in place)
        Param: entry - The string containing the log entry
        Returns True on success; False otherwise
        """

        payload = { "entry" : entry }

        try:
            self.requestSession.post(
                self.BaseLogUrl, 
                data = payload, 
                timeout = 1, 
                verify = False)

            return True

        except Exception as ex:
            # print(f"Error posting log: {str(ex)}")
            print(f"Error posting log: Is the API Server running?")
            return False


    def _cloudLog(self, process: str, method: str, file: str, message: str, exception_type: str) -> bool:
        """
        Logs an error to our remote logging server (errLog.io)
        Param: process - The name of the current process
        Param: method - The name of the current method
        Param: file- The name of the current file
        Param: message - The message to log
        Param: exception_type - The exception type if this logging is the result of an exception
        """

        url = 'https://relay.errlog.io/api/v1/log'

        obj = {
            'message' : message,
            'apikey' : self.errLog_APIkey,
            'applicationname' : 'CodeProject SenseAI',
            'type' : exception_type,
            'errordate' : datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S"),
            'filename' : file,
            'method' : process + "." + method,
            'lineno' : 0,
            'colno' : 0
        }

        # If you want to see the data you're sending:
        # import json
        # data = json.dumps(obj)
        # print "Json Data: ", data

        headers = {'Content-Type': 'application/json','Accept': 'application/json'}
        response = requests.post(url, data = obj, headers = headers)

        return response.status_code == 200