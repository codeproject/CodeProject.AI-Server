#!/bin/sh

## CodeProject.AI Analysis services startup script for Linux and macOS
##
## Usage:
##   . ./start.sh
##
## We assume we're in the /src/AnalysisLayer directory

verbosity="info"

# Get platform
if [[ $OSTYPE == 'darwin'* ]]; then
    platform="macos"
else
    platform="linux"
fi

# Move into the working directory
# if [ "$1" != "" ]; then
#    cd "$1"
#fi
embedded="false"
if [ "$1" == "--embedded" ]; then
    embedded="true"
else
    clear
fi

# ===============================================================================================
# 1. Load environment variables

# If calling from another script then assume parent script has set some env vars (unless nothing
# was actually set)
if [ "${embedded}" == "false" ] || [ "$DATA_DIR" == "" ]; then

    echo "Setting Environment variables..."

    # API server
    if [[ $OSTYPE == 'darwin'* ]]; then
        PORT="5500" # Port 5000 is reserved on macOS
    else
        PORT="5000"
    fi
    export PORT

    ANALYSISDIR="$(pwd)"

    # Text module
    NLTK_DATA="${ANALYSISDIR}/TextSummary/nltk_data"

    # Deepstack stuff
    APPDIR="${ANALYSISDIR}/Vision/"
    DATA_DIR="${APPDIR}datastore/"
    TEMP_PATH="${APPDIR}tempstore/"
    MODELS_DIR="${APPDIR}assets/"
    PROFILE="desktop_cpu"
    CUDA_MODE="False"
    MODE="Medium"

    export DATA_DIR
    export TEMP_PATH
    export MODELS_DIR
    export PROFILE
    export CUDA_MODE
    export MODE
fi

if [ "${verbosity}" == "info" ]; then
    echo "Starting Analysis services (in start-analysis.sh) -------"
    echo "APPDIR    = ${APPDIR}"
    echo "PROFILE   = ${PROFILE}"
    echo "CUDA_MODE = ${CUDA_MODE}"
    echo "DATA_DIR  = ${DATA_DIR}"
    echo "TEMP_PATH = ${TEMP_PATH}"
    echo "PORT      = ${PORT}"
fi


# ===============================================================================================
# 2. Start each module

# Python
python37Path="${APPDIR}/src/AnalysisLayer/bin/${platform}/Python37/venv/bin/python3"
python39Path="${APPDIR}/src/AnalysisLayer/bin/${platform}/Python39/venv/bin/python3"

"${python37Path}" "${APPDIR} "${ANALYSISDIR}/TextSummary/textsummary.py" &
"${python37Path}" "${APPDIR}/BackgroundRemover/rembg_adapter.py" &

"${python37Path}" "${APPDIR}/intelligencelayer/detection.py" &
"${python37Path}" "${APPDIR}/intelligencelayer/scene.py" &
"${python37Path}" "${APPDIR}/intelligencelayer/face.py" &

# Wait forever. We need these processes to stay alive
if [ "${embedded}" == "false" ]; then
    sleep infinity
fi
