#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            Sentiment Analysis
#
# This script is called from the SentimentAnalysis directory using: 
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

if [ "$os" = "macos" ] && [ ! -d /usr/local/include/tensorflow ]; then
    dynlibFile="libtensorflow-cpu-darwin-x86_64-2.15.0.tar.gz"
    wget -q --no-check-certificate "https://storage.googleapis.com/tensorflow/libtensorflow/${dynlibFile}"
    sudo tar -C /usr/local -xzf "${dynlibFile}"
    rm "${dynlibFile}"
fi

if [ "${executionEnvironment}" != "Production" ]; then
    pushd "$moduleDirPath" >/dev/null
    writeLine "Building project..." "$color_info"
    dotnet build -c Debug -o "${moduleDirPath}/bin/Debug/net7.0" >/dev/null
    popd >/dev/null
fi