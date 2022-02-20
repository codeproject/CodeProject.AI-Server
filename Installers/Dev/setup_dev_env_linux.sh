#!/bin/sh
#
# CodeProject SenseAI Server 
# 
# Unix/Linux/macos Development Environment install script
# 
# We assume we're in the source code /install directory.
# 

# Sub Routines :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

function Color () {

    local colorName=$1

    local foreground="true"
    if [ "$2" == "false" ]; then
        foreground="false"
    fi

    if [ "$foreground" == "true" ]; then

        # Foreground Colours
        case "$colorName" in
            "Black" )       echo '\033[0;30m';;
            "DarkRed" )     echo '\033[0;31m';;
            "DarkGreen" )   echo '\033[0;32m';;
            "DarkYellow" )  echo '\033[0;33m';;
            "DarkBlue" )    echo '\033[0;34m';;
            "DarkMagenta" ) echo '\033[0;35m';;
            "DarkCyan" )    echo '\033[0;36m';;
            "Gray" )        echo '\033[0;37m';;
            "DarkGray" )    echo '\033[1;90m';;
            "Red" )         echo '\033[1;91m';;
            "Green" )       echo '\033[1;92m';;
            "Yellow" )      echo '\033[1;93m';;
            "Blue" )        echo '\033[1;94m';;
            "Magenta" )     echo '\033[1;95m';;
            "Cyan" )        echo '\033[1;96m';;
            "White" )       echo '\033[1;97m';;
            *)              echo "";;
        esac

    else
        # Background Colours
        case "$colorName" in
            "Black" )       echo '\033[0;40m';;
            "DarkRed" )     echo '\033[0;41m';;
            "DarkGreen" )   echo '\033[0;42m';;
            "DarkYellow" )  echo '\033[0;43m';;
            "DarkBlue" )    echo '\033[0;44m';;
            "DarkMagenta" ) echo '\033[0;45m';;
            "DarkCyan" )    echo '\033[0;46m';;
            "Gray" )        echo '\033[0;47m';;
            "DarkGray" )    echo '\033[1;100m';;
            "Red" )         echo '\033[1;101m';;
            "Green" )       echo '\033[1;102m';;
            "Yellow" )      echo '\033[1;103m';;
            "Blue" )        echo '\033[1;104m';;
            "Magenta" )     echo '\033[1;105m';;
            "Cyan" )        echo '\033[1;106m';;
            "White" )       echo '\033[1;107m';;
            *)              echo "";;
        esac

    fi
}

function WriteLine () {
    local resetColor='\033[0m'

    local color=$1
    local str=$2

    if [ "$str" == "" ]; then
        printf "\n"
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as strings without error
    if [ "$techniColor" == "true" ]; then
        local colorString=$(Color ${color})
        printf "${colorString}%s${resetColor}\n" "${str}"
    else
        printf "%s\n" "${str}"
    fi
}

function Write () {
    local resetColor="\033[0m"

    local color=$1
    local str=$2

    if [ "$str" == "" ];  then
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as strings without error
    if [ "$techniColor" == "true" ]; then
        local colorString=$(Color ${color})
        printf "${colorString}%s${resetColor}" "${str}"
    else
        printf "%s" "$str"
    fi
}

function checkForTool () {

    local name=$1

    if command -v ${name} &> /dev/null; then
        return
    fi

    WriteLine "$color_primary" ""
    WriteLine "$color_primary" ""
    WriteLine "$color_primary" "------------------------------------------------------------------------"
    WriteLine "$color_error" "Error: ${name} is not installed on your system"

    if [ "$platform" == "macos" ]; then
        WriteLine "$color_error" "       Please run 'brew install ${name}'"

        if ! command -v brew &> /dev/null; then
            WriteLine "" ""
            WriteLine "$color_warn" "Error: It looks like you don't have brew installed either"
            WriteLine "$color_warn" "       Please run:"
            WriteLine "$color_warn" "       /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)'"
            exit
        fi
    else
        WriteLine "$color_error" "       Please run 'sudo apt install ${name}'"
    fi

    WriteLine "" ""
    WriteLine "" ""
    exit
}

