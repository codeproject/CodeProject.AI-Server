#!/bin/bash

# CodeProject.AI Server 
#
# Ubuntu / Debian / WSL cuDNN install script
# https://docs.nvidia.com/cuda/cuda-installation-guide-linux/index.html#wsl
#
# CUDA support:
# 
# Signing key:
#
# 11.8+     - use key management package
# 11.6-11.7 - 3bf863cc
# <= 11.5   - 7fa2af80
# 
# Ubuntu support
#
# 12.x      Ubuntu 22.04, 20.04 + WSL
# 11.7-11.8 Ubuntu 22.04, 20.04, 18.04 + WSL
# 11.4-11.6 Ubuntu 20.04, 18.04 + WSL
# 11.1-11.3 Ubuntu 20.04, 18.04, 16.04 + WSL
# 10.2-11.0 Ubuntu 18.04, 16.04

# To install: (OS_name = ubunut2204 etc or wsl-ubuntu, arch = x86_64 or arm64, key = 3bf863cc or 7fa2af80)
#
# - Using Key management package:
# wget https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/${arch}/cuda-keyring_1.1-1_all.deb
# sudo dpkg -i cuda-keyring_1.1-1_all.deb
# 
# - Using signing key
# wget https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/${arch}/cuda-${OS_name}.pin
# sudo mv cuda-${OS_name}.pin /etc/apt/preferences.d/cuda-repository-pin-600
# sudo apt-key adv --fetch-keys https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/${arch}/${key}.pub
# sudo add-apt-repository "deb https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/${arch}/ /"
#
#-  and then for all...
#   sudo apt-get update -y
#   sudo apt-get -y install cuda-X.Y


# cuda_version=$1
# Get major.minor CUDA version
cuda_version=$(cut -d '.' -f 1,2 <<< "$1")

# This script is intended to be called from setup.sh, which includes architecture
# and os vars as well as writeline methods. If we don't find them, do quick checks

if [[ $(type -t writeLine) != function ]]; then

    function spin () {
        local pid=$1
        while kill -0 $pid 2> /dev/null; do
            [ ]
        done
    }
    function write () {
        printf "%s" "$1"
    }
    function writeLine () {
        printf "%s\n" "$1"
    }

    if [ $(uname -m) == 'arm64' ] || [ $(uname -m) == 'aarch64' ]; then
        architecture='arm64'
    else
        architecture='x86_64'
    fi

    downloadDirPath=../../downloads

    modelInfo=""
    if [ -f "/sys/firmware/devicetree/base/model" ]; then
        modelInfo=$(tr -d '\0' </sys/firmware/devicetree/base/model) >/dev/null 2>&1
    fi

    if [[ "${modelInfo}" == *"Raspberry Pi"* ]]; then       # elif [ $(uname -n) = "raspberrypi" ]; then
        systemName='Raspberry Pi'
    elif [[ "${modelInfo}" == *"Orange Pi"* ]]; then        # elif [ $(uname -n) = "orangepi5" ]; then
        systemName='Orange Pi'
    elif [[ "${modelInfo}" == *"Radxa ROCK"* ]]; then
        systemName='Radxa ROCK'
    elif [[ "${modelInfo}" == *"NVIDIA Jetson"* ]]; then    # elif [ $(uname -n) = "nano" ]; then
        systemName='Jetson'
    elif [ "$inDocker" = true ]; then 
        systemName='Docker'
    elif [[ $(uname -a) =~ microsoft-standard-WSL ]]; then
        systemName='WSL'
   elif [[ $OSTYPE == 'darwin'* ]]; then
        systemName="macOS"
    else
        systemName='Linux'
    fi
fi

writeLine "Setting up CUDA ${cuda_version} and cuDNN" $color_info

# ==============================================================================
# GET SETTINGS

cuda_GPGpublicKey=""

