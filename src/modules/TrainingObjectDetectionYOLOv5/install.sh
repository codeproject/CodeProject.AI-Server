#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                    YOLO Object Detection Model Training
#
# This script is called from the TrainingObjectDetectionYOLOv5 directory using: 
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

# This is needed. 
oneStepPIP=false

if [ "${edgeDevice}" = "Raspberry Pi" ] || [ "${edgeDevice}" = "Orange Pi" ] || 
   [ "${edgeDevice}" = "Radxa ROCK"   ] || [ "${edgeDevice}" = "Jetson"    ]; then
    module_install_errors="Unable to install on Pi, ROCK or Jetson hardware."
fi

# Supporting libraries so the PIP installs will work
if [ "$module_install_errors" = "" ] && [ "$os" = "linux" ] && [ "$architecture" == "x86_64" ]; then

    # ensure libcurl4 is present
    installAptPackages "libcurl4"

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

            # https://docs.voxel51.com/getting_started/troubleshooting.html#database-exits
            sudo ulimit -n 64000

        fi
    fi
fi
 
# PyTorch-DirectML not working for this module
# if [ "$module_install_errors" = "" ] && [ "$hasCUDA" != true ] && [ "$os" = "linux" ]; then
#    writeLine 'Installing PyTorch-DirectML...'
#    installPythonPackagesByName "torch-directml" "PyTorch DirectML plugin"
# fi

if [ "$module_install_errors" = "" ] && [ "$os" = "macos" ]; then
    write 'Installing updated setuptools in venv...' $color_primary
    "$venvPythonCmdPath" -m pip install -U setuptools >/dev/null 2>/dev/null &
    spin $!
    writeLine "done" $color_success
fi

# OpenCV needs a specific version for macOS 11
# https://github.com/opencv/opencv-python/issues/777#issuecomment-1879553756
if [ "$os_name" = "Big Sur" ]; then   # macOS 11.x on Intel, kernal 20.x
    installPythonPackagesByName "opencv-python==4.6.0.66" "OpenCV 4.6.0.66 for macOS 11.x"
fi

if [ "$module_install_errors" = "" ]; then
    # Download the models and store in /assets and /custom-models (already in place in docker)
    getFromServer "models/" "models-yolo5-pt.zip"       "assets" "Downloading Standard YOLO models..."

    # TODO: Check assets created and has files
    # module_install_errors=...
fi