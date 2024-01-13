#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                           OCR
#
# The setup.sh script will find this post_install.sh file and execute it.

if [ "$1" != "post-install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# We have a patch to apply for Raspberry Pi due to a numpy upgrade that 
# deprecates np.int that we can't downgrade
if [ "${systemName}" = "Raspberry Pi" ]; then

    writeLine 'Applying PaddleOCR patch'

    # remove "." from pythonVersion
    pythonNum="${pythonVersion//.}"
    cp "${moduleDirPath}/patch/paddleocr-2.6.0.1/db_postprocess.py" "${packagesDirPath}/paddleocr/ppocr/postprocess/."
fi