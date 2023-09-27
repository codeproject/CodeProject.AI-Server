#!/bin/bash

# Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                          YOLOv5-3.1
#
# This script is called from the YOLOv5-3.1 directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh file will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

pythonLocation="Local"
pythonVersion=3.8

if [ "$systemName" == "Jetson" ]; then pythonVersion=3.6; fi

# Install python and the required dependencies
setupPython

# Ah, Jetson. You could have been so good. Yet here we are.
# Thanks to https://www.hackster.io/spehj/deploy-yolov7-to-jetson-nano-for-object-detection-6728c3
if [ "$systemName" == "Jetson" ]; then 

    pyNumber="${pythonVersion/./}"
    bin_dir="${modulePath}/bin/${os}/python${pyNumber}/venv/bin"
    site_packages="${modulePath}/bin/${os}/python${pyNumber}/venv/lib/python${pyVersionDot}/site-packages/"

    # OpenCV has to be installed system-wide (it comes preinstalled with NVIDIA developer pack
    # Ubuntu 18.04), we have to create a symbolic link from global to our virtual environment.
    # Otherwise, we won't be able to access it from our virtual environment.
    pushd "${site_packages}" >/dev/null
    ln -s /usr/lib/python${pyVersionDot}/dist-packages/cv2/python-${pyVersionDot}/cv2.cpython-${pyNumber}m-aarch64-linux-gnu.so cv2.so
    popd >/dev/null

    sudo apt install libfreetype6-dev -y
    sudo apt-get install python3-dev -y

    sudo "${bin_dir}/python3" -m pip install --upgrade pip setuptools wheel --target "${site_packages}"
    sudo "${bin_dir}/python3" -m pip install numpy==1.19.4 --target "${site_packages}"
    sudo "${bin_dir}/python3" -m pip install matplotlib --target "${site_packages}"
fi

installPythonPackages

# Jetson, the pain continues
if [ "$systemName" == "Jetson" ]; then 
    sudo apt-get install libopenblas-base libopenmpi-dev -y
    "${bin_dir}/python3" -m pip install -U future psutil dataclasses typing-extensions pyyaml tqdm seaborn --target "${site_packages}"
    "${bin_dir}/python3" -m pip install Cython --target "${site_packages}"

    if [ ! -f torch-1.8.0-cp${pyNumber}-cp${pyNumber}m-linux_aarch64.whl ]; then
        wget https://nvidia.box.com/shared/static/p57jwntv436lfrd78inwl7iml6p13fzh.whl -O torch-1.8.0-cp${pyNumber}-cp${pyNumber}m-linux_aarch64.whl  
    fi

    "${bin_dir}/python3" -m pip install torch-1.8.0-cp${pyNumber}-cp${pyNumber}m-linux_aarch64.whl --target "${site_packages}"
    "${bin_dir}/python3" -m pip install thop --target "${site_packages}"

    # Test. Should be 1.8.0
    pushd "${bin_dir}" >/dev/null
    # This will fail with an illegal instruction. 
    ./python3 -c "import torch; print(torch.__version__)"
    popd >/dev/null

    sudo apt install libjpeg-dev zlib1g-dev libpython3-dev libavcodec-dev libavformat-dev libswscale-dev  -y
    "${bin_dir}/python3" -m pip install --upgrade pillow --target "${site_packages}"

     # Pull the code for TorchVision and build a wheel. This will take a couple of min
     # WARNING: this pulls down 655Mb
    if [ ! -d torchvision ]; then
        git clone --branch v0.9.0 https://github.com/pytorch/vision torchvision
    fi

    cd torchvision
    export BUILD_VERSION=0.9.0
    # This results in: 18382 Illegal instruction     (core dumped)
    "${bin_dir}/python3" setup.py bdist_wheel

    # Now install TorchVision
    cd dist/
    "${bin_dir}/python3" -m pip install torchvision-0.9.0-cp${pyNumber}-cp${pyNumber}m-linux_aarch64.whl --target "${site_packages}"
    cd ..

    # Now remove the branch we cloned
    cd ..
    # sudo rm -r torchvision

    # Test. Should be 0.9.0
    pushd "${bin_dir}" >/dev/null
    ./python3 -c "import torchvision; print(torchvision.__version__)"
    popd >/dev/null
fi

# Download the models and store in /assets and /custom-models
getFromServer "models-yolo5-31-pt.zip"        "assets" "Downloading Standard YOLOv5 models..."
getFromServer "custom-models-yolo5-31-pt.zip" "custom-models" "Downloading Custom YOLOv5 models..."

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