function errorNoPython () {
    WriteLine "$color_primary" ""
    WriteLine "$color_primary" ""
    WriteLine "$color_primary" "------------------------------------------------------------------------"
    WriteLine "$color_error" "Error: Python 3.7 not installed"
    WriteLine "" ""
    WriteLine "" ""
    
    exit
}

function spin () {

    local pid=$1

    spin[0]="-"
    spin[1]="\\"
    spin[2]="|"
    spin[3]="/"

    while kill -0 $pid 2> /dev/null; do
        for i in "${spin[@]}"
        do
            echo -ne "\b$i"
            sleep 0.1
        done
    done

    echo -ne "\b"
}

function Download () {

    local storageUrl=$1
    local downloadToDir=$2
    local fileToGet=$3
    local dirToSave=$4
    local message=$5

    # storageUrl = "https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
    # downloadToDir = "downloads/" - relative to the current directory
    # fileToGet = packages_for_gpu.zip
    # dirToSave = packages

    if [ "${message}" == "" ]; then
        message="Downloading ${fileToGet}..."
    fi

    # WriteLine "$color_primary" "Downloading ${fileToGet} to ${downloadToDir}/${dirToSave}"

    Write "$color_primary" "$message"

    if [ -d "${downloadToDir}/${dirToSave}" ]; then
        WriteLine "$color_info" "Directory already exists"
        return 0 # This is ok and assumes it's already downloaded. Whether that's true or not...
    fi

    extension="${fileToGet:(-3)}"
    if [ ! "${extension}" == ".gz" ]; then
        extension="${fileToGet:(-4)}"
        if [ ! "${extension}" == ".zip" ]; then
            WriteLine "$color_error" "Unknown and unsupported file type for file ${fileToGet}"

            exit    # no point in carrying on
            # return 1
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        # WriteLine "$color_warn" "Downloading ${fileToGet} to ${dirToSave}.zip in ${downloadToDir}" 
        wget $wgetFlags --show-progress -O "${downloadToDir}/${fileToGet}" -P "${downloadToDir}" \
                                           "${storageUrl}${fileToGet}"
        
        status=$?    
        if [ $status -ne 0 ]; then
            WriteLine "$color_error" "The wget command failed for file ${fileToGet}."

            exit    # no point in carrying on
            # return 2
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        WriteLine "$color_error" "The downloaded file '${fileToGet}' doesn't appear to exist."

        exit    # no point in carrying on
        # return 3
    fi

    Write "$color_info" "Expanding..."

    pushd "${downloadToDir}" >/dev/null

    if [ ! -d "${dirToSave}" ]; then
      mkdir -p "${dirToSave}"
    fi

    if [ "${extension}" == ".gz" ]; then
        tar $tarFlags "${fileToGet}" -C "${dirToSave}" &  # execute and continue
    else
        unzip $unzipFlags "${fileToGet}" -d "${dirToSave}" &  # execute and continue
    fi
    
    spin $! # process ID of the unzip/tar call
    
    popd >/dev/null

    # rm /s /f /q "${downloadToDir}/${fileToGet}" >/dev/null

    WriteLine "$color_success" "Done."
}

function getBackground () {

    if [ "$platform" == "macos" ]; then
        osascript -e \
        'tell application "Terminal"
            get background color of selected tab of window 1
        end tell'
    else
        echo "0,0,0" # we're making assumptions here
    fi
}

function isDarkMode () {
    local bgColor=$(getBackground)
    IFS=','; colors=($bgColor); IFS=' ';
    if [ ${colors[0]} -lt 20000 ] && [ ${colors[1]} -lt 20000 ] && [ ${colors[2]} -lt 20000 ]; then
        echo "true"
    else
        echo "false"
    fi
}


