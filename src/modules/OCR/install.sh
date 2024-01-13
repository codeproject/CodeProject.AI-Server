#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                           OCR
#
# This script is called from the OCR directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh script will find this install.sh file and execute it.
#
# For help with install scripts, notes on variables and methods available, tips,
# and explanations, see /src/modules/install_script_help.md

if [ "$1" != "install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

if [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ]; then

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
        if [ ! -f "${downloadDirPath}/${os}/packages/${wheel_file}" ]; then
            wget -P "${downloadDirPath}/${os}/packages/" https://github.com/Qengineering/Paddle-Raspberry-Pi/raw/main/${wheel_file}
        fi
        
        # install Paddle
        installPythonPackagesByName "${downloadDirPath}/${os}/packages/${wheel_file}" "PaddlePaddle"
    fi
    
    # clean up
    # rm "${downloadDirPath}/${os}/packages/${wheel_file}"

    # module_install_errors=...

elif [ "${systemName}" = "Jetson" ]; then
    module_install_errors="Unable to install PaddleOCR on Jetson."
fi

# libssl.so.1.1: cannot open shared object file: No such file or directory
# https://github.com/PaddlePaddle/Paddle/issues/55597
if [ "${module_install_errors}" = "" ] && [ "$os" = "linux" ] && [ "$architecture" == "x86_64" ]; then

    if [ ! -f /usr/lib/x86_64-linux-gnu/libssl.so.1.1 ] || [ ! -e /usr/lib/libcrypto.so.1.1 ]; then

        checkForAdminRights
        if [ "$isAdmin" = false ]; then
            writeLine "=========================================================" $color_info
            writeLine "We need to install some system libraries. Please run "     $color_info
            writeLine ""
            writeLine "   cd ${moduleDirPath}"                                    $color_info
            writeLine "   sudo bash ../../setup.sh"                               $color_info
            writeLine ""
            writeLine "To install this module"                                    $color_info
            writeLine "=========================================================" $color_info
            # module_install_errors="Admin permissions are needed to install libraries"

            if [ "$attemptSudoWithoutAdminRights" = true ]; then
                writeLine "We will attempt to run admin-only install commands. You may be prompted" "White" "Red"
                writeLine "for an admin password. If not then please run the script shown above."   "White" "Red"
            fi
        fi

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then

            module_install_errors=""

            echo "deb http://security.ubuntu.com/ubuntu focal-security main" | sudo tee /etc/apt/sources.list.d/focal-security.list
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

if [ "${module_install_errors}" = "" ]; then

    # Download the OCR models and store in /paddleocr
    getFromServer "paddleocr-models.zip" "paddleocr" "Downloading OCR models..."

    # TODO: Check paddleocr created and has files, maybe run paddle check too
    # module_install_errors=...
fi

