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
# If called from within /src, then all analysis modules (in modules/ and
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

# Whether or not downloaded modules can have their Python setup installed in The
# shared area
allowSharedPythonInstallsForModules="true"

# Debug flags for downloads and installs

# If files are already present, then don't overwrite if this is false
forceOverwrite="false"

# If bandwidth is extremely limited, or you are actually offline, set this as true to
# force all downloads to be retrieved from cached downloads. If the cached download
# doesn't exist the install will fail.
offlineInstall="false"

# For speeding up debugging
skipPipInstall="false"

# Whether or not to install all python packages in one step (-r requirements.txt)
# or step by step. Doing this allows the PIP manager to handle incompatibilities 
# better.
# ** WARNING ** There is a big tradeoff on keeping the users informed and speed/
# reliability. Generally one-step shouldn't be needed. But it often is. And if
# often doesn't actually solve problems either. Overall it's safer, but not a panacea
oneStepPIP="false"


# Basic locations

# The path to the directory containing the install scripts
#installerScriptsPath=$(dirname "$0")
installerScriptsPath="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

# The location of large packages that need to be downloaded (eg an AWS S3 bucket name)
storageUrl='https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/'

# The name of the source directory (in development)
srcDir='src'

# The name of the app directory (in docker)
appDir='app'

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir='downloads'

# The name of the dir holding the runtimes
runtimesDir='runtimes'

# The name of the dir holding the downloaded/sideloaded backend analysis services
modulesDir="modules"

# In development, we have the downloadable modules in /src/modules.
# In production, the modules live in /opts/CodeProject/AI/modules.
# In docker, the modules live in /app/modules.
# BUT: 'production' for non-Windows means 'Docker', and for that we map folders
#      to handle things. No need to make any changes at this point.
# persistedModuleDataPath="/opt/CodeProject/AI/"

# Override some values via parameters ::::::::::::::::::::::::::::::::::::::::
while getopts ":h" option; do
    param=$(echo $option | tr '[:upper:]' '[:lower:]')

    if [ "$param" == "--no-color" ]; then set useColor="false"; fi
done

# Pre-setup ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# If offline then force the system to use pre-downloaded files
if [ "$offlineInstall" == "true" ]; then forceOverwrite="false"; fi

# We can't do shared installs in Docker. They won't persist
inDocker="false"
if [ "$DOTNET_RUNNING_IN_CONTAINER" == "true" ]; then 

    inDocker="true"

    echo
    echo "Hi Docker! We will disable shared python installs for downloaded modules"
    echo
    allowSharedPythonInstallsForModules="false"; 
fi

diskSpace=$(df $PWD | awk '/[0-9]%/{print $(NF-2)}')
echo "${diskSpace} available"

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
installScriptDirName="$(basename ${installerScriptsPath})"
installScriptDirName=${installScriptDirName:-/} # correct for the case where pwd=/
popd >/dev/null
executionEnvironment='Production'
if [ "$installScriptDirName" == "$srcDir" ]; then executionEnvironment='Development'; fi

# For Docker
if [ "$inDocker" == "true" ]; then
    # Yes, this is a little contradictory. Maybe "SetupDevEnvironment" should be "SetupAllModules"
    if [ "$currentDirName" == "$appDir" ]; then setupMode='SetupDevEnvironment'; fi
    executionEnvironment='Production'
fi

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
hasCUDA='false'
if [ "$os" == "macos" ]; then 
    supportCUDA="false"
else
    if [ "$supportCUDA" == "true" ]; then
        # https://stackoverflow.com/a/66486390
        cp /usr/lib/wsl/lib/nvidia-smi /usr/bin/nvidia-smi > /dev/null 2>&1
        chmod ogu+x /usr/bin/nvidia-smi > /dev/null 2>&1

        if [ -x "$(command -v nvidia-smi)" ]; then
            nvidia=$(nvidia-smi | grep -i -E 'CUDA Version: [0-9]+.[0-9]+') > /dev/null 2>&1
            if [[ ${nvidia} == *'CUDA Version: '* ]]; then hasCUDA='true'; fi
        fi
    fi
fi

# Test for AMD ROCm drivers 
hasROCm='false'
if [ "$os" == "linux" ]; then 
    if [ ! -x "$(command -v rocminfo)" ]; then
        write "Checking for ROCm support..." $color_primary
        sudo apt install rocminfo -y > /dev/null 2>&1 &
        spin $!
        writeLine "Done" $color_success
    fi
    if [ -x "$(command -v rocminfo)" ]; then
        amdinfo=$(rocminfo | grep -i -E 'AMD ROCm System Management Interface') > /dev/null 2>&1
        if [[ ${amdinfo} == *'AMD ROCm System Management Interface'* ]]; then hasROCm='true'; fi
    fi
fi

# The location of directories relative to the root of the solution directory
runtimesPath="${absoluteAppRootDir}/${runtimesDir}"

# In development, we have the downloadable modules in /src/modules.
# In production, the modules live in /opts/CodeProject/AI/modules.
# In docker, the modules live in /app/modules.
# BUT: 'production' for non-Windows means 'Docker', and for that we map folders
#      to handle things. No need to make any changes at this point.
# if [ "$executionEnvironment" == 'Development' ]; then
    modulesPath="${absoluteAppRootDir}/${modulesDir}"
    downloadPath="${absoluteAppRootDir}/${downloadDir}"
# else
#    modulesPath="${persistedModuleDataPath}/${modulesDir}"
#    downloadPath="${persistedModuleDataPath}/${downloadDir}"
# fi

