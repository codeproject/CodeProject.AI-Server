#!/bin/bash

# CodeProject.AI Server 
#
# Ubuntu / WSL cuDNN install script
# https://docs.nvidia.com/cuda/cuda-installation-guide-linux/index.html#wsl
#

# This script is intended to be called from setup.sh, which includes architecture
# and os vars as well as writeline methods
#
# echo "========================================================================"
# echo ""
# echo "        Setting up cuDNN and CUDA for CodeProject.AI Server             "
# echo ""
# echo "========================================================================"
# echo ""
#
# if [ $(uname -m) == 'arm64' ] || [ $(uname -m) == 'aarch64' ]; then
#     architecture='arm64'
# else
#     architecture='x86_64'
# fi
#
# if [ $(uname -n) == "raspberrypi" ]; then
#     systemName='Raspberry Pi'
# elif [ $(uname -n) == "nano" ]; then
#     systemName='Jetson'
# elif [[ $(uname -a) =~ microsoft-standard-WSL ]]; then
#     systemName='WSL'
# elif [[ $OSTYPE == 'darwin'* ]]; then
#     systemName="macOS"
# else
#     systemName='Linux'
# fi

writeLine "Setting up cuDNN and CUDA for CodeProject.AI Server" $color_info

linux_driver="530.30.02"    # > 450.80.02 for linux
cudnn_version="8.9.4.*"     # latest, works with CUDA 11.8+
cuda_version="11.8"         # 12.1
cuda_version_dash="11-8"    # 12-1
cuda_version_full="11.8.0"  # 12.1.1

distribution=$(. /etc/os-release;echo $ID$VERSION_ID) # eg "ubuntu20.04"
OS_name="${distribution//./}"     # eg "ubuntu2204"

# Install kernel headers and development packages for the currently running kernel
if [ "${systemName}" != 'WSL' ]; then
    write " - Installing kernel headers and development packages for the currently running kernel..." $color_mute
    sudo apt-get install linux-headers-$(uname -r)
    writeLine "Done" $color_success
fi

# Updating Signing keys
write " - Removing old signing key..." $color_mute
apt-key del 7fa2af80 >/dev/null 2>/dev/null
writeLine "Done" $color_success

write " - Downloading new key..." $color_mute

keyring="cuda-keyring_1.0-1_all.deb"

if [ ! -d "${downloadPath}" ]; then mkdir -p "${downloadPath}"; fi
if [ ! -d "${downloadPath}/CUDA" ]; then mkdir -p "${downloadPath}//CUDA"; fi
pushd "${downloadPath}/CUDA"  >/dev/null 2>/dev/null

if [ ! -f "$keyring" ]; then 
    if [ "${architecture}" == "arm64" ]; then
        wget $wgetFlags https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/sbsa/${keyring}
    elif [ "${systemName}" == 'WSL' ]; then
        wget $wgetFlags https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/${keyring}
    else
        wget $wgetFlags https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/x86_64/${keyring}
    fi
    status=$?    
    if [ $status -ne 0 ]; then
        writeLine "Unable to download ${keyring}" "$color_error"
    fi
fi
writeLine "Done" $color_success

if [ -f "$keyring" ]; then 
    write " - Installing key..." $color_mute
    dpkg -E -G -i ${keyring}  >/dev/null 2>/dev/null # don't install same or older package
    writeLine "Done" $color_success
fi
popd  >/dev/null 2>/dev/null


# Install the CUDA SDK

write " - Installing libgomp1..." $color_mute
sudo apt install libgomp1 -y >/dev/null 2>/dev/null &
spin $!
writeLine "Done" $color_success

# The only practical cases here are: Native Linux on x86 or arm64 with CUDA,
# or WSL. Docker already contains the libs, macOS doesn't support CUDA. RPi and
# Orange Pi don't support CUDA and Jetson gets CUDA via Jetpack

installer_repo="https://developer.download.nvidia.com/compute/cuda/${cuda_version_full}/local_installers/"
if [ "${architecture}" == "arm64" ]; then
    pin="cuda-${OS_name}.pin"
    pin_repo="https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/sbsa/"
    installer="cuda-repo-${OS_name}-${cuda_version_dash}-local_${cuda_version_full}-${linux_driver}-1_arm64.deb"
    installed_ring="/var/cuda-repo-${OS_name}-${cuda_version_dash}-local/cuda-*-keyring.gpg"
elif [ "${systemName}" == 'WSL' ]; then
    pin="cuda-wsl-ubuntu.pin"
    pin_repo="https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/"
    installer="cuda-repo-wsl-ubuntu-${cuda_version_dash}-local_${cuda_version_full}-1_amd64.deb"
    installed_ring="/var/cuda-repo-wsl-ubuntu-${cuda_version_dash}-local/cuda-*-keyring.gpg"
