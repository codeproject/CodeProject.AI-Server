#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH  for future imports
sys.path.append("../../SDK/Python")

# HACK: For a module in the demos/modules folder we need to add this search path
sys.path.append("../../../src/SDK/Python")

from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner
from module_options import ModuleOptions
from module_logging import LogMethod, LogVerbosity

# Import the method of the module we're wrapping
# from my_module import my_method

class LongProcessDemo_adapter(ModuleRunner):

    def initialise(self) -> None:

        # Get some values from environment variables
        self.model_size = ModuleOptions.getEnvVariable("MODEL_SIZE", "medium")
        self.models_dir = ModuleOptions.getEnvVariable("MODEL_DIR",  "./models")
               
        # ... typically we'd do something like load a model and initialise an
        #     predictor
        # self.predictor = ...

        # Report back on what hardware we're using
        self.inference_device  = "CPU"
        self.inference_library = ""

        # Setup our long process state variables
        self.result    = ""     # our cumulative result
        self.step      = 0
        self.cancelled = False


    def process(self, data: RequestData) -> JSON:
        # This is a long process module, so all we need to do here is return the
        # long process method that will be run
        return self.long_process


    def long_process(self, data: RequestData) -> JSON:

        self.result = ""
        self.step   = 0
        stop_reason = None

        # An example of grabbing some input from the initial request
        prompt: str        = data.get_value("prompt")
        max_tokens: int    = data.get_int("max_tokens", 0) # 0 means model default
        temperature: float = data.get_float("temperature", 0.4)

        start_time = time.perf_counter()

        # Typically we'd do something long here. It's a long process so maybe we
        # need to continually collect and join a stream of output, or maybe update
        # the latest intermediate result
        # 
        # data = self.predictor.get_result(...)
        # while data:
        #     result += data
            
        # Instead we'll fake it for demonstration purposes
        for i in range(0, 10):
            time.sleep(1)
            self.step += 1
            self.result += " " + str(self.step)

            if self.cancelled:
                self.cancelled = False
                stop_reason = "cancelled"
                break

        inferenceMs : int = int((time.perf_counter() - start_time) * 1000)

        if stop_reason is None:
            stop_reason = "completed"

        response = {
            "success": True, 
            "result": self.result,
            "stop_reason": stop_reason,
            "processMs" : inferenceMs,
            "inferenceMs" : inferenceMs
        }

        return response


    def command_status(self) -> JSON:
        return {
            "success": True, 
            "result":   self.result
        }


    def cancel_command_task(self):
        self.cancelled      = True   # We will cancel this long process ourselves
        self.force_shutdown = False  # Tell ModuleRunner we'll shut ourselves down


    def selftest(self) -> JSON:

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "prompt"

        request_data.add_value("prompt", "How many planets are there in the solar system?")
        request_data.add_value("max_tokens", 256)
        request_data.add_value("temperature", 0.4)

        result = self.long_process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Long process test successful" }


if __name__ == "__main__":
    LongProcessDemo_adapter().start_loop()
