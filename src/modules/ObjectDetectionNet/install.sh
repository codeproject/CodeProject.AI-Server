#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            .NET YOLO Object Detection
#
# This script is called from the ObjectDetectionNet directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh script will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# Pull down the correct .NET image of ObjectDetectionNet based on this OS / GPU combo
if [ "${executionEnvironment}" == "Production" ]; then
    imageName="ObjectDetectionNet-CPU-${moduleVersion}.zip"
    if [ "${enableGPU}" == "true" ] && [ "${os}" != "macos" ]; then
        imageName="ObjectDetectionNet-OpenVINO-${moduleVersion}.zip"
        if [ "${supportCUDA}" == "true" ] && [ "${hasCUDA}" == "true" ]; then
            imageName="ObjectDetectionNet-CUDA-${moduleVersion}.zip"
        fi
    fi
    getFromServer "${imageName}" "" "Downloading ${imageName}..."
else
    getFromServer "ObjectDetectionNetNugets.zip" "LocalNugets" "Downloading Nuget packages..."
fi

# Download the models and store in /assets
getFromServer "yolonet-models.zip" "assets" "Downloading YOLO ONNX models..."
if [ $? -ne 0 ]; then quit 1; fi
getFromServer "yolonet-custom-models.zip" "custom-models" "Downloading Custom YOLO ONNX models..."

if [ "${platform}" == "macos" ]; then
    brew install onnxruntime
elif [ "${platform}" == "macos-arm64" ]; then
    # We need to ensure we use the correct brew on Apple Silicon
    /opt/homebrew/bin/brew install onnxruntime
fi

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