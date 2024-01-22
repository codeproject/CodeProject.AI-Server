#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            Portrait Filter
#
# This script is called from the PortraitFilter directory using: 
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

if [ "${executionEnvironment}" = "Production" ]; then
    writeLine "No custom setup steps for this module" "$color_info"
else
    pushd "$moduleDirPath" >/dev/null
    writeLine "Building project..." "$color_info"
    dotnet build -c Debug -o "${moduleDirPath}/bin/Debug/net7.0" >/dev/null
    popd >/dev/null
fi