# Main script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

clear

if [[ $OSTYPE == 'darwin'* ]]; then
    platform="macos"
else
    platform="linux"
fi

# verbosity can be: quiet | info | loud
verbosity="quiet"

# If files are already present, then don't overwrite if this is false
forceOverwrite=false

# Basic locations

# The location of the solution root directory relative to this script
rootPath="../.."

# SenseAI specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# The name of the dir holding the frontend API server
senseAPIDir="API"

# DeepStack specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# The name of the dir holding the DeepStack analysis services
deepstackDir="DeepStack"

# The name of the dir containing the Python code itself
intelligenceDir="intelligencelayer"

# The name of the dir containing the AI models themselves
modelsDir="assets"

# The name of the dir containing persisted DeepStack data
datastoreDir="datastore"

# The name of the dir containing temporary DeepStack data
tempstoreDir="tempstore"

# Yolo.Net specific
yoloNetDir="CodeProject.SenseAI.AnalysisLayer.Yolo"
yoloModelsDir="yoloModels"

# Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# The location of large packages that need to be downloaded
# a. From AWS
storageUrl="https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
# b. Use a local directory rather than from online. Handy for debugging.
# storageUrl="/mnt/c/dev/CodeProject/CodeProject.SenseAI/install/cached_downloads/"

# The name of the source directory
srcDir="src"

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir="downloads"

# The name of the dir containing the Python interpreter
pythonDir="python37"

# The name of the dir holding the backend analysis services
analysisLayerDir="AnalysisLayer"

# Absolute paths :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
# The absolute path to the root directory of CodeProject.SenseAI
currentDir="$(pwd)"
cd $rootPath
absoluteRootDir="$(pwd)"
cd $currentDir

# The location of directories relative to the root of the solution directory
analysisLayerPath=${absoluteRootDir}/${srcDir}/${analysisLayerDir}
downloadPath=${absoluteRootDir}/Installers/${downloadDir}

# Show output in wild, crazy colours
techniColor="true"
if [ "$1" = "false" ]; then
    techniColor="false"
else
    darkmode=$(isDarkMode)
    echo "Darkmode = $darkmode"
    if [ "$darkmode" == "false" ]; then
        color_primary="Black"
        color_mute="Gray"
        color_info="DarkMagenta"
        color_success="DarkGreen"
        color_warn="DarkYellow"
        color_error="Red"
    else
        color_primary="White"
        color_mute="Gray"
        color_info="Yellow"
        color_success="Green"
        color_warn="DarkYellow"
        color_error="Red"
    fi
fi

# Set Flags

wgetFlags="-q"
pipFlags="-q -q"
copyFlags="/NFL /NDL /NJH /NJS /nc /ns  >/dev/null"
unzipFlags="-qq"
tarFlags="xf"

if [ $verbosity == "info" ]; then
    wgetFlags="--no-verbose"
    pipFlags="-q"
    rmdirFlags="/q"
    copyFlags="/NFL /NDL /NJH"
    unzipFlags="-q"
    tarFlags="xf"
elif [ $verbosity == "loud" ]; then
    wgetFlags="-v"
    pipFlags=""
    rmdirFlags=""
    copyFlags=""
    unzipFlags=""
    tarFlags="xvf"
fi

WriteLine "$color_info" "Setting up CodeProject.SenseAI Development Environment"
WriteLine "$color_primary" ""
WriteLine "$color_primary" "========================================================================"
WriteLine "$color_primary" ""
WriteLine "$color_primary" "                 CodeProject SenseAI Installer"
WriteLine "$color_primary" ""
WriteLine "$color_primary" "========================================================================"
WriteLine "$color_primary" ""

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip


# ============================================================================
# Ensure directories are created and download required assets

# Create some directories
Write "$color_primary" "Creating Directories..."

# For downloading assets
mkdir -p "${downloadPath}"

