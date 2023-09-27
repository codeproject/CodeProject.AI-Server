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

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

verbosity="info"
# This is needed. It should be the default for linux anyway, but just in case, force it
oneStepPIP="false"

pythonLocation="Local"
pythonVersion=3.8

# Install python
setupPython

# Supporting libraries so the PIP installs will work
if [ "$os" == "linux" ]; then

    # ensure libcurl4 is present
    write 'Ensuring libcurl4 present...'
    libcurl4_present==$(dpkg-query -W --showformat='${Status}\n' libcurl4|grep "install ok installed")
    if [ "${libcurl4_present}" == "" ]; then   
        sudo apt install libcurl4 -y >/dev/null &
        spin $1
    fi
    writeLine "Done" $color_success

    # fiftyone on linux hardwires an ancient version of mongod that depends on the ancient
    # libssl1.1. This is 2 major versions old. Well done. 
    if [ ! -L /usr/lib/libcrypto.so.1.1 ] && [ ! -f /usr/lib/libcrypto.so.1.1 ]; then

        write 'Downloading ancient SSL libraries for ancient MongoDB...'
        sudo wget http://security.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2.19_amd64.deb \
              -O libssl1.1_1.1.1f-1ubuntu2.19_amd64.deb >/dev/null
        sudo dpkg -i libssl1.1_1.1.1f-1ubuntu2.19_amd64.deb >/dev/null
        sudo rm libssl1.1_1.1.1f-1ubuntu2.19_amd64.deb >/dev/null
        writeLine "Done" $color_success

        write 'Installing ancient SSL libraries for ancient MongoDB...'

        # Install
        if [ "${inDocker}" == "true" ]; then
            sudo apt update && sudo apt install libssl1.1 -y  >/dev/null
        else
            sudo apt update && sudo apt install libssl1.1.1 -y  >/dev/null
        fi

        # Create symlinks
        if [ ! -e /usr/lib/libcrypto.so.1.1 ]; then
            # Add link at /usr/lib/libcrypto.so.1.1 that points to /lib/x86_64-linux-gnu
            sudo ln -s /lib/x86_64-linux-gnu/libcrypto.so.1.1 /usr/lib/libcrypto.so.1.1  >/dev/null
        fi
        if [ ! -e /usr/lib/libssl.so.1.1 ]; then
            # Add link at /usr/lib/libssl.so.1.1 that points to /lib/x86_64-linux-gnu
            sudo ln -s /lib/x86_64-linux-gnu/libssl.so.1.1 /usr/lib/libssl.so.1.1  >/dev/null
        fi
        writeLine "Done" $color_success

    fi

    # https://docs.voxel51.com/getting_started/troubleshooting.html#database-exits
    ulimit -n 64000
fi

installPythonPackages

# PyTorch-DirectML not working for this module
# if [ "$hasCUDA" != "true" ] && [ "$os" == "linux" ]; then
#    writeLine 'Installing PyTorch-DirectML...'
#    installSinglePythonPackage "torch-directml" "PyTorch DirectML plugin"
# fi

# Download the models and store in /assets and /custom-models (already in place in docker)
getFromServer "models-yolo5-pt.zip"       "assets" "Downloading Standard YOLO models..."

module_install_success='true'


#                         -- Install script cheatsheet -- 
#
# Variables available:
#
#  absoluteRootDir       - the root path of the installation (eg: ~/CodeProject/AI)
#  sdkScriptsPath        - the path to the installation utility scripts ($rootPath/SDK/Scripts)
#  downloadPath          - the path to where downloads will be stored ($sdkScriptsPath/downloads)
#  runtimesPath          - the path to the installed runtimes ($rootPath/src/runtimes)
#  modulesPath           - the path to all the AI modules ($rootPath/src/modules)
#  moduleDir             - the name of the directory containing this module
#  modulePath            - the path to this module ($modulesPath/$moduleDir)
#  os                    - "linux" or "macos"
#  architecture          - "x86_64" or "arm64"
#  platform              - "linux", "linux-arm64", "macos" or "macos-arm64"
#  systemName            - General name for the system. "Linux", "macOS", "Raspberry Pi", "Orange Pi"
#                          "Jetson" or "Docker"
#  verbosity             - quiet, info or loud. Use this to determines the noise level of output.
#  forceOverwrite        - if true then ensure you force a re-download and re-copy of downloads.
#                          getFromServer will honour this value. Do it yourself for downloadAndExtract 
#
# Methods available
#
#  write     text [foreground [background]] (eg write "Hi" "green")
#  writeLine text [foreground [background]]
#  Download  storageUrl downloadPath filename moduleDir message
#        storageUrl    - Url that holds the compressed archive to Download
#        downloadPath  - Path to where the downloaded compressed archive should be downloaded
#        filename      - Name of the compressed archive to be downloaded
#        dirNameToSave - name of directory, relative to downloadPath, where contents of archive 
#                        will be extracted and saved
#
#  getFromServer filename moduleAssetDir message
#        filename       - Name of the compressed archive to be downloaded
#        moduleAssetDir - Name of folder in module's directory where archive will be extracted
#        message        - Message to display during download
#
#  downloadAndExtract  storageUrl filename downloadPath dirNameToSave message
#        storageUrl    - Url that holds the compressed archive to Download
#        filename      - Name of the compressed archive to be downloaded
#        downloadPath  - Path to where the downloaded compressed archive should be downloaded
#        dirNameToSave - name of directory, relative to downloadPath, where contents of archive 
#                        will be extracted and saved
#        message       - Message to display during download
#
#  setupPython Version [install-location]
#       Version - version number of python to setup. 3.8 and 3.9 currently supported. A virtual
#                 environment will be created in the module's local folder if install-location is
#                 "Local", otherwise in $runtimesPath/bin/$platform/python<version>/venv.
#       install-location - [optional] "Local" or "Shared" (see above)
#
#  installPythonPackages Version requirements-file-directory
#       Version - version number, as per SetupPython
#       requirements-file-directory - directory containing the requirements.txt file
#       install-location - [optional] "Local" (installed in the module's local venv) or 
#                          "Shared" (installed in the shared $runtimesPath/bin venv folder)