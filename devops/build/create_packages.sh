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

# Whether or not to use the jq utility for JSON parsing
useJq=true

# Set this to false (or call script with --no-dotnet) to exclude .NET packages
# This saves time to allow for quick packaging of the easier, non-compiled modules
includeDotNet=true

# Width of lines
lineWidth=70

# Set this to true to create packages for modules in the ..\CodeProject.AI-Modules
# folder
createExternalModulePackages=true

# Whether we're creating a package via a github action
githubAction=false

# The current .NET version 
dotNetTarget="net9.0"

# Whether we're creating packages for all modules, or just a single module
singleModule=false


# Basic locations

# The name of the dir, within the root directory, where packages will be stored
packageDir='downloads/modules/packages'

# The name of the source directory (in development)
srcDirName='src'

# The name of the dir holing the SDK
sdkDir='SDK'

# The name of the dir holding the downloaded/sideloaded backend analysis services
modulesDir="modules"

# The name of the dir holding the external modules
externalModulesDir="CodeProject.AI-Modules"

# The name of the dir holding the server code itself
serverDir="CodeProject.AI-Server"

# Location of the utils script in the main CodeProject.AI server repo
utilsScriptGitHubUrl='https://raw.githubusercontent.com/codeproject/CodeProject.AI-Server/refs/heads/main/src/scripts'


# The path to the directory containing this script
thisScriptDirPath="$( cd -- "$(dirname "$0")" >/dev/null 2>&1 ; pwd -P )"

# We're assuming this script lives in /devops/build, but this script could be
# called directly from a module's folder if we're just creating a single package
if [ "$(basename $(cd .. ; pwd))" == "modules" ]; then
    # In a /modules/<moduleID> folder
    singleModule=true
    pushd "${thisScriptDirPath}/../.." >/dev/null
elif [ "$(basename $(cd .. ; pwd))" == "${externalModulesDir}" ]; then
    # In /CodeProject.AI-Modules/<moduleID> folder
    singleModule=true
    pushd "${thisScriptDirPath}/../../${serverDir}" >/dev/null
else
    # Hopefully in the /devops/build folder
    pushd "${thisScriptDirPath}/../.." >/dev/null
fi
rootDirPath="$(pwd)"
popd >/dev/null
sdkPath="${rootDirPath}/${srcDirName}/${sdkDir}"
utilsScriptsDirPath="${rootDirPath}/src/scripts"


# Override some values via parameters ::::::::::::::::::::::::::::::::::::::::::

