#!/bin/bash

# Setup script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                        CodeProject.AI SDK Setup
#
# This script is called from the SDK directory using: 
#
#    bash ../setup.sh
#
# The setup.sh file will find this install.sh file and execute it.
#
# For help with install scripts, notes on variables and methods available, tips,
# and explanations, see /src/modules/install_script_help.md

if [ "$1" != "install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../setup.sh"
    echo
    exit 1 
fi

# Setup anything required for the general SDK (libs and install scripts) to work
 
# Install required libraries --------------------------------------------------

if [ "$os" = "linux" ]; then

    if [ "$os_name" = "ubuntu" ]; then # Not for debian
        # "libstdc++.so.6: version `GLIBCXX_3.4.20' not found" -  https://stackoverflow.com/a/46613656
        write "Adding toolchain repo..." $color_primary
        apt policy 2>/dev/null | grep ubuntu-toolchain >/dev/null 2>/dev/null
        if [ "$?" != 0 ]; then

            # output a warning message if no admin rights and instruct user on manual steps
            install_instructions="cd ${sdkScriptsDirPath}${newline}sudo bash ../setup.sh"
            checkForAdminAndWarn "$install_instructions"

            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                if [ "${verbosity}" = "quiet" ]; then
                    sudo add-apt-repository ppa:ubuntu-toolchain-r/test -y >/dev/null 2>/dev/null &
                    spin $!
                else
                    sudo add-apt-repository ppa:ubuntu-toolchain-r/test -y
                fi
            fi
            
            writeLine "done" $color_success
        else
            writeLine "Already added" $color_success
        fi
    fi
    
    installAptPackages "apt-utils"
    
    # - These libraries are needed for System.Drawing to work on Linux in NET 7
    # - libfontconfig1 is required for SkiaSharp
    # - libc6-dev, libgdplus is required for System.Drawing
    packages="ca-certificates gnupg libc6-dev libfontconfig1 libgdiplus libjpeg-dev zlib1g-dev"
    installAptPackages "${packages}"

    # - Needed for opencv-python (TODO: review these and move into module installers that actually use OpenCV)
    packages="ffmpeg libsm6 libxext6"
    # - So we can query glxinfo for GPU info (mesa) and install modules (the rest)
    packages="${packages} mesa-utils curl rsync unzip wget"
    installAptPackages "${packages}"

else
    if [ "${verbosity}" = "quiet" ]; then

        if [ "$os_name" = "Big Sur" ]; then   # macOS 11.x on Intel, kernal 20.x
            writeLine "** Installing System.Drawing support. On macOS 11 this could take a looong time" "$color_warn"
        else
            write "Installing System.Drawing support "
        fi

        if [ $"$architecture" = 'arm64' ]; then
            arch -x86_64 /usr/local/bin/brew list fontconfig >/dev/null 2>/dev/null || \
                        arch -x86_64 /usr/local/bin/brew install fontconfig  >/dev/null 2>/dev/null &
            spin $!
            # brew install mono-libgdiplus  >/dev/null 2>/dev/null &
            arch -x86_64 /usr/local/bin/brew list libomp >/dev/null 2>/dev/null || \
                        arch -x86_64 /usr/local/bin/brew install libomp >/dev/null 2>/dev/null &
            spin $!
        else
            brew list fontconfig  >/dev/null 2>/dev/null || brew install fontconfig  >/dev/null 2>/dev/null &
            spin $!
            # brew install mono-libgdiplus  >/dev/null 2>/dev/null &
            brew list libomp  >/dev/null 2>/dev/null || brew install libomp  >/dev/null 2>/dev/null &
            spin $!
        fi

        writeLine "done" $color_success
    else
        writeLine "Installing System.Drawing support "

        if [ $"$architecture" = 'arm64' ]; then
            arch -x86_64 /usr/local/bin/brew list fontconfig || arch -x86_64 /usr/local/bin/brew install fontconfig
            arch -x86_64 /usr/local/bin/brew list libomp     || arch -x86_64 /usr/local/bin/brew install libomp
        else
            brew list fontconfig || brew install fontconfig
            brew list libomp     || brew install libomp
        fi
        
        writeLine "done" $color_success
    fi
fi

# .NET -------------------------------------------------------------------------

# Setup .NET for the server, the SDK Utilities, and any .NET modules that may 
# need it
if [ "$executionEnvironment" = "Development" ]; then
    setupDotNet 7.0 "sdk"
else
    setupDotNet 7.0 "aspnetcore"
fi

if [ $? -ne 0 ]; then
    writeLine "Failed to install .NET" $color_error
    quit 1
fi


# CUDA -------------------------------------------------------------------------

if [ "${edgeDevice}" = "Jetson" ]; then
    echo ${PATH} | grep /usr/bin/cuda/bin >/dev/null 2>/dev/null
    if [ "$?" = "1" ] && [ -d "/usr/bin/cuda/bin" ]; then 
        export PATH=${PATH};/usr/bin/cuda/bin
    fi
    
    echo ${LD_LIBRARY_PATH} | grep /usr/local/lib64 >/dev/null 2>/dev/null
    if [ "$?" = "1" ] && [ -d "/usr/local/lib64" ]; then 
        export LD_LIBRARY_PATH=${LD_LIBRARY_PATH};/usr/local/lib64
    fi
    
    source ~/.bashrc
fi


# Utilities --------------------------------------------------------------------

if [ "${useJq}" = false ]; then
    dotnet build "${sdkPath}/Utilities/ParseJSON" /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release > /dev/null 2>&1
    pushd "${sdkPath}/Utilities/ParseJSON/bin/Release/net7.0/" >/dev/null
    mv ./* ../../../
    popd >/dev/null
fi

# TODO: Check .NET installed correctly
# module_install_errors=...
