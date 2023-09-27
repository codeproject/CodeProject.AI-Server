#!/bin/bash

# ============================================================================
#
# CodeProject.AI Server 
#
# Create packages script for Linux / macOS
#
# This script will look for a package.bat script each of the modules directories
# and execute that script. The package.bat script is responsible for packaging
# up everything needed for the module to be ready to install.

# verbosity can be: quiet | info | loud
verbosity="quiet"

# Show output in wild, crazy colours
useColor="true"

# Width of lines
lineWidth=70


# Basic locations

# The path to the directory containing the install scripts
#installerScriptsPath=$(dirname "$0")
installerScriptsPath="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

# The name of the source directory (in development)
srcDir='src'

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir='downloads'

# The name of the dir holding the downloaded/sideloaded backend analysis services
modulesDir="modules"


# Override some values via parameters ::::::::::::::::::::::::::::::::::::::::::
while getopts ":h" option; do
    param=$(echo $option | tr '[:upper:]' '[:lower:]')

    if [ "$param" == "--no-color" ]; then set useColor="false"; fi
done

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

# The absolute path to the installer script and the root directory. Note that
# this script (and the SDK folder) is either in the /src dir or the root dir
sdkScriptsPath="${installerScriptsPath}/SDK/Scripts"
pushd "$installerScriptsPath" >/dev/null
if [ "$executionEnvironment" == 'Development' ]; then cd ..; fi
absoluteRootDir="$(pwd)"
popd >/dev/null

absoluteAppRootDir="${installerScriptsPath}"

# import the utilities :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

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


# Platform can define where things are located :::::::::::::::::::::::::::::::

# The location of directories relative to the root of the solution directory
modulesPath="${absoluteAppRootDir}/${modulesDir}"
downloadPath="${absoluteAppRootDir}/${downloadDir}"

# Let's go

if [ "$setupMode" != 'SetupDevEnvironment' ]; then
    scriptTitle='          Creating CodeProject.AI Module Downloads'
else
    writeLine "Can't run in Production. Exiting." "Red"
    exit
fi

writeLine 
writeLine "$scriptTitle" 'DarkCyan' 'Default' $lineWidth
writeLine 
writeLine '======================================================================' 'DarkGreen'
writeLine 
writeLine '                   CodeProject.AI Packager                           ' 'DarkGreen'
writeLine 
writeLine '======================================================================' 'DarkGreen'
writeLine 


if [ "$verbosity" != "quiet" ]; then 
    writeLine 
    writeLine "executionEnvironment  = ${executionEnvironment}"  $color_mute
    writeLine "installerScriptsPath  = ${installerScriptsPath}"  $color_mute
    writeLine "sdkScriptsPath        = ${sdkScriptsPath}"        $color_mute
    writeLine "absoluteAppRootDir    = ${absoluteAppRootDir}"    $color_mute
    writeLine "modulesPath           = ${modulesPath}"           $color_mute
    writeLine
fi

# And off we go...
success='true'

# Walk through the modules directory and call the setup script in each dir
for d in ${modulesPath}/*/ ; do

    packageModuleDir="$(basename $d)"
    packageModuleId=$packageModuleDir
    packageModulePath=$d

    if [ "${packageModulePath: -1}" == "/" ]; then
        packageModulePath="${packageModulePath:0:${#packageModulePath}-1}"
    fi

    # dirname=${moduleDir,,} # requires bash 4.X, which isn't on macOS by default
    dirname=$(echo $packageModuleDir | tr '[:upper:]' '[:lower:]')

    if [ -f "${packageModulePath}/package.sh" ]; then

        pushd "$packageModulePath" >/dev/null

        # Read the version from the modulesettings.json file and then pass this 
        # version to the package.bat file.
        packageVersion=$(getVersionFromModuleSettings "modulesettings.json" "Version")

        write "Packaging module ${packageModuleId} ${packageVersion}..." "White"

        correctLineEndings package.sh
        bash package.sh ${packageModuleId} ${packageVersion}

        if [ $? -ne 0 ]; then
            writeLine "Error in package.sh for ${packageModuleDir}" "Red"
        fi
       
        popd >/dev/null
        
        # Move package into modules download cache       
        # echo Moving ${packageModulePath}/${packageModuleId}-${packageVersion}.zip to ${downloadPath}/modules/
        mv -f ${packageModulePath}/${packageModuleId}-${packageVersion}.zip ${downloadPath}/modules/  >/dev/null

        if [ $? -ne 0 ]; then
            writeLine "Error" "Red"
            success="false"
        else
            writeLine "Done" "DarkGreen"
        fi
    fi
done

writeLine
writeLine "                Modules packaging Complete" "White" "DarkGreen" $lineWidth
writeLine

if [ "${success}" == "false" ]; then exit 1; fi