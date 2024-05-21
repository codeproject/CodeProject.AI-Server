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

# There is a perfectly good install script in ${moduleDirPath}/edgetpu_runtime/install.sh but it
# handles a lot of stuff that's not needed. Pulling out the core of that into here provides a
# little more control. NOTE this only applies for Linux. macOS always uses the script.
use_edge_install_script=true

# If false, we install the non-desk-melting version. If true, wear gloves. Allow
# this to be overridden by a parameter
tpu_speed="throttle"
if [ "$2" = "max" ]; then tpu_speed="max"; fi

# We may need admin rights, so we may as well check now.
checkForAdminRights

if [ "${edgeDevice}" = "Raspberry Pi" ] || [ "${edgeDevice}" = "Orange Pi" ] || \
   [ "${systemName}" = "Radxa ROCK"   ] || [ "${edgeDevice}" = "Jetson" ]; then

    if [ "$isAdmin" = false ]; then
    
        PACKAGES=""
        # for pkg in libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools; do
        for pkg in libopenblas-dev libblas-dev cmake python3-dev python3-setuptools; do
            if ! dpkg -l "${pkg}" > /dev/null; then PACKAGES+=" ${pkg}"; fi
        done

        if [[ -n "${PACKAGES}" ]]; then

            # output a warning message if no admin rights and instruct user on manual steps
            install_instructions="sudo apt install ${PACKAGES}"
            checkForAdminAndWarn "$install_instructions"
            
            # We won't set an error because once the user runs this script everything else will work
            # module_install_errors="Admin permissions are needed to install libraries"
        fi
    fi

    if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
        module_install_errors=""
        installAptPackages "libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools"

        if [ "${edgeDevice}" = "Jetson" ]; then
            installAptPackages "libc-bin=2.29 libc6=2.29"
        fi
    fi
fi

