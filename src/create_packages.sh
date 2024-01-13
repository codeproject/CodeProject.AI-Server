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
useColor=true

# Set this to false (or call script with --no-dotnet) to exclude .NET packages
# This saves time to allow for quick packaging of the easier, non-compiled modules
includeDotNet=true

# Width of lines
lineWidth=70


# Basic locations

# The path to the directory containing the setup scripts
#setupScriptDirPath=$(dirname "$0")
setupScriptDirPath="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

# The name of the source directory (in development)
srcDir='src'

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir='downloads'

# The name of the dir holding the downloaded/sideloaded backend analysis services
modulesDir="modules"


# Override some values via parameters ::::::::::::::::::::::::::::::::::::::::::
for i in "$@"; do
  case $i in
    --no-dotnet) includeDotNet=false ;;
    --no-color)  useColor=false ;;
    -*|--*)      echo "Unknown option $i" ;;
    *) ;;
  esac
done

# In Development, this script is in the /src folder. In Production there is no
# /src folder; everything is in the root folder. So: go to the folder
# containing this script and check the name of the parent folder to see if
# we're in dev or production.
pushd "$setupScriptDirPath" >/dev/null
setupScriptDirName="$(basename ${setupScriptDirPath})"
setupScriptDirName=${setupScriptDirName:-/} # correct for the case where pwd=/
popd >/dev/null

executionEnvironment='Production'
if [ "$setupScriptDirName" == "$srcDir" ]; then executionEnvironment='Development'; fi

# The absolute path to the installer script and the root directory. Note that
# this script (and the SDK folder) is either in the /src dir or the root dir
sdkScriptsDirPath="${setupScriptDirPath}/SDK/Scripts"
pushd "$setupScriptDirPath" >/dev/null
if [ "$executionEnvironment" == 'Development' ]; then cd ..; fi
rootDirPath="$(pwd)"
popd >/dev/null

appRootDirPath="${setupScriptDirPath}"

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

correctLineEndings ${sdkScriptsDirPath}/utils.sh

# "platform" will be set by this script
source ${sdkScriptsDirPath}/utils.sh


# Platform can define where things are located :::::::::::::::::::::::::::::::

# The location of directories relative to the root of the solution directory
modulesDirPath="${appRootDirPath}/${modulesDir}"
downloadDirPath="${appRootDirPath}/${downloadDir}"

# Let's go

scriptTitle='          Creating CodeProject.AI Module Downloads'
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
    writeLine "executionEnvironment = ${executionEnvironment}" $color_mute
    writeLine "appRootDirPath       = ${appRootDirPath}"       $color_mute
    writeLine "setupScriptDirPath   = ${setupScriptDirPath}"   $color_mute
    writeLine "sdkScriptsDirPath    = ${sdkScriptsDirPath}"    $color_mute
    writeLine "modulesDirPath       = ${modulesDirPath}"       $color_mute
    writeLine
fi

# And off we go...
success='true'

# Walk through the modules directory and call the setup script in each dir
for d in ${modulesDirPath}/*/ ; do

    packageModuleDirName="$(basename $d)"
    packageModuleId=$packageModuleDirName
    packageModuleDirPath=$d

    if [ "${packageModuleDirPath: -1}" == "/" ]; then
        packageModuleDirPath="${packageModuleDirPath:0:${#packageModuleDirPath}-1}"
    fi

    # dirname=${moduleDirName,,} # requires bash 4.X, which isn't on macOS by default
    dirname=$(echo $packageModuleDirName | tr '[:upper:]' '[:lower:]')

    if [ -f "${packageModuleDirPath}/package.sh" ]; then

        doPackage=true

        # TODO: Sniff the modulesettings.json file to get the runtime, and if it's
        # .NET do the test.
        if [ "$includeDotNet" = false ]; then
            if [ "$packageModuleId" = "ObjectDetectionNet" ]; then doPackage=false; fi
            if [ "$packageModuleId" = "PortraitFilter" ];     then doPackage=false; fi
            if [ "$packageModuleId" = "SentimentAnalysis" ];  then doPackage=false; fi
        fi

        if [ "$doPackage" = false ]; then
            writeLine "Skipping packaging module ${packageModuleId}..." $color_info
        else
            pushd "$packageModuleDirPath" >/dev/null

            # Read the version from the modulesettings.json file and then pass this 
            # version to the package.bat file.
            packageVersion=$(getValueFromModuleSettings "modulesettings.json" "Version")

            write "Packaging module ${packageModuleId} ${packageVersion}..." "White"

            correctLineEndings package.sh
            bash package.sh ${packageModuleId} ${packageVersion}

            if [ $? -ne 0 ]; then
                writeLine "Error in package.sh for ${packageModuleDirName}" "Red"
            fi
        
            popd >/dev/null
            
            # Move package into modules download cache       
            # echo Moving ${packageModuleDirPath}/${packageModuleId}-${packageVersion}.zip to ${downloadDirPath}/modules/
            mv -f ${packageModuleDirPath}/${packageModuleId}-${packageVersion}.zip ${downloadDirPath}/modules/  >/dev/null

            if [ $? -ne 0 ]; then
                writeLine "Error" "Red"
                success="false"
            else
                writeLine "Done" "DarkGreen"
            fi
        fi
    fi
done

writeLine
writeLine "                Modules packaging Complete" "White" "DarkGreen" $lineWidth
writeLine

if [ "${success}" == "false" ]; then exit 1; fi