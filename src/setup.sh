#!/bin/bash
# ============================================================================
#
# CodeProject.AI Server 
# 
# Install script for Linux and macOS
# 
# This script can be called in 2 ways:
#
#   1. From within the /src directory in order to setup the Development
#      environment.
#   2. From within an analysis module directory to setup just that module.
#
# If called from within /src, then all analysis modules (in AnalysisLayer/ and
# modules/ dirs) will be setup in turn, as well as the main SDK and demos.
#
# If called from within a module's dir then we assume we're in the 
# /src/modules/ModuleId directory (or modules/ModuleId in Production) for the
# module "ModuleId". This script would typically be called via
#
#    bash ../../setup.sh
#
# This script will look for a install.sh script in the directory from whence
# it was called, and execute that script. The install.sh script is responsible
# for everything needed to ensure the module is ready to run.
# 
# Notes for Windows (WSL) users:
#
# 1. Always ensure this file is saved with line LF endings, not CRLF
#    run: sed -i 's/\r$//' setup_dev_env_linux.sh
# 2. If you get the error '#!/bin/bash - no such file or directory' then this
#    file is broken. Run head -1 setup_dev_env_linux.sh | od -c
#    You should see: 0000000   #  !  /   b   i   n   /   b   a   s   h  \n
#    But if you see: 0000000 357 273 277   #   !   /   b   i   n   /   b   a   s   h  \n
#    Then run: sed -i '1s/^\xEF\xBB\xBF//' setup_dev_env_linux.sh
#    This will correct the file. And also kill the #. You'll have to add it back
# 3. To actually run this file: bash setup_dev_env_linux.sh. In Linux/macOS,
#    obviously.
#
# ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# clear

# verbosity can be: quiet | info | loud
verbosity="quiet"

# Should we use GPU enabled libraries? If true, then any requirements.gpu.txt 
# python packages will be used if available, with a fallback to requirements.txt.
# This allows us the change to use libraries that may support GPUs if the
# hardware is present, but with the understanding that if there's no suitable
# hardware the libraries must still work on CPU. Setting this to false means
# do not load libraries that provide potential GPU support.
enableGPU="true"

# Are we ready to support CUDA enabled GPUs? Setting this to true allows us to
# test if there is CUDA enabled hardware, and if so, to request the 
# requirements.cuda.txt python packages be installed, with a fallback to 
# requirements.gpu.txt, then requirements.txt. 
# DANGER: There is no assumption that the CUDA packages will work if there's 
# no CUDA hardware. 
# NOTE: CUDA packages will ONLY be installed used if CUDA hardware is found. 
#       Setting this to false means do not load libraries that provide potential
#       CUDA support.
# NOTE: enableGPU must be true for this flag to work
supportCUDA="true"

# Show output in wild, crazy colours
useColor="true"

# Width of lines
lineWidth=70


# Debug flags for downloads

# If files are already present, then don't overwrite if this is false
forceOverwrite="false"

# If bandwidth is extremely limited, or you are actually offline, set this as true to
# force all downloads to be retrieved from cached downloads. If the cached download
# doesn't exist the install will fail.
offlineInstall="false"


# Basic locations

# The path to the directory containing the install scripts
#installerScriptsPath=$(dirname "$0")
installerScriptsPath="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

# The location of large packages that need to be downloaded (eg an AWS S3 bucket name)
storageUrl='https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/'

# The name of the source directory
srcDir='src'

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir='downloads'

# The name of the dir holding the pre-installed backend analysis services
analysisLayerDir='AnalysisLayer'

# The name of the dir holding the downloaded/sideloaded backend analysis services
downloadedModulesDir="modules"

# Override some values via parameters ::::::::::::::::::::::::::::::::::::::::
while getopts ":h" option; do
    param=$(echo $option | tr '[:upper:]' '[:lower:]')

    if [ "$param" == "--no-color" ]; then set useColor=false; fi
done

# Pre-setup ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# If offline then force the system to use pre-downloaded files
if [ "$offlineInstall" == "true" ]; then forceOverwrite="false"; fi


# Execution environment, setup mode and Paths ::::::::::::::::::::::::::::::::

