
import asyncio
import json
import os
import sys
import time
import traceback
from typing import Tuple

# TODO: All I/O should be async, non-blocking so that logging doesn't impact 
# the throughput of the requests. Switching to HTTP2 or some persisting 
# connection mechanism would speed thing up as well.


# The purpose of inserting the path is so the Python import system looks in the 
# right spot for packages. ie .../pythonXX/venv/Lib/site-packages. 
# This depends on the VENV we're actually running in. So: get the location of 
# the current exe and work from that.

# Get the location of the current python interpreter, and then add the 
# site-packages associated with that interpreter to the PATH so python will find 
# the packages we've installed
current_python_dir = os.path.dirname(sys.executable)
if current_python_dir != "":
    package_path = os.path.normpath(os.path.join(current_python_dir, '../lib/python' + sys.version[:3] + '/site-packages/'))
    sys.path.insert(0, package_path)
    # print("Adding " + package_path + " to packages search path")

# We can now import these from the appropriate VENV location
import aiohttp

# CodeProject.AI SDK. Import after we've set the import path since logging 
# requires requests
from common import JSON
from analysis.analysislogging import LogMethod, AnalysisLogger
from analysis.requestdata import AIRequestData


class CodeProjectAIRunner:
    """
    A thin abstraction + helper methods to allow python modules to communicate 
    with the backend of main API Server
    """

    def __init__(self, queue_name, process_callback, init_callback = None):

        """ 
        Constructor. 
         - queue_name: str.        The name of the queue this module will service
         - process_callback: func. The function that will be called each time a
                                   command is retrieved from the loop
         - init_callback: func     The function called when the module is first
                                   started.

         TODO: throw and or report error if process_callback or init_callback 
               are not None and not a function, or if process_callback is None.
        """

        # Constants
        self._error_pause_secs      = 1.0   # For general errors
        self._conn_error_pause_secs = 5.0   # for connection / timeout errors
        self._log_timing_events     = True
        self._verbose_exceptions    = True

        # Private fields
        self._execution_provider = "" # backing field for self.execution_provider

        self._cancelled          = False
        self._current_error_pause_secs = 0

        # Public fields
        self.module_id           = os.getenv("CPAI_MODULE_ID",        "CodeProject.AI")
        self.module_name         = os.getenv("CPAI_MODULE_NAME",      queue_name)
        self.queue_name          = os.getenv("CPAI_MODULE_QUEUENAME", queue_name)

        self.init_callback       = init_callback
        self.process_callback    = process_callback

        self.python_dir          = current_python_dir
        self.port                = os.getenv("CPAI_PORT",               "32168")
        self.server_root_path    = os.getenv("CPAI_APPROOTPATH",        os.path.normpath(os.path.join(os.path.dirname(__file__), "../../../..")))
        self.parallelism         = os.getenv("CPAI_MODULE_PARALLELISM", "0");
        self.support_GPU         = os.getenv("CPAI_MODULE_SUPPORT_GPU", "True")

        self.use_openvino        = False
        self.use_onnxruntime     = False

        self.hardware_type       = "CPU"

        # We're hardcoding localhost because we have no plans to have the 
        # analysis services and the server on separate machines or containers.
        # At no point should any outside app have access to the backend 
        # services. It all must be done through the API.
        self.base_api_url        = f"http://localhost:{self.port}/v1/"
        
        # Need to hold off until we're ready to create the main logging loop.
        # self._logger           = AnalysisLogger(self.port, self.server_root_path)

        # Normalise input
        self.port                = int(self.port) if self.port.isnumeric() else 32168

        self.parallelism         = int(self.parallelism) if isinstance(self.parallelism, int) else 0
        if self.parallelism <= 0:
            self.parallelism = os.cpu_count()//2

        # Setup GPU libraries
        if self.support_GPU:
            if self.use_openvino:
                import openvino.utils as utils
                utils.add_openvino_libs_to_path()

            if self.use_onnxruntime:
                import onnxruntime as ort

                ## get the first Execution Provider Name to determine GPU/CPU type
                providers = ort.get_available_providers()
                if len(providers) > 0 :
                    self.execution_provider = str(providers[0]).removesuffix("ExecutionProvider")
                    self.hardware_type      = "GPU"

        # Get some (very!) basic CPU info
        try:
            import cpuinfo
            self.manufacturer = cpuinfo.get_cpu_info().get('brand_raw')
            self.cpu_arch     = cpuinfo.get_cpu_info().get('arch_string_raw')
        except:
            self.manufacturer = ""
            self.cpu_arch     = ""
            
        if self.manufacturer:
            if self.manufacturer.startswith("Apple M"):
                self.cpu_brand          = 'Apple'
                self.cpu_arch  = 'arm64'
            elif self.manufacturer.find("Intel(R)") != -1:
                self.cpu_brand          = 'Intel'

        # Private fields
        self._base_queue_url = self.base_api_url + "queue/"
    

    @property
    def execution_provider(self):
        """ Gets the execution provider """
        return self._execution_provider
      
    @execution_provider.setter
    def execution_provider(self, provider):
        """ 
        Sets the execution provider, and in doing so will also ensure the
        hardware ID makes sense. (Yes, TMI on the hardware ID, but important.
        hardware_id is still a work in progress).
        """

        if not provider:
            self._execution_provider = "CPU"
        else:
            self._execution_provider = provider

        if self._execution_provider != "CPU":
            self.hardware_type = "GPU"


    def start_loop(self):

        """
        Starts the tasks that will run the execution loops that check the 
        command queue and forwards commands to the module. Each task runs 
        asynchronously, with each task's loop independently querying the 
        command queue and sending commands to the (same) callback function.
        """

        try:
            asyncio.run(self.main_init())
        except:
            pass    # try/except because forced shutdown can cause messiness

        # NOTE: If using threads, then do it this way. However, using async tasks
        # is faster and more efficient
        #
        # from threading import Thread
        #
        # self._logger = AnalysisLogger(self.port, self.server_root_path)
        # 
        # nThreads = os.cpu_count() -1
        # theThreads = []
        # for i in range(nThreads):
        #     t = Thread(target=self.main_loop)
        #     theThreads.append(t)
        #     t.start()
        # 
        # for x in theThreads:
        #     x.join()


    async def main_init(self):

        """
        Initialises the set of tasks for this module. Each task will contain a
        a loop that will queury the command queue and forward commands to the
        callback function.

        This method also sets up the shared logging task.
        """
        async with aiohttp.ClientSession() as session:
            self._request_session = session
            self._logger          = AnalysisLogger(self.port, self.server_root_path)

            # Start with just running one logging loop
            logging_task = asyncio.create_task(self._logger.logging_loop())

            # Call the init callback if available
            if self.init_callback:
                loop = asyncio.get_running_loop()
                if asyncio.iscoroutinefunction(self.init_callback):
                    init_task = asyncio.create_task(self.init_callback(self))
                else :
                    init_task = loop.run_in_executor(None, self.init_callback, self)
                await init_task

            sys.stdout.flush()

            # Add main processing loop tasks
            tasks = [ asyncio.create_task(self.main_loop(task_id)) for task_id in range(self.parallelism) ]

            sys.stdout.flush()

            # combine
            tasks.append(logging_task)

            await self.log_async(LogMethod.Info | LogMethod.Server, {
                        "message": self.module_name + " started.",
                        "loglevel": "trace"
                    })

            await asyncio.gather(*tasks)
            self._request_session = None


    # Main loop
    async def main_loop(self, task_id) -> None:

        get_command_task = asyncio.create_task(self.get_command(task_id))
        send_response_task = None

        while not self._cancelled:
            queue_entries: list = await get_command_task

            # Schedule the next get_command request
            get_command_task = asyncio.create_task(self.get_command(task_id))

            if len(queue_entries) == 0:
                continue

            # In theory we may get back multiple command requests. In practice
            # it's always just 1 at a time. At the moment.
            for queue_entry in queue_entries:
                data: AIRequestData = AIRequestData(queue_entry)

                # Special shutdown request
                if data.command and data.command.lower() == "quit" and \
                   self.module_id == data.get_value("moduleId"):

                    await self.log_async(LogMethod.Info | LogMethod.File | LogMethod.Server, { 
                        "process":  self.module_name,
                        "filename": __file__,
                        "method":   "main_loop",
                        "loglevel": "info",
                        "message":  "Shutting down"
                    })
                    self._cancelled = True
                    break

                process_name = f"Queue and Processing {self.module_name}"
                if data.command:
                    process_name += f" command '{data.command}' (#reqid {data.request_id})"

                timer: Tuple[str, float] = self.start_timer(process_name)

                output: JSON = {}
                try:

                    loop = asyncio.get_running_loop()
                    if asyncio.iscoroutinefunction(self.process_callback):
                        callbacktask = asyncio.create_task(self.process_callback(self, data))
                    else:
                        callbacktask = loop.run_in_executor(None, self.process_callback, self, data)

                    # start_time = time.perf_counter()

                    output = await callbacktask

                    # if output:
                    #    output["taskElapsedMs"] = int((time.perf_counter() - start_time) * 1000)

                except asyncio.CancelledError:
                    print(f"The future has been cancelled. Ignoring command {data.command} (#reqid {data.request_id})")

                except Exception as ex:
                    output = {
                        "success": False,
                        "error":   f"unable to process the request (#reqid {data.request_id})",
                        "code":    500
                    }

                    message = "".join(traceback.TracebackException.from_exception(ex).format())
                    await self.log_async(LogMethod.Error | LogMethod.Server, { 
                        "process":        self.module_name,
                        "filename":       __file__,
                        "method":         sys._getframe().f_code.co_name,
                        "loglevel":       "error",
                        "message":        message,
                        "exception_type": ex.__class__.__name__
                    })

                finally:
                    self.end_timer(timer, "command timing")

                    try:
                        if send_response_task != None:
                            await send_response_task

                        send_response_task = asyncio.create_task(self.send_response(data.request_id, output))
                        
                    except Exception:
                        print(f"An exception occurred sending the inference response (#reqid {data.request_id})")
            
                # reset for next command that we retrieved
                start_time = time.perf_counter()

        # method is ending. Let's clean up. self._cancelled == True at this point.
        self._logger.cancel_logging()


    # Performance timer =======================================================

    def start_timer(self, desc: str) -> Tuple[str, float]:
        """
        Starts a timer and initializes the description string that will be 
        associated with the time
        Param: desc - the description
        Returns a tuple containing the description and the timer itself
        """
        return (desc, time.perf_counter())


    def end_timer(self, timer : Tuple[str, float], label: str = "timing") -> None:
        """
        Ends a timing session and logs the time taken along with the initial description if the
        variable logTimingEvents = True
        Param: timer - A tuple containing the initial description and the start time
        """
        (desc, start_time) = timer
        elapsedMs = (time.perf_counter() - start_time) * 1000
    
        if (self._log_timing_events):
            self.log(LogMethod.Info|LogMethod.Server, {
                        "message": f"{desc} took {elapsedMs:.0f}ms",
                        "loglevel": "information",
                        "label": label
                     })


    # Service Commands and Responses ==========================================

    async def log_async(self, log_method: LogMethod, data: JSON) -> None:
        if not data:
            return

        if not data.get("process"):
            data["process"] = self.module_name 
                            
        await self._logger.log_async(log_method, data)

    def log(self, log_method: LogMethod, data: JSON) -> None:
        if not data:
            return

        if not data.get("process"):
            data["process"] = self.module_name 
                            
        self._logger.log(log_method, data)

        
    async def get_command(self, task_id) -> "list[str]":

        """
        Gets a command from the queue associated with this object. 
        CodeProject.AI works on the  basis of having a client pass requests to 
        the frontend server, which in turns places each request into various 
        command queues. The backend analysis services continually pull requests
        from the queue that they can service. Each request for a queued command
        is done via a long poll HTTP request.

        Returns the Json package containing the raw request from the client 
        that was sent to the server

        Remarks: The API server will currently only return a single command, 
        not a list, so we could just as easily return a string instead of a 
        list of strings. We return a list to maintain compatibility with the 
        old legacy modules we started with, but also to future-proof the code 
        in case we want to allow batch processing. Be aware that batch 
        processing will mean less opportunity to load balance the requests.
        """
        commands = []

        try:
            url = self._base_queue_url + self.queue_name + "?moduleId=" + self.module_id
            if self.execution_provider:
                url += "&executionProvider=" + self.execution_provider

            # Send the request to query the queue and wait up to 30 seconds
            # for a response. We're basically long-polling here
            async with self._request_session.get(
                url,
                timeout = 30
                #, verify  = False
            ) as session_response:

                if session_response.ok:
                    content = await session_response.text()
                    if (content):

                        # This method allows multiple commands to be returned,
                        # but to keep things simple we're only ever returning a 
                        # single command at a time (but still: ensure it's as an
                        # array)
                        commands = [content]

                        # The request worked: clear the error pause time, record
                        # last successful call
                        self._current_error_pause_secs = 0

                        await self.log_async(LogMethod.Info|LogMethod.Server, {
                            "message": f"Retrieved {self.queue_name} command",
                            "loglevel": "debug"
                        })
                else:
                    # await self.log_async(LogMethod.Error | LogMethod.Server, {
                    #     "message": f"Error retrieving command from queue {self.queue_name}",
                    #     "method": "get_command",
                    #     "loglevel": "error",
                    #     "process": self.queue_name,
                    #     "filename": __file__,
                    #     "exception_type": "TimeoutError"
                    # })

                    # We'll only calculate the error pause time in task #0, but 
                    # all tasks will pause on error for the same amount of time
                    if task_id == 0:
                        self._current_error_pause_secs = self._current_error_pause_secs * 2 \
                                                         if self._current_error_pause_secs \
                                                         else self._error_pause_secs

        except TimeoutError:
            if not self._cancelled:
                await self.log_async(LogMethod.Error | LogMethod.Server, {
                    "message": f"Timeout retrieving command from queue {self.queue_name}",
                    "method": sys._getframe().f_code.co_name,
                    "loglevel": "error",
                    "process": self.queue_name,
                    "filename": __file__,
                    "exception_type": ex.__class__.__name__
                })

            # We'll only calculate the error pause time in task #0, but all 
            # tasks will pause on error for the same amount of time
            if task_id == 0:
                self._current_error_pause_secs = self._current_error_pause_secs * 2 \
                                                 if self._current_error_pause_secs \
                                                 else self._error_pause_secs

        except ConnectionRefusedError:
            if not self._cancelled:
                await self.log_async(LogMethod.Error, {
                    "message": f"Connection refused trying to check the command queue {self.queue_name}.",
                    "method": sys._getframe().f_code.co_name,
                    "loglevel": "error",
                    "process": self.queue_name,
                    "filename": __file__,
                    "exception_type": ex.__class__.__name__
                })

            # We'll only calculate the error pause time in task #0, but all 
            # tasks will pause on error for the same amount of time
            if task_id == 0:
                self._current_error_pause_secs = self._current_error_pause_secs * 2 \
                                                 if self._current_error_pause_secs \
                                                 else self._error_pause_secs

        #  except aiohttp.client_exceptions.ClientResponseError:
        #     ...handle this directly

        except Exception as ex:

            pause_on_error = False
            err_msg        = None
            exception_type = ex.__class__.__name__

            if hasattr(ex, "os_error") and isinstance(ex.os_error, ConnectionRefusedError):
                err_msg = f"Unable to check the command queue {self.queue_name}. Is the server running, and can you connect to the server?"
                exception_type = "ConnectionRefusedError"
                pause_on_error = True

            elif ex.__class__.__name__ == "ServerDisconnectedError":
                # This happens when the server shuts down but the module doesn't, then on restart we
                # have multiple modules and it all goes weird
                err_msg = f"Unable to check the command queue {self.queue_name}. The server was disconnected."
                exception_type = "ServerDisconnectionError"
                pause_on_error = True

            elif ex.__class__.__name__ == "ClientConnectorError":
                err_msg = f"Unable to check the command queue {self.queue_name}. Is the server URL correct?"
                exception_type = "ClientConnectorError"
                pause_on_error = True

            elif ex.__class__.__name__ == "TimeoutError":
                err_msg        = f"Timeout retrieving command from queue {self.queue_name}"
                exception_type = "TimeoutError"
                pause_on_error = True

            else:
                err_msg = str(ex)

                if not self._cancelled and err_msg and err_msg != 'Session is closed':
                    pause_on_error = True
                    err_msg = "Error retrieving command [" + exception_type + "]: " + err_msg
                    await self.log_async(LogMethod.Error|LogMethod.Server, {
                        "message": err_msg,
                        "method": sys._getframe().f_code.co_name,
                        "loglevel": "error",
                        "process": self.queue_name,
                        "filename": __file__,
                        "exception_type": ex.__class__.__name__
                    })

            # We'll only calculate the error pause time in task #0, but all 
            # tasks will pause on error for the same amount of time
            if pause_on_error and task_id == 0:
                self._current_error_pause_secs = self._current_error_pause_secs * 2 \
                                                 if self._current_error_pause_secs \
                                                 else self._error_pause_secs
                print(f"Pausing on errors for {sleep_time} secs.")

        finally:
            # We'll only calculate the error pause time in task #0, but all 
            # tasks will pause on error. However, if the error pause has hit a
            # threshold then we should quit
            if not self._cancelled and self._current_error_pause_secs:
                sleep_time = min(self._current_error_pause_secs, 60)
                await asyncio.sleep(sleep_time) # Don't pause for more than a minute

            return commands


    async def send_response(self, request_id : str, body : JSON) -> bool:
        """
        Sends the result of a comment to the analysis services back to the API
        server who will then pass this result back to the original calling 
        client. CodeProject.AI works on the basis of having a client pass 
        requests to the frontend server, which in turns places each request 
        into various command queues. The backend analysis services continually 
        pull requests from the queue that they can service, process each 
        request, and then send the results back to the server.

        Param: request_id - the ID of the request that was originally pulled 
                            from the command queue.
        Param: body:      - the Json result (as a string) from the analysis of 
                            the request.

        Returns:          - True on success; False otherwise
        """

        success = False
        # responseTimer = self.start_timer(f"Sending response for request from {self.queue_name}")

        try:
            url = self._base_queue_url + request_id + "?moduleId=" + self.module_id
            if self.execution_provider is not None:
                url += "&executionProvider=" + self.execution_provider
            
            async with self._request_session.post(
                url,
                data    = json.dumps(body),
                timeout = 10
                #, verify  = False
                ):
                success = True

        except Exception as ex:
            await asyncio.sleep(self._error_pause_secs)

            if self._verbose_exceptions:
                print(f"Error sending response: {str(ex)}")
            else:
                print(f"Error sending response: Is the API Server running? [" + ex.__class__.__name__ + "]")

        finally:
            #if success:
            #    self.end_timer(responseTimer)
            return success


    async def call_api(self, method:str, files=None, data=None) -> str:

            url = self.base_api_url + method

            formdata = aiohttp.FormData()
            if data:
                for key, value in data.items():
                    formdata.add_field(key, str(value))

            if files:
                for key, file_info in files.items():
                    formdata.add_field(key, file_info[1], filename=file_info[0], content_type=file_info[2])

            async with self._request_session.post(url, data = formdata) as session_response:
                return await session_response.json()

