#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH for future imports
sys.path.append("../../SDK/Python")

# HACK: For a module in the demos/modules folder we need to add this search path
sys.path.append("../../../SDK/Python")

from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner

# Import the method of the module we're wrapping
from long_process import a_long_process, cancel_process

class PythonLongProcess_adapter(ModuleRunner):

    def initialise(self) -> None:
        # Results from the long process
        self.result      = None
        self.step        = 0
        # Process state
        self.cancelled   = False
        self.stop_reason = None

    def process(self, data: RequestData) -> JSON:
        # This is a long process module, so all we need to do here is return the
        # long process method that will be run
        return self.long_process

    def long_process(self, data: RequestData) -> JSON:

        self.cancelled   = False
        self.stop_reason = None
        self.result      = None
        self.step        = 0

        start_time = time.perf_counter()
        a_long_process(self.long_process_callback)
        inferenceMs : int = int((time.perf_counter() - start_time) * 1000)

        if self.stop_reason is None:
            self.stop_reason = "completed"

        response = {
            "success":     True, 
            "result":      self.result,
            "stop_reason": self.stop_reason,
            "processMs":   inferenceMs,
            "inferenceMs": inferenceMs
        }

        return response

    def command_status(self) -> JSON:
        return {
            "success": True, 
            "result":  self.result or ""
        }

    def cancel_command_task(self):
        cancel_process()
        self.stop_reason = "cancelled"
        self.force_shutdown = False  # Tell ModuleRunner we'll shut ourselves down


    def long_process_callback(self, result, step):
        """ The callback for a_long_process() """
        self.result = result
        self.step   = step


if __name__ == "__main__":
    PythonLongProcess_adapter().start_loop()
