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

if [ "${os}" = "macos" ]; then
    # We have a patch to apply for macOS due to a numpy upgrade that deprecates
    # np.int that we can't downgrade
    writeLine 'Applying PaddleOCR patch'
    cp "${moduleDirPath}/patch/paddleocr-2.6.0.1/db_postprocess.py" "${packagesDirPath}/paddleocr/ppocr/postprocess/."
else
    # We have a patch to apply for everyone else due to a bad truthy test on a
    # multi-dimensional array in paddleocr 2.7.0.3
    writeLine 'Applying PaddleOCR patch'
    cp "${moduleDirPath}/patch/paddleocr-2.7.0.3/paddleocr.py" "${packagesDirPath}/paddleocr/."
fi
