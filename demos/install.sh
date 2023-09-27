#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            CodeProject.AI Demos
#
# This script is called from the Demos directory using: 
#
#    bash ../src/setup.sh
#
# The setup.sh script will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../src/setup.sh"
    echo
    exit 1 
fi

pythonLocation="Shared"
pythonVersion=3.9

# Install python and the required dependencies.
setupPython 
installPythonPackages "${modulePath}/Python" 