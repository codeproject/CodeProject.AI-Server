#!/bin/bash

# Setup script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                        CodeProject.AI Server Setup
#
# This script is called from the server directory using: 
#
#    bash ../setup.sh
#
# The setup.sh file will find this install.sh file and execute it.
#
# For help with install scripts, notes on variables and methods available, tips,
# and explanations, see /src/modules/install_script_help.md

if [ "$1" != "install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../setup.sh"
    echo
    exit 1 
fi

# Nothing to be done here...

# module_install_errors=...
