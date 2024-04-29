#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            Object Detection (YOLOv5 6.2)
#
# This script is called from the ObjectDetectionYOLOv5-6.2 directory using: 
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

if [ "${edgeDevice}" = "Raspberry Pi" ] || [ "${edgeDevice}" = "Orange Pi" ] || 
   [ "${edgeDevice}" = "Radxa ROCK"   ] || [ "${edgeDevice}" = "Jetson"    ]; then
    module_install_errors="Unable to install on Pi, ROCK or Jetson hardware."
fi

# For Jetson, we need to install Torch before the other packages.
# A huge thanks to QEngineering: https://qengineering.eu/install-pytorch-on-jetson-nano.html
if [ "$module_install_errors" = "" ] && [ "$edgeDevice" = "Jetson" ]; then 

    # NOTE: Pytorch 2.0 and above uses CUDA 11. The Jetson Nano has CUDA 10.2.
    # Due to low-level GPU incompatibility, installing CUDA 11 on your Nano is 
    # impossible. Pytorch 2.0 can only be installed on Jetson family members using
    # a JetPack 5.0 or higher, such as the Jetson Nano Orion. Unfortunately, it
    # doesn't appear that this version will be available for the Jetson Nano soon.

    installAptPackages "python3-pip libjpeg-dev libopenblas-dev libopenmpi-dev libomp-dev"
    
    # above setuptools 58.3.0 you get version issues
    installPythonPackagesByName "future wheel mock pillow testresources setuptools==58.3.0 Cython"

    # install gdown to download from Google drive and download wheel
    if [ ! -f "${downloadDirPath}/${modulesDir}/${moduleDirName}/torch-1.10.0a0+git36449ea-cp36-cp36m-linux_aarch64.whl" ]; then
        installPythonPackagesByName "gdown"
        gdown https://drive.google.com/uc?id=1TqC6_2cwqiYacjoLhLgrZoap6-sVL2sd
        mv -f torch-1.10.0a0+git36449ea-cp36-cp36m-linux_aarch64.whl "${downloadDirPath}/${modulesDir}/${moduleDirName}/"
    fi

    # install PyTorch 1.10.0
    installPythonPackagesByName "${downloadDirPath}/${modulesDir}/${moduleDirName}/torch-1.10.0a0+git36449ea-cp36-cp36m-linux_aarch64.whl" "PyTorch"

fi

# Install drivers for non Docker images
if [ "$module_install_errors" = "" ] && [ "$inDocker" != true ] && [ "$os" = "linux" ] ; then

    echo

    # ROCm needed for linux
    # if [ "$hasROCm" = true ]; then
    #    writeLine 'Installing ROCm driver scripts...'
    #    sudo apt-get update
    #    #Ubuntu v20.04
    #    #wget https://repo.radeon.com/amdgpu-install/5.4.2/ubuntu/focal/amdgpu-install_5.4.50402-1_all.deb
    #    
    #    #Ubuntu v22.04
    #    wget  https://repo.radeon.com/amdgpu-install/5.4.2/ubuntu/jammy/amdgpu-install_5.4.50402-1_all.deb
    #
    #    sudo apt-get install ./amdgpu-install_5.4.50402-1_all.deb
    #    spin $!
    #    writeLine "done" "$color_success"
    #
    #    writeLine 'Installing ROCm drivers...'
    #    sudo amdgpu-install --usecase=dkms,graphics,multimedia,opencl,hip,hiplibsdk,rocm
    #    spin $!
    #    writeLine "done" "$color_success"
    # fi
fi

# OpenCV needs a specific version for macOS 11
# https://github.com/opencv/opencv-python/issues/777#issuecomment-1879553756
if [ "$os_name" = "Big Sur" ]; then   # macOS 11.x on Intel, kernal 20.x
    installPythonPackagesByName "opencv-python==4.6.0.66" "OpenCV 4.6.0.66 for macOS 11.x"
fi

# Download the models and store in /assets and /custom-models (already in place in docker)
if [ "$module_install_errors" = "" ]; then
    getFromServer "models/" "models-yolo5-pt.zip"        "assets" "Downloading Standard YOLO models..."
    getFromServer "models/" "custom-models-yolo5-pt.zip" "custom-models" "Downloading Custom YOLO models..."
fi

# TODO: Check assets created and has files
# module_install_errors=...
