#!/bin/bash
# ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
# CodeProject.AI Server 
# 
# Linux/macOS Development Environment install script
# 
# We assume we're in the source code /Installers/Dev directory.
# 
# Notes for Windows users:
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

# A necessary evil due to cross platform editors and source control playing
# silly buggers
function correctLineEndings () {

    local filePath=$1

    # Force correct BOM and CRLF issues in the script. Just in case
    if [ "$platform" == "linux" ]; then 
        sed -i '1s/^\xEF\xBB\xBF//' "${filePath}" # remove BOM
        sed -i 's/\r$//' "${filePath}"            # CRLF to LF
    else
         if [[ ${OSTYPE:6} -ge 13 ]]; then       # Monterry is 'darwin21' -> "21"
            sed -i'.bak' -e '1s/^\xEF\xBB\xBF//' "${filePath}" # remove BOM
            sed -i'.bak' -e 's/\r$//' "${filePath}"            # CRLF to LF
            rm "${filePath}.bak"
         fi
    fi
}


clear

# import the utilities
correctLineEndings $(dirname "$0")/utils.sh

# "platform" will be set by this script
source $(dirname "$0")/utils.sh

useColor="true"

# should we use GPU enabled libraries?
enableGPU="false"

# are we ready to support CUDA enabled GPUs?
supportCUDA="false"

# verbosity can be: quiet | info | loud
verbosity="quiet"

# If files are already present, then don't overwrite if this is false
forceOverwrite="false"


# Basic locations

# The location of the solution root directory relative to this script
rootPath='../..'

# CodeProject.AI Server specific ::::::::::::::::::::::::::::::::::::::::::::::::::::

# The name of the dir holding the frontend API server
APIDir='API'

# Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# The location of large packages that need to be downloaded
# a. From AWS
storageUrl='https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/'
# b. Use a local directory rather than from online. Handy for debugging.
# storageUrl='/mnt/c/dev/CodeProject/CodeProject.AI/install/cached_downloads/'

# The name of the source directory
srcDir='src'

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir='downloads'

# The name of the dir holding the backend analysis services
analysisLayerDir='AnalysisLayer'

# Absolute paths :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
# The absolute path to the root directory of CodeProject.AI
currentDir="$(pwd)"
cd $rootPath
absoluteRootDir="$(pwd)"
cd $currentDir

# The location of directories relative to the root of the solution directory
analysisLayerPath="${absoluteRootDir}/${srcDir}/${analysisLayerDir}"
downloadPath="${absoluteRootDir}/Installers/${downloadDir}"

# Set Flags

wgetFlags='-q --no-check-certificate'
pipFlags='--quiet --quiet'
copyFlags='/NFL /NDL /NJH /NJS /nc /ns  >/dev/null'
unzipFlags='-qq'
tarFlags='xf'

if [ $verbosity == "info" ]; then
    wgetFlags='--no-verbose --no-check-certificate'
    pipFlags='--quiet'
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

if [ "$platform" == "macos" ] || [ "$platform" == "macos-arm" ]; then
    pipFlags="${pipFlags} --no-cache-dir"
fi

if [ "$useColor" != "true" ]; then
    pipFlags="${pipFlags} --no-color"
fi


writeLine '          Setting up CodeProject.AI Development Environment           ' 'DarkCyan'
writeLine '                                                                      ' 'DarkGreen'
writeLine '======================================================================' 'DarkGreen'
writeLine '                                                                      ' 'DarkGreen'
writeLine '                   CodeProject.AI Installer                           ' 'DarkGreen'
writeLine '                                                                      ' 'DarkGreen'
writeLine '======================================================================' 'DarkGreen'
writeLine '                                                                      ' 'DarkGreen'

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip

if [ "$platform" == "linux" ] && [ "$EUID" -ne 0 ]; then
    writeLine "Please run this script as root: sudo bash setup_dev_env_linux.sh" $color_error
    exit
fi

# ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
# 1. Ensure directories are created and download required assets

announcement=$(printf %-70s "                         CodeProject.AI setup")
writeLine
writeLine "${announcement}" "White" "DarkGreen"
writeLine

# Create some directories

# For downloading assets
if [ $verbosity == "loud" ]; then writeLine "downloadPath is ${downloadPath}"; fi;

write "Creating Directories..." $color_primary
mkdir -p "${downloadPath}"
writeLine "Done" $color_success

if [ "$platform" == "macos" ] || [ "$platform" == "macos-arm" ]; then 
    if [[ ! -w "${downloadPath}" ]]; then
        write "We'll need to run under root to set permissions. " $color_warn
        sudo chmod 777 "${downloadPath}"
    fi
fi

# source ${moduleDir}/install.sh

# Walk through the modules directory and call the setup script in each dir
for d in ${analysisLayerPath}/*/ ; do

    moduleDir="$(basename $d)"
    modulePath=$d

    if [ "${modulePath: -1}" == "/" ]; then
        modulePath="${modulePath:0:${#modulePath}-1}"
    fi

    
    # dirname=${moduleDir,,} # requires bash 4.X, which isn't on macOS by default
    dirname=$(echo $moduleDir | tr '[:upper:]' '[:lower:]')
    if [ "${dirname}" != 'bin' ]; then

       if [ -f "${modulePath}/install.sh" ]; then

            # Pad right to 70 chars
            announcement=$(printf %-70s "Processing ${moduleDir}")

            writeLine
            writeLine "${announcement}" "White" "Blue"
            writeLine

            correctLineEndings "${modulePath}/install.sh"
            source "${modulePath}/install.sh"
        fi
    fi
done

# libfontconfig1 is required for SkiaSharp, libgdplus is required for System.Drawing
if [ "${verbosity}" == "quiet" ]; then
    write "Installing supporting image libraries..."
else
    writeLine "Installing supporting image libraries..."
fi

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


# ============================================================================
# ...and we're done.

announcement=$(printf %-70s "                Development Environment setup complete")
writeLine
writeLine "${announcement}" "White" "DarkGreen"
writeLine

quit