if [ "$os" = "linux" ]; then

    installAptPackages "gnupg"

    # Select the Edge TPU library version
    if [ "$tpu_speed" = "max" ]; then
        edgetpu_folder="direct"
        edgetpu_library="libedgetpu1-max"
        desc="the full-speed, desk-melting version of libedgetpu1"
    else
        edgetpu_folder="throttled"
        edgetpu_library="libedgetpu1-std"
        desc="the non-desk-melting version of libedgetpu1"
    fi

    # Quick check before we get messy
    found_edgelib=false
    if [ "$tpu_speed" = "max" ]; then
        apt list libedgetpu1-max 2>/dev/null | grep installed >/dev/null 2>/dev/null
        if [ "$?" = "0" ]; then found_edgelib=true; fi
    else
        apt list libedgetpu1-std 2>/dev/null | grep installed >/dev/null 2>/dev/null
        if [ "$?" = "0" ]; then found_edgelib=true; fi
    fi

    if [ "$found_edgelib" = true ]; then
        writeLine "Edge TPU library found." $color_success
    else
        getFromServer "libraries/" "edgetpu_runtime-20221024.zip" "edgetpu_runtime" "Downloading edge TPU runtime..."

        if [ "$use_edge_install_script" = true ]; then
            install_instructions="sudo bash ${moduleDirPath}/edgetpu_runtime/install.sh"
        else
            install_instructions="echo 'deb https://packages.cloud.google.com/apt coral-edgetpu-stable main' | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list"
            install_instructions+="${newline}curl https://packages.cloud.google.com/apt/doc/apt-key.gpg -s --output apt-key.gpg"
            install_instructions+="${newline}sudo apt-key add apt-key.gpg"
            install_instructions+="${newline}apt-get update -y && apt-get install -y ${edgetpu_library}"
            # TODO: We're missing the instruction to copy the lib to the GNU folder
        fi
        checkForAdminAndWarn "$install_instructions"

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then   

            if [ "$use_edge_install_script" = true ]; then

                pushd "${moduleDirPath}/edgetpu_runtime" >/dev/null
                
                sudo bash "install.sh" "$tpu_speed" "install"
                
                # Use the following 2 lines if running install as part of this process, rather than
                # as a sub-process
                # source "install.sh" "$tpu_speed" "install"
                # set +e    # Remove the -e set in install.sh because it kills our error handling ability

                popd >/dev/null

            else

                # Add the Debian package repository to your system
                echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list

                if [ ! -d "${downloadDirPath}/${ModuleDirName}" ]; then mkdir -p "${downloadDirPath}/${modulesDir}/${ModuleDirName}"; fi
                pushd "${downloadDirPath}/${modulesDir}/${ModuleDirName}" >/dev/null 2>/dev/null

                write "Downloading signing keys..." $color_mute
                curl https://packages.cloud.google.com/apt/doc/apt-key.gpg -s --output apt-key.gpg >/dev/null 2>/dev/null &
                spin $!
                writeLine "done" "$color_success"

                write "Installing signing keys..." $color_mute
                # TODO: We need to transition away from apt key. See https://opensource.com/article/22/9/deprecated-linux-apt-key
                # NOTE: 'add' is deprecated. We should, instead, name apt-key.gpg as coral.ai-apt-key.gpg and
                # place it directly in the /etc/apt/trusted.gpg.d/ directory
                sudo apt-key add apt-key.gpg >/dev/null 2>/dev/null &
                spin $!
                writeLine "done" "$color_success"
        
                popd >/dev/null 2>/dev/null

                installAptPackages ${edgetpu_library} ""

                if [ "$architecture" = "arm64" ]; then
                    cp "${moduleDirPath}/edgetpu_runtime/${edgetpu_folder}/aarch64/libedgetpu.so.1.0" "/usr/lib/aarch64-linux-gnu/libedgetpu.so.1.0"
                else
                    cp "${moduleDirPath}/edgetpu_runtime/${edgetpu_folder}/k8/libedgetpu.so.1.0" "/usr/lib/x86_64-linux-gnu/libedgetpu.so.1.0"
                fi
                ldconfig  # Generates libedgetpu.so.1 symlink

            fi
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
    # writeLine "done" "$color_success"

    /opt/local/bin/port version 2>/dev/null
    if [ "$?" != "0" ]; then
        writeLine "Please update Macports (see https://www.macports.org/install.php)" "$color_error"
        module_install_errors="Macports (https://www.macports.org/install.php) must be updated"
    fi

    if [ "$module_install_errors" == "" ]; then
        # curl -LO https://github.com/google-coral/libedgetpu/releases/download/release-grouper/edgetpu_runtime_20221024.zip
        # We have modified the install.sh script in this zip so it forces the install of the throttled version
        getFromServer "libraries/" "edgetpu_runtime-20221024.zip" "edgetpu_runtime" "Downloading edge TPU runtime..."
        
        # Correction for badly formed zip
        # getFromServer "libraries/" "edgetpu_runtime_20221024.zip" "edgetpu_runtime_temp" "Downloading edge TPU runtime..."
        # move_recursive "${moduleDirPath}/edgetpu_runtime_temp/edgetpu_runtime" "${moduleDirPath}/edgetpu_runtime"
        # rm -rf "${moduleDirPath}/edgetpu_runtime_temp"

        if [ "$full_speed" = true ]; then
            checkForAdminAndWarn "sudo bash install.sh max"
        else
            checkForAdminAndWarn "sudo bash install.sh throttle"
        fi

        sudo chmod -R a+rwX "${moduleDirPath}/edgetpu_runtime"
        pushd "${moduleDirPath}/edgetpu_runtime" >/dev/null
        if [ "$full_speed" = true ]; then
            source "install.sh" "max" "install"
        else
            source "install.sh" "throttle" "install"
        fi
        set +e    # Remove the -e set in install.sh because it kills our error handling ability

        # For whatever reason the libs don't seem to be getting put in place, so do this manually
        if [ "$platform" = "macos-arm64" ]; then
            cp libedgetpu/throttled/darwin_arm64/libedgetpu.1.0.dylib .
        else
            cp libedgetpu/throttled/darwin_x86_64/libedgetpu.1.0.dylib .
        fi

        popd >/dev/null
        
        # Install Tensorflow-Lite runtime. See https://google-coral.github.io/py-repo/tflite-runtime/
        # for all available versions
        pip_options="--extra-index-url https://google-coral.github.io/py-repo/"

        if [ "$os_name" = "Big Sur" ] && [ "$architecture" = "x86_64" ]; then   # macOS 11.x on Intel, kernal 20.x
            installPythonPackagesByName "tflite-runtime==2.5.0.post1" "Tensorflow Lite for Coral" "$pip_options"
            installPythonPackagesByName "pycoral"                     "Coral.AI for Python"       "$pip_options"
        elif [ "$os_name" = "Monterey" ] && [ "$architecture" = "arm64" ]; then # macOS 12.x on Apple Silicon, , kernal 21.x
            installPythonPackagesByName "tflite-runtime==2.5.0.post1" "Tensorflow Lite for Coral" "$pip_options"
            installPythonPackagesByName "pycoral"                     "Coral.AI for Python"       "$pip_options"
        else
            # At this point we don't actually have a supported pre-built package,
            # but we can still install and run, albeit without Coral hardware.
            installPythonPackagesByName "Tensorflow"
        fi
    fi
