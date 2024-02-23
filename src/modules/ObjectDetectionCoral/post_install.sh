#!/bin/bash

# Post Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                          Object Detection (Coral)
#
# The setup.sh file will find this post_install.sh file and execute it.

if [ "$1" != "post-install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
    exit 1 
fi

# Install optimized Pillow-SIMD or fallback to keeping regular Pillow
# Used for fast image image resizing operations with SSE4 or AVX2
# See also: https://python-pillow.org/pillow-perf/

#   This does not work. Headers are required. 
#  
#   # Try installing Pillow-SIMD, and if that doesn't work, undo that and put back Pillow
#   {
#       '${venvPythonCmdPath}' -m pip uninstall --yes pillow &&
#       '${venvPythonCmdPath}' -m pip install pillow-simd
#   } || {
#       '${venvPythonCmdPath}' -m pip uninstall --yes pillow-simd &&
#       '${venvPythonCmdPath}' -m pip install Pillow
#   }