# If we're calling this script from the /src folder directly (and the /src
# folder actually exists) then we're Setting up the dev environment. Otherwise
# we're installing a module.
setupMode='InstallModule'
currentDirName=$(basename $(pwd))      # Get current dir name (not full path)
currentDirName=${currentDirName:-/}    # correct for the case where pwd=/
if [ "$currentDirName" == "$srcDir" ]; then setupMode='SetupDevEnvironment'; fi

# In Development, this script is in the /src folder. In Production there is no
# /src folder; everything is in the root folder. So: go to the folder
# containing this script and check the name of the parent folder to see if
# we're in dev or production.
pushd "$installerScriptsPath" >/dev/null
currentDirName="$(basename ${installerScriptsPath})"
currentDirName=${currentDirName:-/} # correct for the case where pwd=/
popd >/dev/null
executionEnvironment='Production'
if [ "$currentDirName" == "$srcDir" ]; then executionEnvironment='Development'; fi

# The absolute path to the installer script and the root directory. Note that
# this script (and the SDK folder) is either in the /src dir or the root dir
sdkScriptsPath="${installerScriptsPath}/SDK/Scripts"
pushd "$installerScriptsPath" >/dev/null
if [ "$executionEnvironment" == 'Development' ]; then cd ..; fi
absoluteRootDir="$(pwd)"
popd >/dev/null

absoluteAppRootDir="${installerScriptsPath}"

# import the utilities

# A necessary evil due to cross platform editors and source control playing
# silly buggers
function correctLineEndings () {

    local filePath=$1

    # Force correct BOM and CRLF issues in the script. Just in case
    if [[ $OSTYPE == 'darwin'* ]]; then           # macOS
         if [[ ${OSTYPE:6} -ge 13 ]]; then        # Monterry is 'darwin21' -> "21"
            sed -i'.bak' -e '1s/^\xEF\xBB\xBF//' "${filePath}" # remove BOM
            sed -i'.bak' -e 's/\r$//' "${filePath}"            # CRLF to LF
            rm "${filePath}.bak"                               # Clean up. macOS requires backups for sed
         fi
    else                                          # Linux
        sed -i '1s/^\xEF\xBB\xBF//' "${filePath}" # remove BOM
        sed -i 's/\r$//' "${filePath}"            # CRLF to LF
    fi
}

correctLineEndings ${sdkScriptsPath}/utils.sh

# "platform" will be set by this script
source ${sdkScriptsPath}/utils.sh

# Test for CUDA drivers and adjust supportCUDA if needed
if [ "$os" == "macos" ]; then 
    supportCUDA="false"
else 
    if [ "$supportCUDA" == "true" ]; then
        supportCUDA='false'
        if [[ -x nvidia-smi ]]; then
            nvidia=$(nvidia-smi | grep -i -E 'CUDA Version: [0-9]+.[0-9]+') > /dev/null 2>&1
            if [[ ${nvidia} == *'CUDA Version: '* ]]; then 
                supportCUDA='true'
            fi
        fi
    fi
fi

# The location of directories relative to the root of the solution directory
installedModulesPath="${absoluteAppRootDir}/${analysisLayerDir}"
downloadedModulesPath="${absoluteAppRootDir}/${downloadedModulesDir}"
downloadPath="${absoluteAppRootDir}/${downloadDir}"

dataDir="/usr/share/CodeProject/AI"
if [ "$os" == "macos" ]; then 
    dataDir='/Library/Application Support/CodeProject/AI'
fi

# Set Flags

wgetFlags='-q --no-check-certificate'
pipFlags='--quiet --quiet'
copyFlags='/NFL /NDL /NJH /NJS /nc /ns  >/dev/null'
unzipFlags='-o -qq'
tarFlags='-xf'

if [ $verbosity == "info" ]; then
    wgetFlags='--no-verbose --no-check-certificate'
    pipFlags='--quiet'
    rmdirFlags='/q'
    copyFlags='/NFL /NDL /NJH'
    unzipFlags='-q -o'
    tarFlags='-xf'
elif [ $verbosity == "loud" ]; then
    wgetFlags='-v --no-check-certificate'
    pipFlags=''
    rmdirFlags=''
    copyFlags=''
    unzipFlags='-o'
    tarFlags='-xvf'
