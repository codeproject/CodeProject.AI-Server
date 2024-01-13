#!/bin/bash

# =============================================================================
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
#    run: sed -i 's/\r$//' setup.sh
# 2. If you get the error '#!/bin/bash - no such file or directory' then this
#    file is broken. Run head -1 setup.sh | od -c
#    You should see: 0000000   #  !  /   b   i   n   /   b   a   s   h  \n
#    But if you see: 0000000 357 273 277   #   !   /   b   i   n   /   b   a   s   h  \n
#    Then run: sed -i '1s/^\xEF\xBB\xBF//' setup.sh
#    This will correct the file. And also kill the #. You'll have to add it back
# 3. To actually run this file: bash setup.sh. In Linux/macOS,
#    obviously.
#
# ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::


# clear

# verbosity can be: quiet | info | loud
verbosity="quiet"

# Show output in wild, crazy colours
useColor=true

# Width of lines
lineWidth=70

# Whether or not downloaded modules can have their Python setup installed in The
# shared area
allowSharedPythonInstallsForModules=true

# Whether to make sudo calls even if it appears we have no admin rights. For a
# script run at the command line this will usually prompt for a password. For
# scripts run by the server this may result in a timeout or hang.
attemptSudoWithoutAdminRights=true


# Debug flags for downloads and installs

# After a successful install, run a self-test for the module just installed
doPostInstallSelfTest=true

# Perform *only* the post install self tests
selfTestOnly=false

# If files are already present, then don't overwrite if this is false
forceOverwrite=false

# If bandwidth is extremely limited, or you are actually offline, set this as
# true to force all downloads to be retrieved from cached downloads. If the 
# cached download doesn't exist the install will fail.
offlineInstall=false

# For speeding up debugging
skipPipInstall=false

# Whether or not to install all python packages in one step (-r requirements.txt)
# or step by step. Doing this allows the PIP manager to handle incompatibilities 
# better.
# ** WARNING ** There is a big tradeoff on keeping the users informed and speed/
# reliability. Generally one-step shouldn't be needed. But it often is. And if
# often doesn't actually solve problems either. Overall it's safer, but not a
# panacea
oneStepPIP=false

# Whether or not to use the jq utility for JSON parsing
useJq=true


# Basic locations

# The path to the directory containing this setup script. Will end in "\"
setupScriptDirPath=$(dirname "$0")
pushd "$setupScriptDirPath" > /dev/null
setupScriptDirPath=$(pwd -P)
popd > /dev/null

# The path to the application root dir. This is 'src' in dev, or / in production
# This setup script always lives in the app root
appRootDirPath="${setupScriptDirPath}"

# The location of large packages that need to be downloaded (eg an AWS S3 bucket name)
storageUrl='https://codeproject-ai.s3.ca-central-1.amazonaws.com/server/assets/'

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

# The location of directories relative to the root of the solution directory
runtimesDirPath="${appRootDirPath}/${runtimesDir}"
modulesDirPath="${appRootDirPath}/${modulesDir}"
downloadDirPath="${appRootDirPath}/${downloadDir}"
sdkPath="${appRootDirPath}/SDK"
sdkScriptsDirPath="${sdkPath}/Scripts"

# Who launched this script? user or server?
launchedBy="user"

# In development, we have the downloadable modules in /src/modules.
# In production, we really should have modules live in /opts/CodeProject/AI/modules.
# In docker, the modules live in /app/modules, but it's easy to map this to 
# another folder on the host machine so modules are installed outside of the
# docker container. (NOTE: alternative location for modules currently not used)
# persistedModuleDataPath="/opt/CodeProject/AI/"

# Override some values via parameters :::::::::::::::::::::::::::::::::::::::::