# For DeepStack
deepStackPath=${analysisLayerPath}/${deepstackDir}
mkdir -p "${deepStackPath}/${tempstoreDir}"
mkdir -p "${deepStackPath}/${datastoreDir}"

# For Yolo.NET
yoloNetPath=${analysisLayerPath}/${yoloNetDir}

WriteLine "$color_success" "Done"

Write "$color_primary" "Downloading modules and models: "
WriteLine "$color_mute" "Starting"

pythonInstallPath="${analysisLayerPath}/bin/${platform}/${pythonDir}"

# Clean up directories to force a re-download if necessary
if [ "${forceOverwrite}" == "true" ]; then

    # Force re-download
    rm -rf "${downloadPath}/${platform}/${pythonDir}"
    rm -rf "${downloadPath}/${modelsDir}"
    rm -rf "${downloadPath}/${yoloModelsDir}"

    # force overwrite
    rm -rf "${pythonInstallPath}"
    rm -rf "${deepStackPath}/${modelsDir}"
    rm -rf "${yoloNetPath}/${modelsDir}"
fi

if [ ! -d "${pythonInstallPath}" ]; then

    if [ "$platform" == "macos" ]; then
        Download $storageUrl $downloadPath "python3.7.12-osx64.tar.gz" "${platform}/${pythonDir}" "Downloading Python interpreter..."
    else
        # To create the miniconda tarball:  tar -czvf miniconda-python37.tar.gz --exclude=pkgs/* ./miniconda3/
        Download $storageUrl $downloadPath "miniconda-python37.tar.gz" "${platform}/${pythonDir}" "Downloading Python interpreter..."
    fi

    if [ -d "${downloadPath}/${platform}/${pythonDir}" ]; then
        if [ "$platform" == "macos" ]; then
            mkdir -p "${analysisLayerPath}/bin/${platform}"
            mv "${downloadPath}/${platform}/${pythonDir}" "${analysisLayerPath}/bin/${platform}/"
        else
            # Having troubles within WSL. Doing a small hack
            # mkdir -p "${analysisLayerPath}/bin/${platform}/${pythonDir}"
            # mv "${downloadPath}/${platform}/${pythonDir}/miniconda3/*" "${analysisLayerPath}/bin/${platform}/${pythonDir}/"

            mkdir -p "${analysisLayerPath}/bin/${platform}"
            mv "${downloadPath}/${platform}/${pythonDir}/miniconda3/" "${analysisLayerPath}/bin/${platform}/"
            mv "${analysisLayerPath}/bin/${platform}/miniconda3" "${analysisLayerPath}/bin/${platform}/${pythonDir}"
        fi
    fi
fi

# Download whatever packages are missing 
if [ ! -d "${deepStackPath}/${modelsDir}" ]; then
    Download $storageUrl $downloadPath "models.zip" "${modelsDir}" "Downloading models..."
    if [ -d "${downloadPath}/${modelsDir}" ]; then
        mv -f "${downloadPath}/${modelsDir}" "${deepStackPath}/${modelsDir}"
    fi
fi

if [ ! -d "${yoloNetPath}/${modelsDir}" ]; then
    Download $storageUrl $downloadPath "yolonet-models.zip" "${yoloModelsDir}" "Downloading Yolo.Net models..."
    if [ -d "${downloadPath}/${yoloModelsDir}" ]; then
        mv -f "${downloadPath}/${yoloModelsDir}" "${yoloNetPath}/${modelsDir}"
    fi
fi

WriteLine "$color_success" "Modules and models downloaded"

# Copy over the startup script
# Write "$color_primary" "Copying over startup script..."
# cp "Start_SenseAI.sh" "${absoluteRootDir}"
# WriteLine "$color_success" "Done."


# ============================================================================
# 2. Create & Activate Virtual Environment: DeepStack specific / Python 3.7

Write "$color_primary" "Creating Virtual Environment..."

if [ -d  "${pythonInstallPath}/venv"  ]; then
    WriteLine "$color_success" "Already present"
