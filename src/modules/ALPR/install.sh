#!/bin/bash

# Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                          ALPR
#
# This script is called from the ALPR directory using: 
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

if [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ] || \
   [ "${systemName}" = "Radxa ROCK"   ]; then

    installPythonPackagesByName "opencv-python>=4.2.0" "OpenCV, the Computer Vision library for Python"

    # a fresh start
    write "Updating apt-get..."
    sudo apt-get update -y >/dev/null 2>/dev/null
    sudo apt-get upgrade -y >/dev/null 2>/dev/null
    writeLine "Done" "$color_success"

    # install dependencies
    installAptPackages "cmake"
    installAptPackages "libatlas-base-dev libopenblas-dev libblas-dev"
    installAptPackages "liblapack-dev patchelf gfortran"

    installPythonPackagesByName "Cython protobuf<=3.20 six requests wheel pyyaml"

    "$venvPythonCmdPath" -m pip show paddlepaddle >/dev/null 2>/dev/null
    if [ $? -eq 0 ]; then
        writeLine "PaddlePaddle already installed" "$color_success"
    else
        # download the wheel
        wheel_file="paddlepaddle-2.4.2-cp39-cp39-linux_aarch64.whl"
        # wheel_file="paddlepaddle-2.5.1-cp310-cp310-linux_aarch64.whl"
        if [ ! -f "${downloadDirPath}/${os}/packages/${wheel_file}" ]; then
            wget -P "${downloadDirPath}/${os}/packages/" https://github.com/Qengineering/Paddle-Raspberry-Pi/raw/main/${wheel_file}
        fi
        
        # install Paddle
        installPythonPackagesByName "${downloadDirPath}/${os}/packages/${wheel_file}" "PaddlePaddle"
    fi
    
    # clean up
    # rm "${downloadDirPath}/${os}/packages/${wheel_file}"

    # module_install_errors= ...

elif [ "${systemName}" = "Jetson" ]; then

    case "$pythonVersion" in
        "3.6") 
            wheel_file="paddlepaddle_gpu-2.4.1-cp36-cp36m-linux_aarch64.whl"
            wheel_path="https://paddle-inference-lib.bj.bcebos.com/2.4.1/python/Jetson/jetpack4.5_gcc7.5/all/" ;;
        "3.7") 
            wheel_file="paddlepaddle_gpu-2.4.1-cp37-cp37m-linux_aarch64.whl"
            wheel_path="https://paddle-inference-lib.bj.bcebos.com/2.4.1/python/Jetson/jetpack4.6.1_gcc7.5/all/" ;;
        "3.8") 
            wheel_file="paddlepaddle_gpu-2.4.1-cp38-cp38-linux_aarch64.whl"
            wheel_path="https://paddle-inference-lib.bj.bcebos.com/2.4.1/python/Jetson/jetpack5.0.2_gcc9.4/all/" ;;
        *) 
            wheel_file="paddlepaddle_gpu-2.4.1-cp36-cp36m-linux_aarch64.whl"
            wheel_path="https://paddle-inference-lib.bj.bcebos.com/2.4.1/python/Jetson/jetpack4.5_gcc7.5/all/" ;;
    esac

    "$venvPythonCmdPath" -m pip show paddlepaddle >/dev/null 2>/dev/null
    if [ $? -eq 0 ]; then
        writeLine "PaddlePaddle already installed" "$color_success"
    else
        # download the wheel
        if [ ! -f "${downloadDirPath}/${os}/packages/${wheel_file}" ]; then
            wget -P "${downloadDirPath}/${os}/packages/" ${wheel_path}${wheel_file}
        fi
        
        # install Paddle
        installPythonPackagesByName "${downloadDirPath}/${os}/packages/${wheel_file}" "PaddlePaddle"
    fi
    
    # clean up
    # rm "${downloadDirPath}/${os}/packages/${wheel_file}"

fi

# libssl.so.1.1: cannot open shared object file: No such file or directory
# https://github.com/PaddlePaddle/Paddle/issues/55597
if [ "${module_install_errors}" = "" ] && [ "$os" = "linux" ] && [ "$architecture" == "x86_64" ]; then

    if [ ! -f /usr/lib/x86_64-linux-gnu/libssl.so.1.1 ] || [ ! -e /usr/lib/libcrypto.so.1.1 ]; then

        # output a warning message if no admin rights and instruct user on manual steps
        install_instructions="cd ${moduleDirPath}${newline}sudo bash ../../setup.sh"
        checkForAdminAndWarn "$install_instructions"

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then

            module_install_errors=""

            if [ "$os_name" != "debian" ]; then
                echo "deb http://security.ubuntu.com/ubuntu focal-security main" | sudo tee /etc/apt/sources.list.d/focal-security.list
            fi
            installAptPackages "libssl1.1"

            write "Ensuring symlinks are created..." $color_info

            # LIBSSL: Add link at /usr/lib/libssl.so.1.1 that points to /lib/x86_64-linux-gnu/libssl.so.1.1
            if [ -f /lib/x86_64-linux-gnu/libssl.so.1.1 ] && [ ! -e /usr/lib/libssl.so.1.1 ]; then
                if [ "${verbosity}" = "loud" ]; then
                    sudo ln -s /lib/x86_64-linux-gnu/libssl.so.1.1 /usr/lib/libssl.so.1.1
                else
                    sudo ln -s /lib/x86_64-linux-gnu/libssl.so.1.1 /usr/lib/libssl.so.1.1 >/dev/null 2>/dev/null
                fi
            fi

            # LIBRYPTO: Add link at /usr/lib/libcrypto.so.1.1 that points to /lib/x86_64-linux-gnu/libcrypto.so.1.1
            if [ -f /lib/x86_64-linux-gnu/libcrypto.so.1.1 ] && [ ! -e /usr/lib/libcrypto.so.1.1 ]; then
                if [ "${verbosity}" = "loud" ]; then
                    sudo ln -s /lib/x86_64-linux-gnu/libcrypto.so.1.1 /usr/lib/libcrypto.so.1.1
                else
                    sudo ln -s /lib/x86_64-linux-gnu/libcrypto.so.1.1 /usr/lib/libcrypto.so.1.1 >/dev/null 2>/dev/null
                fi
            fi

            writeLine "Done" $color_success
        fi
    fi
fi

if [ "$os" = "macos" ]; then
    if [ "$os_name" = "Big Sur" ]; then   # macOS 11.x on Intel, kernal 20.x
        # https://github.com/opencv/opencv-python/issues/777
        installPythonPackagesByName "opencv-python==4.6.0.66" "OpenCV 4.8, the Computer Vision library for Python"
    elif [ "$arcbhitecture" == "arm64" ]; then
        installPythonPackagesByName "opencv-python==4.5.5.64" "OpenCV 4.5, the Computer Vision library for Python"
    else
        installPythonPackagesByName "opencv-python"           "OpenCV, the Computer Vision library for Python"
    fi
fi

if [ "${module_install_errors}" = "" ]; then

    # Download the OCR models and store in /paddleocr
    getFromServer "models/" "ocr-en-pp_ocrv4-paddle.zip" "paddleocr" "Downloading OCR models..."

    # TODO: Check paddleocr created and has files, maybe run paddle check too
    # module_install_errors=...
fi
