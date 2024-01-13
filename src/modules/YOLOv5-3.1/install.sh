#!/bin/bash

# Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                          YOLOv5-3.1
#
# This script is called from the YOLOv5-3.1 directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh file will find this install.sh file and execute it.
#
# For help with install scripts, notes on variables and methods available, tips,
# and explanations, see /src/modules/install_script_help.md


if [ "$1" != "install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# Ah, Jetson. You could have been so good. Yet here we are.
# Thanks to https://www.hackster.io/spehj/deploy-yolov7-to-jetson-nano-for-object-detection-6728c3
if [ "$systemName" = "Jetson" ]; then 

    pyNumber="${pythonVersion/./}"

    # OpenCV has to be installed system-wide (it comes preinstalled with NVIDIA developer pack
    # Ubuntu 18.04), we have to create a symbolic link from global to our virtual environment.
    # Otherwise, we won't be able to access it from our virtual environment.
    if [ ! -e "${packagesDirPath}/cv2.so" ]; then
        ln -s /usr/lib/python${pythonVersion}/dist-packages/cv2/python-${pythonVersion}/cv2.cpython-${pyNumber}m-aarch64-linux-gnu.so "${packagesDirPath}/cv2.so"
    fi

    # https://qengineering.eu/install-pytorch-on-jetson-nano.html
    installAptPackages "libfreetype6-dev python3-dev libjpeg-dev libomp-dev libopenblas-base libopenmpi-dev"
    installPythonPackagesByName "future wheel mock pillow testresources Cython gdown"

    mkdir -p "${downloadDirPath}/${os}/packages/"
    torch_file="torch-1.10.0a0+git36449ea-cp36-cp36m-linux_aarch64.whl"
    vision_file="torchvision-0.11.0a0+fa347eb-cp36-cp36m-linux_aarch64.whl"

    # See https://github.com/Qengineering/PyTorch-Jetson-Nano for wheels

    "$venvPythonCmdPath" -m pip show torch >/dev/null 2>/dev/null
    if [ $? -gt 0 ]; then
        if [ ! -f "${downloadDirPath}/${os}/packages/${torch_file}" ]; then
            sudo gdown https://drive.google.com/uc?id=1TqC6_2cwqiYacjoLhLgrZoap6-sVL2sd -O "${downloadDirPath}/${os}/packages/"
        fi
        installPythonPackagesByName "${downloadDirPath}/${os}/packages/${torch_file}" "torch"
    fi

    "$venvPythonCmdPath" -m pip show torchvision >/dev/null 2>/dev/null
    if [ $? -gt 0 ]; then
        if [ ! -f "${downloadDirPath}/${os}/packages/${vision_file}" ]; then
            sudo gdown https://drive.google.com/uc?id=1C7y6VSIBkmL2RQnVy8xF9cAnrrpJiJ-K -O "${downloadDirPath}/${os}/packages/"
        fi
        installPythonPackagesByName "${downloadDirPath}/${os}/packages/${vision_file}" "torchvision"
    fi

    # clean up
    # rm "${downloadDirPath}/${os}/packages/${torch_file}"
    # rm "${downloadDirPath}/${os}/packages/${vision_file}"

    installAptPackages "zlib1g-dev libpython3-dev libavcodec-dev libavformat-dev libswscale-dev"
    
fi

# Download the models and store in /assets and /custom-models
getFromServer "models-yolo5-31-pt.zip"        "assets" "Downloading Standard YOLOv5 models..."
getFromServer "custom-models-yolo5-31-pt.zip" "custom-models" "Downloading Custom YOLOv5 models..."

# TODO: Check assets created and has files
# module_install_errors=...
