#!/bin/bash

# Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                          Object Detection (YOLOv5 3.1)
#
# This script is called from the ObjectDetectionYOLOv5-3.1 directory using: 
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

# OpenCV needs a specific version for macOS 11
# https://github.com/opencv/opencv-python/issues/777#issuecomment-1879553756
if [ "$os_name" = "Big Sur" ]; then   # macOS 11.x on Intel, kernal 20.x
    installPythonPackagesByName "opencv-python==4.6.0.66" "OpenCV 4.6.0.66 for macOS 11.x"
fi

# Ah, Jetson. You could have been so good. Yet here we are.
# Thanks to https://www.hackster.io/spehj/deploy-yolov7-to-jetson-nano-for-object-detection-6728c3
if [ "$edgeDevice" = "Jetson" ]; then 

    pyNumber="${pythonVersion/./}"

    # OpenCV has to be installed system-wide (it comes preinstalled with NVIDIA developer pack
    # Ubuntu 18.04), we have to create a symbolic link from global to our virtual environment.
    # Otherwise, we won't be able to access it from our virtual environment.
    if [ ! -e "${packagesDirPath}cv2.so" ]; then
        ln -s /usr/lib/python${pythonVersion}/dist-packages/cv2/python-${pythonVersion}/cv2.cpython-${pyNumber}m-aarch64-linux-gnu.so "${packagesDirPath}cv2.so"
    fi

    if [ -d "/usr/local/lib/python3.8/dist-packages/torch/" ]; then
        writeLine "PyTorch is already installed." $color_info
    else
        # https://qengineering.eu/install-pytorch-on-jetson-nano.html
        installAptPackages "libfreetype6-dev python3-dev libjpeg-dev libomp-dev libopenblas-base libopenmpi-dev"
        installPythonPackagesByName "future wheel mock pillow testresources Cython gdown"

        mkdir -p "${downloadDirPath}/${os}/packages/"

        torch_version="1.11"
        case "$torch_version" in
            "1.10") 
                torch_file="torch-1.10.0a0+git36449ea-cp36-cp36m-linux_aarch64.whl"
                torch_id="1TqC6_2cwqiYacjoLhLgrZoap6-sVL2sd"
                vision_file="torchvision-0.11.0a0+fa347eb-cp36-cp36m-linux_aarch64.whl"
                vision_id="1C7y6VSIBkmL2RQnVy8xF9cAnrrpJiJ-K"
            ;;
            "1.11") 
                torch_file="torch-1.11.0a0+gitbc2c6ed-cp38-cp38-linux_aarch64.whl"
                torch_id="1AQQuBS9skNk1mgZXMp0FmTIwjuxc81WY"
                vision_file="torchvision-0.12.0a0+9b5a3fe-cp38-cp38-linux_aarch64.whl"
                vision_id="1BaBhpAizP33SV_34-l3es9MOEFhhS1i2"
            ;;
            "1.12") 
                torch_file="torch-1.12.0a0+git67ece03-cp38-cp38-linux_aarch64.whl"
                torch_id="1MnVB7I4N8iVDAkogJO76CiQ2KRbyXH_e"
                vision_file="torchvision-0.13.0a0+da3794e-cp38-cp38-linux_aarch64.whl"
                vision_id="11DPKcWzLjZa5kRXRodRJ3t9md0EMydhj"
            ;;
            "1.13") 
                torch_file="torch-1.13.0a0+git7c98e70-cp38-cp38-linux_aarch64.whl"
                torch_id="1e9FDGt2zGS5C5Pms7wzHYRb0HuupngK1"
                vision_file="torchvision-0.14.0a0+5ce4506-cp38-cp38-linux_aarch64.whl"
                vision_id="19UbYsKHhKnyeJ12VPUwcSvoxJaX7jQZ2"
            ;;
        esac

        # See https://github.com/Qengineering/PyTorch-Jetson-Nano for wheels

        "$venvPythonCmdPath" -m pip show torch >/dev/null 2>/dev/null
        if [ $? -gt 0 ]; then
            if [ ! -f "${downloadDirPath}/${os}/packages/${torch_file}" ]; then
                sudo gdown https://drive.google.com/uc?id=${torch_id} -O "${downloadDirPath}/${os}/packages/"
            fi
            installPythonPackagesByName "${downloadDirPath}/${os}/packages/${torch_file}" "torch"
        fi

        "$venvPythonCmdPath" -m pip show torchvision >/dev/null 2>/dev/null
        if [ $? -gt 0 ]; then
            if [ ! -f "${downloadDirPath}/${os}/packages/${vision_file}" ]; then
                sudo gdown https://drive.google.com/uc?id=${vision_id} -O "${downloadDirPath}/${os}/packages/"
            fi
            installPythonPackagesByName "${downloadDirPath}/${os}/packages/${vision_file}" "torchvision"
        fi

        installAptPackages "zlib1g-dev libpython3-dev libavcodec-dev libavformat-dev libswscale-dev"
    fi
    
fi

# Download the models and store in /assets and /custom-models
getFromServer "models/" "models-yolo5-31-pt.zip"        "assets" "Downloading Standard YOLOv5 models..."
getFromServer "models/" "custom-models-yolo5-31-pt.zip" "custom-models" "Downloading Custom YOLOv5 models..."

# TODO: Check assets created and has files
# module_install_errors=...
