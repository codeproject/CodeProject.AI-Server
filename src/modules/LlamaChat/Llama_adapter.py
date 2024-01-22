#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH  for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner
from module_options import ModuleOptions
from module_logging import LogVerbosity

# Import the method of the module we're wrapping
from Llama_chat import init_chat, do_chat, do_completion

class LLama_Adapter(ModuleRunner):

    def initialise(self) -> None:
        
        verbose = self.log_verbosity != LogVerbosity.Quiet
        init_chat(model_path="./models/codellama-7b.Q4_K_M.gguf", n_ctx=512, verbose=verbose)

        self.success_inferences   = 0
        self.total_success_inf_ms = 0
        self.failed_inferences    = 0


    def process(self, data: RequestData) -> JSON:
        
        prompt: str        = data.get_value("prompt")
        max_tokens: int    = data.get_int("max_tokens", 256)
        temperature: float = data.get_float("temperature", 0.4)

        try:
            start_time = time.perf_counter()

            prompt = "Q: " + prompt + " A: "
            reply_text: str = do_completion(prompt=prompt, temperature=temperature, max_tokens=max_tokens,
                                            stream=False, stop=[ "\n", "Q:" ])
            # reply_text: str = do_chat(prompt=prompt, temperature=temperature, max_tokens=max_tokens,
            #                          stream=False,  stop=None)

            inferenceMs : int = int((time.perf_counter() - start_time) * 1000)

            print("Llama response is " + reply_text)

            response = {
                "success": True, 
                "reply": reply_text,
                "processMs" : inferenceMs,
                "inferenceMs" : inferenceMs
            }

        except Exception as ex:
            self.report_error(ex, __file__)
            response = { "success": False, "error": "Unable to generate text" }

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
        
        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "prompt"

        request_data.add_value("prompt", "How many planets are there in the solar system?")
        request_data.add_value("max_tokens", 256)
        request_data.add_value("temperature", 0.4)

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Face detection test successful" }


    def _update_statistics(self, response):   
        if "success" in response and response["success"]:
            self.success_inferences += 1
            if "inferenceMs" in response:
                self.total_success_inf_ms += response["inferenceMs"]
        else:
            self.failed_inferences += 1


if __name__ == "__main__":
    LLama_Adapter().start_loop()

