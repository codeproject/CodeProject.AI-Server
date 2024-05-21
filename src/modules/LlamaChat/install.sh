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

if [ "${edgeDevice}" = "Raspberry Pi" ] || [ "${edgeDevice}" = "Orange Pi" ] || 
   [ "${edgeDevice}" = "Radxa ROCK" ]   || [ "${edgeDevice}" = "Jetson" ]; then
    module_install_errors="Unable to install on Pi, ROCK or Jetson hardware."
fi

if [ "$module_install_errors" = "" ]; then

    # Disable this. req.txt should be good now
    if [ "${os}" = "macos" ] && [ "${architecture}" = "arm64" ]; then

        # Wouldn't it be nice if this just worked?
        # installPythonPackagesByName "llama-cpp-python" "Simple Python bindings for the llama.cpp library"

        pushd "${virtualEnvDirPath}/bin" >/dev/null
        ./pip3 uninstall llama-cpp-python -y 

        checkForTool "cmake"
        checkForTool "ninja"
        #checkForTool "gcc@11"
        #checkForTool "g++@11"

        # For Python >= 3.8
        # METAL causes a warning "llama.cpp/ggml.c:1509:5: warning: implicit conversion increases 
        # floating-point precision: 'float' to 'ggml_float' (aka 'double') [-Wdouble-promotion]
        #  GGML_F16_VEC_REDUCE(sumf, sum);" which then borks the entire llama.cpp build. Turn off
        # metal to get this overly pedantic codee to build. macOS just gets no love.

        if [ "${architecture}" = "arm64" ]; then
            if [ "$verbosity" = "loud" ]; then
                # CMAKE_ARGS="-DLLAMA_METAL=on" FORCE_CMAKE=1 "${venvPythonCmdPath}" -m pip install -U git+https://github.com/abetlen/llama-cpp-python.git --no-cache-dir
                CMAKE_ARGS="-DLLAMA_METAL=off -DLLAMA_CLBLAST=on" FORCE_CMAKE=1 "${venvPythonCmdPath}" -m pip install llama-cpp-python
            else
                CMAKE_ARGS="-DLLAMA_METAL=off -DLLAMA_CLBLAST=on" FORCE_CMAKE=1 "${venvPythonCmdPath}" -m pip install llama-cpp-python > /dev/null
            fi
        else
            if [ "$verbosity" = "loud" ]; then
                # "${venvPythonCmdPath}" -m pip install llama-cpp-python
                # CMAKE_ARGS="-DLLAMA_METAL=off -DLLAMA_CLBLAS=on" FORCE_CMAKE=1 "${venvPythonCmdPath}" -m pip install llama-cpp-python
                CMAKE_ARGS="-DLLAMA_METAL=off" FORCE_CMAKE=1 "${venvPythonCmdPath}" -m pip install llama-cpp-python
            else
                CMAKE_ARGS="-DLLAMA_METAL=off" FORCE_CMAKE=1 "${venvPythonCmdPath}" -m pip install llama-cpp-python > /dev/null
            fi
        fi

        popd >/dev/null
    fi

    # sourceUrl="https://huggingface.co/TheBloke/CodeLlama-7B-GGUF/resolve/main/"
    # fileToGet="codellama-7b.Q4_K_M.gguf"
    
    sourceUrl="https://huggingface.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF/resolve/main/"
    fileToGet=mistral-7b-instruct-v0.2.Q4_K_M.gguf

    if [ "$verbosity" = "loud" ]; then writeLine "Looking for model: ${moduleDirPath}/models/${fileToGet}"; fi

    if [ ! -f "${moduleDirPath}/models/${fileToGet}" ]; then
        
        cacheDirPath="${downloadDirPath}/${modulesDir}/${moduleDirName}/${fileToGet}"
        
        if [ "$verbosity" = "loud" ]; then writeLine  "Looking for cache: ${cacheDirPath}"; fi
        if [ ! -f "${cacheDirPath}" ]; then
            mkdir -p "${downloadDirPath}/${modulesDir}/${moduleDirName}"
            mkdir -p "${moduleDirPath}/models"
            wget $wgetFlags -P "${downloadDirPath}/${modulesDir}/${moduleDirName}" "${sourceUrl}${fileToGet}"
        elif [ "$verbosity" = "loud" ]; then
            writeLine "File is cached" 
        fi

        if [ -f "${cacheDirPath}" ]; then 
            cp "${cacheDirPath}" "${moduleDirPath}/models/."
        fi

    else
        writeLine "${fileToGet} already downloaded." "$color_success"
    fi
    
fi