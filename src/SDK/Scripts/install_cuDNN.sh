# CodeProject.AI Server 
#
# Ubuntu / WSL cuDNN install script
# https://docs.nvidia.com/cuda/cuda-installation-guide-linux/index.html#wsl

echo "========================================================================"
echo ""
echo "            Setting up cuDNN for CodeProject.AI Server                  "
echo ""
echo "========================================================================"
echo ""

# Remove Outdated Signing Key:
sudo apt-key del 7fa2af80

# Install the newcuda-keyring package
if grep -qi microsoft /proc/version; then
    # Ubuntu under WSL
    wget https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.0-1_all.deb
else
    # Native Ubuntu. Use ubuntu1804, ubuntu2004 or ubuntu2204. Get this from lsb_release
    version=$(cut -f2 <<< $(lsb_release -r)) 
    version="${version//./}"
    wget https://developer.download.nvidia.com/compute/cuda/repos/ubuntu${version}/x86_64/cuda-keyring_1.0-1_all.deb
fi

sudo dpkg -i cuda-keyring_1.0-1_all.deb

# Update the Apt repository cache:
sudo apt-get update

# Install CUDA SDK:
sudo apt-get install cuda -y

# To include all GDS packages:
sudo apt-get install nvidia-gds -y

export PATH=/usr/local/cuda-12.0/bin${PATH:+:${PATH}}
export LD_LIBRARY_PATH=/usr/local/cuda-12.0/lib64 ${LD_LIBRARY_PATH:+:${LD_LIBRARY_PATH}}
