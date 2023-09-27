#!/bin/bash

# Setup script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                            CodeProject.AI Server
#
# This script is called from the SDK directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh file will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../setup.sh"
    echo
    exit 1 
fi

#verbosity="info"
# Setup libs required by a bunch of things we'll eventually need

# Install required libraries --------------------------------------------------

if [ "$os" == "linux" ]; then

    # Need to add the noise level toggling:
    #
    # if [ "${verbosity}" == "quiet" ]; then
    #     write "Installing glxinfo so we can query GPU information..."
    #     sudo apt install mesa-utils >/dev/null 2>/dev/null &
    #     spin $!
    # else
    # etc...

    # "libstdc++.so.6: version `GLIBCXX_3.4.20' not found"
    # https://stackoverflow.com/a/46613656
    if [ "${verbosity}" == "quiet" ]; then
        write "Adding toolchain repo..." $color_primary
        add-apt-repository ppa:ubuntu-toolchain-r/test -y >/dev/null 2>/dev/null &
        spin $!
        writeLine "Done" $color_success
        
        installAptPackages "gcc-4.9 apt-utils"

        write "Upgrading libstdc..." $color_primary
        apt-get upgrade libstdc++6 -y >/dev/null 2>/dev/null &
        spin $!
        writeLine "Done" $color_success
    else
        add-apt-repository ppa:ubuntu-toolchain-r/test -y 
        installAptPackages "gcc-4.9 apt-utils"
        apt-get upgrade libstdc++6 -y
    fi

    # Make a list of the packages we need installed
    packages=""

    # - These libraries are needed for System.Drawing to work on Linux in NET 7
    # - libfontconfig1 is required for SkiaSharp
    # - libc6-dev, libgdplus is required for System.Drawing
    packages="${packages} ca-certificates gnupg libc6-dev libfontconfig1 libgdiplus libjpeg-dev zlib1g-dev"
    installAptPackages "${packages}"

    # - Needed for opencv-python (TODO: review these and move into module installers that actually use OpenCV)
    packages="ffmpeg libsm6 libxext6"
    # - So we can query glxinfo for GPU info (mesa) and install modules (the rest)
    packages="${packages} mesa-utils curl rsync unzip wget"
    installAptPackages "${packages}"

else
    if [ "${verbosity}" == "quiet" ]; then
        write "Installing System.Drawing support "

        if [ $"$architecture" == 'arm64' ]; then
            arch -x86_64 /usr/local/bin/brew install fontconfig  >/dev/null 2>/dev/null &
            spin $!
            # brew install mono-libgdiplus  >/dev/null 2>/dev/null &
            arch -x86_64 /usr/local/bin/brew install libomp  >/dev/null 2>/dev/null &
            spin $!
        else
            brew install fontconfig  >/dev/null 2>/dev/null &
            spin $!
            # brew install mono-libgdiplus  >/dev/null 2>/dev/null &
            brew install libomp  >/dev/null 2>/dev/null &
            spin $!
        fi

        writeLine "Done" $color_success
    else
        writeLine "Installing System.Drawing support "

        if [ $"$architecture" == 'arm64' ]; then
            arch -x86_64 /usr/local/bin/brew install fontconfig
            arch -x86_64 /usr/local/bin/brew install libomp
        else
            brew install fontconfig
            brew install libomp
        fi
        
        writeLine "Done" $color_success
    fi
fi

# .NET -------------------------------------------------------------------------

# Setup .NET for the server and any .NET modules that may need it
setupDotNet 7.0
if [ $? -ne 0 ]; then quit 1; fi


# CUDA -------------------------------------------------------------------------

# BEFORE WE START: Ensure CUDA and cuDNN is installed. Note this is only for 
# native linux since macOS no longer supports NVIDIA, and docker images and
# Jetson already contain the  necessary SDKs and libraries.
# We install CUDA now so that any installs down the track can take into account
# the presence of CUDA for install selection
if [ "$hasCUDA" == "true" ] && [ "${inDocker}" == "false" ] && [ "${systemName}" != "Jetson" ]; then
    correctLineEndings "${sdkScriptsPath}/install_cuDNN.sh"
    source "${sdkScriptsPath}/install_cuDNN.sh"
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