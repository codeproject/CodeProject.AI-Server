#!/bin/bash

# Module Packaging script. To be called from create_packages.sh

# The executable for this module is downloaded from S3. There are multiple forms
# of the executable, corresponding to different hardware, and the appropriate
# exe will be downloaded based on the local hardware. 


moduleId=$1
version=$2

# FIRST: We build the .NET app in Release mode. For this module we're building 4 
# different versions, with each being (manually) uploaded to S3. The install
# script simply pulls down and unpacks the correct version based on the Hardware
# and OS.

configuration="Release"
target="net7.0"

declare -a gpuTypes
gpuTypes[0]="GPU_NONE"
gpuTypes[1]="GPU_CUDA"
gpuTypes[2]="GPU_DIRECTML"
gpuTypes[3]="GPU_OPENVINO"

# The csproj file uses the gpuTypes for clarity, but we don't want them as the file suffix
declare -a fileSuffixes
fileSuffixes[0]="CPU"
fileSuffixes[1]="CUDA"
fileSuffixes[2]="DirectML"
fileSuffixes[3]="OpenVINO"

echo

length=${#gpuTypes[@]}
for (( index=0; index<${length}; index++ )); do

    gpuType=${gpuTypes[$index]}
    fileSuffix=${fileSuffixes[$index]}
   
    # Build
    echo " - Building ObjectDetection (.Net) for ${gpuType}"
    dotnet build -c ${configuration} --no-self-contained /p:DefineConstants=${gpuType} >/dev/null 2>/dev/null

    if [ ! $? -eq 0 ]; then
        echo "BUILD FAILED. Cancelling"
        quit
    fi

    # Zip it up. Note that we're excluding models because the istall scripts will 
    # pull them down separately
    pushd ./bin/${configuration}/${target}/  >/dev/null 2>/dev/null

    tar -a -cf ../../../${moduleId}-${fileSuffix}-${version}.zip \
        --exclude=*.development.* --exclude=*.docker.build.* *.*

    popd >/dev/null 2>/dev/null

    # Cleanup
    rm -rf /q ./bin/${configuration}/${target}/ # >/dev/null 2>/dev/null
    rm -rf /q ./obj/${configuration}/${target}/ # >/dev/null 2>/dev/null

done

# ... and create the actual module package. It's just the install scripts. All assets are in S3.
tar -caf ${moduleId}-${version}.zip  --exclude=*.development.* --exclude=*.docker.build.* --exclude=*.log \
    modulesettings.* install.sh install.bat explore.html test/*