else
    pin="cuda-${OS_name}.pin"
    pin_repo="https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/x86_64/"
    installer="cuda-repo-${OS_name}-${cuda_version_dash}-local_${cuda_version_full}-${linux_driver}-1_amd64.deb"
    installed_ring="/var/cuda-repo-${OS_name}-${cuda_version_dash}-local/cuda-*-keyring.gpg"
fi

pushd "${downloadPath}/CUDA"  >/dev/null 2>/dev/null
if [ ! -f "${pin}" ]; then 
    write " - Downloading ${pin}..." $color_mute
    wget $wgetFlags ${pin_repo}${pin} &
    spin $!
    if [ -f "$pin" ]; then 
        writeLine "Done" "$color_success"
    else
        writeLine "Unable to download ${pin}" "$color_error"
    fi
fi
if [ -f "$pin" ]; then 
    write " - Installing cuda-repository-pin..." $color_mute
    sudo cp ${pin} /etc/apt/preferences.d/cuda-repository-pin-600 >/dev/null 2>/dev/null
    writeLine "Done" "$color_success"
fi

if [ ! -f "${installer}" ]; then
    write " - Downloading ${installer}..." $color_mute
    wget $wgetFlags ${installer_repo}${installer} &
    spin $!
    if [ -f "$installer" ]; then 
        writeLine "Done" "$color_success"
    else
        writeLine "Unable to download ${installer}" "$color_error"
    fi
fi
if [ -f "$installer" ]; then 
    write " - Installing cuda-*-keyring.gpg..." $color_mute
    sudo dpkg -E -G -i "${installer}" >/dev/null 2>/dev/null
    status=$?    
    if [ $status -ne 0 ]; then
        writeLine "Unable to install ${installer}" "$color_error"
    else
        sudo cp "${installed_ring}" /usr/share/keyrings/ >/dev/null 2>/dev/null
        writeLine "Done" "$color_success"
    fi
fi
popd "${downloadPath}/CUDA"  >/dev/null 2>/dev/null

write " - Installing CUDA library..." $color_mute
sudo apt-get update -y >/dev/null 2>/dev/null &
spin $!
sudo apt-get install cuda -y >/dev/null 2>/dev/null &
spin $!
writeLine "Done" $color_success


# Now Install cuDNN

# Ensure zlib is installed
write " - Installing zlib1g..." $color_mute
sudo apt-get install zlib1g -y >/dev/null 2>/dev/null &
spin $!
writeLine "Done" $color_success

# Enable the repo
write " - Enabling the CUDA repository..." "$color_mute"
if [ "${architecture}" == "arm64" ]; then
    sudo apt-key adv --fetch-keys https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/arm64/3bf863cc.pub  >/dev/null 2>/dev/null &
    spin $!
    sudo add-apt-repository -y "deb https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/arm64/ /"  >/dev/null 2>/dev/null &
    spin $!
else
    sudo apt-key adv --fetch-keys https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/x86_64/3bf863cc.pub  >/dev/null 2>/dev/null &
    spin $!
    sudo add-apt-repository -y "deb https://developer.download.nvidia.com/compute/cuda/repos/${OS_name}/x86_64/ /"  >/dev/null 2>/dev/null &
    spin $!
fi
writeLine "Done" $color_success

# install the cuDNN library
write " - Installing cuDNN libraries..." $color_mute
sudo apt-get update -y >/dev/null 2>/dev/null &
spin $!
sudo apt-get install libcudnn8=${cudnn_version}-1+cuda${cuda_version} -y >/dev/null 2>/dev/null &
spin $!
sudo apt-get install libcudnn8-dev=${cudnn_version}-1+cuda${cuda_version} -y >/dev/null 2>/dev/null &
spin $!
writeLine "Done" $color_success


write " - Exporting PATHs..." $color_mute
export PATH=/usr/local/cuda-${cuda_version}/bin${PATH:+:${PATH}}
export LD_LIBRARY_PATH=/usr/local/cuda-${cuda_version}/lib64${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}
writeLine "Done" $color_success


# And finally, include all GDS packages:
write " - Installing nvidia-gds..." $color_mute
sudo apt-get install nvidia-gds -y >/dev/null 2>/dev/null &
spin $!
writeLine "Done" $color_success

writeLine "==================================================================" $color_warn
writeLine "A number of packages have been installed and are no longer needed." $color_warn
writeLine "Use 'sudo apt autoremove' to remove them." $color_warn
writeLine "==================================================================" $color_warn
