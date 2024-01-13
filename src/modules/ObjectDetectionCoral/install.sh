#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            ObjectDetection (Coral)
#
# This script is called from the ObjectDetectionCoral directory using: 
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

# If false, we install the non-desk-melting version. If true, wear gloves
full_speed=false

if [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ] || \
   [ "${systemName}" = "Jetson" ]; then

    checkForAdminRights
    if [ "$isAdmin" = false ]; then
    
        PACKAGES=""
        # for pkg in libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools; do
        for pkg in libopenblas-dev libblas-dev cmake python3-dev python3-setuptools; do
            if ! dpkg -l "${pkg}" > /dev/null; then PACKAGES+=" ${pkg}"; fi
        done

        if [[ -n "${PACKAGES}" ]]; then
            writeLine "=================================================================================" $color_info
            writeLine "Please run:" $color_info
            writeLine ""
            writeLine "  sudo apt install ${PACKAGES} " $color_info
            writeLine ""
            writeLine "to complete the setup for ObjectDetectionCoral" $color_info
            writeLine "=================================================================================" $color_info
            
            if [ "$attemptSudoWithoutAdminRights" = true ]; then
                writeLine "We will attempt to run admin-only install commands. You may be prompted" "White" "Red"
                writeLine "for an admin password. If not then please run the script shown above."   "White" "Red"
            fi

            # We won't set an error because once the user runs this script everything else will work
            # module_install_errors="Admin permissions are needed to install libraries"
        fi
    fi

    if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
        module_install_errors=""
        installAptPackages "libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools"

        if [ "${systemName}" = "Jetson" ]; then
            sudo apt install libc-bin=2.29 libc6=2.29
        fi
    fi
fi

if [ "$os" = "linux" ]; then

    # Select the Edge TPU library version
    if [ "$full_speed" = true ]; then
        edgetpu_library="libedgetpu1-max"
        desc="the full-speed, desk-melting version of libedgetpu1"
    else
        edgetpu_library="libedgetpu1-std"
        desc="the non-desk-melting version of libedgetpu1"
    fi

    # Quick check before we get messy
    found_edgelib=false
    if [ "$full_speed" = true ]; then
        apt list libedgetpu1-max 2>/dev/null | grep installed >/dev/null 2>/dev/null
        if [ "$?" = "0" ]; then found_edgelib=true; fi
    else
        apt list libedgetpu1-std 2>/dev/null | grep installed >/dev/null 2>/dev/null
        if [ "$?" = "0" ]; then found_edgelib=true; fi
    fi

    if [ "$found_edgelib" = true ]; then
        writeLine "Edge TPU library found." $color_success
    else
        if [ "$isAdmin" = false ]; then

            # Download the Edge TPU instal package for the user to install manually. If we have
            # admin rights then we'll be doing this work within this script ourselves
            getFromServer "edgetpu_runtime_20221024.zip" "" "Downloading edge TPU runtime..."
            sudo chmod -R a+rwX "${moduleDirPath}/edgetpu_runtime"

            writeLine "=================================================================================" $color_info
            writeLine "Please run the following to complete the setup for ObjectDetectionCoral:" $color_info
            writeLine ""
            writeLine "   sudo bash ${moduleDirPath}/edgetpu_runtime/install.sh"                 $color_info
            writeLine ""
            # writeLine " echo 'deb https://packages.cloud.google.com/apt coral-edgetpu-stable main' | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list" $color_info
            # writeLine " curl https://packages.cloud.google.com/apt/doc/apt-key.gpg -s --output apt-key.gpg" $color_info
            # writeLine " sudo apt-key add apt-key.gpg"                                          $color_info
            # writeLine " apt-get update -y && apt-get install -y ${edgetpu_library}"            $color_info
            writeLine "=================================================================================" $color_info

            if [ "$attemptSudoWithoutAdminRights" = true ]; then
                writeLine "We will attempt to run admin-only install commands. You may be prompted" "White" "Red"
                writeLine "for an admin password. If not then please run the script shown above."   "White" "Red"
            fi

        fi

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then   

            # There is a perfectly good install script in ${moduleDirPath}/edgetpu_runtime/install.sh
            # but it handles a lot of stuff that's not needed. Pulling out the core of that into here
            # provides a little more control

            # Add the Debian package repository to your system
            echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list

            if [ ! -d "${downloadDirPath}" ]; then mkdir -p "${downloadDirPath}"; fi
            if [ ! -d "${downloadDirPath}/Coral" ]; then mkdir -p "${downloadDirPath}/Coral"; fi
            pushd "${downloadDirPath}/Coral" >/dev/null 2>/dev/null

            write "Downloading signing keys..." $color_mute
            curl https://packages.cloud.google.com/apt/doc/apt-key.gpg -s --output apt-key.gpg >/dev/null 2>/dev/null &
            spin $!
            writeLine "Done" "$color_success"

            write "Installing signing keys..." $color_mute
            # TODO: We need to transition away from apt key. See https://opensource.com/article/22/9/deprecated-linux-apt-key
            # NOTE: 'add' is deprecated. We should, instead, name apt-key.gpg as coral.ai-apt-key.gpg and
            # place it directly in the /etc/apt/trusted.gpg.d/ directory
            sudo apt-key add apt-key.gpg >/dev/null 2>/dev/null &
            spin $!
            writeLine "Done" "$color_success"
    
            popd >/dev/null 2>/dev/null

            installAptPackages ${edgetpu_library} ""
        fi
    fi