fi

if [ "$os" == "macos" ]; then
    pipFlags="${pipFlags} --no-cache-dir"
else
    pipFlags="${pipFlags} --progress-bar off"
fi

if [ "$useColor" != "true" ]; then
    pipFlags="${pipFlags} --no-color"
fi

if [ "$setupMode" != 'SetupDevEnvironment' ]; then
    scriptTitle='          Setting up CodeProject.AI Development Environment'
else
    scriptTitle='             Installing CodeProject.AI Analysis Module'
fi

writeLine 
writeLine "$scriptTitle" 'DarkCyan' 'Default' $lineWidth
writeLine 
writeLine '======================================================================' 'DarkGreen'
writeLine 
writeLine '                   CodeProject.AI Installer                           ' 'DarkGreen'
writeLine 
writeLine '======================================================================' 'DarkGreen'
writeLine 

if [ "$verbosity" != "quiet" ]; then 
    writeLine 
    writeLine "setupMode             = ${setupMode}"             $color_mute
    writeLine "executionEnvironment  = ${executionEnvironment}"  $color_mute
    writeLine "installerScriptsPath  = ${installerScriptsPath}"  $color_mute
    writeLine "sdkScriptsPath        = ${sdkScriptsPath}"        $color_mute
    writeLine "absoluteAppRootDir    = ${absoluteAppRootDir}"    $color_mute
    writeLine "installedModulesPath  = ${installedModulesPath}"  $color_mute
    writeLine "downloadedModulesPath = ${downloadedModulesPath}" $color_mute
    writeLine "downloadPath          = ${downloadPath}"          $color_mute
    writeLine 
fi

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip

writeLine ""
write "Allowing GPU Support: "
if [ "$enableGPU" == "true" ]; then writeLine "Yes" $color_success; else writeLine "No" $color_warn; fi
write "Allowing CUDA Support: "
if [ "$supportCUDA" == "true" ]; then writeLine "Yes" $color_success; else writeLine "No" $color_warn; fi


# ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
# 1. Ensure directories are created and download required assets

writeLine
writeLine "General CodeProject.AI setup" "White" "DarkGreen" $lineWidth
writeLine

# Create some directories
write "Creating Directories..." $color_primary

# For downloading assets
mkdir -p "${downloadPath}"

if [ "$os" == "macos" ]; then 
    if [[ ! -w "${downloadPath}" ]]; then
        sudo chmod 777 "${downloadPath}"
    fi
fi

# For persisting settings
if [ "$os" == "linux" ]; then 
    sudo mkdir -p "${dataDir}"
    if [[ ! -w "${dataDir}" ]]; then
        sudo chmod 777 "${dataDir}"
    fi
fi
writeLine "Done" $color_success

# And off we go...

