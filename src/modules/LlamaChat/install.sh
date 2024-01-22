#!/bin/bash

# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            LlamaChat
#
# This script is called from the LlamaChat directory using: 
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

if [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ] || [ "${systemName}" = "Jetson" ]; then
    module_install_errors="Unable to install on Pi or Jetson hardware."
fi

if [ "$module_install_errors" = "" ]; then

    if [ "${os}" = "macos" ] && [ "${architecture}" = "arm64" ]; then
        # "${venvPythonCmdPath}" -m pip uninstall llama-cpp-python -y
        CMAKE_ARGS="-DLLAMA_METAL=on"
        "${venvPythonCmdPath}" -m pip install -U llama-cpp-python --no-cache-dir
        # installPythonPackagesByName "llama-cpp-python" "Simple Python bindings for the llama.cpp library" # "--force-reinstall --upgrade --no-cache-dir"
    fi

    # wget "https://huggingface.co/TheBloke/CodeLlama-7B-GGUF/resolve/main/codellama-7b.Q4_K_M.gguf"

    fileToGet="codellama-7b.Q4_K_M.gguf"
    if [ ! -f "${moduleDirPath}/models/${fileToGet}" ]; then
        
        sourceUrl="https://huggingface.co/TheBloke/CodeLlama-7B-GGUF/resolve/main/"
        destination="${downloadDirPath}/${moduleDirName}/${fileToGet}"
        
        if [ ! -f "${destination}" ]; then
            mkdir -p "${downloadDirPath}/${moduleDirName}"
            mkdir -p "${moduleDirPath}/models"
            wget $wgetFlags -P "${downloadDirPath}/${moduleDirName}" "${sourceUrl}${fileToGet}"
        fi

        if [ -f "${destination}" ] && [ ! -f "${moduleDirPath}/models/${fileToGet}" ]; then 
            mv "${destination}" "${moduleDirPath}/models/"
        fi

    else
        writeLine "${fileToGet} already downloaded." "$color_success"
    fi
    
fi