#!/bin/bash
#
# CodeProject SenseAI Server 
# 
# Unix/Linux/macOS Development Environment install script
# 
# We assume we're in the source code /Installers/Dev directory.
# 
# import the utilities
source $(dirname "$0")/utils.sh

useColor="true"
darkmode=$(isDarkMode)

# Setup some predefined colours. Note that we can't reliably determine the background 
# color of the terminal so we avoid specifically setting black or white for the foreground
# or background. You can always just use "White" and "Black" if you specifically want
# this combo, but test thoroughly
if [ "$darkmode" == "true" ]; then
    color_primary='White'
    color_mute='Gray'
    color_info='Yellow'
    color_success='Green'
    color_warn='DarkYellow'
    color_error='Red'
else
    color_primary='Black'
    color_mute='Gray'
    color_info='Magenta'
    color_success='DarkGreen'
    color_warn='DarkYellow'
    color_error='Red'
fi

clear

# verbosity can be: quiet | info | loud
verbosity="quiet"

# If files are already present, then don't overwrite if this is false
forceOverwrite=false

# Platform can define where things are located
if [[ $OSTYPE == 'darwin'* ]]; then
    platform='macos'
else
    platform='linux'
fi


# Basic locations

# The location of the solution root directory relative to this script
rootPath='../..'

# SenseAI Server specific ::::::::::::::::::::::::::::::::::::::::::::::::::::

# The name of the dir holding the frontend API server
senseAPIDir='API'


# Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# The location of large packages that need to be downloaded
# a. From AWS
storageUrl='https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/'
# b. Use a local directory rather than from online. Handy for debugging.
# storageUrl='/mnt/c/dev/CodeProject/CodeProject.SenseAI/install/cached_downloads/'

# The name of the source directory
srcDir='src'

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir='downloads'

# The name of the dir holding the backend analysis services
analysisLayerDir='AnalysisLayer'

# Absolute paths :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
# The absolute path to the root directory of CodeProject.SenseAI
currentDir="$(pwd)"
cd $rootPath
absoluteRootDir="$(pwd)"
cd $currentDir

# The location of directories relative to the root of the solution directory
analysisLayerPath="${absoluteRootDir}/${srcDir}/${analysisLayerDir}"
downloadPath="${absoluteRootDir}/Installers/${downloadDir}"

# Set Flags

wgetFlags='-q --no-check-certificate'
pipFlags='-q'
copyFlags='/NFL /NDL /NJH /NJS /nc /ns  >/dev/null'
unzipFlags='-qq'
tarFlags='xf'

if [ $verbosity == "info" ]; then
    wgetFlags='--no-verbose --no-check-certificate'
    pipFlags='-q'
    rmdirFlags='/q'
    copyFlags='/NFL /NDL /NJH'
    unzipFlags='-q'
    tarFlags='xf'
elif [ $verbosity == "loud" ]; then
    wgetFlags='-v --no-check-certificate'
    pipFlags=''
    rmdirFlags=''
    copyFlags=''
    unzipFlags=''
    tarFlags='xvf'
fi

writeLine '        Setting up CodeProject.SenseAI Development Environment          ' 'DarkYellow'
writeLine '                                                                        ' 'DarkGreen'
writeLine '========================================================================' 'DarkGreen'
writeLine '                                                                        ' 'DarkGreen'
writeLine '                 CodeProject SenseAI Installer                          ' 'DarkGreen'
writeLine '                                                                        ' 'DarkGreen'
writeLine '========================================================================' 'DarkGreen'
writeLine '                                                                        ' 'DarkGreen'

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip

if [ "$platform" == "linux" ] && [ "$EUID" -ne 0 ]; then
    writeLine "Please run this script as root: sudo bash setup_dev_env_linux.sh" $color_error
    exit
fi

# ============================================================================
# 1. Ensure directories are created and download required assets

writeLine
writeLine 'General SenseAI setup                                                   ' "White" "Blue"

# Create some directories

# For downloading assets
write "Creating Directories..." $color_primary
if [ $verbosity == "loud" ]; then writeLine "downloadPath is ${downloadPath}"; fi;

mkdir -p "${downloadPath}"
if [ "$platform" == "macos" ]; then 
    write "We'll need to run under root to set permissions. " $color_warn
    sudo chmod 777 "${downloadPath}"
else
    write "Creating Directories..." $color_primary
fi
writeLine "Done" $color_success


# The Docs ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::: 

# Currently 3.9 is the latest python version our modules are using, so we'll just use this to 
# save installing a one-off (but potentially better) version. It's just docs. Nothing crazy.
setupPython 3.9

write "Installing MKDocs..." 
installPythonPackages 3.9 "../../docs/mkdocs/requirements.txt" "mkdocs"
writeLine "Done" "DarkGreen" 


# TextSummary specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'TextSummary setup                                                       ' "White" "Blue"

# The name of the dir containing the TextSummary module
moduleDir='TextSummary'

# Full path to the TextSummary dir
modulePath="${analysisLayerPath}/${moduleDir}"

setupPython 3.8
installPythonPackages 3.8 "${modulePath}/requirements.txt" "nltk"


# Background Remover :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'Background Remover setup                                                ' "White" "Blue"

# The name of the dir containing the background remover module
moduleDir='BackgroundRemover'

# The name of the dir containing the background remover models
modulePath="${analysisLayerPath}/${moduleDir}"

