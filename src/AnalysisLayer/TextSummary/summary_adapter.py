#!/usr/bin/env python
# coding: utf-8

# Import our general libraries
import os
import sys
import traceback

# Import the CodeProject.AI SDK. This will add to the PATH vaar for future imports
sys.path.append("../SDK/Python")
from common import JSON
from codeprojectai import CodeProjectAIRunner
from requestdata import AIRequestData
from analysislogging import LogMethod

# Hack for debug mode
if not os.getenv("NLTK_DATA", ""):
    os.environ["NLTK_DATA"] = os.path.join(os.path.dirname(os.path.realpath(__file__)), "nltk_data")

# Import the method of the module we're wrapping
from summarize import Summarize

summary = Summarize()

def main():

    # create a CodeProject.AI module object
    module_runner = CodeProjectAIRunner("textsummary_queue", textsummary_callback)

    # Hack for debug mode
    if module_runner.module_id == "CodeProject.AI":
        module_runner.module_id   = "TextSummary"
        module_runner.module_name = "Text Summary"

    # Start the module
    module_runner.start_loop()


def textsummary_callback(module_runner: CodeProjectAIRunner, data: AIRequestData) -> JSON:

    input_text: str    = data.get_value("text")
    num_sentences: int = int(data.get_value("num_sentences"))

    try:
        # If we're passing a file by Id (path)
        # summaryText = summary.generate_summary_from_file(file_path, num_sentences)

        # If we're passing a file itself (generate_summary_from_textfile to be added)
        # summaryText = summary.generate_summary_from_textfile(text_file, num_sentences)

        summary_text: str = summary.generate_summary_from_text(input_text, num_sentences)

        return {"success": True, "summary": summary_text}

    except Exception as ex:
        err_trace = traceback.format_exc()
        module_runner.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                          {
                              "filename": "summary_adapter.py",
                              "method": "textsummary_callback",
                              "loglevel": "error",
                              "message": ex, # err_trace,
                              "exception_type": "Exception"
                          })

        return {"success": False, "error": "Unable to summarize", "code": 500}

    finally:
        # if os.path.exists(file_path):
        #    os.remove(file_path)
        pass


if __name__ == "__main__":
    main()