case "$cuda_version" in
  "12.2") cuda_version_full="12.2.1";  cuda_GPGpublicKey="3bf863cc" ;;
  "12.1") cuda_version_full="12.1.1";  cuda_GPGpublicKey="3bf863cc" ;;
  "12.0") cuda_version_full="12.0.1";  cuda_GPGpublicKey="3bf863cc" ;;
  "11.8") cuda_version_full="11.8.0";  cuda_GPGpublicKey="3bf863cc" ;;
  "11.7") cuda_version_full="11.7.1";  cuda_GPGpublicKey="3bf863cc" ;;
  "11.6") cuda_version_full="11.6.2";  cuda_GPGpublicKey="3bf863cc" ;;
  "11.5") cuda_version_full="11.5.2";  cuda_GPGpublicKey="3bf863cc" ;;
  "11.4") cuda_version_full="11.4.4";  cuda_GPGpublicKey="7fa2af80" ;;
  "11.3") cuda_version_full="11.3.1";  cuda_GPGpublicKey="7fa2af80" ;;
  "11.2") cuda_version_full="11.2.2";  cuda_GPGpublicKey="7fa2af80" ;;
  "11.1") cuda_version_full="11.1.1";  cuda_GPGpublicKey="7fa2af80" ;;
  "11.0") cuda_version_full="11.0.1";  cuda_GPGpublicKey="7fa2af80" ;;
  "10.2") cuda_version_full="10.2.89"; cuda_GPGpublicKey="7fa2af80" ;;
  *) cuda_version_full="${cuda_version}.0" ;;
esac

cudnn_version="8.9.5.*"             # latest, works with CUDA 11.8+

distribution=$(. /etc/os-release;echo $ID$VERSION_ID) # eg "ubuntu20.04", "debian12"

OS_name="${distribution//./}"       # eg "ubuntu2204", "debian12"
if [ "${systemName}" == 'WSL' ]; then OS_name="wsl-ubuntu"; fi

system_arch="$architecture"

amd_or_arm="amd64"
# Adjust for arm64 
if [ "$architecture" = "arm64" ]; then 
    system_arch="ssba"              # SBSA (server based system architecture)
    amd_or_arm="arm64"
fi

# ==============================================================================
# UPDATE SYSTEM

# Install kernel headers and development packages for the currently running kernel
if [ "${systemName}" != 'WSL' ]; then
    write " - Installing kernel headers and development packages for the currently running kernel..." $color_mute
    sudo apt-get install linux-headers-$(uname -r)
    writeLine "done" $color_success
fi


# ==============================================================================
### CUDA 
### https://docs.nvidia.com/cuda/cuda-installation-guide-linux/#wsl

# REMOVE KEY

# even though apt-key is now deprecated...

write " - Removing signing key 7fa2af80..." $color_mute
apt-key del 7fa2af80 >/dev/null 2>/dev/null
writeLine "done" $color_success

write " - Removing signing key 3bf863cc..." $color_mute
apt-key del 3bf863cc >/dev/null 2>/dev/null
writeLine "done" $color_success

# INSTALL NEW KEY

keyring_package="cuda-keyring_1.1-1_all.deb"

if [ ! -d "${downloadDirPath}" ]; then mkdir -p "${downloadDirPath}"; fi
if [ ! -d "${downloadDirPath}/CUDA" ]; then mkdir -p "${downloadDirPath}//CUDA"; fi
pushd "${downloadDirPath}/CUDA"  >/dev/null 2>/dev/null

if [ ! -f "$keyring_package" ]; then 
    write " - Downloading new key..." $color_mute
    wget $wgetFlags https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/${system_arch}/${keyring_package} >/dev/null
    if [ ! -f "$keyring_package" ]; then 
        writeLine "Unable to download ${keyring_package}" "$color_error"
    else
        writeLine "done" $color_success
    fi
else
    writeLine "Key management package already exists" "$color_info"
fi

if [ -f "$keyring" ]; then 
    write " - Installing key..." $color_mute
    dpkg -E -G -i ${keyring_package}  >/dev/null 2>/dev/null # don't install same or older package
    sudo rm ${keyring_package}
    writeLine "done" $color_success
fi
popd  >/dev/null 2>/dev/null


# Install the CUDA SDK

# write " - Removing existing CUDA toolkit..." $color_mute
# sudo apt-get remove nvidia-cuda-toolkit
# writeLine "done" $color_success

write " - Installing CUDA library..." $color_mute
sudo apt-get update -y >/dev/null 2>/dev/null &
spin $!
sudo apt-get install cuda-${cuda_version} -y >/dev/null 2>/dev/null &
spin $!
writeLine "done" $color_success


