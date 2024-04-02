#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import os
import sys
import time

# Import the CodeProject.AI SDK. This will add to the PATH  for future imports
sys.path.append("../../SDK/Python")
from common import JSON
from request_data import RequestData
from module_runner import ModuleRunner

# Hack for debug mode
if not os.getenv("NLTK_DATA", ""):
    os.environ["NLTK_DATA"] = os.path.join(os.path.dirname(os.path.realpath(__file__)), "nltk_data")

# Import the method of the module we're wrapping
from summarize import Summarize

summary = Summarize()

class TextSummary(ModuleRunner):

    def initialise(self) -> None:
        pass
        
    def process(self, data: RequestData) -> JSON:
        input_text: str    = data.get_value("text")
        num_sentences: int = int(data.get_value("num_sentences"))

        try:
            start_time = time.perf_counter()

            # If we're passing a file by Id (path)
            # summaryText = summary.generate_summary_from_file(file_path, num_sentences)

            # If we're passing a file itself (generate_summary_from_textfile to be added)
            # summaryText = summary.generate_summary_from_textfile(text_file, num_sentences)

            start_time = time.perf_counter()
            summary_text: str = summary.generate_summary_from_text(input_text, num_sentences)
            inferenceMs : int = int((time.perf_counter() - start_time) * 1000)

            response = {
                "success": True, 
                "summary": summary_text,
                "processMs" : inferenceMs,
                "inferenceMs" : inferenceMs
            }

        except Exception as ex:
            self.report_error(ex, __file__)
            response = {"success": False, "error": "Unable to summarize" }

        return response
    

    def selftest(self) -> JSON:
        
        request_data = RequestData()
        request_data.queue   = self.queue_name
        request_data.command = "summarize"
        request_data.add_value("text", "This is sentence 1. This is sentence 2. This is sentence 3.")
        request_data.add_value("num_sentences", "2")

        result = self.process(request_data)
        print(f"Info: Self-test for {self.module_id}. Success: {result['success']}")
        # print(f"Info: Self-test output for {self.module_id}: {result}")

        return { "success": result['success'], "message": "Text summary test successful" }


if __name__ == "__main__":
    TextSummary().start_loop()

