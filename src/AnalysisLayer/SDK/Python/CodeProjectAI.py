
import json
import os
import sys
import time
import traceback

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
current_python_dir = os.path.join(os.path.dirname(sys.executable))
if current_python_dir != "":
    package_path = os.path.normpath(os.path.join(current_python_dir, '../lib/python' + sys.version[:3] + '/site-packages/'))
    sys.path.insert(0, package_path)
    # print("Adding " + package_path + " to packages search path")

# We can now import these from the appropriate VENV location
import requests
# from PIL import Image

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

    # Constructor
    def __init__(self, queue_name):

        # Constants
        self._error_pause_secs   = 1.0
        self._log_timing_events  = False
        self._verbose_exceptions = True

        # Backing var for property execution_provider
        self._execution_provider = ""

        # Public fields
        self.queue_name          = os.getenv("CPAI_MODULE_QUEUENAME", queue_name)
        self.module_name         = os.getenv("CPAI_MODULE_NAME",      queue_name)
        self.python_dir          = current_python_dir
        self.errLog_APIkey       = os.getenv("CPAI_ERRLOG_APIKEY",    "")
        self.port                = os.getenv("CPAI_PORT",             "5000")
        self.server_root_path    = os.getenv("CPAI_APPROOTPATH",      "")
        self.module_id           = os.getenv("CPAI_MODULE_ID",        "CodeProject.AI")

        self.hardware_id         = "CPU"
        self.execution_provider  = ""

        self.use_openvino        = False
        self.use_onnxruntime     = False

        # Private fields
        # We're hardcoding localhost because we have no plans to have the 
        # analysis services and the server on separate machines or containers.
        # At no point should any outside app have access to the backend 
        # services. It all must be done through the API.
        self._base_queue_url     = f"http://localhost:{self.port}/v1/queue/"

        self._request_session    = requests.Session()
        self._logger             = AnalysisLogger(self.port, self.server_root_path, self.errLog_APIkey)
    
    @property
    def execution_provider(self):
        return self._execution_provider
      
    @execution_provider.setter
    def execution_provider(self, provider):
        if (provider is None) or (provider == ""):
            self._execution_provider = "CPU"
        else:
            self._execution_provider = provider

        if self.execution_provider != "CPU":
            self.hardware_id = "GPU"

    # Main loop
    def start_loop(self, callback) -> None:

        # Setup libraries
        if self.use_openvino:
            import openvino.utils as utils
            utils.add_openvino_libs_to_path()

        if self.use_onnxruntime:
            import onnxruntime as ort

            ## get the first Execution Provider Name to determine GPU/CPU type
            providers = ort.get_available_providers()
            if len(providers) > 0 :
                self.execution_provider = str(providers[0]).removesuffix("ExecutionProvider")
                self.hardware_id        = "GPU"

        # Commenting so we don't get one message per thread
        # self.log(LogMethod.Info | LogMethod.Server, {
        #            "message": self.module_name + " started.",
        #            "loglevel": "information"
        #        })

        while True:
            queue_entries: list = self.get_command()

            # In theory we may get back multiple command requests. In practice
            # it's always just 1 at a time. At the moment.
            if len(queue_entries) > 0:

                for queue_entry in queue_entries:

                    data: AIRequestData = AIRequestData(queue_entry)

                    proc_name = self.module_name
                    if data.command:
                        proc_name += f" ({data.command})"

                    timer: tuple = self.start_timer(proc_name)

                    output: JSON = {}
                    try:
                        output = callback(self, data)

                    except Exception:
                        output = {
                           "success": False,
                           "error":   "unable to process the request",
                           "code":    500
                        }

                        err_trace = traceback.format_exc()

                        self.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server, { 
                            "process":        self.module_name,
                            "filename":       "codeprojectai.py",
                            "method":         "start_loop",
                            "loglevel":       "error",
                            "message":        err_trace,
                            "exception_type": "Exception"
                        })

                    finally:
                        self.end_timer(timer, "command timing")

                        try:
                            self.send_response(data.request_id, output)
                        except Exception:
                            print("An exception occured sending the inference response")


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

    def log(self, log_method: LogMethod, data: JSON) -> None:

        if not data:
            return

        if not data.get("process"):
            data["process"] = self.module_name 
                            
        self._logger.log(log_method, data)


    def get_command(self) -> "list[str]":

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
            #  we're long-polling
            cmdTimer = self.start_timer(f"Idle time on queue {self.queue_name}")

            url = self._base_queue_url + self.queue_name + "?moduleId=" + self.module_id
            if self.execution_provider is not None :
                url += "&executionProvider=" + self.execution_provider

            # Send the request to query the queue and wait up to 30 seconds
            # for a response. We're basically long-polling here
            response = self._request_session.get(
                url,
                timeout = 30,
                verify  = False
            )

            if response.ok and len(response.content) > 0:
                success = True
                content = response.text
                self.log(LogMethod.Info|LogMethod.Server, {
                    "message": f"retrieved {self.queue_name} command",
                    "loglevel": "debug"
                })

                return [content]
            else:
                return []
        
        except Exception as ex:

            err_msg = "Error retrieving command: Is the API Server running?"
            if self._verbose_exceptions:
                err_msg = str(ex)

            self.log(LogMethod.Error|LogMethod.Server|LogMethod.Cloud, {
                "message": err_msg,
                "method": "get_command",
                "loglevel": "error",
                "process": self.queue_name,
                "filename": "codeprojectai.py",
                "exception_type": "Exception"
            })
            time.sleep(self._error_pause_secs)
            return []

        finally:
            if success:
                self.end_timer(cmdTimer)


    def send_response(self, request_id : str, body : JSON) -> bool:
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
            
            self._request_session.post(
                url,
                data    = json.dumps(body),
                timeout = 1,
                verify  = False)

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
