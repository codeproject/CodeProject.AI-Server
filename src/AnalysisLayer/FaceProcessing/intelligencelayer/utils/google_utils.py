# This file contains google utils: https://cloud.google.com/storage/docs/reference/libraries
# pip install --upgrade google-cloud-storage
# from google.cloud import storage

import subprocess

def gsutil_getsize(url=""):
    # gs://bucket/file size https://cloud.google.com/storage/docs/gsutil/commands/du
    s = subprocess.check_output("gsutil du %s" % url, shell=True).decode("utf-8")
    return eval(s.split(" ")[0]) if len(s) else 0  # bytes