# Create directories for persisted application data
if [ "$os" == "macos" ]; then 
    commonDataDir='/Library/Application Support/CodeProject/AI'
else
    commonDataDir='/etc/codeproject/ai'

    # Correct for mis-placed data
    if [ -d "/usr/share/CodeProject/AI" ]; then
        if [ ! -d "${commonDataDir}" ]; then
            sudo mv "/usr/share/CodeProject/AI" "${commonDataDir}"
        fi
        sudo mv "/usr/share/CodeProject/AI" "/usr/share/CodeProject/AI.bak"
    fi
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

# ** WARNING 2 ** Turns out PIP is more painful that we thought it could be:
#  - For Windows, oneStep is necessary otherwise FaceProcessing fails.
#  - For Mac and Linux, oneStep will NOT work
#  - For Docker, which is Linux, it DOES work. Sometimes. Or maybe not.
if [ "$inDocker" == "true" ]; then 
    oneStepPIP="true"
elif [ "$os" == "linux" ] || [ "$os" == "macos" ]; then
    oneStepPIP="false"
elif [ "$os" == "windows" ]; then 
    oneStepPIP="true"
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
    writeLine "runtimesPath          = ${runtimesPath}"          $color_mute
    writeLine "modulesPath           = ${modulesPath}"           $color_mute
    writeLine "downloadPath          = ${downloadPath}"          $color_mute
    writeLine 
fi

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip
writeLine ""

# ============================================================================
# Checks on GPU ability

writeLine "Checking GPU support" "White" "Blue" $lineWidth
writeLine

write "CUDA Present..."
if [ "$hasCUDA" == "true" ]; then writeLine "Yes" $color_success; else writeLine "No" $color_warn; fi
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
if [ ! -d "${downloadPath}" ]; then
    mkdir -p "${downloadPath}"
fi
if [ "$os" == "macos" ]; then 
    if [[ ! -w "${downloadPath}" ]]; then
        sudo chmod 777 "${downloadPath}"
    fi
fi

# For persisting settings
if [ "$os" == "linux" ]; then 
    if [ ! -d "${commonDataDir}" ]; then
        sudo mkdir -p "${commonDataDir}"
    fi
    if [[ ! -w "${commonDataDir}" ]]; then
        sudo chmod 777 "${commonDataDir}"
    fi
fi

# for the runtimes
if [ ! -d "${runtimesPath}" ]; then
    sudo mkdir -p "${runtimesPath}"
fi
sudo chmod a+w "${runtimesPath}"

writeLine "Done" $color_success


# And off we go...
success='true'

if [ "$setupMode" == 'SetupDevEnvironment' ]; then 

    # Walk through the modules directory and call the setup script in each dir
    for d in ${modulesPath}/*/ ; do

        moduleDir="$(basename $d)"
        modulePath=$d

        if [ "${modulePath: -1}" == "/" ]; then
            modulePath="${modulePath:0:${#modulePath}-1}"
        fi

        # dirname=${moduleDir,,} # requires bash 4.X, which isn't on macOS by default
        dirname=$(echo $moduleDir | tr '[:upper:]' '[:lower:]')

        if [ -f "${modulePath}/install.sh" ]; then

            writeLine
            writeLine "Processing side-loaded module ${moduleDir}" "White" "Blue" $lineWidth
            writeLine

            correctLineEndings "${modulePath}/install.sh"
            source "${modulePath}/install.sh" "install"

            # if [ $? -ne 0 ]; then success='false'; fi
        fi
    done

    writeLine
    writeLine "Modules setup Complete" $color_success

    # Now do SDK
    moduleDir="SDK"
    modulePath="${absoluteAppRootDir}/${moduleDir}"
    writeLine
    writeLine "Processing SDK" "White" "Blue" $lineWidth
    writeLine
    correctLineEndings "${modulePath}/install.sh"
    source "${modulePath}/install.sh" "install"
    # if [ $? -ne 0 ]; then success='false'; fi

    # And Demos
    moduleDir="demos"
    modulePath="${absoluteRootDir}/${moduleDir}"
    writeLine
    writeLine "Processing demos" "White" "Blue" $lineWidth
    writeLine
    correctLineEndings "${modulePath}/install.sh"
    source "${modulePath}/install.sh" "install"
    # if [ $? -ne 0 ]; then success='false'; fi

    # And finally, supporting library packages
    # TODO: Move this into the DSK install.sh script

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

    # We'll have all downloads go to the same place
    # downloadPath=${modulePath}/${downloadDir}

    if [ -f "${modulePath}/install.sh" ]; then

        writeLine
        writeLine "Installing module ${moduleDir}" "White" "Blue" $lineWidth
        writeLine

        correctLineEndings "${modulePath}/install.sh"
        source "${modulePath}/install.sh" "install"

        # if [ $? -ne 0 ]; then success='false'; fi
    fi

    # ============================================================================
    # ...and we're done.

    writeLine
    writeLine "                Module setup complete" "White" "DarkGreen" $lineWidth
    writeLine
fi

if [ "${success}" != "true" ]; then quit 1; fi

quit 0

# The return codes
# 1 - General error
# 2 - failed to install required runtime
# 3 - required runtime missing, needs installing
# 4 - required tool missing, needs installing
# 5 - unable to create Python virtual environment
# 6 - unable to download required asset
# 7 - unable to expand compressed archive
# 8 - unable to create file or directory
# 9 - required parameter not supplied
# 10 - failed to install required tool
# 100 - impossible code path executed
