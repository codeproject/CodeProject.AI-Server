# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            Object Detection (YOLO)
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

# verbosity="loud"

location="Shared"
pythonVersion="3.8"

pythonName="python${pythonVersion/./}"
if [ "${location}" == "Local" ]; then
    virtualEnv="${modulePath}/bin/${os}/${pythonName}/venv"
else
    virtualEnv="${runtimesPath}/bin/${os}/${pythonName}/venv"
fi
pythonCmd="${virtualEnv}/bin/python${pythonVersion}"
packagesPath="${virtualEnv}/lib/python${pythonVersion}/site-packages/"

# Docker images already have the drivers installed
if [ "$inDocker" != "true" ] && [ "$os" == "linux" ]; then

    # cuDNN needed for linux
    if [ "$hasCUDA" == "true" ]; then
        writeLine 'Installing nvidia-cudnn...'
        sudo apt install nvidia-cudnn -y >/dev/null 2>/dev/null &
        spin $!
        writeLine "Done" "$color_success"
    fi

    # ROCm needed for linux
    # if [ "$hasROCm" == "true" ]; then
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
    #    writeLine "Done" "$color_success"
    #
    #    writeLine 'Installing ROCm drivers...'
    #    sudo amdgpu-install --usecase=dkms,graphics,multimedia,opencl,hip,hiplibsdk,rocm
    #    spin $!
    #    writeLine "Done" "$color_success"
    #fi

fi

# Install python and the required dependencies. If we find torch then asssume it's all there
setupPython "${pythonVersion}" "${location}"
if [ $? -ne 0 ]; then quit 1; fi

# PyTorch-DirectML not working for this module
# if [ "$hasCUDA" != "true" ] && [ "$os" == "linux" ]; then
#     writeLine 'Installing PyTorch-DirectML...'
#     "${pythonCmd}" -m pip install torch-directml --target "${packagesPath}"
# fi

installPythonPackages "${pythonVersion}" "${modulePath}" "${location}"
if [ $? -ne 0 ]; then quit 1; fi
installPythonPackages "${pythonVersion}" "${absoluteAppRootDir}/SDK/Python" "${location}"
if [ $? -ne 0 ]; then quit 1; fi

# Download the models and store in /assets and /custom-models (already in place in docker)
getFromServer "models-yolo5-pt.zip"        "assets" "Downloading Standard YOLO models..."
if [ $? -ne 0 ]; then quit 1; fi
getFromServer "custom-models-yolo5-pt.zip" "custom-models" "Downloading Custom YOLO models..."

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