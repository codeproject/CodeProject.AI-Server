#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            ObjectDetection (Fast Deploy RKNN)
#
# This script is called from the ObjectDetectionFastDeployRKNN directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh script will find this install.sh file and execute it.
#
# For help with install scripts, notes on variables and methods available, tips,
# and explanations, see /src/modules/install_script_help.md

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# FastDeploy requires a version of GCLIB higher than what's in the RPi Ubuntu OS.
# We need to work around this - except this lib is tied hard to the OS. Most likely
# solution is we use the standard FastDeploy libs for non-RockNPU hardware

if [ "${edgeDevice}" != "Orange Pi" ] && [ "${systemName}" != "Radxa ROCK" ]; then
    # writeLine "ObjectDetection (Fast Deploy RKNN) can only be installed on Orange Pi devices" "$color_error"
    module_install_errors="Unable to install on non-Rockchip hardware."
else
    # Download the models and store in /assets and /custom-models
    getFromServer "models/" "objectdetect-rknn-models.zip"        "assets" "Downloading Standard YOLOv5 RKNN models..."
    getFromServer "models/" "objectdetect-rknn-custom-models.zip" "custom-models" "Downloading Custom YOLOv5 RKNN models..."
    
    # TODO: Check assets created and has files
    # module_install_errors=...
fi
