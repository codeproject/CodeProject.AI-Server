#
# Install Coral Drivers
#
#   bash install_coral.sh
#


# Setup the Coral libraries
if [[ $OSTYPE == 'darwin'* ]]; then

    curl -LO https://github.com/google-coral/libedgetpu/releases/download/release-grouper/edgetpu_runtime_20221024.zip
    mv edgetpu_runtime_20221024.zip ../../downloads/ObjectDetectionTFLite/.
    pushd ../../downloads/ObjectDetectionTFLite/ >/dev/null
    unzip edgetpu_runtime_20221024.zip
    cd edgetpu_runtime
    sudo bash install.sh
    popd >/dev/null

else

    if [ $(uname -n) == "raspberrypi" ]; then
        sudo apt install libopenblas-dev libblas-dev m4 cmake cython python3-dev python3-yaml python3-setuptools
    fi

    # Add the Debian package repository to your system
    echo "deb https://packages.cloud.google.com/apt coral-edgetpu-stable main" | sudo tee /etc/apt/sources.list.d/coral-edgetpu.list
    curl https://packages.cloud.google.com/apt/doc/apt-key.gpg | sudo apt-key add -
    sudo apt-get update

    # Install the Edge TPU runtime (standard, meaning half speed, or max, meaning full speed.
    # BE CAREFUL. If you want your TPU to go to 11 and choose 'max' you may burn a hole in your desk
    sudo apt-get install libedgetpu1-std
    # sudo apt-get install libedgetpu1-max

fi