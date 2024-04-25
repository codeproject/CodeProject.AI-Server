#!/bin/bash

# Post Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                          ALPR
#
# The setup.sh file will find this post_install.sh file and execute it.

if [ "$1" != "post-install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# Patch to provide a lock around object fusing
writeLine 'Applying Ultralytics patch'
cp "${moduleDirPath}/patch/ultralytics/nn/tasks.py" "${packagesDirPath}/ultralytics/nn/."