if [ "$setupMode" == 'SetupDevEnvironment' ]; then 

    # Walk through the modules directory and call the setup script in each dir
    for d in ${installedModulesPath}/*/ ; do

        moduleDir="$(basename $d)"
        modulePath=$d

        if [ "${modulePath: -1}" == "/" ]; then
            modulePath="${modulePath:0:${#modulePath}-1}"
        fi

        # dirname=${moduleDir,,} # requires bash 4.X, which isn't on macOS by default
        dirname=$(echo $moduleDir | tr '[:upper:]' '[:lower:]')
        if [ "${dirname}" != 'bin' ]; then

            if [ -f "${modulePath}/install.sh" ]; then

                writeLine
                writeLine "Processing pre-installed module ${moduleDir}" "White" "Blue" $lineWidth
                writeLine

                correctLineEndings "${modulePath}/install.sh"
                source "${modulePath}/install.sh" "install"
            fi
        fi
    done

    writeLine
    writeLine "Pre-installed Modules setup Complete" $color_success    

    # Walk through the sideloaded / downloaded modules directory and call the setup script in each dir
    for d in ${downloadedModulesPath}/*/ ; do

        moduleDir="$(basename $d)"
        modulePath=$d

        if [ "${modulePath: -1}" == "/" ]; then
            modulePath="${modulePath:0:${#modulePath}-1}"
        fi

        # dirname=${moduleDir,,} # requires bash 4.X, which isn't on macOS by default
        dirname=$(echo $moduleDir | tr '[:upper:]' '[:lower:]')
        if [ "${dirname}" != 'bin' ]; then

            if [ -f "${modulePath}/install.sh" ]; then

                writeLine
                writeLine "Processing side-loaded module ${moduleDir}" "White" "Blue" $lineWidth
                writeLine

                correctLineEndings "${modulePath}/install.sh"
                source "${modulePath}/install.sh" "install"
            fi
        fi
    done

    writeLine
    writeLine "Sideloaded Modules setup Complete" $color_success


    # Now do SDK
    moduleDir="SDK"
    modulePath="${absoluteAppRootDir}/${moduleDir}"
    writeLine
    writeLine "Processing SDK" "White" "Blue" $lineWidth
    writeLine
    correctLineEndings "${modulePath}/install.sh"
    source "${modulePath}/install.sh" "install"

    # And Demos
    moduleDir="demos"
    modulePath="${absoluteRootDir}/${moduleDir}"
    writeLine
    writeLine "Processing demos" "White" "Blue" $lineWidth
    writeLine
    correctLineEndings "${modulePath}/install.sh"
    source "${modulePath}/install.sh" "install"
    writeLine "Done" $color_success

    # And finally, supporting library packages

    # libfontconfig1 is required for SkiaSharp, libgdplus is required for System.Drawing
    if [ "${verbosity}" == "quiet" ]; then
        write "Installing supporting image libraries..."
    else
        writeLine "Installing supporting image libraries..."
    fi

    if [ "$os" == "linux" ]; then
        if [ "${verbosity}" == "quiet" ]; then

            # install the cv2 dependencies that are normally present on the local machine, but sometimes aren't
            sudo apt-get update >/dev/null 2>/dev/null &
            spin $!
            sudo apt-get install ffmpeg libsm6 libxext6 libfontconfig1 libgdiplus -y >/dev/null 2>/dev/null &
            spin $!
        else
            sudo apt-get update
            sudo apt-get install ffmpeg libsm6 libxext6 libfontconfig1 libgdiplus -y 
        fi
    else
        if [ "${verbosity}" == "quiet" ]; then
            brew install fontconfig  >/dev/null 2>/dev/null &
            spin $!
            # brew install mono-libgdiplus  >/dev/null 2>/dev/null &
            brew install libomp  >/dev/null 2>/dev/null &
            spin $!
        else
            brew install fontconfig
            # brew install mono-libgdiplus
            brew install libomp
        fi
    fi
    writeLine "Done" $color_success

    if [ "$os" == "linux" ]; then
        if [ "${verbosity}" == "quiet" ]; then
            write "Installing glxinfo so we can query GPU information..."
            sudo apt install mesa-utils >/dev/null 2>/dev/null &
            spin $!
        else
            writeLine "Installing glxinfo so we can query GPU information..."
            sudo apt install mesa-utils
        fi
        writeLine "Done" $color_success
    fi


    # ============================================================================
    # ...and we're done.

    writeLine
    writeLine "                Development Environment setup complete" "White" "DarkGreen" $lineWidth
    writeLine
else

    # Install an individual module

    modulePath=$(pwd)
    if [ "${modulePath: -1}" == "/" ]; then
        modulePath="${modulePath:0:${#modulePath}-1}"
    fi
    moduleDir="$(basename ${modulePath})"
    # dirname=${moduleDir,,} # requires bash 4.X, which isn't on macOS by default
    dirname=$(echo $moduleDir | tr '[:upper:]' '[:lower:]')

    downloadPath=${modulePath}/${downloadDir}

    if [ -f "${modulePath}/install.sh" ]; then

        writeLine
        writeLine "Installing module ${moduleDir}" "White" "Blue" $lineWidth
        writeLine

        correctLineEndings "${modulePath}/install.sh"
        source "${modulePath}/install.sh" "install"
    fi

    # ============================================================================
    # ...and we're done.

    writeLine
    writeLine "                Module setup complete" "White" "DarkGreen" $lineWidth
    writeLine
fi

quit