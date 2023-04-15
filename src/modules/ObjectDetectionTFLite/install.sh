# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            ObjectDetection (Coral)
#
# This script is called from the ObjectDetectionCoral directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh script will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
	exit 1 
fi

# We no longer try installing the Coral libraries directly. They are no longer supported and
# Tensorflow provide access to the Coral TPU directly
# source "${modulePath}/install_coral.sh"

if [ $(uname -n) == "raspberrypi" ]; then
    if [[ $EUID -ne 0 ]]; then
        writeLine "=================================================================================" $color_error
        writeLine "Please run: sudo apt install libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools " $color_info
        writeLine "to complete the setup for ObjectDetectionTFLite" $color_info
        writeLine "=================================================================================" $color_error
    else
        sudo apt install libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools
    fi
fi

if [ "$os" == "linux" ]; then

    apt-get install curl -y

    # Add the Debian package repository to your system
    echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list
    curl https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo apt-key add -

    if [[ $EUID -ne 0 ]]; then
        writeLine "=================================================================================" $color_error
        writeLine "Please run the following commands to complete the setup for ObjectDetectionTFLite:" $color_info
        writeLine "sudo apt-get update && apt-get install libedgetpu1-std" $color_info
        writeLine "=================================================================================" $color_error
    else
        # Install the Edge TPU runtime (standard, meaning half speed, or max, meaning full speed)
        sudo apt-get update && apt-get install libedgetpu1-std

        # BE CAREFUL. If you want your TPU to go to 11 and choose 'max' you may burn a hole in your desk
        # sudo apt-get update && apt-get install libedgetpu1-max
    fi
fi

setupPython 3.9 "Local"
if [ $? -ne 0 ]; then quit 1; fi

# Required for tensorflow. See https://medium.com/@sorenlind/tensorflow-with-gpu-support-on-apple-silicon-mac-with-homebrew-and-without-conda-miniforge-915b2f15425b
#if [ "${platform}" == "macos-arm64" ]; then
#    arch -x86_64 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)"
#    brew install hdf5
#fi

# install the python packages for this module and the SDK
installPythonPackages 3.9 "${modulePath}" "Local"
if [ $? -ne 0 ]; then quit 1; fi
installPythonPackages 3.9 "${absoluteAppRootDir}/SDK/Python" "Local"
if [ $? -ne 0 ]; then quit 1; fi

# Download the MobileNet TFLite models and store in /assets
getFromServer "objectdetect-tflite-models.zip" "assets" "Downloading MobileNet models..."
if [ $? -ne 0 ]; then quit 1; fi


#                         -- Install script cheatsheet -- 
#
# Variables available:
#
#  absoluteRootDir       - the root path of the installation (eg: ~/CodeProject/AI)
#  sdkScriptsPath        - the path to the installation utility scripts ($rootPath/Installers)
#  downloadPath          - the path to where downloads will be stored ($sdkScriptsPath/downloads)
#  runtimesPath          - the path to the installed runtimes ($rootPath/src/runtimes)
#  modulesPath           - the path to all the AI modules ($rootPath/src/modules)
#  moduleDir             - the name of the directory containing this module
#  modulePath            - the path to this module ($modulesPath/$moduleDir)
#  os                    - "linux" or "macos"
#  architecture          - "x86_64" or "arm64"
#  platform              - "linux", "linux-arm64", "macos" or "macos-arm64"
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