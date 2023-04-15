
import asyncio
import json
import os
import platform
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
current_python_dir = os.path.dirname(sys.executable)
if current_python_dir:
    package_path = os.path.normpath(os.path.join(current_python_dir, '../lib/python' + sys.version[:3] + '/site-packages/'))
    sys.path.insert(0, package_path)
    # print("Adding " + package_path + " to packages search path")

# We can now import these from the appropriate VENV location
import aiohttp

# CodeProject.AI SDK. Import these *after* we've set the import path
from common import JSON
from module_logging import LogMethod, ModuleLogger, LogVerbosity
from request_data   import RequestData
from module_options import ModuleOptions

class ModuleRunner:
    """
    A thin abstraction + helper methods to allow python modules to communicate 
    with the backend of main API Server
    """

    async def initialise(self) -> None:
        """
        Called when this module first starts up. To be overridden by child classes 
        """
        pass

    async def process(self, data: RequestData) -> JSON:
        """
        Called each time a request is retrieved from this module's queue is to be
        processed. To be overridden by child classes

        self - This ModuleRunner
        data - The RequestData retrieved from the processing queue. It contains all
               the info needed for this module to satisfy the request
        returns: A JSON package containing the results of this request.
        """
        pass

    def shutdown(self) -> None:
        """
        Called when this module has been asked to shutdown. To be overridden by child
        classes
        """
        pass

    def __init__(self):
    #
    #    """ 
    #    Constructor. 
    #    """

        # Constants
        self._error_pause_secs      = 1.0   # For general errors
        self._conn_error_pause_secs = 5.0   # for connection / timeout errors
        self._log_timing_events     = True
        self._verbose_exceptions    = True

        # Private fields
        self._execution_provider    = "" # backing field for self.execution_provider
        self._cancelled             = False
        self._current_error_pause_secs = 0

        self._hasTorchCuda          = None
        self._hasTorchHalfPrecision = None
        self._hasTorchMPS           = None
        self._hasONNXRuntime        = None
        self._hasONNXRuntimeGPU     = None
        self._hasOpenVINO           = None
        self._hasPaddleGPU          = None
        self._hasCoralTPU           = None

        # Public fields -------------------------------------------------------

        # A note about the use of ModuleOptions. ModuleOptions is simply a way 
        # to hide all the calls to _get_env_var behind a simple class. While
        # there is a lot of repitition in self.property = ModuleOptions.property,
        # it means we have the means of keeping the initial values the module
        # had at launch separate from the working values which may change during.
        # It's tempting to remove all values that ModuleOptions supplies, and
        # instead just have ModuleRunner.ModuleOptions.property, but many 
        # properties such as module_id are intrinsic to this module, and exposing
        # a ModuleOptions property exposes too much information on the internals
        # of this class.

        # Module Descriptors
        self.module_id           = ModuleOptions.module_id
        self.module_name         = ModuleOptions.module_name
        self.module_path         = ModuleOptions.module_path

        # Server API location and Queue
        self.base_api_url        = ModuleOptions.base_api_url
        self.port                = ModuleOptions.port
        self.queue_name          = ModuleOptions.queue_name

        # General Module and Server settings
        self.server_root_path    = ModuleOptions.server_root_path
        self.python_dir          = current_python_dir
        self.log_verbosity       = ModuleOptions.log_verbosity
        self.launched_by_server  = ModuleOptions.launched_by_server

        # Hardware / accelerator info
        self.support_GPU         = ModuleOptions.support_GPU
        self.accel_device_name   = ModuleOptions.accel_device_name
        self.half_precision      = ModuleOptions.half_precision
        self.parallelism         = ModuleOptions.parallelism
        self.processor_type      = "CPU" # may be overridden by the module
        self.cpu_brand           = ""
        self.cpu_vendor          = ""
        self.cpu_arch            = ""

        # General purpose flags. These aren't currently supported as common flags
        # self.use_CUDA          = ModuleOptions.use_CUDA
        # self.use_ROCm          = ModuleOptions.use_ROCm
        # self.use_Coral         = ModuleOptions.use_Coral
        # self.use_ONNXRuntime   = ModuleOptions.use_ONNXRuntime
        # self.use_OpenVINO      = ModuleOptions.use_OpenVINO

        # What system are we running on?
        self.system = { 'Linux': 'Linux', 'Darwin': 'macOS', 'Windows': 'Windows'}[platform.system()]

        # Further tests for RaspberryPi
        if self.system == 'Linux': 
            try:
                import io
                with io.open('/sys/firmware/devicetree/base/model', 'r') as m:
                    if 'raspberry pi' in m.read().lower(): self.system = 'RaspberryPi'
            except Exception: pass

        # Need to hold off until we're ready to create the main logging loop.
        # self._logger           = ModuleLogger(self.port, self.server_root_path)

        # Get some (very!) basic CPU info
        try:
            import cpuinfo
            info = cpuinfo.get_cpu_info()
            self.cpu_brand = info.get('brand_raw')
            self.cpu_arch  = info.get('arch_string_raw')
        except:
            self.cpu_brand = ""
            self.cpu_arch  = ""

        self.cpu_vendor = self.cpu_brand
        if self.cpu_brand:
            if self.cpu_brand.startswith("Apple M"):
                self.cpu_vendor = 'Apple'
                self.cpu_arch   = 'arm64'
            elif self.cpu_brand.find("Intel(R)") != -1:
                self.cpu_vendor = 'Intel'

        if self.support_GPU and self.hasTorchMPS:
            os.environ["PYTORCH_ENABLE_MPS_FALLBACK"] = "1"

        # Private fields
        self._base_queue_url = self.base_api_url + "queue/"
    
    """
    @staticmethod
    def supports_half_precision(gpu_card_name: str) -> bool:
        " ""
        Returns a True or False depending on whether the aupplies card 
        supports half-precision operations.
        This is terribly flaky. The docs seem to suggest anything with
        a compute capability >= 6.0 supports half, so we'll roll with that
        Half-precision is supported by Pascal architecture and above.
        https://en.wikipedia.org/wiki/CUDA#GPUs_supported - GPUs by architecture
        https://www.eatyourbytes.com/list-of-gpus-by-processing-power-half-precision/ - half precision speeds
        " ""
        no_half = ["TU102","TU104","TU106","TU116", "TU117",
                   "GeForce 960",
                   "GeoForce GT 1030", "GeForce GTX 1050","GeForce GTX 1060",
                   "GeForce GTX 1060","GeForce GTX 1070","GeForce GTX 1080",
                   "GeForce RTX 2060", "GeForce RTX 2070", "GeForce RTX 2080",
                   "GeForce GTX 1650", "GeForce GTX 1660", "MX550", "MX450",
                   "Quadro RTX 8000", "Quadro RTX 6000", "Quadro RTX 5000", "Quadro RTX 4000"
                   # "Quadro P1000", - this works with half!
                   "Quadro P620", "Quadro P400",
                   "T1000", "T600", "T400","T1200","T500","T2000",
                   "Tesla T4"]
        
        return not any(check_name in gpu_card_name for check_name in no_half)
        """

    @property
    def hasTorchCuda(self):
        if self._hasTorchCuda == None:
            self._hasTorchCuda = False
            try:
                import torch
                self._hasTorchCuda = torch.cuda.is_available()
            except: pass
        return self._hasTorchCuda

    @property
    def hasTorchHalfPrecision(self):
        if self._hasTorchHalfPrecision == None:
            self._hasTorchHalfPrecision = False
            try:
                # Half precision supported on Pascal architecture, which means compute
                # capability 6.0 and above
                import torch
                self._hasTorchHalfPrecision = torch.cuda.get_device_capability()[0] >= 6
            except: pass
        return self._hasTorchHalfPrecision

    @property
    def hasONNXRuntime(self):
        if self._hasONNXRuntime == None:
            self._hasONNXRuntime = False
            try:
                import onnxruntime as ort
                providers = ort.get_available_providers()
                self._hasONNXRuntime = len(providers) > 0
            except: pass
        return self._hasONNXRuntime

    @property
    def hasONNXRuntimeGPU(self):
        if self._hasONNXRuntimeGPU == None:
            self._hasONNXRuntimeGPU = False
            try:
                import onnxruntime as ort
                self._hasONNXRuntimeGPU = ort.get_device() == "GPU"
            except: pass
        return self._hasONNXRuntimeGPU

    @property
    def hasOpenVINO(self):
        if self._hasOpenVINO == None:
            self._hasOpenVINO = False
            try:
                import openvino.utils as utils
                utils.add_openvino_libs_to_path()
                self._hasOpenVINO = True
            except: pass
        return self._hasOpenVINO

    @property
    def hasTorchMPS(self):
        if self._hasTorchMPS == None:
            self._hasTorchMPS = False
            if self.cpu_vendor == 'Apple' and self.cpu_arch == 'arm64':
                try:
                    import torch
                    self._hasTorchMPS = hasattr(torch.backends, "mps") and torch.backends.mps.is_available()
                except: pass
        return self._hasTorchMPS

    @property
    def hasPaddleGPU(self):
        if self._hasPaddleGPU == None:
            self._hasPaddleGPU = False
            try:
                import paddle
                self._hasPaddleGPU = paddle.device.get_device().startswith("gpu")
            except: pass
        return self._hasPaddleGPU

    @property
    def hasCoralTPU(self):
        if self._hasCoralTPU == None:
            self._hasCoralTPU = False

            # First see if the incredibly difficult to install python-pycoral pkg
            # can help us.
            try:
                from pycoral.utils.edgetpu import list_edge_tpus
                self._hasCoralTPU = len(list_edge_tpus()) > 0
                return self._hasCoralTPU
            except: pass

            # Second, determine if we have TensorFlow-Lite runtime installed, or 
            # the whole Tensorflow. In either case we're looking to load TFLite models
            try:
                try:
                    from tflite_runtime.interpreter import load_delegate
                except ImportError:
                    import tensorflow as tf
                    load_delegate = tf.lite.experimental.load_delegate

                # On Windows, the interpreter.__init__ method accepts experimental
                # delegates. These are used in self._interpreter.ModifyGraphWithDelegate, 
                # which fails on Windows
                import platform
                if platform.system() != "Windows":  
                    delegate = {
                        'Linux': 'libedgetpu.so.1',
                        'Darwin': 'libedgetpu.1.dylib',
                        'Windows': 'edgetpu.dll'}[platform.system()]
                    delegates = [load_delegate(delegate)]
                    self._hasCoralTPU = len(delegates) > 0
                    return self._hasCoralTPU
            except Exception as ex:
                pass

        return self._hasCoralTPU

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
            self.processor_type = "GPU"


    def start_loop(self):

        """
        Starts the tasks that will run the execution loops that check the 
        command queue and forwards commands to the module. Each task runs 
        asynchronously, with each task's loop independently querying the 
        command queue and sending commands to the (same) callback function.
        """

        try:
            asyncio.run(self.main_init())
        except Exception as ex:
            message = "".join(traceback.TracebackException.from_exception(ex).format())
            self.log(LogMethod.Error | LogMethod.Server, { 
                "process":        self.module_name,
                "filename":       __file__,
                "method":         sys._getframe().f_code.co_name,
                "loglevel":       "error",
                "message":        message,
                "exception_type": ex.__class__.__name__
            })

        # NOTE: If using threads, then do it this way. However, using async tasks
        # is faster and more efficient
        #
        # from threading import Thread
        #
        # self._logger = ModuleLogger(self.port, self.server_root_path)
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
            self._logger          = ModuleLogger(self.port, self.server_root_path)

            # Start with just running one logging loop
            logging_task = asyncio.create_task(self._logger.logging_loop())

            # Call the init callback if available
            if True : # self.init_callback:
                self._logger.log(LogMethod.Info | LogMethod.Server,
                { 
                    "filename": __file__,
                    "loglevel": "information",
                    "method": "main_init",
                    "message": f"Running init for {self.module_name}"
                })

                # Overriding issue here: We need to await self.initialise in the
                # asyncio loop. This means we can't just 'await self.initialise'

                if asyncio.iscoroutinefunction(self.initialise):
                    # if initialise is async, then it's a coroutine. In this
                    # case we create an awaitable asyncio task to execute this
                    # method.
                    init_task = asyncio.create_task(self.initialise())
                else:
                    # If the method is not async, then we wrap it in an awaitable
                    # method which we await.
                    loop = asyncio.get_running_loop()
                    init_task = loop.run_in_executor(None, self.initialise)

                await init_task

            sys.stdout.flush()

            # Add main processing loop tasks
            tasks = [ asyncio.create_task(self.main_loop(task_id)) for task_id in range(self.parallelism) ]

            sys.stdout.flush()

            # combine
            tasks.append(logging_task)

            await self.log_async(LogMethod.Info | LogMethod.Server, {
                        "message": self.module_name + " started.",
                        "loglevel": "information"
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

            # print(f"Found a queue entry")

            # In theory we may get back multiple command requests. In practice
            # it's always just 1 at a time. At the moment.
            for queue_entry in queue_entries:
                data: RequestData = RequestData(queue_entry)

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

                process_name = f"Queue request for {self.module_name}"
                if data.command:
                    process_name += f" command '{data.command}' (#reqid {data.request_id})"

                timer: Tuple[str, float] = self.start_timer(process_name)

                output: JSON = {}
                try:
                    # Overriding issue here: We need to await self.process in the
                    # asyncio loop. This means we can't just 'await self.process'

                    # print(f"About to run process")

                    if asyncio.iscoroutinefunction(self.process):
                        # if process is async, then it's a coroutine. In this
                        # case we create an awaitable asyncio task to execute
                        # this method.
                        callbacktask = asyncio.create_task(self.process(data))
                    else:
                        # If the method is not async, then we wrap it in an
                        # awaitable method which we await.
                        loop = asyncio.get_running_loop()
                        callbacktask = loop.run_in_executor(None, self.process, data)

                    # print(f"Process task created")

                    # Await 
                    output = await callbacktask

                    # print(f"Process Response is {output['message']}")

                except asyncio.CancelledError:
                    print(f"The future has been cancelled. Ignoring command {data.command} (#reqid {data.request_id})")

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
                        "message":        message,
                        "exception_type": ex.__class__.__name__
                    })

                finally:
                    self.end_timer(timer, "command timing")

                    try:
                        if send_response_task != None:
                            # print("awaiting old send task")
                            await send_response_task

                        # Legacy code. Deprecated
                        output["code"] = 200 if output["success"] == True else 500
                        
                        # print("creating new send task")
                        send_response_task = asyncio.create_task(self.send_response(data.request_id, output))
                        
                    except Exception:
                        print(f"An exception occured sending the inference response (#reqid {data.request_id})")
        
        # Cleanup
        self.shutdown()

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

                        # This method allows multiple commands to be returned, but to
                        # keep things simple we're only ever returning a single command
                        # at a time (but still: ensure it's as an array)
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
            
            # print("Sending response to server")
            async with self._request_session.post(
                url,
                data    = json.dumps(body),
                timeout = 10
                #, verify  = False
                ):
                success = True
            # print("Response sent")

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


    def report_error(self, exception: Exception, filename: str, message: str = None) -> None:

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