else
    "${pythonInstallPath}/bin/python3" -m venv "${pythonInstallPath}/venv" &

    spin $! # process ID of the unzip/tar call
    WriteLine "$color_success" "Done"
fi

Write "$color_primary" "Enabling our Virtual Environment..."
pushd "${pythonInstallPath}" >/dev/null

# PYTHONHOME="$(pwd)/venv"
# export PYTHONHOME

VIRTUAL_ENV="$(pwd)/venv"
export VIRTUAL_ENV

PATH="$VIRTUAL_ENV/bin:$PATH"
export PATH

pythonInterpreterPath="${VIRTUAL_ENV}/bin/python3"

PS1="(venv) ${PS1:-}"

popd >/dev/null
WriteLine "$color_success" "Done"

# Ensure Python Exists
Write "$color_primary" "Checking for Python 3.7..."
pyVersion=$($pythonInterpreterPath --version)
Write "$color_mute" "Found ${pyVersion}. "

echo $pyVersion | grep "3.7" >/dev/null
if [ $? -ne 0 ]; then
    errorNoPython
fi 
WriteLine "$color_success" "present"

if [ "${verbosity}" == "loud" ]; then
    whereis python
fi

# ============================================================================
# 3. Install PIP packages

# ASSUMPTION: If venv/Lib/python3.7/site-packages/torch exists then no need to do this

Write "$color_primary" "Checking for required packages..."
if [ ! -d "${VIRTUAL_ENV}/Lib/python3.7/site-packages/torch" ]; then

    WriteLine "$color_info" "Installing"

    Write "$color_primary" "  - Installing Python package manager..."
    $pythonInterpreterPath -m pip install --trusted-host pypi.python.org \
                                          --trusted-host files.pythonhosted.org \
                                          --trusted-host pypi.org --upgrade pip $pipFlags &
    spin $!
    WriteLine "$color_success" "Done"

    # We'll do this the long way so we can see some progress
    # Write "$color_primary" "Installing Packages into Virtual Environment..."
    # pip install -r "${deepStackPath}/${intelligenceDir}/requirements.txt" $pipFlags
    # WriteLine "$color_success" "Success"

    # Open requirements.txt and grab each line. We need to be careful with --find-links lines
    requirementsFile="${deepStackPath}/${intelligenceDir}/requirements.txt"

    currentOption=""

    IFS=$'\n' # set the Internal Field Separator as end of line
    cat "${requirementsFile}" | while read -r line
    do

        line="$(echo $line | tr -d '\r\n')"    # trim newlines / CRs

        if [ "${line}" == "" ]; then
            currentOption=""
        elif [ "${line:0:2}" == "##" ]; then
            currentOption=""
        elif [ "${line:0:12}" == "--find-links" ]; then
            currentOption="${line}"
        else
            
            module="${line}"
            description=""
        
            # breakup line into module name and description
            IFS='#'; tokens=($module); IFS=$'\n';

            if [ ${#tokens[*]} -gt 1 ]; then
                module="${tokens[0]}"
                description="${tokens[1]}"
            fi

            if [ "${description}" == "" ]; then
                description="Installing ${module}"
            fi

            if [ "${module}" != "" ]; then

                Write "$color_primary" "  ${description}..."

                if [ "${verbosity}" == "quiet" ]; then
                    $pythonInterpreterPath -m pip install $module $currentOption $pipFlags >/dev/null 2>/dev/null &
                    spin $!
                else
                    $pythonInterpreterPath -m pip install $module $currentOption $pipFlags
                fi

                WriteLine "$color_success" "Done"

            fi

            currentOption=""

        fi

    done
    unset IFS
else
    WriteLine "$color_success" "present."
fi

# ============================================================================
# ...and we're done.

WriteLine "$color_info" "Development Environment setup complete" 
WriteLine "$color_primary" ""
WriteLine "$color_primary" ""