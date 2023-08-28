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

# verbosity="loud"

# Python setup first

setupPython 3.9 "Local"
installPythonPackages 3.9 "${modulePath}" "Local"
installPythonPackages 3.9 "${absoluteAppRootDir}/SDK/Python" "Local"    

# Now the supporting libraries

if [ "${systemName}" == "Raspberry Pi" ] || [ "${systemName}" == "Orange Pi" ] || \
   [ "${systemName}" == "Jetson" ]; then

    if [[ $EUID -ne 0 ]]; then
        writeLine "=================================================================================" $color_error
        writeLine "Please run: sudo apt install libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools " $color_info
        writeLine "to complete the setup for ObjectDetectionCoral" $color_info
        writeLine "=================================================================================" $color_error
    else
        sudo apt install libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools -y
    fi
fi

if [ "$os" == "linux" ]; then

    write "Ensuring curl is installed (just in case)..." $color_mute
    apt-get install curl -y  >/dev/null 2>/dev/null &
    spin $!
    writeLine "Done" "$color_success"

    # Add the Debian package repository to your system
    echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list

    if [ ! -d "${downloadPath}" ]; then mkdir -p "${downloadPath}"; fi
    if [ ! -d "${downloadPath}/Coral" ]; then mkdir -p "${downloadPath}/Coral"; fi
    pushd "${downloadPath}/Coral" >/dev/null 2>/dev/null

    write "Downloading signing keys..." $color_mute
    curl https://packages.cloud.google.com/apt/doc/apt-key.gpg -s --output apt-key.gpg >/dev/null 2>/dev/null &
    spin $!
    writeLine "Done" "$color_success"

    write "Installing signing keys..." $color_mute
    # NOTE: 'add' is deprecated. We should, instead, name apt-key.gpg as coral.ai-apt-key.gpg and
    # place it directly in the /etc/apt/trusted.gpg.d/ directory
    sudo apt-key add apt-key.gpg >/dev/null 2>/dev/null &
    spin $!
    writeLine "Done" "$color_success"

    popd "${downloadPath}/Coral" >/dev/null 2>/dev/null

    if [[ $EUID -ne 0 ]]; then
        writeLine "=================================================================================" $color_error
        writeLine "Please run the following commands to complete the setup for ObjectDetectionCoral:" $color_info
        writeLine "sudo apt-get update && sudo apt-get install libedgetpu1-std" $color_info
        writeLine "=================================================================================" $color_error
    else
        # Install the Edge TPU runtime (standard, meaning half speed, or max, meaning full speed)
        write "Installing libedgetpu1-std (the non-desk-melting version of libedgetpu1)..." $color_mute
        sudo apt-get update  -y  >/dev/null 2>/dev/null &
        spin $!
        sudo apt-get install libedgetpu1-std -y  >/dev/null 2>/dev/null &
        spin $!
        writeLine "Done" "$color_success"

        # BE CAREFUL. If you want your TPU to go to 11 and choose 'max' you may burn a hole in your desk
        # sudo apt-get update && apt-get install libedgetpu1-max
    fi

elif [ "$os" == "macos" ]; then
    
    # brew install doesn't seem to be enough. Macports gets it right
    if ! command -v /opt/local/bin/port >/dev/null; then 
        writeLine "Please install Macports from https://www.macports.org/install.php before you run this script" "$color_success"
        quit 1
    fi

    # curl -LO https://github.com/google-coral/libedgetpu/releases/download/release-grouper/edgetpu_runtime_20221024.zip
    # We have modified the install.sh script in this zip so it forces the install of the throttled version
    getFromServer "edgetpu_runtime_20221024.zip" "" "Downloading edge TPU runtime..."

    unzip edgetpu_runtime_20221024.zip
    pushd edgetpu_runtime
    bash install.sh

    # For whatever reason the libs don't seem to be getting put in place, so do this manually
    sudo cp edgetpu_runtime/libedgetpu/throttled/darwin_x86_64/libedgetpu.1.0.dylib .
    # sudo cp edgetpu_runtime/libedgetpu/throttled/darwin_x86_64/libedgetpu.1.dylib .
    popd
    
    venvPath="${modulePath}/bin/${os}/python39/venv"
    packagesPath="${venvPath}/lib/python3.9/site-packages/"

    if [ "$os_name" == "Big Sur" ] && [ "$platform" == "macos" ]; then          # macOS 11.x on Intel
        "${venvPath}/bin/python" -m pip install tflite-runtime==2.5.0.post1 pycoral --extra-index-url https://google-coral.github.io/py-repo/ 
    elif [ "$os_name" == "Monterey" ] && [ "$platform" == "macos-arm64" ]; then # macOS 12.x on Apple Silicon
        "${venvPath}/bin/python" -m pip install tflite-runtime==2.5.0.post1 pycoral --extra-index-url https://google-coral.github.io/py-repo/ 
    else
        "${venvPath}/bin/python" -m pip install tflite-runtime==2.5.0.post1 pycoral --extra-index-url https://google-coral.github.io/py-repo/ 
    fi
fi

# Download the MobileNet TFLite models and store in /assets
getFromServer "objectdetect-coral-models.zip" "assets" "Downloading MobileNet models..."
if [ $? -ne 0 ]; then quit 1; fi


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