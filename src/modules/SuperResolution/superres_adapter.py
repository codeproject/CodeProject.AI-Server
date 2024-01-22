#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import os
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH var for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner
from threading import Lock

# Import libraries needed
from PIL import Image

# Import the method of the module we're wrapping
from superresolution import superresolution, load_pretrained_weights

class SuperRes_adapter(ModuleRunner):

    def initialise(self) -> None:
        assets_path = os.path.normpath(os.path.join(os.path.dirname(__file__), "assets/"))
        load_pretrained_weights(assets_path)

        # TODO: This module also supports ONNX
        self.can_use_GPU = self.system_info.hasTorchCuda

        if self.enable_GPU and self.can_use_GPU:
            self.execution_provider = "CUDA"

        self.success_inferences   = 0
        self.total_success_inf_ms = 0
        self.failed_inferences    = 0


    def process(self, data: RequestData) -> JSON:
        try:
            img: Image = data.get_image(0)

            start_time = time.perf_counter()

            (out_img, inferenceMs) = superresolution(img)

            response = {
                "success": True,
                "imageBase64": RequestData.encode_image(out_img),
                "processMs" : int((time.perf_counter() - start_time) * 1000),
                "inferenceMs": inferenceMs
            }

        except Exception as ex:
            self.report_error(ex, __file__)
            response = {"success": False, "error": "unable to process the image"}

        self._update_statistics(response)
        return response 


    def status(self, data: RequestData = None) -> JSON:
        return { 
            "successfulInferences" : self.success_inferences,
            "failedInferences"     : self.failed_inferences,
            "numInferences"        : self.success_inferences + self.failed_inferences,
            "averageInferenceMs"   : 0 if not self.success_inferences 
                                     else self.total_success_inf_ms / self.success_inferences,
        }


    def selftest(self) -> JSON:
        
        import os
        file_name = os.path.join("test", "quail.jpg")

        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "superresolution"
        request_data.add_file(file_name)

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Super resolution test successful" }


    def _update_statistics(self, response):

        if "success" in response and response["success"]:
            self.success_inferences += 1
            if "inferenceMs" in response:
                self.total_success_inf_ms += response["inferenceMs"]
        else:
            self.failed_inferences += 1


    def _status_summary(self):
        summary  = "Inference Operations: " + str(self.success_inferences)  + "\n"
        return summary


if __name__ == "__main__":
    SuperRes_adapter().start_loop()