fi

if [ "$module_install_errors" == "" ]; then
    # Download the full set of TFLite / edgeTPU models and store in /assets
    # getFromServer "models/" "objectdetect-coral-models.zip" "assets" "Downloading MobileNet models..."

    # Download individual assets
    # getFromServer "models/" "objectdetection-efficientdet-large-edgetpu.zip" "assets" "Downloading EfficientDet (large) models..."
    # getFromServer "models/" "objectdetection-efficientdet-medium-edgetpu.zip" "assets" "Downloading EfficientDet (medium) models..."
    # getFromServer "models/" "objectdetection-efficientdet-small-edgetpu.zip" "assets" "Downloading EfficientDet (small) models..."
    # getFromServer "models/" "objectdetection-efficientdet-tiny-edgetpu.zip" "assets" "Downloading EfficientDet (tiny) models..."

    # getFromServer "models/" "objectdetection-mobilenet-large-edgetpu.zip" "assets" "Downloading MobileNet (large) models..."
    # getFromServer "models/" "objectdetection-mobilenet-medium-edgetpu.zip" "assets" "Downloading MobileNet (medium) models..."
    # getFromServer "models/" "objectdetection-mobilenet-small-edgetpu.zip" "assets" "Downloading MobileNet (small) models..."
    # getFromServer "models/" "objectdetection-mobilenet-tiny-edgetpu.zip" "assets" "Downloading MobileNet (tiny) models..."

    # getFromServer "models/" "objectdetection-yolov5-large-edgetpu.zip" "assets" "Downloading YOLOv5 (large) models..."
    # getFromServer "models/" "objectdetection-yolov5-medium-edgetpu.zip" "assets" "Downloading YOLOv5 (medium) models..."
    # getFromServer "models/" "objectdetection-yolov5-small-edgetpu.zip" "assets" "Downloading YOLOv5 (small) models..."
    # getFromServer "models/" "objectdetection-yolov5-tiny-edgetpu.zip" "assets" "Downloading YOLOv5 (tiny) models..."

    # getFromServer "models/" "objectdetection-yolov8-large-edgetpu.zip" "assets" "Downloading YOLOv8 (large) models..."
    # getFromServer "models/" "objectdetection-yolov8-medium-edgetpu.zip" "assets" "Downloading YOLOv8 (medium) models..."
    # getFromServer "models/" "objectdetection-yolov8-small-edgetpu.zip" "assets" "Downloading YOLOv8 (small) models..."
    # getFromServer "models/" "objectdetection-yolov8-tiny-edgetpu.zip" "assets" "Downloading YOLOv8 (tiny) models..."

    # - no downloads here. It's all handled via modulesettings now
    echo

    # TODO: Check assets created and has files
    # module_install_errors=...
fi