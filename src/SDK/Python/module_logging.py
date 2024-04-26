
from datetime import datetime
from enum import Enum, Flag, unique
import os
import sys
from threading import Lock

from common import JSON
import asyncio
from asyncio import Queue
import aiohttp
import aiofiles


class LogVerbosity(Enum):
    """ The logging noise level"""
    Unknown = "unknown"
    Quiet   = "quiet"  # Minimal logging
    Info    = "info"   # Useful and interesting information
    Loud    = "loud"   # Everything that can be shown. 

@unique
class LogMethod(Flag):
    """ The types of logging that can be done"""
    Unknown = 0
    Info    = 1   # Standard info output such as the console
    Error   = 2   # Standard error output
    Server  = 4   # Send the log to the front end API server
    File    = 8   # Store the logs directly in a file
    All     = 15  # It's a job lot

class LogItem:
    def __init__(self, logMethod: LogMethod, data: JSON):
        self.method = logMethod
        self.data   = data

class ModuleLogger():

    def __init__(self, server_port: str, log_dir: str):

        """
        Constructor
        """

        # Hardcoding localhost because the current plans are to never have
        # backend analysis servers NOT on the same machine as the server
        self.base_log_url         = f"http://localhost:{server_port}/v1/log/"
        self.log_dir              = log_dir
        self.defaultLogging       = LogMethod.File | LogMethod.Info   # Always included
        self._sync_log_lock       = Lock()
        self._logging_queue       = Queue(1024)
        self._cancelled           = False
        self._server_healthy      = True # We'll be optimistic to start
        self.logging_loop_started = False


    async def logging_loop(self):

        """ 
        Runs the main logging loop which queries the logging queue and then
        forwards each logging request to the logging methods themselves.
        """
        async with aiohttp.ClientSession() as session:
            self._request_session = session

            while not self._cancelled:
                self.logging_loop_started = True

                try:
                    log_item: LogItem = await self._logging_queue.get()
                    await self.do_log(log_item.method, log_item.data)
                except asyncio.CancelledError:
                    # task was canceled
                    pass
                except Exception as ex:
                    print(f"Exception while logging: {ex}")

            self._request_session     = None
            self.logging_loop_started = False


    async def log_async (self, logMethod: LogMethod, data: JSON) -> None:

        """
        Performs an async logging operation. All this does is place the logging
        request on a queue, which is fetched and processed by the main logging
        loop

        Quick note: we using log, the caller can get its current method name via
        sys._getframe().f_code.co_name, and then name of that method that call
        it via sys._getframe().f_back.f_code.co_name.

        This can obviously be extended so that this method here can query the
        method name the log was called from without needing the caller to provide
        its own name.

        """

        # We'll allow empty messages since it can help with output formatting
        # if not data or not data.get("message", ""):
        #    return

        # so we have basic logging before main loop starts
        if not self.logging_loop_started:
            try:
                message = data.get("message", "")
                if message and isinstance(message, str):
                    print(message)
            except:
                pass
        else:
            try:
                await self._logging_queue.put(LogItem(logMethod, data))
            except:
                print("The logging queue is full: unable to accept any more log entries for now")


    def log(self, logMethod: LogMethod, data: JSON) -> None:

        """
        Performs a logging operation, synchronously. Sort of. This places a
        logging request on a queue, which is fetched and processed by the main
        logging loop (a separate task). So: the logging in this method isn't a
        direct call to the logging methods, but it's not an async call so is
        compatible with non-async (and legacy) code.
        """

        # We'll allow empty messages since it can help with output formatting
        # if not data or not data.get("message", ""):
        #     return

        # so we have basic logging before main loop starts
        if not self.logging_loop_started:
            message = data.get("message", "")
            try:
                if message and isinstance(message, str):
                    print(message)
            except: pass
        else:
            # Being really paranoid. The documentation suggests the Queue is not
            # thread safe but it appears to be ok without. Just to be safe ...    
            with self._sync_log_lock:
                try:
                    self._logging_queue.put_nowait(LogItem(logMethod, data))
                except:
                    print("The logging queue is full: unable to accept any more log entries for now")


    def cancel_logging(self) -> None:
        """ Cancels the main logging loop"""
        self._cancelled = True;


    async def do_log(self, logMethod: LogMethod, data: JSON) -> None:

        """
        Outputs a log entry to one or more logging providers

        Param: logMethod - can be Info (console), Error (err output), Server 
               (sends the log to the API server), or File (write to file)
        Param: data - a JSON object (really: a dictionary) of the form:
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

        # if exception == "Exception":
        #    exception = "(General Exception)"

        if filename:
            entry += " (" + filename + ")"

        if exception:
            entry += " [" + exception + "] "

        if entry:
            entry += ": "

        if message:
            entry += message

        # HACK: if we're out of memory we may want to disable CUDA for 10 seconds 
        # until the GC has cleaned up and recovered memory. 
        # if (message == "cuDNN error: CUDNN_STATUS_NOT_INITIALIZED")
        #    ... 

        # HACK to trim superfluous logs
        unimportant = message.startswith("Cannot connect to host")

        logged_to_server = False
        loggingTasks = []

        if logMethod & LogMethod.Server or self.defaultLogging & LogMethod.Server:
            log_task = asyncio.create_task(self._server_log(entry, process, 
                                                            label, loglevel))
            loggingTasks.append(log_task)

            # Note that the error may not actually be logged to server. Check 
            # self._server_healthy as well as logged_to_server to be sure.
            logged_to_server = True 

        if process:
           entry += process + ": " + entry

        # The server already captures stdout and stderr so we only log to those
        # if no server logging was done (either not requested or server failed)
        no_server_log = not self._server_healthy or not logged_to_server

        if logMethod & LogMethod.Error or self.defaultLogging & LogMethod.Error:
            if no_server_log:
                print(entry, file=sys.stderr, flush=True)

        if logMethod & LogMethod.Info or self.defaultLogging & LogMethod.Info:
            if no_server_log:
                loglevel = loglevel.lower()
                if loglevel == "critical":
                    entry = "critical: " + entry
                elif loglevel == "error":
                    entry = "error: " + entry
                elif loglevel == "warning":
                    entry = "warning: " + entry
                elif loglevel == "debug":
                    entry = "debug: " + entry
                elif loglevel == "trace":
                    entry = "trace: " + entry

                print(entry, file=sys.stdout, flush=True)

        if not unimportant:
            if no_server_log and (logMethod & LogMethod.File or self.defaultLogging & LogMethod.File):
                loggingTasks.append( asyncio.create_task(self._file_log(process, method, 
                                                                        filename, message,
                                                                        exception)) )
   
        # Wait for all the tasks that have now on the list of logging tasks
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
            # print(f"About to post log {entry}")
            async with self._request_session.post(self.base_log_url, 
                                                  data = payload,
                                                  timeout = 1) as resp:
                self._server_healthy = resp.status == 200

            return self._server_healthy

        except TimeoutError:
            # print(f"Timeout posting log")
            self._server_healthy = False
            return False

        except Exception as ex:

            exception_type = ex.__class__.__name__
            if hasattr(ex, "os_error") and isinstance(ex.os_error, ConnectionRefusedError):
                err_msg              = f"Server connection refused. Is the server running, and can you connect to the server?"
                exception_type       = "ConnectionRefusedError"
                self._server_healthy = False
            elif exception_type == "ClientConnectorError":
                err_msg              = f"Server connection error. Is the server URL correct?"
                self._server_healthy = False
            elif exception_type == "TimeoutError":
                err_msg              = f"Timeout connecting to the server"
                self._server_healthy = False
            elif exception_type == "CancelledError":
                err_msg        = f"HTTP post to server log API was cancelled"
            else:
                err_msg = f"Error posting log [{exception_type}]: {str(ex)}"

            print(f"{err_msg}\n")

            return False


    async def _file_log(self, process: str, method: str, filename: str, message: str,
                        exception_type: str) -> bool:
        """
        Logs an error to a file. In a perfect world we'd send all logs to the default system logger
        and the server would catch these and process them. Or we'd send logs to the server directly
        so we had a single system handling logging. Except if the server fails we then have no 
        logging, so we do it ourselves here just in case.

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
            print(f"Unable to write to the file log: {str(ex)}")
            return False