while [[ $# -gt 0 ]]; do
    param=$(echo $1 | tr '[:upper:]' '[:lower:]')

    if [ "$param" = "--github-action" ]; then githubAction=true; fi
    if [ "$param" = "--no-dotnet" ];     then includeDotNet=false; fi
    if [ "$param" = "--no-color" ];      then useColor=false; fi
    if [ "$param" = "--verbosity" ]; then
        shift
        if [[ $# -gt 0 ]]; then
            param_value=$(echo $1 | tr '[:upper:]' '[:lower:]')
            if [[ "$param_value" =~ ^(quiet|info|loud)$ ]]; then
                # echo "Verbosity is $1 -> ${param_value}"
                verbosity="$param_value"
                echo "Setting verbosity to ${verbosity}"
            else
                echo "No Verbosity value provided"
            fi
        else
            echo "Verbosity does not match the expected values quiet|info|loud"
        fi
    fi
    shift
done

# Load vars in .env. This may update things like dotNetTarget
if [ -f ${rootDirPath}/.env ]; then
    # Export each line from the .env file
    while IFS='=' read -r key value; do
        # Ignore lines starting with `#` (comments) and empty lines
        if [[ ! "$key" =~ ^# ]] && [[ -n "$key" ]]; then
            # Trim any surrounding whitespace
            key=$(echo $key | xargs)
            value=$(echo $value | xargs)
            export "$key=$value"
        fi
    done < ${rootDirPath}/.env
else
    echo "${rootDirPath}/.env file not found"
    # exit 1
fi

# Standard output may be used as a return value in the functions. Expose stream
# 3 so we can do 'echo "Hello, World!" >&3' within these functions for debugging
# without interfering with return values.
exec 3>&1

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

if [ "${githubAction}" == true ]; then
    curl -sL ${utilsScriptGitHubUrl}/utils.sh -o utils.sh
    source utils.sh
    rm utils.sh
else
    correctLineEndings ${utilsScriptsDirPath}/utils.sh
    source ${utilsScriptsDirPath}/utils.sh
fi

# Helper method ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

function doModulePackage () {

    packageModuleId="$1"
    packageModuleDirName="$2"
    packageModuleDirPath="$3"

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
            if [ "$packageModuleId" = "ObjectDetectionYOLOv5Net" ]; then doPackage=false; fi
            if [ "$packageModuleId" = "PortraitFilter" ];           then doPackage=false; fi
            if [ "$packageModuleId" = "SentimentAnalysis" ];        then doPackage=false; fi
        fi

        if [ "$doPackage" = false ]; then
            writeLine "Skipping packaging module ${packageModuleId}..." $color_info
        else
            pushd "$packageModuleDirPath" >/dev/null

            # Read the version from the modulesettings.json file and then pass this 
            # version to the package.bat file.
            packageVersion=$(getValueFromModuleSettings "modulesettings.json" "${packageModuleId}" "Version")

            if [ "${githubAction}" != true ]; then
                write "Packaging module ${packageModuleId} ${packageVersion}..." "White"
            fi

            packageFileName="${packageModuleId}-${packageVersion}.zip"

            if [ "${githubAction}" != true ]; then
                correctLineEndings package.sh
            fi
            bash package.sh ${packageModuleId} ${packageVersion}

            if [ $? -ne 0 ]; then
                writeLine "Error in package.sh for ${packageModuleDirName}" "Red"
            fi
        
            popd >/dev/null

            # Move package into modules download cache       
            # echo Moving ${packageModuleDirPath}/${packageFileName} to ${packageDirPath}/
            mv -f ${packageModuleDirPath}/${packageFileName} ${packageDirPath}/  >/dev/null

            if [ $? -ne 0 ]; then
                writeLine "Error" "Red"
                success="false"
            elif [ "${githubAction}" == true ]; then
                echo $packageFileName
            else
                writeLine "done" "DarkGreen"
            fi
        fi
    fi

}


# The location of directories relative to the root of the solution directory
modulesDirPath="${rootDirPath}//${modulesDir}"
externalModulesDirPath="${rootDirPath}/../${externalModulesDir}"
packageDirPath="${rootDirPath}/${packageDir}"

if [ ! -d "${packageDirPath}" ]; then mkdir -p "${packageDirPath}"; fi

# Let's go

if [ "${githubAction}" != true ]; then
    scriptTitle='          Creating CodeProject.AI Module Packages'
    writeLine 
    writeLine "$scriptTitle" 'DarkCyan' 'Default' $lineWidth
    writeLine 
    writeLine '======================================================================' 'DarkGreen'
    writeLine 
    writeLine '                   CodeProject.AI Packager                            ' 'DarkGreen'
    writeLine 
    writeLine '======================================================================' 'DarkGreen'
    writeLine 
fi

if [ "$verbosity" != "quiet" ]; then 
    writeLine 
    writeLine "rootDirPath            = ${rootDirPath}"            $color_mute
    writeLine "thisScriptDirPath      = ${thisScriptDirPath}"      $color_mute
    writeLine "utilsScriptsDirPath    = ${utilsScriptsDirPath}"    $color_mute
    writeLine "modulesDirPath         = ${modulesDirPath}"         $color_mute
    writeLine "externalModulesDirPath = ${externalModulesDirPath}" $color_mute
    writeLine
fi

# And off we go...
success='true'

if [ "${singleModule}" == true ]; then

    packageModuleDirPath=$(pwd)
    packageModuleDirName="$(basename $(pwd))"
    packageModuleId=$(getModuleIdFromModuleSettings "${packageModuleDirPath}/modulesettings.json")

    if [ "${packageModuleId}" != "" ]; then
        doModulePackage "$packageModuleId" "$packageModuleDirName" "$packageModuleDirPath"
    fi

else

    # Walk through the internal modules directory and call the setup script in each dir
    for d in "${modulesDirPath}/"*/ ; do
        packageModuleDirPath=$d
        packageModuleDirName="$(basename $d)"
        packageModuleId=$(getModuleIdFromModuleSettings "${packageModuleDirPath}/modulesettings.json")

        if [ "${packageModuleId}" != "" ]; then    
            doModulePackage "$packageModuleId" "$packageModuleDirName" "$packageModuleDirPath"
        fi
    done

    if [ "$createExternalModulePackages" == "true" ]; then
        # Walk through the external modules directory and call the setup script in each dir
        for d in "${externalModulesDirPath}/"*/ ; do
            packageModuleDirName="$(basename $d)"
            packageModuleDirPath=$d
            packageModuleId=$(getModuleIdFromModuleSettings "${packageModuleDirPath}/modulesettings.json")

            if [ "${packageModuleId}" != "" ]; then
                doModulePackage "$packageModuleId" "$packageModuleDirName" "$packageModuleDirPath"
            fi
        done
    fi

fi

if [ "${githubAction}" != true ]; then
    writeLine
    writeLine "                Modules packaging Complete" "White" "DarkGreen" $lineWidth
    writeLine
fi

if [ "${success}" == "false" ]; then exit 1; fi