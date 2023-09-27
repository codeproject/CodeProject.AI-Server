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

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# Work needs to be done to get Paddle to install on the Raspberry Pi 
if [ "${systemName}" == "Raspberry Pi" ] || [ "${systemName}" == "Orange Pi" ] || [ "${systemName}" == "Jetson" ]; then
    writeLine 'Unable to install PaddleOCR on Raspberry Pi, Orange Pi or Jetson. Quitting.' 'Red'
else

    pythonLocation="Local"
    pythonVersion=3.8

    if [ "${systemName}" == "Raspberry Pi" ] || [ "${systemName}" == "Orange Pi" ] || [ "${systemName}" == "Jetson" ]; then
        pythonVersion=3.7
    fi

    # Install python and the required dependencies.
    setupPython
    installPythonPackages

    # remove "." from pythonVersion
    pythonDir="${pythonVersion//.}"

    if [ "${systemName}" != "Raspberry Pi" ] && [ "${systemName}" != "Orange Pi" ] && [ "${systemName}" != "Jetson" ]; then

        # We have a patch to apply for linux and macOS due to a numpy upgrade that 
        # deprecates np.int that we can't downgrade
        writeLine 'Applying PaddleOCR patch'
        cp ${modulePath}/patch/paddleocr-2.6.0.1/db_postprocess.py ${modulePath}/bin/${os}/${pythonDir}/venv/lib/${pythonVersion}/site-packages/paddleocr/ppocr/postprocess/.

    else

        # Installing PaddlePaddle: Gotta do this the hard way for RPi and Jetson.
        # Thanks to https://qengineering.eu/install-paddlepaddle-on-raspberry-pi-4.html
        # NOTE: This, so far, hasn't been working. Sorry.

        # a fresh start
        sudo apt-get update -y
        sudo apt-get upgrade -y
        # install dependencies
        sudo apt-get install cmake wget -y
        sudo apt-get install libatlas-base-dev libopenblas-dev libblas-dev -y
        sudo apt-get install liblapack-dev patchelf gfortran -y

        cd ./bin/linux/${pythonDir}/venv/bin

        # Using python -m pip instead of ./pip3 because pip3 may not be present
        # sudo -H ./pip3 install Cython
        sudo -H ./python3 -m pip install Cython

        # sudo -H ./pip3 install -U setuptools
        sudo -H ./python3 -m pip install -U setuptools

        # ./pip3 install six requests wheel pyyaml
        ./python3 -m pip install six requests wheel pyyaml

        # upgrade version 3.0.0 -> 3.13.0
        #./pip3 install -U protobuf
        ./python3 -m pip install protobuf
        
        # download the wheel
        wget https://github.com/Qengineering/Paddle-Raspberry-Pi/raw/main/paddlepaddle-2.0.0-cp37-cp37m-linux_aarch64.whl
        mv paddlepaddle-2.0.0-cp37-cp37m-linux_aarch64.whl "${absoluteRootDir}/downloads/OCR/."

        # install Paddle
        # sudo -H ./pip3 install paddlepaddle-2.0.0-cp37-cp37m-linux_aarch64.whl
        sudo -H ./python3 -m pip install "${absoluteRootDir}/downloads/OCR/paddlepaddle-2.0.0-cp37-cp37m-linux_aarch64.whl"
        if [ $? -ne 0 ]; then quit 1; fi

        # clean up
        # rm paddlepaddle-2.0.0-cp37-cp37m-linux_aarch64.whl

        popd
    fi

    # Download the OCR models and store in /paddleocr
    getFromServer "paddleocr-models.zip" "paddleocr" "Downloading OCR models..."

    module_install_success='true'

fi


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
#  systemName            - General name for the system. Linux, macOS, or "Raspberry Pi", Jetson or Docker
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