while [[ $# -gt 0 ]]; do
    # echo "There are $# parameters"
    param=$(echo $1 | tr '[:upper:]' '[:lower:]')
    # echo "Parm is $1 -> ${param}"

    if [ "$param" = "--launcher" ]; then
        shift
        if [[ $# -gt 0 ]]; then
            param_value=$(echo $1 | tr '[:upper:]' '[:lower:]')
            if [ "$param_value" = "server" ]; then launchedBy="server"; fi
        fi
    fi    
    if [ "$param" = "--no-color" ]; then useColor=false; fi
    if [ "$param" = "--selftest-only" ]; then selfTestOnly=true; fi
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

# Pre-setup :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

inDocker=false
if [ "$DOTNET_RUNNING_IN_CONTAINER" = "true" ]; then inDocker=true; fi

# If offline then force the system to use pre-downloaded files
if [ "$offlineInstall" = true ]; then forceOverwrite=false; fi

# launching via the server means there's no opportunity to enter a sudo password
# if required. It will eventually timeout but that's annoying. Just throw a 
# warning and move on.
if [ "$launchedBy" = "server" ]; then attemptSudoWithoutAdminRights=false; fi

# Speaking of sudo: if we're in Docker, or any other environment where sudo
# doesn't exist, then ensure the sudo command is present so calls to it in
# scripts won't simply fail. For Docker and macOS we create a no-op proxy. For
# others, we install the Real Deal
if ! command -v sudo &> /dev/null; then
    if [ "$inDocker" = true ] || [ "$os" = "macos" ]; then 
        cat > "/usr/sbin/sudo" <<EOF
#!/bin/sh
\${@}
EOF
        if [ -f /usr/sbin/sudo ]; then chmod +x /usr/sbin/sudo; fi
    else
        apt-get install sudo -y
    fi
fi

# We can't do shared installs for downloaded modules in Docker. They won't
# necessarily persist because the shared venv is in /runtimes which is in the 
# container itself, rather than then venv being in a mapped folder outside of the
# container.
if [ "$inDocker" = true ]; then 
    echo
    echo "Hi Docker! We will disable shared python installs for downloaded modules"
    echo
    allowSharedPythonInstallsForModules=false
fi

# Standard output may be used as a return value in the functions. Expose stream
# 3 so we can do 'echo "Hello, World!" >&3' within these functions for debugging
# wihtout interfering with return values.
exec 3>&1

# Execution environment, setup mode and Paths ::::::::::::::::::::::::::::::::

# If we're calling this script from the /src folder directly (and the /src
# folder actually exists) then we're Setting up the dev environment. Otherwise
# we're installing a module.
setupMode='InstallModule'
currentDirName=$(basename "$(pwd)")     # Get current dir name (not full path)
currentDirName=${currentDirName:-/} # correct for the case where pwd=/

# when executionEnvironment = "Development" this may be the case
if [ "$currentDirName" = "$srcDir" ]; then setupMode='SetupDevEnvironment'; fi

# when executionEnvironment = "Production" this may be the case
if [ "$currentDirName" = "$appDir" ]; then setupMode='SetupDevEnvironment'; fi


# In Development, this script is in the /src folder. In Production there is no
# /src folder; everything is in the root folder. So: go to the folder
# containing this script and check the name of the parent folder to see if
# we're in dev or production.
pushd "$setupScriptDirPath" >/dev/null
setupScriptDirName=$(basename "${setupScriptDirPath}")
setupScriptDirName=${setupScriptDirName:-/} # correct for the case where pwd=/
popd >/dev/null

executionEnvironment='Production'
if [ "$setupScriptDirName" = "$srcDir" ]; then executionEnvironment='Development'; fi

# The absolute path to the installer script and the app root directory. Note that
# this script (and the SDK folder) is either in the "/src" dir (for Development) 
# or the app root dir "/" (for Production)
pushd "$setupScriptDirPath" >/dev/null
if [ "$executionEnvironment" = 'Development' ]; then cd ..; fi
rootDirPath="$(pwd)"
popd >/dev/null

# Check if we're in a SSH session. If so it means we need to avoid anything GUI
inSSH=false
if [ -n "$SSH_CLIENT" ] || [ -n "$SSH_TTY" ]; then
  inSSH=true
else
  case $(ps -o comm= -p "$PPID") in sshd|*/sshd) inSSH=true;; esac
fi
if [ "$os" = 'linux' ] && [ "$inSSH" = false ]; then
    pstree -s $$ | grep sshd >/dev/null
    if [[ $? -eq 0 ]]; then inSSH=true; fi
fi

# Helper method ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

function setupPythonPaths () {

    runtimeLocation="$1"
    pythonVersion=$2

    # Version with ".'s removed
    pythonName="python${pythonVersion/./}"

    # The path to the python installation, either local or shared. The
    # virtual environment will live in here
    if [ "${runtimeLocation}" = "Local" ]; then
        pythonDirPath="${moduleDirPath}/bin/${os}/${pythonName}"
    else
        pythonDirPath="${runtimesDirPath}/bin/${os}/${pythonName}"
    fi
    virtualEnvDirPath="${pythonDirPath}/venv"

    # The path to the python intepreter for this venv
    venvPythonCmdPath="${virtualEnvDirPath}/bin/python${pythonVersion}"

    # The location where python packages will be installed for this venv
    packagesDirPath="${virtualEnvDirPath}/lib/python${pythonVersion}/site-packages/"
}

function doModuleInstall () {

    moduleDirName="$1"
    moduleDirPath="${modulesDirPath}/${moduleDirName}"

    # Get the module name, version, runtime location and python version from the
    # modulesettings.

    writeLine
    write "Reading module settings" $color_mute
    moduleName=$(getValueFromModuleSettingsFile          "${moduleDirPath}" "${moduleDirName}" "Name")
    write "." $color_mute
    moduleVersion=$(getValueFromModuleSettingsFile       "${moduleDirPath}" "${moduleDirName}" "Version" )
    write "." $color_mute
    runtime=$(getValueFromModuleSettingsFile             "${moduleDirPath}" "${moduleDirName}" "Runtime")
    write "." $color_mute
    runtimeLocation=$(getValueFromModuleSettingsFile     "${moduleDirPath}" "${moduleDirName}" "RuntimeLocation")
    write "." $color_mute
    installGPU=$(getValueFromModuleSettingsFile          "${moduleDirPath}" "${moduleDirName}" "InstallGPU")
    write "." $color_mute
    moduleStartFilePath=$(getValueFromModuleSettingsFile "${moduleDirPath}" "${moduleDirName}" "FilePath")
    write "." $color_mute
    platforms=$(getValueFromModuleSettingsFile           "${moduleDirPath}" "${moduleDirName}" "Platforms")
    write "." $color_mute
    writeLine "Done" $color_success
    
    if [ "$moduleName" = "" ]; then moduleName="$moduleDirName"; fi

    # writeLine
    writeLine "Processing module ${moduleDirName} ${moduleVersion}" "White" "Blue" $lineWidth
    writeLine

    # Convert brackets, quotes, commas and newlines to spaces
    platform_list=${platforms//[$'\r\n'\",\[\]]/ }
    # convert to array
    platform_array=($platform_list)

    can_install=false
    for item in ${platform_array[@]}; do
        item=$( echo "$item" | tr '[:upper:]' '[:lower:]' )
        # echo "Checking ${platform} against ${item}"
        if [ "$item" = "!${platform}" ]; then
            can_install=false
            break
        fi
        if [ "$item" = "all" ] || [ "$item" = "$platform" ]; then
            can_install=true
        fi
    done

    if [ "$can_install" = false ]; then
        writeLine "This module cannot be installed on this system" $color_warn
        return
    fi

    if [ "${runtimeLocation}" = "" ]; then runtimeLocation="Shared"; fi

    if [ "${allowSharedPythonInstallsForModules}" = false ]; then
        if [[ "${moduleDirPath}" == *"/modules/"* ]] && [ "${runtimeLocation}" = "Shared" ]; then
            writeLine "Downloaded modules must have local Python install. Changing install location" $color_warn
            runtimeLocation="Local"
        fi
    fi

    # Get python version from runtime
    # For python, the runtime is in the form "Python3.8", so get the "3.8".
    # However, we also allow just "python" meaning "use whatever is default"
    pythonVersion=""
    runtime=$( echo $runtime | tr '[:upper:]' '[:lower:]' | tr -d [:space:] )

    # TODO: Allow 'python<=3.9' type specifiers so it will use the native python
    #       if it's <= 3.9, or install and use 3.9 otherwise
    if [ "$runtime" = "python" ]; then

        # Get current Python version, and trim down to just major.minor
        currentPythonVersion=$(python3 --version 2>&1)

        if [ "$currentPythonVersion" != "" ]; then 
            versionNumber=$(echo $currentPythonVersion | awk -F ' ' '{print $2}')
            # echo "Current Python = $versionNumber"
            major=$(echo $versionNumber | awk -F '.' '{print $1}')
            minor=$(echo $versionNumber | awk -F '.' '{print $2}')
            pythonVersion="${major}.${minor}"
        fi

        if [ "$pythonVersion" == "" ]; then pythonVersion="3.9"; fi
        # echo "Current Python = $pythonVersion"

    elif [ "${runtime:0:6}" = "python" ]; then
        pythonVersion="${runtime:6}";
        pythonVersion=$(echo "${pythonVersion}" | tr -d [:space:])
    fi

    setupPythonPaths "${runtimeLocation}" "$pythonVersion"

    if [ "$verbosity" != "quiet" ]; then
        writeLine "moduleName        = $moduleName"        $color_info
        writeLine "moduleVersion     = $moduleVersion"     $color_info
        writeLine "runtime           = $runtime"           $color_info
        writeLine "runtimeLocation   = $runtimeLocation"   $color_info
        writeLine "installGPU        = $installGPU"        $color_info
        writeLine "pythonVersion     = $pythonVersion"     $color_info
        writeLine "virtualEnvDirPath = $virtualEnvDirPath" $color_info
        writeLine "venvPythonCmdPath = $venvPythonCmdPath" $color_info
        writeLine "packagesDirPath   = $packagesDirPath"   $color_info
    fi

    module_install_errors=""

    if [ -f "${moduleDirPath}/install.sh" ]; then

        # If a python version has been specified then we'll automatically setup
        # the correct python environment. We do this before the script runs so 
        # the script can use python in the script.
        if [ "${pythonVersion}" != "" ] && [ "$selfTestOnly" = false ]; then
            writeLine "Installing Python ${pythonVersion}"
            setupPython 
            if [ $? -gt 0 ]; then
                module_install_errors="Unable to install Python ${pythonVersion}"
            fi
        fi

        # Install the module, but only if there were no issues installing python
        # (or a python install wasn't needed)
        if [ "${module_install_errors}" = "" ] && [ "$selfTestOnly" = false ]; then

            currentDir="$(pwd)"
            correctLineEndings "${moduleDirPath}/install.sh"
            source "${moduleDirPath}/install.sh" "install"
            # if [ $? -gt 0 ] && [ "${module_install_errors}" = "" ]; then
            #    module_install_errors="${moduleName} failed to install"
            # fi
            cd "$currentDir" >/dev/null
        fi

        # If a python version has been specified then we'll automatically look 
        # for, and install, the requirements file for the module, and then also 
        # the requirements file for the SDK since it'll be assumed the Python SDK
        # will come into play.
        if [ "$module_install_errors" = "" ] && [ "${pythonVersion}" != "" ]; then
            if [ "$selfTestOnly" = false ]; then

                writeLine "Installing Python packages for ${moduleName}"

                write "Installing GPU-enabled libraries: " $color_info
                if [ "$installGPU" = "true" ]; then writeLine "If available" $color_success; else writeLine "No" $color_warn; fi

                installRequiredPythonPackages 
                if [ $? -gt 0 ]; then 
                    module_install_errors="Unable to install Python packages for ${moduleName}"; 
                fi

                writeLine "Installing Python packages for the CodeProject.AI Server SDK" 
                installRequiredPythonPackages "${sdkPath}/Python"
                if [ $? -gt 0 ]; then 
                    module_install_errors="Unable to install Python packages for CodeProject SDK"; 
                fi
            fi
        fi

        # And finally, the post install script if one was provided
        if [ "$module_install_errors" = "" ] && [ -f "${moduleDirPath}/post_install.sh" ]; then
            if [ "$selfTestOnly" = false ]; then
                writeLine "Executing post-install script for ${moduleName}"

                currentDir="$(pwd)"
                correctLineEndings "${moduleDirPath}/post_install.sh"
                source "${moduleDirPath}/post_install.sh" "post-install"
                if [ $? -gt 0 ]; then
                    module_install_errors="Error running post-install script"
                fi
                cd "$currentDir" >/dev/null
            fi
        fi

        # Perform a self-test
        if [ "${doPostInstallSelfTest}" = true ] && [ "${module_install_errors}" = "" ]; then

            pushd "${moduleDirPath}" >/dev/null
            if [ "${verbosity}" = "quiet" ]; then
                write "Self test: "
            else
                writeLine "SELF TEST START ======================================================" $color_info
            fi

            # TODO: Load these values from the module settings and set them as env variables
            #   CPAI_MODULE_ID, CPAI_MODULE_NAME, CPAI_MODULE_PATH, CPAI_MODULE_ENABLE_GPU,
            #   CPAI_ACCEL_DEVICE_NAME, CPAI_HALF_PRECISION"
            # Then load and set all env vars in modulesettings "EnvironmentVariables" collection

            testRun=false
            if [ "${pythonVersion}" != "" ]; then

                testRun=true
                if [ "${verbosity}" = "quiet" ]; then
                    "$venvPythonCmdPath" "$moduleStartFilePath" --selftest >/dev/null
                else
                    "$venvPythonCmdPath" "$moduleStartFilePath" --selftest
                fi

            elif [ "${runtime}" = "dotnet" ]; then

                # should probably generalise this to:
                #   $runtime "$moduleStartFilePath" --selftest

                exePath="./"
                # if [ "${executionEnvironment}" = "Development" ]; then
                #     exePath="./bin/Debug/net7.0/"
                # fi
                exePath="${exePath}${moduleStartFilePath//\\//}"

                if [ -f "${exePath}" ]; then
                    testRun=true
                    if [ "${verbosity}" = "quiet" ]; then
                        dotnet "${exePath}" --selftest >/dev/null
                    else
                        dotnet "${exePath}" --selftest
                    fi
                else
                    writeLine "${exePath} does not exist" $color_error
                fi

            fi

            if [[ $? -eq 0 ]]; then
                if  [ "$testRun" = "true" ]; then
                    writeLine "Self-test passed" $color_success
                else
                    writeLine "No self-test available" $color_warn
                fi
            else
                writeLine "Self-test failed" $color_error
            fi

            if [ "${verbosity}" != "quiet" ]; then
                writeLine "SELF TEST END   ======================================================" $color_info
            fi
            
            popd >/dev/null
                
        fi

    else
        writeLine "No install.sh present in ${moduleDirPath}" $color_warn
    fi

    # return result
    # echo "${module_install_errors}"
}

# import the utilities :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# A necessary evil due to cross platform editors and source control playing
# silly buggers
function correctLineEndings () {

    local filePath="$1"

    # Force correct BOM and CRLF issues in the script. Just in case
    if [[ $OSTYPE == 'darwin'* ]]; then           # macOS
         if [[ ${OSTYPE:6} -ge 13 ]]; then        # Monterry is 'darwin21' -> "21"
            sed -i'.bak' -e '1s/^\xEF\xBB\xBF//' "${filePath}" > /dev/null 2>&1 # remove BOM
            sed -i'.bak' -e 's/\r$//' "${filePath}"            > /dev/null 2>&1 # CRLF to LF
            rm "${filePath}.bak"                               > /dev/null 2>&1 # Clean up. macOS requires backups for sed
         fi
    else                                          # Linux
        sed -i '1s/^\xEF\xBB\xBF//' "${filePath}" > /dev/null 2>&1 # remove BOM
        sed -i 's/\r$//' "${filePath}"            > /dev/null 2>&1 # CRLF to LF
    fi
}

# $os, $platform and $architecture and $systemName will be set by this script
correctLineEndings "${sdkScriptsDirPath}/utils.sh"
source "${sdkScriptsDirPath}/utils.sh"

# Create directories for persisted application data
if [ "$os" = "macos" ]; then 
    commonDataDirPath='/Library/Application Support/CodeProject/AI'
else
    commonDataDirPath='/etc/codeproject/ai'
fi

# Set Flags

wgetFlags='--no-check-certificate --tries 5'
wgetFlags="${wgetFlags} --progress=bar:force:noscroll"
if [[ $(wget -h 2>&1 | grep -E 'waitretry|connect-timeout') ]]; then
    wgetFlags="${wgetFlags} --waitretry 2 --connect-timeout 15"
fi

# pipFlags='--quiet --quiet' - not actually supported, even though docs say it is
pipFlags=''
copyFlags='/NFL /NDL /NJH /NJS /nc /ns  >/dev/null'
unzipFlags='-o -qq'
tarFlags='-xf'
curlFlags='-fL --retry 5'

if [ $verbosity = "info" ]; then
    wgetFlags="${wgetFlags} --no-verbose"
    # pipFlags='--quiet' - not actually supported, even though docs say it is
    pipFlags=''
    rmdirFlags='/q'
    copyFlags='/NFL /NDL /NJH'
    unzipFlags='-q -o'
    tarFlags='-xf'
elif [ $verbosity = "loud" ]; then
    wgetFlags="${wgetFlags} -v"
    curlFlags="${curlFlags} --verbose"
    pipFlags=''
    rmdirFlags=''
    copyFlags=''
    unzipFlags='-o'
    tarFlags='-xvf'
else
    wgetFlags="${wgetFlags} -q"
    curlFlags="${curlFlags} --silent"
fi

# --progress-bar is in pip 22+. TODO: Sniff pip version, update if necessary,
# and disable progress bar if we can
# if [ "$setupMode" != 'SetupDevEnvironment' ]; then
#    pipFlags="${pipFlags} --progress-bar off"
# fi

# oneStep means we install python packages using pip -r requirements.txt rather
# than installing module by module. one-step allows the dependency manager to
# make some better calls, but also means the entire install can fail on a single
# bad (and potentially unnneeded) module. Turning one-step off means you get a
# more granular set of error messages should things go wrong, and a nicer UX.
if [ "$inDocker" = true ]; then 
    oneStepPIP=false
elif [ "$os" = "linux" ] || [ "$os" = "macos" ]; then
    oneStepPIP=false
elif [ "$os" = "windows" ]; then 
    oneStepPIP=false
fi

if [ "$useColor" != true ]; then
    pipFlags="${pipFlags} --no-color"
fi

if [ "$setupMode" = 'SetupDevEnvironment' ]; then
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

# -P = one line, -k = kb. NR=2 means get second row. $4=4th item. Add 000 = kb -> bytes
diskSpace="$(df -Pk / | awk 'NR==2 {print $4}')000"
formatted_space=$(bytesToHumanReadableKilo $diskSpace)
writeLine "${formatted_space} available on ${systemName}" $color_mute

if [ "$verbosity" != "quiet" ]; then 
    writeLine 
    writeLine "os, arch             = ${os} ${architecture}"      $color_mute
    writeLine "systemName, platform = ${systemName}, ${platform}" $color_mute
    writeLine "SSH                  = ${inSSH}"                   $color_mute
    writeLine "setupMode            = ${setupMode}"               $color_mute
    writeLine "executionEnvironment = ${executionEnvironment}"    $color_mute
    writeLine "rootDirPath          = ${rootDirPath}"             $color_mute
    writeLine "appRootDirPath       = ${appRootDirPath}"          $color_mute
    writeLine "setupScriptDirPath   = ${setupScriptDirPath}"      $color_mute
    writeLine "sdkScriptsDirPath    = ${sdkScriptsDirPath}"       $color_mute
    writeLine "runtimesDirPath      = ${runtimesDirPath}"         $color_mute
    writeLine "modulesDirPath       = ${modulesDirPath}"          $color_mute
    writeLine "downloadDirPath      = ${downloadDirPath}"         $color_mute
    writeLine 
fi

# =============================================================================
# House keeping

# if [ "$inSSH" = false ] && [ "$os" = "linux" ]; then
#     Here it would be great to install a ASKPASS help app that would prompt
#     for passwords in a GUI rather than on the terminal. This would allow us
#     to request passwords for scripts called via the CodeProject.AI dashboard.
#     Read this awesome article https://blog.djnavarro.net/posts/2022-09-04_sudo-askpass/
# fi

# Install tools that we know are available via apt-get or brew
if [ "$os" = "linux" ]; then checkForTool curl; fi
checkForTool wget
checkForTool unzip
if [ "${useJq}" = true ]; then checkForTool jq; fi
writeLine ""


# :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
# 1. Ensure directories are created and download required assets

writeLine
writeLine "General CodeProject.AI setup" "White" "DarkGreen" $lineWidth
writeLine

# Create some directories (run under a subshell, all with sudo)

CreateWriteableDir "${downloadDirPath}"   "downloads"
CreateWriteableDir "${runtimesDirPath}"   "runtimes"
CreateWriteableDir "${commonDataDirPath}" "persisted data"

writeLine


# =============================================================================
# Report on GPU ability

# GPU / CPU support :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine "GPU support" "White" "DarkGreen" $lineWidth
writeLine

# Test for CUDA 

hasCUDA=false

cuDNN_version=""
if [ "$os" = "macos" ]; then 
    cuda_version=""
elif [ "${systemName}" = "Jetson" ]; then
    hasCUDA=true
    cuda_version=$(getCudaVersion)
    cuDNN_version=$(getcuDNNVersion)
elif [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ]; then
    cuda_version=""
else 
    cuda_version=$(getCudaVersion)

    if [ "$cuda_version" != "" ]; then

        hasCUDA=true
        cuDNN_version=$(getcuDNNVersion)

        # disable this
        if [ "${systemName}" = "WSL-but-we're-ignoring-this-for-now" ]; then # we're disabling this on purpose
            checkForAdminRights
            if [ "$isAdmin" = false ]; then
                writeLine "insufficient permission to install CUDA toolkit. Rerun under sudo" $color_error
            else
                # https://stackoverflow.com/a/66486390
                cp /usr/lib/wsl/lib/nvidia-smi /usr/bin/nvidia-smi > /dev/null 2>&1
                chmod a+x /usr/bin/nvidia-smi > /dev/null 2>&1
        
                # Out of the box, WSL might come with CUDA 11.5, and no toolkit, 
                # meaning we rely on nvidia-smi for version info, which is wrong.
                # We also should be thinking about CUDA 11.8 as a minimum, so let's
                # upgrade.
                cuda_comparison=$(versionCompare $cuda_version "11.8")
                if [ "$cuda_comparison" = "-1" ]; then
                    writeLine "Upgrading WSL's CUDA install to 11.8" $color_info
                    currentDir="$(pwd)"
                    correctLineEndings "${sdkScriptsDirPath}/install_cuDNN.sh"
                    source "${sdkScriptsDirPath}/install_cuDNN.sh" 11.8
                    cd "$currentDir" >/dev/null
                fi
            fi
        fi

        # We may have nvidia-smi, but not nvcc (eg in WSL). Fix this.
        if [ -x "$(command -v nvidia-smi)" ] && [ ! -x "$(command -v nvcc)" ]; then

            installAptPackages "nvidia-cuda-toolkit"

            # The initial version we got would have been from nvidia-smi, which
            # is wrong. Redo.
            cuda_version=$(getCudaVersion)
        fi
    fi
fi

write "CUDA (NVIDIA) Present: "
if [ "$hasCUDA" = true ]; then 
    if [ "$cuDNN_version" = "" ]; then
        writeLine "Yes (CUDA $cuda_version, No cuDNN found)" $color_success
    else
        writeLine "Yes (CUDA $cuda_version, cuDNN $cuDNN_version)" $color_success
    fi
else 
    writeLine "No" $color_warn;
fi

# Test for AMD ROCm drivers 
write "ROCm (AMD) Present:    "

hasROCm=false
if [ "${systemName}" = "Raspberry Pi" ] || [ "${systemName}" = "Orange Pi" ] || [ "${systemName}" = "Jetson" ]; then
    hasROCm=false
elif [ "$os" = "linux" ]; then 
    if [ ! -x "$(command -v rocminfo)" ]; then
        write "(attempt to install rocminfo..." $color_primary
        # not using installAptPackages so we get a better UX
        sudo apt install rocminfo -y > /dev/null 2>&1 &
        spin $!
        write ") " $color_primary
    fi
    if [ -x "$(command -v rocminfo)" ]; then
        amdinfo=$(rocminfo | grep -i -E 'AMD ROCm System Management Interface') > /dev/null 2>&1
        if [[ ${amdinfo} == *'AMD ROCm System Management Interface'* ]]; then hasROCm=true; fi
    fi
fi
if [ "$hasROCm" = true ]; then writeLine "Yes" $color_success; else writeLine "No" $color_warn; fi

hasMPS=false
if [ "${platform}" = "macos-arm64" ]; then hasMPS=true; fi
write "MPS (Apple) Present:   "
if [ "$hasMPS" = true ]; then writeLine "Yes" $color_success; else writeLine "No" $color_warn; fi


# And off we go...

success=true

if [ "$setupMode" = 'SetupDevEnvironment' ]; then 

    if [ "$selfTestOnly" = false ]; then
        writeLine
        writeLine "Processing CodeProject.AI SDK" "White" "DarkGreen" $lineWidth
        writeLine

        moduleDirName="SDK"
        moduleDirPath="${appRootDirPath}/${moduleDirName}"
        
        currentDir="$(pwd)"
        correctLineEndings "${moduleDirPath}/install.sh"
        source "${moduleDirPath}/install.sh" "install"
        if [ $? -gt 0 ] || [ "${module_install_errors}" != "" ]; then success=false; fi
        cd "$currentDir" >/dev/null

        writeLine
        writeLine "Processing CodeProject.AI Server" "White" "DarkGreen" $lineWidth
        writeLine

        moduleDirName="server"
        moduleDirPath="${appRootDirPath}/${moduleDirName}"
        
        currentDir="$(pwd)"
        correctLineEndings "${moduleDirPath}/install.sh"
        source "${moduleDirPath}/install.sh" "install"
        if [ $? -gt 0 ] || [ "${module_install_errors}" != "" ]; then success=false; fi
        cd "$currentDir" >/dev/null
    fi

    # Walk through the modules directory and call the setup script in each dir,
    # as well as setting up the demos

    for d in ${modulesDirPath}/*/ ; do
        moduleDirName=$(basename "$d")
        currentDir="$(pwd)"
        doModuleInstall "${moduleDirName}"
        cd "$currentDir" >/dev/null

        if [ "${module_install_errors}" != "" ]; then
            success=false
            writeLine "Install failed: ${module_install_errors}" "$color_error"
        fi
    done

    writeLine
    writeLine "Module setup Complete" $color_success
    writeLine

    # Setup Demos
    if [ "$selfTestOnly" = false ]; then
        writeLine
        writeLine "Processing demos" "White" "Blue" $lineWidth
        writeLine

        moduleDirName="demos"
        moduleDirPath="${rootDirPath}/${moduleDirName}"

        currentDir="$(pwd)"
        correctLineEndings "${moduleDirPath}/install.sh"
        source "${moduleDirPath}/install.sh" "install"
        # Ignoring this
        # if [ $? -gt 0 ] || [ "${module_install_errors}" != "" ]; then success=false; fi   
        cd "$currentDir" >/dev/null

        writeLine "Done" $color_success
    fi
    
else

    # Install an individual module

    moduleDirPath=$(pwd)
    if [ "${moduleDirPath: -1}" = "/" ]; then
        moduleDirPath="${moduleDirPath:0:${#moduleDirPath}-1}"
    fi
    moduleDirName=$(basename "${moduleDirPath}")

    if [ "$moduleDirName" = "server" ]; then
        if [ "$selfTestOnly" = false ]; then
            moduleDirPath="${appRootDirPath}/${moduleDirName}"
            
            currentDir="$(pwd)"
            correctLineEndings "${moduleDirPath}/install.sh"
            source "${moduleDirPath}/install.sh" "install"
            cd "$currentDir" >/dev/null
        fi
    elif [ "$moduleDirName" = "demos" ]; then
        if [ "$selfTestOnly" = false ]; then
            moduleDirPath="${rootDirPath}/${moduleDirName}"
            
            currentDir="$(pwd)"
            correctLineEndings "${moduleDirPath}/install.sh"
            source "${moduleDirPath}/install.sh" "install"
            cd "$currentDir" >/dev/null
        fi
    else
        doModuleInstall "${moduleDirName}"
    fi

    if [ "${module_install_errors}" != "" ]; then
        success=false
        writeLine "Install failed: ${module_install_errors}" "$color_error"
    fi

fi

# =============================================================================
# ...and we're done.

writeLine ""
writeLine "                Setup complete" "White" "DarkGreen" $lineWidth
writeLine ""

if [ "${success}" != true ]; then
    quit 1
fi

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
# 11 - unable to copy file or directory
# 12 - parameter value invalid
# 100 - impossible code path executed
