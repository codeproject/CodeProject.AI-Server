
from datetime import datetime
from enum import Flag, unique
import os
import sys
from threading import Lock

from common import JSON
import asyncio
from asyncio import Queue
import aiohttp
import aiofiles


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

class LogItem:
    def __init__(self, logMethod: LogMethod, data: JSON):
        self.method = logMethod
        self.data   = data

class AnalysisLogger():

    def __init__(self, server_port: str, log_dir: str, errLog_APIkey: str):

        """
        Constructor
        """

        # Hardcoding localhost because the current plans are to never have
        # backend analysis servers NOT on the same machine as the server
        self.base_log_url        = f"http://localhost:{server_port}/v1/log/"
        self.log_dir             = log_dir
        self.errLog_APIkey       = errLog_APIkey
        self.defaultLogging      = LogMethod.File | LogMethod.Info   # Always included
        self._sync_log_lock      = Lock()
        self._verbose_exceptions = True
        self._logging_queue      = Queue(1024)
        self._cancelled          = False


    async def logging_loop(self):

        """ 
        Runs the main logging loop which queries the logging queue and then
        forwards each logging request to the logging methods themselves.
        """

        self._request_session = aiohttp.ClientSession()

        while not self._cancelled:
            try:
                log_item: LogItem = await self._logging_queue.get()
                await self.do_log(log_item.method, log_item.data)
            except Exception as ex:
                print(f"Exception while logging: {ex}")

        await self._request_session.close()


    async def log_async (self, logMethod: LogMethod, data: JSON) -> None:

        """
        Performs an async logging operation. All this does is place the logging
        request on a queue, which is fetched and processed by the main logging
        loop
        """
        # if data or not data.get("message", ""):
        #    return

        try:
            await self._logging_queue.put(LogItem(logMethod, data))
        except:
            print("Queue Full Error")


    def log(self, logMethod: LogMethod, data: JSON) -> None:

        """
        Performs a logging operation, synchronously. Sort of. This places a
        logging request on a queue, which is fetched and processed by the main
        logging loop (a separate task). So: the logging in this method isn't a
        direct call to the logging methods, but it's not an async call so is
        compatible with non-async (and legacy) code.
        """
        # if data or not data.get("message", ""):
        #     return

        # being really paranoid.  The documentation suggests the Queue is not
        # thread safe but it appears to be ok without. Just to be safe ...
        with self._sync_log_lock:
            try:
                self._logging_queue.put_nowait(LogItem(logMethod, data))
            except:
                print("Queue Full Error")


    def cancel_logging(self) -> None:
        """ Cancels the main logging loop"""
        self._cancelled = True;


    async def do_log(self, logMethod: LogMethod, data: JSON) -> None:

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

        if exception == "Exception":
            exception = "(General Exception)"

        if filename:
            entry += " (" + filename + ")"

        if exception:
            entry += " " + exception + " "

        if entry:
            entry += ": "

        if message:
            entry += message

        # HACK to trim superfluous logs
        unimportant = message.startswith("Cannot connect to host")

        loggingTasks = []

        logged_to_server = False
        if logMethod & LogMethod.Server or self.defaultLogging & LogMethod.Server:
            loggingTasks.append( asyncio.create_task(self._server_log(entry, process, label, loglevel)) )
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

        if not unimportant:
            if logMethod & LogMethod.Cloud or self.defaultLogging & LogMethod.Cloud:
                loggingTasks.append( asyncio.create_task(self._cloud_log(process, method, filename, message, exception)) )

            if logMethod & LogMethod.File or self.defaultLogging & LogMethod.File:
                loggingTasks.append( asyncio.create_task(self._file_log(process, method, filename, message, exception)) )
   
        [await task for task in loggingTasks]


    async def _server_log(self, entry : str, category: str, label: str, loglevel: str) -> bool:

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
            await self._request_session.post(
                self.base_log_url, 
                data    = payload, 
                timeout = 1)

            return True

        except TimeoutError:
            print(f"Timeout posting log")
            return False

        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error posting log: {str(ex)}")
            else:
                print(f"Error posting log: Is the API Server running?")
            return False


    async def _cloud_log(self, process: str, method: str, filename: str, message: str,
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

        if not self.errLog_APIkey:
            return

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
            response = await self._request_session.post(url, data = obj, headers = headers)
        except Exception as ex:
            if self._verbose_exceptions:
                print(f"Error posting server log: {str(ex)}")
            else:
                print(f"Error posting server log: Do you have interwebz?")
            return False

        return hasattr(response, 'status_code') and response.status_code == 200


    async def _file_log(self, process: str, method: str, filename: str, message: str,
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
            line += ' [' + exception_type + ']'      
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
            async with aiofiles.open(filepath, 'a') as file_object:
                await file_object.write(line)

            return True

        except OSError as os_error:
            print(f"Unable to store log entry: {os_error.strerror}")
            return False
        except Exception as ex:
            print(f"Unable to write to the file log")
            return False