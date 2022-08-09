
from datetime import datetime
from enum import Flag, unique
import os
import sys

import requests
from common import JSON


@unique
class LogMethod(Flag):
    """ The types of logging that can be done"""
    Unknown = 0
    Info    = 1   # Standard info output such as the console
    Error   = 2   # Standard error output
    Server  = 4   # Send the log to the front end API server
    Cloud   = 8   # Send the log to a cloud provider such as errlog.io
    File    = 16  # Send the log to a cloud provider such as errlog.io
    All     = 31  # It's a job lot

class AnalysisLogger():

    # Constructor
    def __init__(self, server_port: str, log_dir: str, errLog_APIkey: str):

        # Hardcoding localhost because the current plans are to never have
        # backend analysis servers NOT on the same machine as the server
        self.base_log_url        = f"http://localhost:{server_port}/v1/log/"
        self.log_dir             = log_dir
        self.errLog_APIkey       = errLog_APIkey
        self.defaultLogging      = LogMethod.File | LogMethod.Info # Always included

        self._request_session    = requests.Session()
        self._verbose_exceptions = True


    def log(self, logMethod: LogMethod, data: JSON) -> None:

        """
        Outputs a log entry to one or more logging providers
        Param: logMethod - can be Info (console), Error (err output), Server 
        (sends the log to the API server), or Cloud (sends the log to a cloud 
        provider such as errlog.io)
        Param: data - a Json object (really: a dictionary) of the form:
           { 
              "process": "Name of process", 
              "filename": "filename.ext", 
              "label": "my label",
              "loglevel": "information", 
              "message": "The message to log",
              "exception_type": "Exception Type"
           }

        Only "message" is required.
        """

        entry     = ""

        message   = data.get("message", "")
        process   = data.get("process", "")
        label     = data.get("label",   "")
        loglevel  = data.get("loglevel", "information")
        method    = data.get("method", ""),
        filename  = data.get("filename", ""),
        exception = data.get("exception_type", "")

        # checks
        message   = message   if message   and isinstance(message, str)   else ""
        process   = process   if process   and isinstance(process, str)   else ""
        label     = label     if label     and isinstance(label, str)     else ""
        loglevel  = loglevel  if loglevel  and isinstance(loglevel, str)  else ""
        method    = method    if method    and isinstance(method, str)    else ""
        filename  = filename  if filename  and isinstance(filename, str)  else ""
        exception = exception if exception and isinstance(exception, str) else ""

        if filename:
            entry += " (" + filename + ")"

        if exception:
            entry += " Exception: " + exception + " "

        if entry:
            entry += ": "

        if message:
            entry += message

        logged_to_server = False
        if logMethod & LogMethod.Server or self.defaultLogging & LogMethod.Server:
            self._server_log(entry, process, label, loglevel)
            logged_to_server = True

        # The server already captures stdout and stderr so no sense in logging
        # to those and then also logging to the server

        if process:
           entry += process + ": " + entry

        if logMethod & LogMethod.Error or self.defaultLogging & LogMethod.Error:
            if not logged_to_server:
                print(entry, file=sys.stderr, flush=True)

        if logMethod & LogMethod.Info or self.defaultLogging & LogMethod.Info:
            if not logged_to_server:
                print(entry, file=sys.stdout, flush=True)

        if logMethod & LogMethod.Cloud or self.defaultLogging & LogMethod.Cloud:
            self._cloud_log(process, method, filename, message, exception)

        if logMethod & LogMethod.File or self.defaultLogging & LogMethod.File:
            self._file_log(process, method, filename, message, exception)
   

    def _server_log(self, entry : str, category: str, label: str, loglevel: str) -> bool:

        """
        Sends a log entry to the API server. Handy if you wish to send logging 
        info to clients that are using the API server (eg any dashboard app you 
        have in place)
        Param: entry - The string containing the log entry
        Returns True on success; False otherwise
        """

        payload = {
           "entry" : entry, 
           "category": category, 
           "label": label, 
           "log_level" : loglevel
        }

        try:
            self._request_session.post(
                self.base_log_url, 
                data    = payload, 
                timeout = 1, 
                verify  = False)

            return True

        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error posting log: {str(ex)}")
            else:
                print(f"Error posting log: Is the API Server running?")
            return False


    def _cloud_log(self, process: str, method: str, filename: str, message: str,
                   exception_type: str) -> bool:
        """
        Logs an error to our remote logging server (errLog.io)
        Param: process - The name of the current process
        Param: method - The name of the current method
        Param: filename - The name of the current file
        Param: message - The message to log
        Param: exception_type - The exception type if this logging is the result
                                of an exception
        """

        url = 'https://relay.errlog.io/api/v1/log'

        obj = {
            'message' : message,
            'apikey' : self.errLog_APIkey,
            'applicationname' : 'CodeProject.AI',
            'type' : exception_type,
            'errordate' : datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S"),
            'filename' : filename,
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
            response = self._request_session.post(url, data = obj, headers = headers)
        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error posting server log: {str(ex)}")
            else:
                print(f"Error posting server log: Do you have interwebz?")
            return False

        return response.status_code == 200


    def _file_log(self, process: str, method: str, filename: str, message: str,
                  exception_type: str) -> bool:
        """
        Logs an error to a file
        Param: process - The name of the current process
        Param: method - The name of the current method
        Param: filename - The name of the current file
        Param: message - The message to log
        Param: exception_type - The exception type if this logging is the result
                                of an exception
        """

        line = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        if len(exception_type) > 0:
            line += ' [Exception: ' + exception_type + ']'      
        line += ': ' + message
       
        if len(filename) > 0:
            line += '(file: ' + filename
            if len(process) > 0:
                line += ' in ' + process + "." + method
            line += ')'

        line += '\n'

        try:
            directory = self.log_dir + os.sep + 'logs'
            if not os.path.isdir(directory):
                os.mkdir(directory)

            filepath = directory + os.sep + 'log-' + datetime.now().strftime("%Y-%m-%d") + '.txt'
            with open(filepath, 'a') as file_object:
                file_object.write(line)

            return True

        except Exception as ex:
            print(f"Unable to write to the file log")
            return False