elif [ "$os" = "macos" ]; then
    
    # brew install doesn't seem to be enough. Macports gets it right
    if ! command -v /opt/local/bin/port >/dev/null; then 
        writeLine "Please install Macports from https://www.macports.org/install.php before you run this script" "$color_error"
        module_install_errors="Macports (https://www.macports.org/install.php) must be installed"
    fi

    # Wouldn't this be nice! Except... to run port selfupdate you need to have an updated version of port.
    # write "Updating macports..."
    # sudo /opt/local/bin/port selfupdate >/dev/null
    # spin $!
    # writeLine "Done" "$color_success"

    /opt/local/bin/port version 2>/dev/null
    if [ "$?" != "0" ]; then
        writeLine "Please update Macports (see https://www.macports.org/install.php)" "$color_error"
        module_install_errors="Macports (https://www.macports.org/install.php) must be updated"
    fi

    if [ "$module_install_errors" == "" ]; then
        # curl -LO https://github.com/google-coral/libedgetpu/releases/download/release-grouper/edgetpu_runtime_20221024.zip
        # We have modified the install.sh script in this zip so it forces the install of the throttled version
        getFromServer "edgetpu_runtime_20221024.zip" "" "Downloading edge TPU runtime..."

        sudo chmod -R a+rwX "${moduleDirPath}/edgetpu_runtime"
        pushd "${moduleDirPath}/edgetpu_runtime" >/dev/null
        sudo bash install.sh

        # For whatever reason the libs don't seem to be getting put in place, so do this manually
        if [ "$platform" = "macos-arm64" ]; then
            cp libedgetpu/throttled/darwin_arm64/libedgetpu.1.0.dylib .
        else
            cp libedgetpu/throttled/darwin_x86_64/libedgetpu.1.0.dylib .
        fi

        popd >/dev/null
        
        # Install Tensorflow-Lite runtime. See https://google-coral.github.io/py-repo/tflite-runtime/
        # for all available versions
        package="tflite-runtime==2.5.0.post1"
        package_desc="Tensorflow Lite for Coral"
        pip_options="--extra-index-url https://google-coral.github.io/py-repo/"

        if [ "$os_name" = "Big Sur" ] && [ "$architecture" = "x86_64" ]; then   # macOS 11.x on Intel
            installPythonPackagesByName "$package" "$package_desc" "$pip_options"
            installPythonPackagesByName "pycoral"
        elif [ "$os_name" = "Monterey" ] && [ "$architecture" = "arm64" ]; then # macOS 12.x on Apple Silicon
            installPythonPackagesByName "$package" "$package_desc" "$pip_options"
            installPythonPackagesByName "pycoral"
        else
            # At this point we don't actually have a supported pre-built package,
            # but we can still install and run, albeit without Coral hardware.
            installPythonPackagesByName "Tensorflow"
        fi
    fi
fi

if [ "$module_install_errors" == "" ]; then
    # Download the MobileNet TFLite models and store in /assets
    getFromServer "objectdetect-coral-models.zip" "assets" "Downloading MobileNet models..."

    # TODO: Check assets created and has files
    # module_install_errors=...
fi