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
from module_logging import LogMethod, LogVerbosity

from llama_chat import LlamaChat

class LlamaChat_adapter(ModuleRunner):

    def initialise(self) -> None:

        self.models_dir      = ModuleOptions.getEnvVariable("CPAI_MODULE_LLAMA_MODEL_DIR",      "./models")
        
        # For loading model downloaded at install time
        self.model_filename  = ModuleOptions.getEnvVariable("CPAI_MODULE_LLAMA_MODEL_FILENAME", "mistral-7b-instruct-v0.2.Q4_K_M.gguf")

        # fallback loading (at runtime, needs internet) via llama-cpp.from_pretrained
        self.model_repo      = ModuleOptions.getEnvVariable("CPAI_MODULE_LLAMA_MODEL_REPO",     "TheBloke/Llama-2-7B-Chat-GGUF")
        self.models_fileglob = ModuleOptions.getEnvVariable("CPAI_MODULE_LLAMA_MODEL_FILEGLOB", "*.Q4_K_M.gguf")

        # llama-cpp-python packages that we are using will use GPU when it can.
        # But Llama doesn't report this, so we have to make our best guess:
        #  - on Windows and Linux, it will use CUDA 11.6+ if possible, else CPU
        #  - on macOS, "Metal" is always (sorry, sometimes) used, meaning always GPU
        # There is support for ROCm for when we add the appropriate requirements files.
        self.inference_device = "CPU"

        num_gpu_layers = -1
        if self.system_info.os == "macOS":
            if self.system_info.cpu_arch == 'arm64':
                self.inference_device  = "GPU"
                self.inference_library = "Metal"
            else:
                num_gpu_layers = 0 # There's a bug at the moment on Intel macs
        else:
            (cuda_major, cuda_minor) = self.system_info.getCudaVersion
            if cuda_major and (cuda_major > 11 or (cuda_major == 11 and cuda_minor >= 6)):
                self.inference_device  = "GPU"
                self.inference_library = "CUDA"

        verbose = self.log_verbosity != LogVerbosity.Quiet
        self.llama_chat = LlamaChat(repo_id=self.model_repo,
                                    fileglob=self.models_fileglob,
                                    filename=self.model_filename,
                                    model_dir=self.models_dir,
                                    n_ctx=0, n_gpu_layers=num_gpu_layers,
                                    verbose=verbose)
        
        if self.llama_chat.model_path:
            self.log(LogMethod.Info|LogMethod.Server, {
                "message": f"Using model from '{self.llama_chat.model_path}'",
                "loglevel": "information"
            })
        else:
            self.log(LogMethod.Error|LogMethod.Server, {
                "message": f"Unable to load Llama model",
                "loglevel": "error"
            })

        self.reply_text  = ""
        self.cancelled   = False

    def process(self, data: RequestData) -> JSON:
        return self.long_process

    def long_process(self, data: RequestData) -> JSON:

        self.reply_text = ""
        stop_reason = None

        prompt: str        = data.get_value("prompt")
        max_tokens: int    = data.get_int("max_tokens", 0) #0 means model default
        temperature: float = data.get_float("temperature", 0.4)

        try:
            start_time = time.perf_counter()

            completion = self.llama_chat.do_chat(prompt=prompt, max_tokens=max_tokens,
                                                 temperature=temperature, stream=True)
            if completion:
                try:
                    for output in completion:
                        if self.cancelled:
                            self.cancelled = False
                            stop_reason = "cancelled"
                            break

                        # Using the raw result from the llama_chat module. In
                        # building modules we don't try adn rewrite the code we
                        # are wrapping. Rather, we wrap the code so we can take
                        # advantage of updates to the original code more easily
                        # rather than having to re-apply fixes.
                        delta = output["choices"][0]["delta"]
                        if "content" in delta:
                            self.reply_text += delta["content"]
                except StopIteration:
                    pass
                
            inferenceMs : int = int((time.perf_counter() - start_time) * 1000)

            if stop_reason is None:
                stop_reason = "completed"

            response = {
                "success": True, 
                "reply": self.reply_text,
                "stop_reason": stop_reason,
                "processMs" : inferenceMs,
                "inferenceMs" : inferenceMs
            }

        except Exception as ex:
            self.report_error(ex, __file__)
            response = { "success": False, "error": "Unable to generate text" }

        return response

    def command_status(self) -> JSON:
        return {
            "success": True, 
            "reply":   self.reply_text
        }

    def cancel_command_task(self):
        self.cancelled      = True   # We will cancel this long process ourselves
        self.force_shutdown = False  # Tell ModuleRunner not to go ballistic

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

        return { "success": result['success'], "message": "LlamaChat test successful" }


if __name__ == "__main__":
    LlamaChat_adapter().start_loop()
