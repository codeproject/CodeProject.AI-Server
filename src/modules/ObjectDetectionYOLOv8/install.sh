#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            Object Detection (YOLOv8)
#
# This script is called from the ObjectDetectionYOLOv8 directory using: 
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

# For Jetson, we need to install Torch before the other packages.
# A huge thanks to QEngineering: https://qengineering.eu/install-pytorch-on-jetson-nano.html
if [ "$module_install_errors" = "" ] && [ "$edgeDevice" = "Jetson" ]; then 

    # NOTE: Pytorch 2.0 and above uses CUDA 11. The Jetson Nano has CUDA 10.2.
    # Due to low-level GPU incompatibility, installing CUDA 11 on your Nano is 
    # impossible. Pytorch 2.0 can only be installed on Jetson family members using
    # a JetPack 5.0 or higher, such as the Jetson Nano Orion. Unfortunately, it
    # doesn't appear that this version will be available for the Jetson Nano soon.

    # Dependencies (Libraries)
    installAptPackages "python3-pip libjpeg-dev libopenblas-dev libopenmpi-dev libomp-dev zlib1g-dev"
    
    # Dependencies (Python packages). Above setuptools 58.3.0 you get version issues
    installPythonPackagesByName "future wheel mock testresources setuptools==58.3.0 Cython"

    # Ultralytics will install everything. Usually. Sometimes...
    install_torch=true
    use_NVIDIA=true

    if [ "$use_NVIDIA" == true ]; then
        torch_version="1.11"
        torchvision_version="0.12"
    else
        distribution=$(. /etc/os-release;echo $VERSION_ID)
        if [ "$pythonVersion" == "3.6" ] || [ "$pythonVersion" == "3.7" ] || [ "$distribution" == "18.04" ]; then
            torch_version="1.10"
            torchvision_version="0.11"
        else
            torch_version="1.13"
            torchvision_version="0.14"
        fi
    fi
    
    if [ "$install_torch" = true ]; then

        if [ "$use_NVIDIA" == true ]; then
            # ------------------------------------------------------------------
            # NVIDIA versions:

            if [ "$torch_version" == "1.10" ]; then
                # Torch 1.10: Ubuntu 18.04+ / Python 3.6
                torch_location="https://nvidia.box.com/shared/static/fjtbno0vpo676a25cgvuqc1wty0fkkg6.whl"
                torch_wheel="torch-1.10.0-cp36-cp36m-linux_aarch64.whl"
                torch_vision="torchvision==0.11.0+cu102 -f https://download.pytorch.org/whl/torch_stable.html"
            else            
                # Torch 1.11: Ubuntu 18.04+ / Python 3.6
                torch_location="https://developer.download.nvidia.com/compute/redist/jp/v461/pytorch/torch-1.11.0a0+17540c5+nv22.01-cp36-cp36m-linux_aarch64.whl"
                torch_wheel="torch-1.11.0a0+17540c5+nv22.01-cp36-cp36m-linux_aarch64.whl"
                torch_vision="torchvision==0.12.0+cu102 --extra-index-url https://download.pytorch.org/whl/cu102"
            fi
        else
            # ------------------------------------------------------------------
            # QEngineering versions (https://qengineering.eu/install-pytorch-on-jetson-nano.html)

            if [ "$torch_version" == "1.10" ]; then
                # Torch 1.10: Ubuntu 18.04+ / Python 3.6
                torch_location="https://drive.google.com/uc?id=1TqC6_2cwqiYacjoLhLgrZoap6-sVL2sd"
                torch_wheel="torch-1.10.0a0+git36449ea-cp36-cp36m-linux_aarch64.whl"
                torchvision_location="https://drive.google.com/uc?id=1C7y6VSIBkmL2RQnVy8xF9cAnrrpJiJ-K"
                torchvision_wheel="torchvision-0.11.0a0+fa347eb-cp36-cp36m-linux_aarch64.whl"
            elif [ "$torch_version" == "1.11" ]; then
                # Torch 1.11: Needs Ubuntu 20.04 / Python 3.8
                torch_location="https://drive.google.com/uc?id=1AQQuBS9skNk1mgZXMp0FmTIwjuxc81WY"
                torch_wheel="torch-1.11.0a0+gitbc2c6ed-cp38-cp38-linux_aarch64.whl"
                torchvision_location="https://drive.google.com/uc?id=1BaBhpAizP33SV_34-l3es9MOEFhhS1i2"
                torchvision_wheel="torchvision-0.12.0a0+9b5a3fe-cp38-cp38-linux_aarch64.whl"
            elif [ "$torch_version" == "1.12" ]; then
                # Torch 1.12: Needs Ubuntu 20.04 / Python 3.8
                torch_location="https://drive.google.com/uc?id=1MnVB7I4N8iVDAkogJO76CiQ2KRbyXH_e"
                torch_wheel="torch-1.12.0a0+git67ece03-cp38-cp38-linux_aarch64.whl"
                torchvision_location="https://drive.google.com/uc?id=11DPKcWzLjZa5kRXRodRJ3t9md0EMydhj"
                torchvision_wheel="torchvision-0.13.0a0+da3794e-cp38-cp38-linux_aarch64.whl"
            elif [ "$torch_version" == "1.13" ]; then
                # Torch 1.13: Needs Ubuntu 20.04 / Python 3.8
                torch_location="https://drive.google.com/uc?id=1e9FDGt2zGS5C5Pms7wzHYRb0HuupngK1"
                torch_wheel="torch-1.13.0a0+git7c98e70-cp38-cp38-linux_aarch64.whl"
                torchvision_location="https://drive.google.com/uc?id=19UbYsKHhKnyeJ12VPUwcSvoxJaX7jQZ2"
                torchvision_wheel="torchvision-0.14.0a0+5ce4506-cp38-cp38-linux_aarch64.whl"
            fi
        fi

        "$venvPythonCmdPath" -m pip show torch >/dev/null 2>/dev/null
        if [ $? -eq 0 ]; then
            writeLine "Torch aleady installed" $color_info
        else
            if [ ! -f "${downloadDirPath}/${os}/packages/${torch_wheel}" ]; then
                if [ ! -d "${downloadDirPath}/${os}/packages" ]; then
                    mkdir -p "${downloadDirPath}/${os}/packages"
                fi

                if [ "$use_NVIDIA" == true ]; then
                    wget $torch_location -O "${downloadDirPath}/${os}/packages/${torch_wheel}"
                else
                    gdown $torch_location
                    mv -f $torch_wheel "${downloadDirPath}/${os}/packages/"
                fi
            fi
    
             # install PyTorch 
            cp "${downloadDirPath}/${os}/packages/${torch_wheel}" .
            installPythonPackagesByName "${torch_wheel}" "PyTorch"
            rm "${torch_wheel}"
        fi

        "$venvPythonCmdPath" -m pip show torchvision >/dev/null 2>/dev/null
        if [ $? -eq 0 ]; then
            writeLine "TorchVision aleady installed" $color_info
        else       

            # install TorchVision

            if [ "$use_NVIDIA" == true ]; then
                installPythonPackagesByName "${torch_vision}" "TorchVision"
            else
                if [ ! -f "${downloadDirPath}/${os}/packages/${torchvision_wheel}" ]; then
                    if [ ! -d "${downloadDirPath}/${os}/packages" ]; then
                        mkdir -p "${downloadDirPath}/${os}/packages"
                    fi
                    gdown $torchvision_location
                    mv -f $torchvision_wheel "${downloadDirPath}/${os}/packages/"
                fi

                cp "${downloadDirPath}/${os}/packages/${torchvision_wheel}" .
                installPythonPackagesByName "${torchvision_wheel}" "TorchVision"
                rm "${torchvision_wheel}"
            fi

            # If we wish to build from source:
            if [ "$build_torchvision_from_source" == true ]; then
                installAptPackages "gcc python${pythonVersion}-dev"

                # Except git is occasionally broken on Jetson
                # mkdir torchvision && cd torchvision
                # git clone https://github.com/pytorch/vision .
                # git checkout v${torchvision_version}.0
                
                getFromServer "libraries/" "torchvision-${torchvision_version}.0.zip" "torchvision" "Downloading TorchVision to ${moduleDirPath}"

                pushd "${moduleDirPath}/torchvision"
                "$venvPythonCmdPath" setup.py install --user 
                popd
            fi
        fi

        # This is a hammer that's sometimes needed to allow the ultralytics package to be installed
        sudo apt-get install python${pythonVersion}-dev -y

        # NEXT STEP: Use TensorRT to Improve Inference Speed
        # https://wiki.seeedstudio.com/YOLOv8-TRT-Jetson/#use-tensorrt-to-improve-inference-speed

        getFromServer "libraries/" "ultralytics-8.0.225.zip" "ultralytics" "Downloading Ultralytics..."
        
    fi
fi

# OpenCV needs a specific version for macOS 11
# https://github.com/opencv/opencv-python/issues/777#issuecomment-1879553756
if [ "$os_name" = "Big Sur" ]; then   # macOS 11.x on Intel, kernal 20.x
    installPythonPackagesByName "opencv-python==4.6.0.66" "OpenCV 4.6.0.66 for macOS 11.x"
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

# Download the models and store in /assets and /custom-models (already in place in docker)
# if [ "$module_install_errors" = "" ]; then
#     getFromServer "models/" "models-yolo8-pt.zip"                     "assets" "Downloading YOLO object detection models..."
#     getFromServer "models/" "objectsegmentation-coco-yolov8-pt-m.zip" "assets" "Downloading YOLO segmentation models..."
#
#     getFromServer "models/" "objectdetection-custom-yolov8-pt-m.zip" "custom-models" "Downloading Custom YOLO models..."
# fi

# TODO: Check assets created and has files
# module_install_errors=...