# ==============================================================================
# cuDNN

# Ensure zlib is installed
write " - Installing zlib1g..." $color_mute
sudo apt-get install zlib1g -y >/dev/null 2>/dev/null &
spin $!
writeLine "done" $color_success

# wget https://developer.download.nvidia.com/compute/cuda/repos/<distro>/<arch>/cuda-archive-keyring.gpg
# sudo mv cuda-archive-keyring.gpg /usr/share/keyrings/cuda-archive-keyring.gpg
#
# echo "deb [signed-by=/usr/share/keyrings/cuda-archive-keyring.gpg] https://developer.download.nvidia.com/compute/cuda/repos/<distro>/<arch>/ /" | sudo tee /etc/apt/sources.list.d/cuda-<distro>-<arch>.list
#
# wget https://developer.download.nvidia.com/compute/cuda/repos/<distro>/<arch>/cuda-<distro>.pin
# sudo mv cuda-<distro>.pin /etc/apt/preferences.d/cuda-repository-pin-600

# install the cuDNN library
write " - Installing cuDNN libraries..." $color_mute
sudo apt-get install libcudnn8=${cudnn_version}-1+cuda${cuda_version} -y >/dev/null 2>/dev/null &
spin $!
sudo apt-get install libcudnn8-dev=${cudnn_version}-1+cuda${cuda_version} -y >/dev/null 2>/dev/null &
spin $!
writeLine "done" $color_success

# ==============================================================================
# EXPORTING PATHS

# To remove a directory 'Directory1' from path:
# export PATH="$( echo $PATH| tr : '\n' |grep -v Directory1 | paste -s -d: )"

write " - Exporting PATHs..." $color_mute

cuda_path="/usr/local/cuda-${cuda_version}/bin"
if [ -d "${cuda_path}" ]; then 
    if ! grep -q "${cuda_path}" "${HOME}/.bashrc";  then
        # echo "cuda path not in bashrc"
        echo "export PATH=${cuda_path}${PATH:+:${PATH}}" >> "${HOME}/.bashrc"
    # else
    #     echo "** CUDA IS IN BASHRC"
    #     grep -n "${cuda_path}" "${HOME}/.bashrc"
    fi
    if ! echo ${PATH} | grep "${cuda_path}"; then
        # echo "cuda path not in current path: $PATH"
        export PATH=${cuda_path}${PATH:+:${PATH}}
    fi
else
    echo "${cuda_path} doesn't exist"
fi

# for WSL, libcuda.so is in /usr/lib/wsl/lib/libcuda.so

library_path="/usr/local/cuda-${cuda_version}/lib64"
if [ -d "${library_path}" ]; then 
    if ! grep -q "${library_path}" "${HOME}/.bashrc";  then
      echo "export LD_LIBRARY_PATH=${library_path}${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}" >> "${HOME}/.bashrc"
    fi
    if ! echo ${LD_LIBRARY_PATH} | grep "${library_path}"; then
        export LD_LIBRARY_PATH=${cuda_path}${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}
    fi
else
    echo "${library_path} doesn't exist"
fi

library_path="/usr/local/cuda-${cuda_version}/include"
if [ -d "${library_path}" ]; then 
    if ! grep -q "${library_path}" "${HOME}/.bashrc";  then
      echo "export LD_LIBRARY_PATH=${library_path}${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}" >> "${HOME}/.bashrc"
    fi
    if ! echo ${LD_LIBRARY_PATH} | grep "${library_path}"; then
        export LD_LIBRARY_PATH=${cuda_path}${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}
    fi
else
    echo "${library_path} doesn't exist"
fi

# cat "${HOME}/.bashrc"

writeLine "done" $color_success

# ==============================================================================
# FINAL

# GDS enables a direct data path for direct memory access transfers between GPU
# memory and storage
write " - Installing NVIDIA GPU Direct Storage..." $color_mute
sudo apt-get install nvidia-gds -y >/dev/null 2>/dev/null &
spin $!
writeLine "done" $color_success

# writeLine "==================================================================" $color_warn
# writeLine "A number of packages have been installed and are no longer needed." $color_warn
# writeLine "Use 'sudo apt autoremove' to remove them."                          $color_warn
# writeLine "==================================================================" $color_warn
