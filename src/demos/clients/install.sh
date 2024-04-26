#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            CodeProject.AI Demos
#
# This script is called from the Demos directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh script will find this install.sh file and execute it.
#
# For help with install scripts, notes on variables and methods available, tips,
# and explanations, see /src/modules/install_script_help.md

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../src/setup.sh"
    echo
    exit 1 
fi

runtimeLocation="Shared"
pythonVersion=3.9
if [ "${edgeDevice}" == "Jetson" ]; then pythonVersion=3.8; fi

setupPythonPaths "$runtimeLocation" "$pythonVersion"

# Install python and the required dependencies.
setupPython 

# OpenCV needs a specific version for macOS 11
# https://github.com/opencv/opencv-python/issues/777#issuecomment-1879553756
if [ "$os_name" = "Big Sur" ]; then   # macOS 11.x on Intel, kernal 20.x
    installPythonPackagesByName "opencv-python==4.6.0.66" "OpenCV 4.6.0.66 for macOS 11.x"
fi

installRequiredPythonPackages "${moduleDirPath}/Python" 