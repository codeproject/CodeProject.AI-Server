## Trying to put this in a common place, but the import system is very strange.

import os
import io
import sys
import base64
import time
from datetime import datetime
from enum import Flag, unique

# The purpose of inserting the path is so the Python import system looks in the right spot for packages. 
# ie .../pythonXX/venv/Lib/site-packages. 
# This depends on the VENV we're actually running in. So: get the location of the current exe
# and work from that.

# Get the location of the current python interpreter, and then add the site-packages associated
# with that interpreter to the PATH so python will find the packages we've installed
current_python_dir = os.path.join(os.path.dirname(sys.executable))
if current_python_dir != "":
    package_path = os.path.normpath(os.path.join(current_python_dir, '../lib/python' + sys.version[:3] + '/site-packages/'))
    sys.path.insert(0, package_path)
    # print("Adding " + package_path + " to packages search path")

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

class ModuleWrapper:
    """
    A thin abstraction + helper methods to allow python modules to communicate with the
    backend of main API Server
    """
    _execution_provider  = "CPU"

    python_dir          = current_python_dir
    virtual_env         = os.getenv("VIRTUAL_ENV",   f"{python_dir}/venv")
    errLog_APIkey       = os.getenv("ERRLOG_APIKEY", "")
    port                = os.getenv("PORT",          "5000")

    moduleId            = os.getenv("MODULE_ID",     "CodeProject.AI")
    hardwareId          = "CPU"
    
    _error_pause_secs   = 1.0
    _log_timing_events  = True
    _verbose_exceptions = True

    _base_queue_url     = f"http://localhost:{port}/v1/queue/"
    _base_log_url       = f"http://localhost:{port}/v1/log/"


    @property
    def executionProvider(self):
        return self._execution_provider
      
    @executionProvider.setter
    def executionProvider(self, execution_provider):
        if (execution_provider is None) or (execution_provider == ""):
            self._execution_provider = "CPU"
        else:
            self._execution_provider = execution_provider

    _request_session = requests.Session()

    # Performance timer ===========================================================================

    def start_timer(self, desc: str) -> tuple:
        """
        Starts a timer and initializes the description string that will be associated with the time
        Param: desc - the description
        Returns a tuple containing the description and the timer itself
        """
        return (desc, time.perf_counter())

    def end_timer(self, timer : tuple) -> None:
        """
        Ends a timing session and logs the time taken along with the initial description if the
        variable logTimingEvents = True
        Param: timer - A tuple containing the initial description and the timer object
        """
        (desc, startTime) = timer
        elapsedSeconds = time.perf_counter() - startTime
    
        if (self._log_timing_events):
            self.log(LogMethod.Info, {"message": f"{desc} took {elapsedSeconds:.3} seconds"})


    # Service Commands and Responses ==============================================================

    def get_command(self, queueName : str) -> "list[str]":

        """
        Gets a command from the given queue. CodeProject.AI works on the basis of having a client
        pass requests to the frontend server, which in turns places each request into various command
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
            cmdTimer = self.start_timer(f"Getting Command from {queueName}")

            url      = self._base_queue_url + queueName + "?moduleId=" + self.moduleId
            if self.executionProvider is not None :
                url += "&executionProvider=" + self.executionProvider

            response = self._request_session.get(
                url,
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

            err_msg = "Error retrieving command: Is the API Server running?"
            if self._verbose_exceptions:
                err_msg = str(ex)

            self.log(LogMethod.Error|LogMethod.Cloud, {
                "message": err_msg,
                "method": "get_command",
                "process": queueName,
                "file": "CodeProjectAI.py",
                "exception_type": "Exception"
            })
            time.sleep(self._error_pause_secs)
            return []

        finally:
            if success:
                self.end_timer(cmdTimer)

        
    def get_image_from_request(self, req_data: JSON, index : int) -> Image:
        """
        Gets an image from the requests 'files' array that was passed in as part of a HTTP POST.
        Param: req_data - the request data from the HTTP form
        Param: index - the index of the image to return
        Returns: An image if succesful; None otherwise.
        """

        payload = None
        try:
            payload     = req_data["payload"]
            queueName   = payload.get("queue","N/A")
            files       = payload["files"]
            img_file    = files[index]
            img_dataB64 = img_file["data"]
            img_bytes   = base64.b64decode(img_dataB64)
            img_stream  = io.BytesIO(img_bytes)
            img         = Image.open(img_stream).convert("RGB")

            return img

        except Exception as ex:
            err_msg = "Unable to get image from request"
            if self._verbose_exceptions:
                err_msg = str(ex)

            self.log(LogMethod.Error|LogMethod.Server|LogMethod.Cloud, {
                "message": err_msg,
                "method": "getImageFromRequest",
                "process": queueName,
                "file": "CodeProjectAI.py",
                "exception_type": "Exception"
            })

            return None


    def get_request_image_count(self, req_data: JSON) -> int:
        """
        Returns the number of images included in the HTTP request Form from the client
        Param: req_data - the request data from the HTTP form
        Returns: The number of images if successful; 0 otherwise. 
        """
        try:
            # req_data is a dict
            payload = req_data["payload"]
            # payload is also a dict
            files   = payload["files"]
            return len(files)

        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error getting getRequestImageCount: {str(ex)}")
            return 0

    def get_request_value(self, req_data, key : str, defaultValue : str = None):
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

        # self.log(LogMethod.Info, {"message": f"Getting request for module {self.moduleId}"})

        try:
            # req_data is a dict
            payload = req_data["payload"]
            valueList = payload["values"]

            # valueList is a list. Note that in a HTML form, each element may have multiple values 
            for value in valueList:
                if value["key"] == key :
                    return value["value"][0]
        
            return defaultValue

        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error getting get_request_value: {str(ex)}")
            return defaultValue

    def send_response(self, req_id : str, body : str) -> bool:
        """
        Sends the result of a comment to the analysis services back to the API server who will
        then pass this result back to the original calling client. CodeProject.AI works on the basis 
        of having a client pass requests to the frontend server, which in turns places each request 
        into various command queues. The backend analysis services continually pull requests from 
        the queue that they can service, process each request, and then send the results back to
        the server.
        Param: req_id - the ID of the request that was originally pulled from the command queue.
        Param: body: - the Json result (as a string) from the analysis of the request.
        Returns True on success; False otherwise
        """

        # self.log(LogMethod.Info, {"message": f"Sending response for module {self.moduleId}"})

        success       = False
        responseTimer = self.start_timer("Sending Response")

        try:
            url = self._base_queue_url + req_id + "?moduleId=" + self.moduleId
            if self.executionProvider is not None:
                url += "&executionProvider=" + self.executionProvider
            
            self._request_session.post(
                url,
                data = body,
                timeout=1,
                verify=False)

            success = True

        except Exception as ex:
            time.sleep(self._error_pause_secs)

            if self._verbose_exceptions:
                print(f"Error sending response: {str(ex)}")
            else:
                print(f"Error sending response: Is the API Server running?")

        finally:
            if success:
                self.end_timer(responseTimer)
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
            self._server_log(message)

        if logMethod & LogMethod.Cloud:
            self._cloud_log(data.get("process", ""),
                            data.get("method", ""),
                            data.get("file", ""),
                            data.get("message", ""),
                            data.get("exception_type", ""))
   
    def _server_log(self, entry : str) -> bool:

        """
        Sends a log entry to the API server. Handy if you wish to send logging info to clients
        that are using the API server (eg any dashboard app you have in place)
        Param: entry - The string containing the log entry
        Returns True on success; False otherwise
        """

        payload = { "entry" : entry }

        try:
            self._request_session.post(
                self._base_log_url, 
                data = payload, 
                timeout = 1, 
                verify = False)

            return True

        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error posting log: {str(ex)}")
            else:
                print(f"Error posting log: Is the API Server running?")
            return False


    def _cloud_log(self, process: str, method: str, file: str, message: str, exception_type: str) -> bool:
        """
        Logs an error to our remote logging server (errLog.io)
        Param: process - The name of the current process
        Param: method - The name of the current method
        Param: file - The name of the current file
        Param: message - The message to log
        Param: exception_type - The exception type if this logging is the result of an exception
        """

        url = 'https://relay.errlog.io/api/v1/log'

        obj = {
            'message' : message,
            'apikey' : self.errLog_APIkey,
            'applicationname' : 'CodeProject.AI',
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
        try:
            response = requests.post(url, data = obj, headers = headers)
        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error posting server log: {str(ex)}")
            else:
                print(f"Error posting server log: Do you have interwebz?")
            return False

        return response.status_code == 200