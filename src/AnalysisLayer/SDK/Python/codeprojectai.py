
import json
import os
from pickle import NONE
import sys
import time
import traceback
import asyncio

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
import cpuinfo

# CodeProject.AI SDK. Import after we've set the import path since logging 
# requires requests
from common import JSON
from analysislogging import LogMethod, AnalysisLogger
from requestdata import AIRequestData


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
        self._error_pause_secs   = 1.0
        self._log_timing_events  = True
        self._verbose_exceptions = True

        # Private fields
        self.hardware_type       = "CPU"
        self._execution_provider = "" # backing field for self.execution_provider

        self._cancelled          = False

        # Public fields
        self.module_id           = os.getenv("CPAI_MODULE_ID",        "CodeProject.AI")
        self.module_name         = os.getenv("CPAI_MODULE_NAME",      queue_name)
        self.queue_name          = os.getenv("CPAI_MODULE_QUEUENAME", queue_name)

        self.init_callback       = init_callback
        self.process_callback    = process_callback

        self.python_dir          = current_python_dir
        self.errLog_APIkey       = os.getenv("CPAI_ERRLOG_APIKEY",      "")
        self.port                = os.getenv("CPAI_PORT",               "32168")
        self.server_root_path    = os.getenv("CPAI_APPROOTPATH",        os.path.normpath(os.path.join(os.path.dirname(__file__), "../../../..")))
        self.parallelism         = os.getenv("CPAI_MODULE_PARALLELISM", "0");
        self.support_GPU         = os.getenv("CPAI_MODULE_SUPPORT_GPU", "True")

        self.use_openvino        = False
        self.use_onnxruntime     = False
        
        # Need to hold off until we're ready to create the main logging loop.
        # self._logger           = AnalysisLogger(self.port, self.server_root_path, self.errLog_APIkey)

        # Normalise input
        self.port = int(self.port) if self.port.isnumeric() else 32168

        self.parallelism = int(self.parallelism) if isinstance(self.parallelism, int) else 0
        if self.parallelism <= 0:
            self.parallelism = os.cpu_count() - 1

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
        self.manufacturer = cpuinfo.get_cpu_info().get('brand_raw')
        self.cpu_arch     = cpuinfo.get_cpu_info().get('arch_string_raw')

        if self.manufacturer.startswith("Apple M"):
            self.cpu_brand          = 'Apple'
            self.cpu_arch           = 'arm'
        elif self.manufacturer.startswith("Intel(R)"):
            self.cpu_brand          = 'Intel'

        # Private fields
        # We're hardcoding localhost because we have no plans to have the 
        # analysis services and the server on separate machines or containers.
        # At no point should any outside app have access to the backend 
        # services. It all must be done through the API.
        self._base_queue_url = f"http://localhost:{self.port}/v1/queue/"
    

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
        # self._logger = AnalysisLogger(self.port, self.server_root_path, self.errLog_APIkey)
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

        self._request_session = aiohttp.ClientSession()
        self._logger          = AnalysisLogger(self.port, self.server_root_path, self.errLog_APIkey)

        # Start with just running one logging loop
        logging_task = asyncio.create_task(self._logger.logging_loop())

        # Call the init callback if available
        if self.init_callback:
            loop = asyncio.get_running_loop()
            init_task =  loop.run_in_executor(None, self.init_callback, self)
            await init_task

        # Add main processing loop tasks
        tasks = [ asyncio.create_task(self.main_loop()) for i in range(self.parallelism) ]

        # combine
        tasks.append(logging_task)

        await self.log_async(LogMethod.Info | LogMethod.Server, {
                    "message": self.module_name + " started.",
                    "loglevel": "information"
                })

        [await task for task in tasks]

        await self._request_session.close()


    # Main loop
    async def main_loop(self) -> None:

        # Commenting so we don't get one message per thread
        #print(f"{self.module_name}#{id}: Getting first request")
        get_command_task = asyncio.create_task(self.get_command())
        send_response_task = None

        while not self._cancelled:
            queue_entries: list = await get_command_task

            #start getting the next request
            #print(f"{self.module_name}#{id}: Getting next request")
            get_command_task = asyncio.create_task(self.get_command())

            # In theory we may get back multiple command requests. In practice
            # it's always just 1 at a time. At the moment.
            if len(queue_entries) > 0:

                for queue_entry in queue_entries:
                    data: AIRequestData = AIRequestData(queue_entry)

                    # Special shutdown request
                    if data.command is not None and data.command.lower() == "quit":
                        await self.log_async(LogMethod.Info | LogMethod.File | LogMethod.Server, { 
                            "process":        self.module_name,
                            "filename":       "codeprojectai.py",
                            "method":         "main_loop",
                            "loglevel":       "info",
                            "message":        "Shutting down"
                        })
                        self._cancelled = True
                        break

                    process_name = f"Module '{self.module_name}'"
                    if data.command:
                        process_name += f" (command: {data.command}"
                        #if data.urlSegments and len(data.urlSegments) > 0 and data.urlSegments[0]:
                        #    process_name += f"/{data.urlSegments[0]}"
                        process_name += ")"

                    timer: tuple = self.start_timer(process_name)

                    output: JSON = {}
                    try:
                        loop = asyncio.get_running_loop()
                        callbacktask = loop.run_in_executor(None, self.process_callback, self, data)
                        output = await callbacktask

                    except asyncio.CancelledError:
                        print(f"The future has been cancelled. Ignoring command {data.command}")
                        pass

                    except Exception as ex:
                        output = {
                           "success": False,
                           "error":   "unable to process the request",
                           "code":    500
                        }

                        err_trace = traceback.format_exc()

                        await self.log_async(LogMethod.Error | LogMethod.Cloud | LogMethod.Server, { 
                            "process":        self.module_name,
                            "filename":       "codeprojectai.py",
                            "method":         "main_loop",
                            "loglevel":       "error",
                            "message":        str(ex), # err_trace,
                            "exception_type": "Exception"
                        })

                    finally:
                        self.end_timer(timer, "command timing")

                        try:
                            if send_response_task != None:
                                await send_response_task

                            send_response_task = asyncio.create_task(self.send_response(data.request_id, output))
                        except Exception:
                            print("An exception occured sending the inference response")

            #else:
            #    print(f"{self.module_name}#{id}: No request available, will try again")

            # This is (currently) superfluous but it's here as a reminder that if you add code after
            # here then be careful: the module may be shutting down.
            if self._cancelled:
                break;
            # Potential further stuff...

        # method is ending. Let's clean up
        self._logger.cancel_logging()


    # Performance timer =======================================================

    def start_timer(self, desc: str) -> tuple:
        """
        Starts a timer and initializes the description string that will be 
        associated with the time
        Param: desc - the description
        Returns a tuple containing the description and the timer itself
        """
        return (desc, time.perf_counter())


    def end_timer(self, timer : tuple, label: str = "timing") -> None:
        """
        Ends a timing session and logs the time taken along with the initial description if the
        variable logTimingEvents = True
        Param: timer - A tuple containing the initial description and the timer object
        """
        (desc, startTime) = timer
        elapsedMs = (time.perf_counter() - startTime) * 1000
    
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


    async def get_command(self) -> "list[str]":

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

        success = False
        try:
            # Turning off timing since it doesn't make a lot of sense given
            # we're long-polling
            # cmdTimer = self.start_timer(f"Idle time on queue {self.queue_name}")

            url = self._base_queue_url + self.queue_name + "?moduleId=" + self.module_id
            if self.execution_provider:
                url += "&executionProvider=" + self.execution_provider

            # Send the request to query the queue and wait up to 30 seconds
            # for a response. We're basically long-polling here
            response = await self._request_session.get(
                url,
                timeout = 30
                #, verify  = False
            )

            if response.ok:
                content = await response.text()
                if (content):
                    success = True
                    await self.log_async(LogMethod.Info|LogMethod.Server, {
                        "message": f"Retrieved {self.queue_name} command",
                        "loglevel": "debug"
                    })

                    return [content]
                else:
                    return []
            else:
                return []

        except TimeoutError:
            await self.log_async(LogMethod.Error|LogMethod.Server|LogMethod.Cloud, {
                "message": f"Timeout retrieving command from queue {self.queue_name}",
                "method": "get_command",
                "loglevel": "error",
                "process": self.queue_name,
                "filename": "codeprojectai.py",
                "exception_type": "TimeoutError"
            })

        except Exception as ex:
            err_msg = "Error retrieving command: Is the API Server running?"
            if self._verbose_exceptions:
                err_msg = str(ex)

            await self.log_async(LogMethod.Error|LogMethod.Server|LogMethod.Cloud, {
                "message": err_msg,
                "method": "get_command",
                "loglevel": "error",
                "process": self.queue_name,
                "filename": "codeprojectai.py",
                "exception_type": "Exception"
            })
            await asyncio.sleep(self._error_pause_secs)
            return []

        finally:
            if success:
                # self.end_timer(cmdTimer)
                pass


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

        success       = False
        responseTimer = self.start_timer(f"Sending response for request from {self.queue_name}")

        try:
            url = self._base_queue_url + request_id + "?moduleId=" + self.module_id
            if self.execution_provider is not None:
                url += "&executionProvider=" + self.execution_provider
            
            await self._request_session.post(
                url,
                data    = json.dumps(body),
                timeout = 10
                #, verify  = False
                )

            success = True

        except Exception as ex:
            await asyncio.sleep(self._error_pause_secs)

            if self._verbose_exceptions:
                print(f"Error sending response: {str(ex)}")
            else:
                print(f"Error sending response: Is the API Server running?")

        finally:
            if success:
                self.end_timer(responseTimer)
            return success
