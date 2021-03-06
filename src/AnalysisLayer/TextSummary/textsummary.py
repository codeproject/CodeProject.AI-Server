#!/usr/bin/env python
# coding: utf-8

import sys
sys.path.append("../SDK/Python")
from CodeProjectAI import ModuleWrapper, LogMethod # will also set the python packages path correctly

from summarize import Summarize
import json
import traceback

module = ModuleWrapper("summary_queue")

# Hack for debug mode
if module.moduleId == "CodeProject.AI":
    module.moduleId = "TextSummary";

def textsummary():

    summary = Summarize()

    while True:
        queue_entries: list = module.get_command()

        if len(queue_entries) > 0:
            timer: tuple = module.start_timer("Text Summary")

            for queue_entry in queue_entries:

                req_data: dict = json.JSONDecoder().decode(queue_entry)

                # If we're passing a file by Id (path)
                # file_id     = req_data.get("fileid", "")
                # file_path   = os.path.join(TEMP_PATH, file_id)

                # If we're passing a file itself
                # payload     = req_data.get("payload", None)
                # files       = payload.get("files", None)
                # text_file   = files[0]

                req_id: str        = req_data.get("reqid", "")
                req_type: str      = req_data.get("reqtype", "")
                req_text: str      = module.get_request_value(req_data, "text")
                num_sentences: int = int(module.get_request_value(req_data, "num_sentences"))

                output: any = {}

                try:
                    # If we're passing a file by Id (path)
                    # summaryText = summary.generate_summary_from_file(file_path, num_sentences)

                    # If we're passing a file itself (generate_summary_from_textfile to be added)
                    # summaryText = summary.generate_summary_from_textfile(text_file, num_sentences)

                    #print("Will summarize the text: ", req_text);
                    summaryText: str = summary.generate_summary_from_text(req_text, num_sentences)

                    output = {"success": True, "summary": summaryText}

                except Exception:
                    err_trace = traceback.format_exc()

                    output = {"success": False, "error": "unable to summarize", "code": 500}

                    module.log(LogMethod.Error | LogMethod.Cloud | LogMethod.Server,
                               { "process": "textsummary",
                                 "file": "textsummary.py",
                                 "method": "textsummary",
                                 "message": err_trace,
                                 "exception_type": "Exception"})

                finally:
                    module.end_timer(timer)

                    try:
                        module.send_response(req_id, json.dumps(output))
                    except Exception:
                        print("An exception occured")

                    # if os.path.exists(file_path):
                    #    os.remove(file_path)


if __name__ == "__main__":
    module.log(LogMethod.Info | LogMethod.Server, {"message":"TextSummary module started."})
    textsummary()
