# Import standard libs
import asyncio
import json
import os
import platform
import sys
import time
import traceback
from typing import Tuple

# These are annoying and often unavoidable
import warnings
warnings.simplefilter("ignore", DeprecationWarning)

# Ensure the Python import system looks in the right spot for packages.
# ie .../pythonXX/venv/Lib/site-packages. This depends on the VENV we're 
# actually running in. So: get the location of the current executable and 
# work from that.
current_python_dir = os.path.dirname(sys.executable)
package_path=None
if current_python_dir:
    if platform.system() == "Windows":
        package_path = os.path.normpath(os.path.join(current_python_dir, "..\\lib\\site-packages\\"))
    else:
        package_path = '../lib/python' + sys.version[:3] + '/site-packages/'
        package_path = os.path.normpath(os.path.join(current_python_dir, package_path))
    sys.path.insert(0, package_path)

# We can now import installed packages from the appropriate location
import aiohttp

# Import the CodeProject.AI SDK as the last step
from common import JSON
from system_info    import SystemInfo
from module_logging import LogMethod, ModuleLogger, LogVerbosity
from request_data   import RequestData
from module_options import ModuleOptions
# from utils.environment_check import check_requirements


# Our base class that module adapters should derive from.

class ModuleRunner:
    """
    A thin abstraction + helper methods to allow python modules to communicate 
    with the backend of main API Server
    """

    def initialise(self) -> None:
        """ Overridable:
        Called when this module first starts up. To be overridden by child classes 
        """
        pass

    def initialize(self) -> None:
        """ Overridable:
        Called when this module first starts up. To be overridden by child classes.
        This method provided for those who like month-day-year.
        """
        pass

    def process(self, data: RequestData) -> JSON:
        """ Overridable:
        Called each time a request is retrieved from this module's queue is to
        be processed. To be overridden by child classes

        self - This ModuleRunner
        data - The RequestData retrieved from the processing queue. It contains
               all the info needed for this module to satisfy the request
        returns: A JSON package containing the results of this request.

        Notes: to indicate a long running process, return the function that will
               be run as the long process rather than a JSON package

        TODO: alternatively, return JSON, and have 'long_process' as a field
        that has a ref to the method to run. This gets removed from the JSON and
        then the JSON gets sent back to the client
        """
        pass

    def status(self) -> JSON:
        """
        Deprecated: this is here purely for backwards compatibility. Added v2.5.5
        """
        return self.module_status(self)
    
    def module_status(self) -> JSON:
        """ Overridable:
        Called when this module has been asked to provide its current status.
        Typically returns statistics and device capabilities
        """
        return {}
    
    def command_status(self) -> JSON:
         """ Overridable:
         Called when this module has been asked to provide the current status of
         a long running command. Can return None if there's nothing to report, or
         can return a progress %, or intermediate results (eg current training
         state), or accumulated results (eg result of long running chat)
         """
         return {}
    
    def cancel_command_task(self) -> None:
         """ Overridable:
         Called when this module is about to be cancelled
         """
         pass

    def update_statistics(self, response) -> None:
        """ Overridable:
        Called after `process` is called in order to update the stats on the 
        number of successful and failed calls as well as average inference time.
        """
        if "success" in response and response["success"] == True:
            self._successful_inferences += 1
            if "inferenceMs" in response:
                self._total_inference_time_ms += int(response["inferenceMs"])
        else:
            self._failed_inferences += 1

    def selftest(self) -> JSON:
        """ Overridable:
        Called to run general tests against this module to ensure it's in good
        working order. Typically this should run unit and/or integration tests
        and report back the results. Used for post-install checks.
        """
        return { "success": True }

    def cleanup(self) -> None:
        """ Overridable:
        Called when this module has been asked to shutdown. To be overridden by
        child classes to provide the means to cleanup resource use
        """
        pass
    

    def __init__(self) -> None:
        """ 
        Constructor. 
        """

        self.system_info = SystemInfo()

        # Constants ------------------------------------------------------------

        # We have a reasonably short "long poll" time to ensure the dashboard gets regular pings
        # to indicate the module is still alive
        self._wait_for_command_secs    = 15.0  # Time to wait for a command before cancelling and retrying
        self._response_timeout_secs    = 10    # For sending info to the server
        self._status_sleep_time        = 2.0  # Time to wait between status updates

        self._error_pause_secs         = 1.0   # For general errors
        self._conn_error_pause_secs    = 5.0   # for connection / timeout errors
        self._log_timing_events        = True

        # Some commands are just annoying in the logs
        self._ignore_timing_commands   = [ 
            "list-custom",
            "get_module_status", "status", "get_status", # status is deprecated alias
            "get_command_status"
        ]

        # Private fields -------------------------------------------------------

        self._cancelled                = False
        self._current_error_pause_secs = 0
        self._performing_self_test     = False

        self._successful_inferences    = 0
        self._total_inference_time_ms  = 0
        self._failed_inferences        = 0

        # Public fields --------------------------------------------------------

        # A note about the use of ModuleOptions. ModuleOptions is simply a way 
        # to hide all the calls to _get_env_var behind a single class. While
        # there is a lot of repetition in self.property = ModuleOptions.property,
        # it means we have the means of keeping the initial values the module
        # had at launch separate from the working values which may change during
        # runtime. ie we prefer to keep a record of the initial settings in the
        # ModuleOptions class, and have the transferred values in this class be
        # mutable. 

        # Module Descriptors
        self.module_id           = ModuleOptions.module_id             # ID of the module
        self.module_name         = ModuleOptions.module_name           # Name of the module

        # Server API location and Queue
        self.base_api_url        = ModuleOptions.base_api_url          # Base URL for making calls to the server
        self.port                = ModuleOptions.port                  # Port on which the server is listening
        self.queue_name          = ModuleOptions.queue_name            # Name of request queue for this module

        # General Module and Server settings
        self.server_root_path    = ModuleOptions.server_root_path      # Absolute folder path to root of this application
        self.module_path         = ModuleOptions.module_path           # Absolute folder path to this module
        self.python_dir          = current_python_dir                  # Absolute folder path to Python venv if applicable
        self.python_pkgs_dir     = package_path                        # Absolute folder path to Python venv packages folder if app.
        self.log_verbosity       = ModuleOptions.log_verbosity         # Logging level: Quiet, Info or Loud
        self.launched_by_server  = ModuleOptions.launched_by_server    # Was this module launched by the server (or launched separately?)
        self.selftest_check_pkgs = True

        # Hardware / accelerator info
        self.required_MB         = int(ModuleOptions.required_MB or 0) # Min RAM needed to launch this module
        self.accel_device_name   = ModuleOptions.accel_device_name     # eg CUDA:0, usb:0. Module/library specific
        self.parallelism         = ModuleOptions.parallelism           # Number of parallel instances launched at runtime
        self.enable_GPU          = ModuleOptions.enable_GPU            # Whether to use GPU support if available

        self.inference_device    = "CPU"                               # The processor type reported as being used (CPU, GPU, TPU etc)
        self.inference_library   = ""                                  # The inference library in use (CUDA, Tensorflow, DirectML, Paddle)
        self.can_use_GPU         = False                               # Whether this module can support the current hardware

        self.half_precision      = ModuleOptions.half_precision        # Whether to use half precision in CUDA (module specific)
        if not self.half_precision:
            self.half_precision = "enable" if self.system_info.hasTorchHalfPrecision else "disable"

        # General purpose flags. These aren't currently supported as common flags
        # self.use_CUDA          = ModuleOptions.use_CUDA
        # self.use_ROCm          = ModuleOptions.use_ROCm
        # self.use_Coral         = ModuleOptions.use_Coral
        # self.use_ONNXRuntime   = ModuleOptions.use_ONNXRuntime
        # self.use_OpenVINO      = ModuleOptions.use_OpenVINO
        # self.use_MPS           = ModuleOptions.use_MPS

        # Logger needs to be setup as part of the asyncio loop later on
        # self._logger           = ModuleLogger(self.port, self.server_root_path)
        self._logger             = None

        # Private fields
        self._base_queue_url     = self.base_api_url + "queue/"
        
        # if cancel_command_task is called and force_shutdown is true, then the
        # long running process will be killed. Setting false (either here or in
        # a cancel_command_task override) allows the long process to stop gracefully
        self.force_shutdown = True

        self.long_running_command_task = None
        self.long_running_command_id   = None
        self.last_long_running_output  = None

        # General setup

        # Do this now in case we forget to do it later
        if self.enable_GPU and self.system_info.hasTorchMPS:
            os.environ["PYTORCH_ENABLE_MPS_FALLBACK"] = "1"


    def start_loop(self) -> None:
        """
        Starts the tasks that will run the execution loops that check the 
        command queue and forwards commands to the module. Each task runs 
        asynchronously, with each task's loop independently querying the 
        command queue and sending commands to the (same) callback function.

        TODO: All I/O should be async, non-blocking so that logging doesn't 
        impact the throughput of the requests. Switching to HTTP2 or some 
        persisting connection mechanism would speed things up as well.
        """

        # SELF TEST: 
        # If this module has been called from the command line and a self test
        # has been requested, then we'll run that test and exit immediately,
        # rather than firing up the loop to handle messages. 
        # We could call this from the __init__ method to be cleaner, but child
        # modules would then need to ensure they called super().__init__ at the
        # *end* of their __init__ call, rather than at the start, and that's
        # just fragile.

        if len(sys.argv) > 1 and sys.argv[1] == "--selftest":
            
            if self.log_verbosity == LogVerbosity.Loud:
                print(f"{self.module_id} self-test called")

            self._logger = ModuleLogger(self.port, self.server_root_path)
            self._performing_self_test = True
            
            # We allow 'initialize' and 'initialise'. Find which was overridden
            if self.initialize.__qualname__ == "ModuleRunner.initialize":
                self.initialise()
            else:
                self.initialize()

            if self.selftest_check_pkgs:
                self.check_packages()
            result = self.selftest()
            self.cleanup()

            self._performing_self_test = False
            if result and "success" in result and result["success"]:
                if self.log_verbosity == LogVerbosity.Loud:
                    print(f"{self.module_id} self-test succeeded")
                quit(0)
            else:
                if self.log_verbosity == LogVerbosity.Loud:
                    print(f"{self.module_id} self-test failed")
                quit(1)

        # No self test, so on to the main show

        try:
            # asyncio.run was only added in Python 3.7
            if (sys.version_info.major == 3 and sys.version_info.minor < 7):
                # loop = asyncio.get_event_loop()
                # loop.run_until_complete(self.main_init())
                self.log(LogMethod.Error | LogMethod.Server, { 
                    "process":        self.module_name,
                    "filename":       __file__,
                    "method":         sys._getframe().f_code.co_name,
                    "loglevel":       "error",
                    "message":        "Python < 3.7 isn't supported"
                })
            else:
                asyncio.run(self.main_init())

        except Exception as ex:
            message = "".join(traceback.TracebackException.from_exception(ex).format())
            self.log(LogMethod.Error | LogMethod.Server, { 
                "process":        self.module_name,
                "filename":       __file__,
                "method":         sys._getframe().f_code.co_name,
                "loglevel":       "error",
                "message":        "Error running main_init: " + message,
                "exception_type": ex.__class__.__name__
            })

        # NOTE: If using threads, then do it this way. However, using async tasks
        # is faster and more efficient
        #
        # from threading import Thread
        #
        # self._logger = ModuleLogger(self.port, self.server_root_path)
        # 
        # # cpu_count => process_cpu_count in Python 3.13
        # if (sys.version_info.major >= 3 and sys.version_info.minor >= 13):
        #    nThreads = os.process_cpu_count() // 2
        # else:
        #    nThreads = os.cpu_count() // 2
        #
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
        a loop that will query the command queue and forward commands to the
        callback function.

        This method also sets up the shared logging task.
        """

        if self.log_verbosity == LogVerbosity.Loud:
            print(f"{self.module_id} starting main_init")

        # if Debug:
        #    self._request_session = aiohttp.ClientSession()
        async with aiohttp.ClientSession() as session:
            self._request_session = session

            if self.log_verbosity == LogVerbosity.Loud:
                print(f"{self.module_id} starting logging_loop")

            # Start with just running one logging loop
            self._logger = ModuleLogger(self.port, self.server_root_path)

            self._logger.log(LogMethod.Info | LogMethod.Server,
            { 
                "filename": __file__,
                "loglevel": "trace",
                "method": "main_init",
                "message": f"Trace: Running init for {self.module_name}"
            })

            # NOTE: We need to await self.initialise in the asyncio loop. This
            # means we can't just 'await Chris Maunder on 4/20/2024, 10:56:22 AMinitialise'. Find which was overridden
            init_method = self.initialize
             # if self.initialize wasn't overridden, use self.initialise
            if self.initialize.__qualname__ == "ModuleRunner.initialize":
                init_method = self.initialise

            if self.log_verbosity == LogVerbosity.Loud:
                print(f"{self.module_id} call module's init method")

            if asyncio.iscoroutinefunction(init_method):
                # if initialise is async, then it's a coroutine. In this case
                # we create an awaitable asyncio task to execute this method.
                init_task = asyncio.create_task(init_method())
            else:
                # If the method is not async, then we wrap it in an awaitable
                # method which we await.
                loop = asyncio.get_running_loop()
                init_task = loop.run_in_executor(None, init_method)

            try:
                await init_task
            except Exception as ex:
                print(f"An exception occurred initialising the module: {str(ex)}")    
                
            if self.log_verbosity == LogVerbosity.Loud:
                print(f"{self.module_id} module init complete")

            sys.stdout.flush()

            if self.log_verbosity == LogVerbosity.Loud:
                print(f"{self.module_id} setting up main loop")

            # Add main processing loop tasks
            logging_task = asyncio.create_task(self._logger.logging_loop())
            status_task  = asyncio.create_task(self.status_update_loop())

            tasks = [ asyncio.create_task(self.main_loop(task_id)) \
                      for task_id in range(self.parallelism) ]

            sys.stdout.flush()

            # combine
            tasks.append(logging_task)
            tasks.append(status_task)

            await self.log_async(LogMethod.Info | LogMethod.Server, {
                        "message": self.module_name + " started.",
                        "loglevel": "trace"
                    })

            try:
                await asyncio.gather(*tasks)
                if self.log_verbosity == LogVerbosity.Loud:
                    print(f"{self.module_id} all tasks complete. Ending.")
                    
            except Exception as ex:
                print(f"An exception occurred completing all module tasks: {str(ex)}")    

            # if Debug:
            #     await self._request_session.close()
            self._request_session = None


    async def status_update_loop(self):
        """ 
        Runs the main status update loop which updates the server with the status
        of each module.
        """
        if self.log_verbosity == LogVerbosity.Loud:
            print(f"starting status_update_loop")
            
        self.status_loop_started = True
        while not self._cancelled:
            
            status_object = self._get_module_status()
            statusData = { "statusData": json.dumps(status_object) }

            try:
                await self.call_api(f"queue/updatemodulestatus/{self.module_id}",
                                    None, statusData)
            except Exception as ex:
                print(f"An exception occurred updating the module status: {str(ex)}")    
            
            await asyncio.sleep(self._status_sleep_time)
                
        self.status_loop_started = False


    # Main loop
    async def main_loop(self, task_id) -> None:
        """
        This is the main request processing loop. This method continually polls
        the queue that this module is servicing, and each time it sees a request
        it will grab the request data, send it to the `process` method, then
        gather the results and post them back to the queue. The server is 
        responsible for placing requests from the calling client onto the queue,
        and then taking responses off the queue and returning them to the client.

        Special requests, such as quit, status and selftest are handled 
        carefully.
        """

        if self.log_verbosity == LogVerbosity.Loud:
            print(f"{self.module_id} starting main_loop {task_id}")

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
                
                data: RequestData = RequestData(queue_entry)

                # The method to call to process this request
                method_to_call = self.process
                update_statistics  = True

                # Some requests need to be handled differently
                if data.command:
                    command = data.command.lower()

                    if self.log_verbosity == LogVerbosity.Loud:
                        print(f"{self.module_id} command {command} pulled from queue for task {task_id}")

                    # Special requests
                    if command == "quit" and \
                       self.module_id.lower() == data.get_value("moduleId").lower():
                        
                        update_statistics = False

                        if self.log_verbosity == LogVerbosity.Loud:
                            print(f"{self.module_id} 'quit' called. Signaling shutdown for task {task_id}")

                        await self.log_async(LogMethod.Info | LogMethod.File | LogMethod.Server, { 
                            "process":  self.module_name,
                            "filename": __file__,
                            "method":   "main_loop",
                            "loglevel": "info",
                            "message":  "Shutting down"
                        })
                        self._cancelled = True
                        break
                    
                    elif command == "status" or command == "get_module_status": 
                        update_statistics = False
                        method_to_call    = self._get_module_status
                    
                    elif command == "get_command_status":
                        update_statistics = False
                        method_to_call    = self._get_command_status
                        
                    elif command == "cancel_command":
                        update_statistics = False
                        method_to_call    = self._cancel_command_task

                    elif command == "selftest":
                        # NOTE: selftest generally won't actually be called here 
                        #       - it'll be called via command line. This is here
                        #       in case selftest is triggered via API
                        update_statistics = False
                        method_to_call    = self.selftest

                    # Annoying requests
                    if command in self._ignore_timing_commands:
                        update_statistics = False

                output: JSON = {}
                try:
                    # Overriding issue here: We need to await self.process in the
                    # asyncio loop. This means we can't just 'await self.process'

                    if self.log_verbosity == LogVerbosity.Loud:
                        print(f"{self.module_id} calling process with '{command}' for task {task_id}")

                    if asyncio.iscoroutinefunction(method_to_call):
                        # if process is async, then it's a coroutine. In this
                        # case we create an awaitable asyncio task to execute
                        # this method.
                        callbacktask = asyncio.create_task(method_to_call(data))
                    else:
                        # If the method is not async, then we wrap it in an
                        # awaitable method which we will await.
                        loop = asyncio.get_running_loop()
                        callbacktask = loop.run_in_executor(None, method_to_call, data)

                    # Await 
                    output = await callbacktask

                    # if a coroutine was returned then this is a "long process"
                    # call. We'll run the coroutine (the long process) in the
                    # background and return a message to the server. Only one
                    # long running command can be in progress at a time
                    if asyncio.iscoroutinefunction(output) or callable(output):
                        
                        if self.long_running_command_task and not self.long_running_command_task.done():
                            output = {
                                "success": False,
                                "commandId": self.long_running_command_id,
                                "error": "A long running command is already in progress"
                            }
                        else:
                            # We have a previous long running process that is now
                            # done, but we have not stored (nor returned) the
                            # result. We can read the result now, but we have to
                            # start a new process, so...???
                            # if self.long_running_command_task and self.long_running_command_task.done() and \
                            #   not self.last_long_running_output:
                            #    last_long_running_output = ...

                            # Store the request ID as the command Id for later, reset the last result
                            self.long_running_command_id  = data.request_id
                            self.last_long_running_output = None

                            # Start the long running process
                            if asyncio.iscoroutinefunction(output):
                                self.long_running_command_task = asyncio.create_task(output(data))
                            else:
                                loop = asyncio.get_running_loop()
                                self.long_running_command_task = loop.run_in_executor(None, output, data)
                                
                            output = { 
                                "success":       True, 
                                "message":       "Command is running in the background",
                                "commandId":     data.request_id,
                                "commandStatus": "running"
                            }

                    if update_statistics:
                        self.update_statistics(output)

                    if self.log_verbosity == LogVerbosity.Loud:
                        print(f"{self.module_id} process call complete for task {task_id}")

                    # print(f"Process Response is {output['message']}")

                except asyncio.CancelledError:
                    print(f"Task cancelled. Ignoring command {data.command} (#reqid {data.request_id})")

                except Exception as ex:
                    output = {
                        "success": False,
                        "error":   f"unable to process the request (#reqid {data.request_id})"
                    }

                    message = "".join(traceback.TracebackException.from_exception(ex).format())
                    await self.log_async(LogMethod.Error | LogMethod.Server, { 
                        "process":        self.module_name,
                        "filename":       __file__,
                        "method":         sys._getframe().f_code.co_name,
                        "loglevel":       "error",
                        "message":        "Error during main_loop: " + message,
                        "exception_type": ex.__class__.__name__
                    })

                finally:
                    try:
                        if send_response_task != None:
                            if self.log_verbosity == LogVerbosity.Loud:
                                print(f"{self.module_id} awaiting previous send operation for task {task_id}")
                            await send_response_task

                        output["moduleId"]        = self.module_id
                        output["moduleName"]      = self.module_name
                        output["code"]            = 200 if output["success"] == True else 500
                        output["command"]         = data.command or ''
                        output["requestId"]       = data.request_id or ''
                        output["inferenceDevice"] = self.inference_device
                       
                        if self.log_verbosity == LogVerbosity.Loud:
                            print(f"{self.module_id} sending result of process to server for task {task_id}")

                        co_route = self.send_response(data.request_id, output)
                        send_response_task = asyncio.create_task(co_route)
                        
                    except Exception as ex:
                        print(f"An exception occurred sending the inference response (#reqid {data.request_id}): {str(ex)}")
        
        if self.log_verbosity == LogVerbosity.Loud:
            print(f"{self.module_id} completed. Cleaning up task {task_id}")

        # Cleanup
        self.cleanup()

        # method is ending. Let's clean up. self._cancelled == True at this point.
        self._logger.cancel_logging()

        if self.log_verbosity == LogVerbosity.Loud:
            print(f"{self.module_id} task {task_id} complete.")


    def _get_command_status(self, data: RequestData) -> JSON:
        """
        Called when this module has been asked to provide the response to a long
        running command.
        """
        command_id      = data.get_value("commandId")
        command_success = False

        if not command_id:
            # Bad input
            return {
                "success":       False,
                "commandStatus": "unknown",
                "error":         "No command ID provided"
            }
        
        elif not self.long_running_command_id:
            # Nothing running
            return {
                "success":       False,
                "commandStatus": "not started",
                "error":         "No long running command is currently in progress"
            }
        
        elif command_id != self.long_running_command_id:
            # Mistaken identity: we know not of this "command_id" that you speak
            return {
                "success":       False,
                "commandStatus": "unknown",
                "error":         "The command ID provided does not match the current long running command"
            }
        
        elif not self.long_running_command_task:
            # This is not the long running process you are looking for
            return {
                "success":       False,
                "commandStatus": "unknown",
                "error":         "The long running command task has been lost"
            }
        
        elif not self.long_running_command_task.done():
            # Long process is still running

            status = {
                "success":       True,
                "message":       "The long running command is still in progress",
                "commandId":     command_id,
                "commandStatus": "running",
            }

            command_status = self.command_status()
            
            if command_status:
                status.update(command_status)

            return status
        
        else:
            # The long process has ended. If we've stored the last result return
            # it, else get the result and store it
            command_success = True

            if not self.last_long_running_output:
                error  = None
                result = None
                if self.long_running_command_task:
                    try:
                        result = self.long_running_command_task.result()
                    except Exception as ex:
                        command_success = False
                        error = str(ex)
                else:
                    command_success = False
                    error = "Lost contact with long running task"

                self.last_long_running_output = {
                    "success":       command_success, 
                    "message":       "Command has completed",
                    "commandId":     command_id,
                    "commandStatus": "completed",
                    "error":         error
                }
                if result:
                    self.last_long_running_output.update(result) # 'update' merges the two objects
            
            return self.last_long_running_output
    

    def _get_module_status(self) -> JSON:
        """
        Called when this module has been asked to provide its overall status
        """
        status = self.module_status()
        if status is None:
            status = {}
            
        status.update({
            "inferenceDevice"      : self.inference_device,
            "inferenceLibrary"     : self.inference_library,
            "canUseGPU"            : str(self.can_use_GPU).lower(),

            "successfulInferences" : self._successful_inferences,
            "failedInferences"     : self._failed_inferences,
            "numInferences"        : self._successful_inferences + self._failed_inferences,
            "averageInferenceMs"   : 0 if not self._successful_inferences 
                                     else self._total_inference_time_ms / self._successful_inferences,
        })

        # HACK: For old modules. Remove server version 2.6
        if hasattr(self, "execution_provider"):
            if self.execution_provider == "CPU":
                status["inferenceDevice"]  = "CPU"
                status["inferenceLibrary"] = ""
            else:
                status["canUseGPU"]        = "True"
                status["inferenceDevice"]  = "GPU"
                status["inferenceLibrary"] = self.execution_provider

        return status
        

    def _cancel_command_task(self, data: RequestData) -> JSON:
        """
        This method is called to cancel a long running command.
        """
        command_id = data.get_value("commandId")
        if not command_id:
            return {
                "success": False,
                "error":   "No command ID provided"
            }
        elif not self.long_running_command_id:
            return {
                "success": False,
                "error":   "No long running command is currently in progress"
            }
        elif command_id != self.long_running_command_id:
            return {
                "success": False,
                "error":   "The command ID provided does not match the current long running command"
            }
        elif not self.long_running_command_task:
            return {
                "success": False,
                "error":   "The long running command task has been lost"
            }
        
        # We want module authors to focus on logic not plumbing. Long running 
        # modules will have unique needs when it comes to shutting down, but we
        # should provide a default solution for the simple case (no resource 
        # cleanup issues), so the module author does not need to add plumbing.
        # However, we also provide the means for an author to do a careful 
        # shutdown if required. In this case, the module author can set
        # self.force_shutdown = False in their override of cancel_command_task.
        # NOTE: self.force_shutdown = True by default.

        # Call the module's override method (if provided)
        # This is where the module author can whatever they need to do to
        # gracefully shutdown the long running process. If the module author
        # sets force_shutdown to False, then we assume the module will handle
        # the shutdown itself and we don't cancel the task.
        self.cancel_command_task()

        # If the module's override of cancel_command_task didn't set force_shutdown
        # to False then we go ahead and kill this process. Otherwise we assume the 
        # module will gracefully shut itself down
        if self.force_shutdown:
            try:
                if not self.long_running_command_task.done():
                    self.long_running_command_task.cancel()
            except:
                pass

        # NOTE: At this point the long running task may still be running! By
        #       allowing modules to abort the shutdown, we let a long running 
        #       process continue on so it has time to shutdown properly.
        if self.long_running_command_task.done():
            self.long_running_command_id   = None
            self.long_running_command_task = None
        
        output = {
            "success"         : True, 
            "message"         : "The command has been cancelled",
            "commandId"       : command_id,
            "commandStatus"   : "cancelled",

            "moduleId"        : self.module_id,
            "moduleName"      : self.module_name,
            "code"            : 200,
            "command"         : data.command or '',
            "requestId"       : data.request_id or '',
            "inferenceDevice" : self.inference_device,
        }
        
        return output
    

    # Performance timer =======================================================

    def start_timer(self, desc: str) -> Tuple[str, float]:
        """
        Starts a timer and initializes the description string that will be 
        associated with the time
        Param: desc - the description
        Returns a tuple containing the description and the timer itself
        """
        return (desc, time.perf_counter())


    def end_timer(self, timer : Tuple[str, float], label: str = "timing", command: str = None) -> None:
        """
        Ends a timing session and logs the time taken along with the initial description if the
        variable logTimingEvents = True
        Param: timer - A tuple containing the initial description and the start time
        """
        (desc, start_time) = timer
        elapsedMs = (time.perf_counter() - start_time) * 1000
    
        if self._log_timing_events and command not in self._ignore_timing_commands:
            self.log(LogMethod.Info|LogMethod.Server, {
                "message": f"{desc} took {elapsedMs:.0f}ms",
                "loglevel": "information",
                "label": label
            })


    # Service Commands and Responses ==========================================

    async def log_async(self, log_method: LogMethod, data: JSON) -> None:
        if not data or not self._logger:
            return

        if not data.get("process"):
            data["process"] = self.module_name 
                            
        await self._logger.log_async(log_method, data)

    def log(self, log_method: LogMethod, data: JSON) -> None:
        if not data or not self._logger:
            return

        if not data.get("process"):
            data["process"] = self.module_name 

        self._logger.log(log_method, data)

        
    async def get_command(self, task_id) -> "list[str]":

        """
        Gets a command from the queue associated with this object. 

        CodeProject.AI works on the  basis of having a client pass requests to 
        the server's public API, which in turns places each request into various 
        command queues. The backend analysis modules continually pull requests
        from the queue that they can service. Each request for a queued command
        is done via a long poll HTTP request.

        Returns the JSON package containing the raw request from the client 
        that was sent to the server

        Remarks: The API server will currently only return a single command, 
        not a list, so we could just as easily return a string instead of a 
        list of strings. We return a list to maintain compatibility with the 
        old legacy modules we started with, but also to future-proof the code 
        in case we want to allow batch processing. Be aware that batch 
        processing will mean less opportunity to load balance the requests.
        """
        commands = []

        if self.log_verbosity == LogVerbosity.Loud:
            print(f"{self.module_id} in get_command for task {task_id}")

        if not self._request_session or self._request_session.closed:
            await self.log_async(LogMethod.Error, {
                "message": f"No open session available for {self.module_id} to connect to the server.",
                "method": sys._getframe().f_code.co_name,
                "loglevel": "error",
                "process": self.queue_name,
                "filename": __file__
            })
            return commands

        try:
            url = self._base_queue_url + self.queue_name + "?moduleId=" + self.module_id

            # Send a request to query the queue and wait up to 30 seconds for a
            # response. We're basically long-polling here
            async with self._request_session.get(
                url,
                timeout = self._wait_for_command_secs
                #, verify = False
            ) as session_response:

                if session_response.ok:
                    content = await session_response.text()
                    if content:

                        # This method allows multiple commands to be returned, but to
                        # keep things simple we're only ever returning a single command
                        # at a time (but still: ensure it's as an array)
                        commands = [content]

                        # The request worked: clear the error pause time, record
                        # last successful call
                        self._current_error_pause_secs = 0

                        data = RequestData(content)
                        # HACK: logging this command is just annoying to everyone concerned.                        
                        if data.command not in self._ignore_timing_commands:
                            await self.log_async(LogMethod.Info|LogMethod.Server, {
                                "message": f"Retrieved {self.queue_name} command '{data.command}'",
                                "loglevel": "debug"
                            })
                else:
                    # await self.log_async(LogMethod.Error | LogMethod.Server, {
                    #     "message": f"Error retrieving command from queue {self.queue_name}",
                    #     "method": sys._getframe().f_code.co_name,
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

        except asyncio.TimeoutError as t_ex:
            # We long-poll the server to get commands, so timeouts are how we roll.
            pass
            """
            if not self._cancelled:
                await self.log_async(LogMethod.Error | LogMethod.Server, {
                    "message": f"Timeout retrieving command from queue {self.queue_name}",
                    "method": sys._getframe().f_code.co_name,
                    "loglevel": "error",
                    "process": 'get_command',
                    "filename": __file__,
                    "exception_type": t_ex.__class__.__name__
                })

            # We'll only calculate the error pause time in task #0, but all 
            # tasks will pause on error for the same amount of time
            if task_id == 0:
                self._current_error_pause_secs = self._current_error_pause_secs * 2 \
                                                 if self._current_error_pause_secs \
                                                 else self._error_pause_secs
            """

        except ConnectionRefusedError as c_ex:
            if not self._cancelled:
                await self.log_async(LogMethod.Error, {
                    "message": f"Connection refused trying to check the command queue {self.queue_name}.",
                    "method": sys._getframe().f_code.co_name,
                    "loglevel": "error",
                    "process": self.queue_name,
                    "filename": __file__,
                    "exception_type": c_ex.__class__.__name__
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
                err_msg        = f"Unable to check the command queue {self.queue_name}. " + \
                                  "Is the server running, and can you connect to the server?"
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
                err_msg = "Error in get_command: " + str(ex)

                if not self._cancelled and err_msg and err_msg != 'Session is closed':
                    pause_on_error = True
                    err_msg = f"Error retrieving command: {err_msg}"
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
        Sends the result of a command to the analysis modules back to the API
        server, which will then pass this result back to the original calling 
        client. CodeProject.AI works on the basis of having a client pass 
        requests to the server's public API, which in turns places each request 
        into various command queues. The backend analysis modules continually 
        pull requests from the queue that they can service, process each 
        request, and then send the results back to the server.

        Param: request_id - the ID of the request that was originally pulled 
                            from the command queue.
        Param: body:      - the JSON result (as a string) from the analysis of 
                            the request.

        Returns:          - True on success; False otherwise
        """

        success = False
        # responseTimer = self.start_timer(f"Sending response for request from {self.queue_name}")

        try:
            # print("Sending response to server")
            url = self._base_queue_url + request_id + "?moduleId=" + self.module_id
            
            async with self._request_session.post(
                url,
                data    = json.dumps(body),
                timeout = self._response_timeout_secs
                #, verify  = False
                ):
                success = True
            # print("Response sent")

        except asyncio.TimeoutError as t_ex:
            if self.log_verbosity == LogVerbosity.Quiet:
                print(f"Timeout sending response: Is the API Server running? [{t_ex.__class__.__name__}]")
            else:
                print(f"Timeout sending response: {str(t_ex)}")
            await asyncio.sleep(self._error_pause_secs)

        except Exception as ex:
            # if self.log_verbosity == LogVerbosity.Quiet:
            #    print(f"Timeout sending response: Is the API Server running? [{ex.__class__.__name__}]")
            # else:
            print(f"Error sending response: {str(ex)}")
            await asyncio.sleep(self._error_pause_secs)

        finally:
            #if success:
            #    self.end_timer(responseTimer)
            return success


    async def call_api(self, method:str, files=None, data=None) -> str:
        """ 
        Provides the means to make a call to a CodeProject.AI API. Handy if this
        module wishes to make use of another module's functionality, or simply
        to pass data back to the server
        """

        url = self.base_api_url + method

        formdata = aiohttp.FormData()
        if data:
            for key, value in data.items():
                formdata.add_field(key, str(value))

        if files:
            for key, file_info in files.items():
                formdata.add_field(key, file_info[1], filename=file_info[0], content_type=file_info[2])

        try:
            async with self._request_session.post(
                url,
                data = formdata,
                timeout = self._response_timeout_secs
            ) as session_response:
                response = ""
                if session_response.content_type == 'text/plain':
                    response = await session_response.text()
                elif session_response.content_type == 'application/json':
                    response = await session_response.json()
        except Exception as ex:
            response = ""

        return response

    def report_error(self, exception: Exception, filename: str, message: str = None) -> None:
        """
        Shortcut method provided solely to allow a module to report an error
        """
        
        if not message and exception:
            message = "".join(traceback.TracebackException.from_exception(exception).format())

        self.log(LogMethod.Error | LogMethod.Server,
        {
            "filename": filename,
            "method":   sys._getframe().f_back.f_code.co_name,
            "loglevel": "error",
            "message": message,
            "exception_type": exception.__class__.__name__ if exception else None
        })

    async def report_error_async(self, exception: Exception, filename: str, message: str = None) -> None:
        """
        Shortcut method provided solely to allow a module to report an error asynchronously
        """

        if not message and exception:
            message = "".join(traceback.TracebackException.from_exception(exception).format())

        await self.log_async(LogMethod.Error | LogMethod.Server,
        {
            "filename": filename,
            "method":   sys._getframe().f_back.f_code.co_name,
            "loglevel": "error",
            "message": message,
            "exception_type": exception.__class__.__name__ if exception else None
        })


    def check_packages(self) -> None:
        """
        Checks that the packages defined in the requirements.*.txt file for this
        platform are actually installed
        """
        requirements_filepath = self.get_requirements_filepath()
        if requirements_filepath:
            # DISABLED: check_installed_packages is deprecated
            # print(check_installed_packages(requirements_filepath, False))
            # DISABLED: check_requirements causes Paddle to explode with warnings 
            #           about Setuptools replacing distutils
            # print(check_requirements(requirements_filepath, False))
            pass

    def get_requirements_filepath(self) -> str:
        """
        Gets the requirements.*.txt file for the given system and hardware, ensuring
        that this file actually exists
        """

        # This is getting complicated. The order of priority for the requirements file is:
        #
        #  requirements.device.txt                              (device = "raspberrypi", "orangepi", "radxarock" or "jetson" )
        #  requirements.os.architecture.cuda.cuda_version.txt   (version is in form 11_7, 12_2 etc)
        #  requirements.os.architecture.cuda.cuda_major.txt     (major is in form 11, 12 etc)
        #  requirements.os.architecture.(cuda|rocm).txt
        #  requirements.os.(cuda|rocm).txt
        #  requirements.cuda.txt
        #  requirements.os.architecture.gpu.txt
        #  requirements.os.gpu.txt
        #  requirements.gpu.txt
        #  requirements.os.architecture.txt
        #  requirements.os.txt
        #  requirements.txt
        #
        # The logic here is that we go from most specific to least specific. The only
        # real tricky bit is the subtlety around .cuda vs .gpu. CUDA / ROCm are specific
        # types of card. We may not be able to support that, but may be able to support
        # other cards generically via OpenVINO or DirectML. So CUDA or ROCm first,
        # then GPU, then CPU. With a query at each step for OS and architecture.

        filename = ""
        os_name  = self.system_info.os.lower()
        arch     = self.system_info.cpu_arch.lower()

        if self.system_info.system == 'Raspberry Pi':
            if os.path.exists(os.path.join(self.module_path, f"requirements.raspberrypi.txt")):
                filename = f"requirements.raspberrypi.txt"
        elif self.system_info.system == 'Orange Pi':
            if os.path.exists(os.path.join(self.module_path, f"requirements.orangepi.txt")):
                filename = f"requirements.orangepi.txt"
        elif self.system_info.system == 'Radxa ROCK':
            if os.path.exists(os.path.join(self.module_path, f"requirements.radxarock.txt")):
                filename = f"requirements.radxarock.txt"
        elif self.system_info.system == 'Jetson':
            if os.path.exists(os.path.join(self.module_path, f"requirements.jetson.txt")):
                filename = f"requirements.jetson.txt"

        if not filename and self.enable_GPU:
            # TODO: Change this to system_info.hasCudaGpu, and add system_info.CudaVersion (major, minor)
            # prop so we can do requirements.os.architecture.cuda.cuda_version.txt and
            # requirements.os.architecture.cuda.cuda_major.txt

            if self.system_info.hasTorchCuda:
                # TODO: Get the specific CUDA version and then add tests for .cudaMajor, .cudaMajor_Minor
                if os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.{arch}.cuda.txt")):
                    filename = f"requirements.{os_name}.{arch}.cuda.txt"
                elif os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.cuda.txt")):
                    filename = f"requirements.{os_name}.cuda.txt"
                elif os.path.exists(os.path.join(self.module_path, f"requirements.cuda.txt")):
                    filename = f"requirements.cuda.txt"

            if self.system_info.hasTorchROCm:
                if os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.{arch}.rocm.txt")):
                    filename = f"requirements.{os_name}.{arch}.rocm.txt"
                elif os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.rocm.txt")):
                    filename = f"requirements.{os_name}.rocm.txt"
                elif os.path.exists(os.path.join(self.module_path, f"requirements.rocm.txt")):
                    filename = f"requirements.rocm.txt"

            if not filename:
                if os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.{arch}.gpu.txt")):
                    filename = f"requirements.{os_name}.{arch}.gpu.txt"
                elif os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.gpu.txt")):
                    filename = f"requirements.{os_name}.gpu.txt"
                elif os.path.exists(os.path.join(self.module_path, f"requirements.gpu.txt")):
                    filename = f"requirements.gpu.txt"

        if not filename:
            if os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.{arch}.txt")):
                filename = f"requirements.{os_name}.{arch}.txt"
            elif os.path.exists(os.path.join(self.module_path, f"requirements.{os_name}.txt")):
                filename = f"requirements.{os_name}.txt"
            elif os.path.exists(os.path.join(self.module_path, f"requirements.txt")):
                filename = f"requirements.txt"

        if filename:
            return os.path.join(self.module_path, filename)

        return None