# The name of the dir containing the background remover models
moduleAssetsDir='models'

# The name of the file in our S3 bucket containing the assets required for this module
modelsAssetFilename='rembg-models.zip'

setupPython 3.9
installPythonPackages 3.9 "${modulePath}/requirements.txt" "onnxruntime"

# Clean up directories to force a re-copy if necessary
if [ "${forceOverwrite}" == "true" ]; then
    rm -rf "${downloadPath}/${moduleDir}"
    rm -rf "${modulePath}/${moduleAssetsDir}"
fi

if [ ! -d  "${modulePath}/${moduleAssetsDir}" ]; then
    Download $storageUrl "${downloadPath}" $modelsAssetFilename "${moduleDir}" "Downloading models..."
    if [ -d "${downloadPath}/${moduleDir}" ]; then
        mv -f "${downloadPath}/${moduleDir}" "${modulePath}/${moduleAssetsDir}"
    fi
fi


# DeepStack specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'Vision toolkit setup                                                    ' "White" "Blue"

# The name of the dir containing the background remover module
moduleDir='DeepStack'

# The name of the dir containing the background remover models
modulePath="${analysisLayerPath}/${moduleDir}"

# The name of the dir containing the background remover models
moduleAssetsDir='assets'

# The name of the file in our S3 bucket containing the assets required for this module
modelsAssetFilename='models.zip'

setupPython 3.8
if [ "$platform" == "macos" ]; then
    installPythonPackages 3.8 "${modulePath}/intelligencelayer/requirements.macos.txt" "torch"
else
    installPythonPackages 3.8 "${modulePath}/intelligencelayer/requirements.txt" "torch"
fi

# Clean up directories to force a re-copy if necessary
if [ "${forceOverwrite}" == "true" ]; then
    rm -rf "${downloadPath}/${moduleDir}"
    rm -rf "${modulePath}/${moduleAssetsDir}"
fi

if [ ! -d  "${modulePath}/${moduleAssetsDir}" ]; then
    Download $storageUrl "${downloadPath}" $modelsAssetFilename "${moduleDir}" "Downloading models..."
    if [ -d "${downloadPath}/${moduleDir}" ]; then
        mv -f "${downloadPath}/${moduleDir}" "${modulePath}/${moduleAssetsDir}"
    fi
fi

# Deepstack needs these to store temp and pesrsisted data
mkdir -p "${deepStackPath}/${tempstoreDir}"

# To do this properly we're going to use the standard directories for common application data
# mkdir -p "${deepStackPath}/${datastoreDir}"
commonDataDir='/usr/share/CodeProject/SenseAI'
if [ "$platform" == "macos" ]; then 
    commonDataDir="/Library/Application Support/CodeProject/SenseAI"
fi

if [ ! -d "${commonDataDir}" ]; then
    if [ "$platform" == "macos" ]; then 
        if [[ $EUID > 0 ]]; then
            writeLine "Creating data directory at ${commonDataDir}. We'll need admin access..." $color_info
        fi

        sudo mkdir -p "${commonDataDir}"   
        if [ $? -ne 0 ]; then
            displayMacOSPermissionError "${commonDataDir}"
        fi
        sudo chmod 777 "${commonDataDir}"
    else
        mkdir -p "${commonDataDir}"
    fi
fi

# For Yolo.NET :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'Object Detector setup                                                   ' "White" "Blue"

# The name of the dir containing the background remover module
moduleDir='CodeProject.SenseAI.AnalysisLayer.Yolo'

# The name of the dir containing the background remover models
modulePath="${analysisLayerPath}/${moduleDir}"

# The name of the dir containing the background remover models
moduleAssetsDir='assets'

# The name of the file in our S3 bucket containing the assets required for this module
modelsAssetFilename='yolonet-models.zip'

# Clean up directories to force a re-copy if necessary
if [ "${forceOverwrite}" == "true" ]; then
    rm -rf "${downloadPath}/${moduleDir}"
    rm -rf "${modulePath}/${moduleAssetsDir}"
fi

if [ ! -d  "${modulePath}/${moduleAssetsDir}" ]; then
    Download $storageUrl "${downloadPath}" $modelsAssetFilename "${moduleDir}" "Downloading models..."
    if [ -d "${downloadPath}/${moduleDir}" ]; then
        mv -f "${downloadPath}/${moduleDir}" "${modulePath}/${moduleAssetsDir}"
    fi
fi

# libfontconfig1 is required for SkiaSharp, libgdplus is required for System.Drawing
write "Installing supporting image libraries..."
if [ "$platform" == "linux" ]; then
    if [ "${verbosity}" == "quiet" ]; then
        apt-get install libfontconfig1 -y  >/dev/null 2>/dev/null &
        spin $!
        apt-get install libgdiplus -y  >/dev/null 2>/dev/null &
        spin $!
    else
        apt-get install libfontconfig1 -y 
        apt-get install libgdiplus -y 
    fi
else
    if [ "${verbosity}" == "quiet" ]; then
        brew install fontconfig  >/dev/null 2>/dev/null &
        spin $!
        brew install mono-libgdiplus  >/dev/null 2>/dev/null &
        spin $!
    else
        brew install fontconfig
        brew install mono-libgdiplus
    fi
fi
writeLine "Done" $color_success


# ============================================================================
# ...and we're done.

writeLine 
writeLine '                Development Environment setup complete                  ' 'White' 'DarkGreen'
writeLine 

quit