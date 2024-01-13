#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                    YOLO Object Detection Model Training
#
# This script is called from the ObjectDetectionYolo directory using: 
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

if [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ] || [ "${systemName}" = "Jetson" ]; then
    module_install_errors="Unable to install on Pi or Jetson hardware."
fi

# Supporting libraries so the PIP installs will work
if [ "$module_install_errors" = "" ] && [ "$os" = "linux" ] && [ "$architecture" == "x86_64" ]; then

    # ensure libcurl4 is present
    installAptPackages "libcurl4"

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
            module_install_errors="Admin permissions are needed to install libraries"

            if [ "$attemptSudoWithoutAdminRights" = true ]; then
                writeLine "We will attempt to run admin-only install commands. You may be prompted" "White" "Red"
                writeLine "for an admin password. If not then please run the script shown above."   "White" "Red"
            fi
        fi

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then

            module_install_errors=""

            echo "deb http://security.ubuntu.com/ubuntu focal-security main" | sudo tee /etc/apt/sources.list.d/focal-security.list
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

if [ "$module_install_errors" = "" ]; then
    # Download the models and store in /assets and /custom-models (already in place in docker)
    getFromServer "models-yolo5-pt.zip"       "assets" "Downloading Standard YOLO models..."

    # TODO: Check assets created and has files
    # module_install_errors=...
fi