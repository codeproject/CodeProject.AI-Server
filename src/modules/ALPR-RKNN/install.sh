# Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                          ALPR-RKNN
#
# This script is called from the ALPR-RKNN directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh file will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

module_install_errors=""

if [ "${edgeDevice}" != "Orange Pi" ] && [ "${edgeDevice}" != "Radxa ROCK" ]; then
    module_install_errors="Unable to install on non-Rockchip hardware."
else
    # Download the OCR models and store in /paddleocr
    getFromServer "models/" "paddleocr-rknn-models.zip" "paddleocr" "Downloading Plate and OCR models..."

    # TODO: Check paddleocr created and has files, maybe run paddle check too
    # module_install_errors=...
fi
