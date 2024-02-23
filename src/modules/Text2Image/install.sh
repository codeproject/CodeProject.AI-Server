if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# Download the YOLO models from the CodeProject models/ folder and store in /assets 
# getFromServer "models/" "models-yolo8-pt.zip"  "assets" "Downloading Standard YOLO models..."