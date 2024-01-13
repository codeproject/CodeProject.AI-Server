#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            FaceProcessing
#
# This script is called from the Vision directory using: 
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

if [ "$systemName" = "Jetson" ]; then 

    pyNumber="${pythonVersion/./}"

    # OpenCV has to be installed system-wide (it comes preinstalled with NVIDIA developer pack
    # Ubuntu 18.04), we have to create a symbolic link from global to our virtual environment.
    # Otherwise, we won't be able to access it from our virtual environment.
    if [ ! -e "${packagesDirPath}/cv2.so" ]; then
        ln -s /usr/lib/python${pythonVersion}/dist-packages/cv2/python-${pythonVersion}/cv2.cpython-${pyNumber}m-aarch64-linux-gnu.so "${packagesDirPath}/cv2.so"
    fi
    
    installAptPackages "libfreetype6-dev python3-dev libopenblas-base libopenmpi-dev"

    # installPythonPackagesByName "pip wheel setuptools future Cython dataclasses typing-extensions"

    installAptPackages "libjpeg-dev zlib1g-dev libpython3-dev libavcodec-dev libavformat-dev libswscale-dev"
    
fi

# Download the models and store in /models
getFromServer "models.zip" "assets" "Downloading YOLO models..."

# ... also needs SQLite
if [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ] || \
   [ "${systemName}" = "Jetson" ]; then
    installAptPackages "sqlite3"
fi

# TODO: Check assets created and has files
# module_install_errors=...
