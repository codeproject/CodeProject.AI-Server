#!/bin/bash
#
# CodeProject SenseAI Server 
# 
# Unix/Linux/macOS Development Environment install script
# 
# We assume we're in the source code /install directory.
# 

# Sub Routines :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

function Color () {

    local foreground=$1
    local background=$2

    if [ "$background" == "" ]; then
        background="Default"
    fi

    local colorString='\033['

    # Foreground Colours
    case "$foreground" in
        "Default")      colorString='\033[0;39m';;
        "Black" )       colorString='\033[0;30m';;
        "DarkRed" )     colorString='\033[0;31m';;
        "DarkGreen" )   colorString='\033[0;32m';;
        "DarkYellow" )  colorString='\033[0;33m';;
        "DarkBlue" )    colorString='\033[0;34m';;
        "DarkMagenta" ) colorString='\033[0;35m';;
        "DarkCyan" )    colorString='\033[0;36m';;
        "Gray" )        colorString='\033[0;37m';;
        "DarkGray" )    colorString='\033[1;90m';;
        "Red" )         colorString='\033[1;91m';;
        "Green" )       colorString='\033[1;92m';;
        "Yellow" )      colorString='\033[1;93m';;
        "Blue" )        colorString='\033[1;94m';;
        "Magenta" )     colorString='\033[1;95m';;
        "Cyan" )        colorString='\033[1;96m';;
        "White" )       colorString='\033[1;97m';;
        *)              colorString='\033[0;39m';;
    esac

    # Background Colours
    case "$background" in
        "Default" )     colorString="${colorString}\033[49m";;
        "Black" )       colorString="${colorString}\033[40m";;
        "DarkRed" )     colorString="${colorString}\033[41m";;
        "DarkGreen" )   colorString="${colorString}\033[42m";;
        "DarkYellow" )  colorString="${colorString}\033[43m";;
        "DarkBlue" )    colorString="${colorString}\033[44m";;
        "DarkMagenta" ) colorString="${colorString}\033[45m";;
        "DarkCyan" )    colorString="${colorString}\033[46m";;
        "Gray" )        colorString="${colorString}\033[47m";;
        "DarkGray" )    colorString="${colorString}\033[100m";;
        "Red" )         colorString="${colorString}\033[101m";;
        "Green" )       colorString="${colorString}\033[102m";;
        "Yellow" )      colorString="${colorString}\033[103m";;
        "Blue" )        colorString="${colorString}\033[104m";;
        "Magenta" )     colorString="${colorString}\033[105m";;
        "Cyan" )        colorString="${colorString}\033[106m";;
        "White" )       colorString="${colorString}\033[107m";;
        *)              colorString="${colorString}\033[49m";;
    esac

    echo "${colorString}"
}

function WriteLine () {
    local resetColor='\033[0m'

    local forecolor=$1
    local backcolor=$2
    local str=$3

    if [ "$str" == "" ]; then
        printf "\n"
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as strings without error
    if [ "$techniColor" == "true" ]; then
        local colorString=$(Color ${forecolor} ${backcolor})
        printf "${colorString}%s${resetColor}\n" "${str}"
    else
        printf "%s\n" "${str}"
    fi
}

function Write () {
    local resetColor="\033[0m"

    local forecolor=$1
    local backcolor=$2
    local str=$3

    if [ "$str" == "" ];  then
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as strings without error
    if [ "$techniColor" == "true" ]; then
        local colorString=$(Color ${forecolor} ${backcolor})
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

    WriteLine "$color_primary" "Default" ""
    WriteLine "$color_primary" "Default"  ""
    WriteLine "$color_primary" "Default"  "------------------------------------------------------------------------"
    WriteLine "$color_error" "Default"  "Error: ${name} is not installed on your system"

    if [ "$platform" == "osx" ]; then
        WriteLine "$color_error" "Default"  "       Please run 'brew install ${name}'"

        if ! command -v brew &> /dev/null; then
            WriteLine "" ""
            WriteLine "$color_warn" "Default"  "Error: It looks like you don't have brew installed either"
            WriteLine "$color_warn" "Default"  "       Please run:"
            WriteLine "$color_warn" "Default"  "       /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)'"
            quit
        fi
    else
        WriteLine "$color_error" "Default"  "       Please run 'sudo apt install ${name}'"
    fi

    WriteLine "" ""
    WriteLine "" ""
    quit
}

function errorNoPython () {
    WriteLine "$color_primary" "Default"  ""
    WriteLine "$color_primary" "Default"  ""
    WriteLine "$color_primary" "Default"  "------------------------------------------------------------------------"
    WriteLine "$color_error" "Default"  "Error: Python 3.7 not installed"
    WriteLine "" "" ""
    WriteLine "" "" ""
    
    quit
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

    # WriteLine "$color_primary" "Default"  "Downloading ${fileToGet} to ${downloadToDir}/${dirToSave}"

    Write "$color_primary" "Default" "$message"

    if [ -d "${downloadToDir}/${dirToSave}" ]; then
        WriteLine "$color_info" "Default"  "Directory already exists"
        return 0 # This is ok and assumes it's already downloaded. Whether that's true or not...
    fi

    extension="${fileToGet:(-3)}"
    if [ ! "${extension}" == ".gz" ]; then
        extension="${fileToGet:(-4)}"
        if [ ! "${extension}" == ".zip" ]; then
            WriteLine "$color_error" "Default"  "Unknown and unsupported file type for file ${fileToGet}"

            quit    # no point in carrying on
            # return 1
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        # WriteLine "$color_warn" "DEfault" "Downloading ${fileToGet} to ${dirToSave}.zip in ${downloadToDir}" 
        wget $wgetFlags --show-progress -O "${downloadToDir}/${fileToGet}" -P "${downloadToDir}" \
                                           "${storageUrl}${fileToGet}"
        
        status=$?    
        if [ $status -ne 0 ]; then
            WriteLine "$color_error" "Default" "The wget command failed for file ${fileToGet}."

            quit    # no point in carrying on
            # return 2
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        WriteLine "$color_error" "Default"  "The downloaded file '${fileToGet}' doesn't appear to exist."

        quit    # no point in carrying on
        # return 3
    fi

    Write "$color_info" "Default"  "Expanding..."

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

    if [[ ! -d "${dirToSave}" ]]; then
        WriteLine "$color_error" "Default"  "Unable to extract download. Can you please check you have write permission to "${dirToSave}"."
        quit    # no point in carrying on
    fi
    
    popd >/dev/null

    # rm /s /f /q "${downloadToDir}/${fileToGet}" >/dev/null

    WriteLine "$color_success" "Default"  "Done."
}

function getBackground () {

    if [ "$platform" == "osx" ]; then
        osascript -e \
        'tell application "Terminal"
            get background color of selected tab of window 1
        end tell'
    else

        # See https://github.com/rocky/shell-term-background/blob/master/term-background.bash
        # for a comprehensive way to test for background colour. For now we're just going to
        # assume that non-macOS terminals have a black background.

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

function getDisplaySize () {
    # See https://linuxcommand.org/lc3_adv_tput.php some great tips around this
    echo "Rows=$(tput lines) Cols=$(tput cols)"
}

function quit () {

    if [ "${techniColor}" == "true" ] && "${darkmode}" == "true" ]; then
        # this resets the terminal, but also clears the screen which isn't great
        # tput reset
        echo
    fi
    exit
}

# Main script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

clear

# verbosity can be: quiet | info | loud
verbosity="quiet"

# If files are already present, then don't overwrite if this is false
forceOverwrite=false

# Platform can define where things are located
if [[ $OSTYPE == 'darwin'* ]]; then
    platform="osx"
else
    platform="linux"
fi

# Basic locations

# The location of the solution root directory relative to this script
rootPath="../.."

# SenseAI specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# The name of the dir holding the frontend API server
senseAPIDir="API"

# TextSummary specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::

textSummaryDir="TextSummary"

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
analysisLayerPath="${absoluteRootDir}/${srcDir}/${analysisLayerDir}"
downloadPath="${absoluteRootDir}/Installers/${downloadDir}"

# Show output in wild, crazy colours
techniColor="true"
if [ "$1" = "false" ]; then
    techniColor="false"
else
    darkmode=$(isDarkMode)
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

        # We can't reliably find the background colour of the current terminal so we'll make 
        # assumptions and then, for dark backgrounds, set the text output background as Black.
        # This will mean that if we assumed correct, nothing will change. If we assumed wrong,
        # then at least the text is readable
        tput setab 0
    fi
fi

# Set Flags

wgetFlags="-q"
pipFlags="-q"
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

WriteLine "$color_info" "Default"  "Setting up CodeProject.SenseAI Development Environment"
WriteLine "$color_primary" "Default"  ""
WriteLine "$color_primary" "Default"  "========================================================================"
WriteLine "$color_primary" "Default"  ""
WriteLine "$color_primary" "Default"  "                 CodeProject SenseAI Installer                          "
WriteLine "$color_primary" "Default"  ""
WriteLine "$color_primary" "Default"  "========================================================================"
WriteLine "$color_primary" "Default"  ""

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip

if [ "$platform" == "linux" ] && [ "$EUID" -ne 0 ]; then
    WriteLine "$color_error" "Default"  "Please run this script as root: sudo bash setup_dev_env_linux.sh"
    exit
fi

# ============================================================================
# 1. Ensure directories are created and download required assets

# Create some directories
Write "$color_primary" "Creating Directories..."

# For downloading assets
mkdir -p "${downloadPath}"

# For Text Summary 
textSummaryPath="${analysisLayerPath}/${textSummaryDir}"

# For DeepStack
deepStackPath="${analysisLayerPath}/${deepstackDir}"
mkdir -p "${deepStackPath}/${tempstoreDir}"
mkdir -p "${deepStackPath}/${datastoreDir}"

# For Yolo.NET
yoloNetPath=${analysisLayerPath}/${yoloNetDir}

WriteLine "$color_success" "Default"  "Done"

Write "$color_primary" "Default"  "Downloading modules and models: "
WriteLine "$color_mute" "Default"  "Starting"

pythonInstallPath="${analysisLayerPath}/bin/${platform}/${pythonDir}"

# Clean up directories to force a re-download if necessary
if [ "${forceOverwrite}" == "true" ]; then

    # Force re-download
    rm -rf "${downloadPath}/${modelsDir}"
    rm -rf "${downloadPath}/${yoloModelsDir}"

    # force overwrite
    rm -rf "${deepStackPath}/${modelsDir}"
    rm -rf "${yoloNetPath}/${modelsDir}"
fi


# Install Python 3.7. Using deadsnakes for Linux, so be aware if you have concerns about potential
# late adoption of security patches 

if [ ! -d "${pythonInstallPath}" ]; then
    mkdir -p "${analysisLayerPath}/bin/${platform}"
    mkdir -p "${analysisLayerPath}/bin/${platform}/${pythonDir}"
fi

if command -v python3.7 &> /dev/null; then
     WriteLine "$color_mute" "Default"  "Python 3.7 is already installed"
else

    WriteLine "$color_primary" "Default"  "Installing Python 3.7"

    if [ "$platform" == "osx" ]; then
        Download $storageUrl $downloadPath "python3.7.12-osx64.tar.gz" "${platform}/${pythonDir}" "Downloading Python interpreter..."
    else
        apt-get update -y
        apt install software-properties-common -y
        add-apt-repository ppa:deadsnakes/ppa -y
        apt update -y
        apt-get install python3.7 -y
        # This is just in case: Correct https://askubuntu.com/a/1090081
        cp /usr/lib/python3/dist-packages/apt_pkg.cpython-35m-x86_64-linux-gnu.so /usr/lib/python3.7/apt_pkg.cpython-37m-x86_64-linux-gnu.so >/dev/null
        ln -s /usr/lib/python3.5/lib-dynload/_gdbm.cpython-35m-x86_64-linux-gnu.so /usr/lib/python3.7/lib-dynload/_gdbm.cpython-37m-x86_64-linux-gnu.so >/dev/null
    fi
    WriteLine "$color_success" "Default"  "Python install complete"
fi

# We need to be sure on linux that pip/venv is available for python3.7 specifically
if [ "$platform" == "linux" ]; then
    WriteLine "$color_primary" "Default"  "Installing PIP and venv to enable final Python environment setup"
    apt-get install python3-pip -y
    apt-get install python3.7-venv -y
    WriteLine "$color_success" "Default"  "PIP and venv setup"
fi


# Download the models 
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

WriteLine "$color_success" "Default"  "Modules and models downloaded"

# ============================================================================
# 2. Create & Activate Virtual Environment: DeepStack specific / Python 3.7

Write "$color_primary" "Default"  "Creating Virtual Environment..."

if [ -d  "${pythonInstallPath}/venv"  ]; then
    WriteLine "$color_success" "Default"  "Already present"
else

    python3.7 -m venv "${pythonInstallPath}/venv" &

    spin $! # process ID of the unzip/tar call
    WriteLine "$color_success" "Default"  "Done"
fi

Write "$color_primary" "Default" "Enabling our Virtual Environment..."
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
WriteLine "$color_success" "Default"  "Done"

# Ensure Python Exists
Write "$color_primary" "Default"  "Checking for Python 3.7..."
pyVersion=$($pythonInterpreterPath --version)
Write "$color_mute" "Default"  "Found ${pyVersion}. "

echo $pyVersion | grep "3.7" >/dev/null
if [ $? -ne 0 ]; then
    errorNoPython
fi 
WriteLine "$color_success" "Default"  "present"

# ============================================================================
# 3a. Install PIP packages

Write "$color_primary" "Default"  "Installing Python package manager..."
pushd "$VIRTUAL_ENV/bin" > /dev/null
./python3 -m pip install --upgrade pip $pipFlags &
spin $!
popd > /dev/null
WriteLine "$color_success" "Default"  "Done"

Write "$color_primary" "Default"  "Checking for required packages..."
# ASSUMPTION: If venv/Lib/python3.7/site-packages/torch exists then no need to do this
if [ ! -d "${VIRTUAL_ENV}/Lib/python3.7/site-packages/torch" ]; then

    WriteLine "$color_info" "Default"  "Installing"

    # We'll do this the long way so we can see some progress
    # Write "$color_primary" "Default"  "Installing Packages into Virtual Environment..."
    # pip install -r "${deepStackPath}/${intelligenceDir}/requirements.txt" $pipFlags
    # WriteLine "$color_success" "Default"  "Success"

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

                # Some packages have a version nunber after a "==". We need to trim that here.
                IFS='='; tokens=($module); IFS=$'\n';
                if [ ${#tokens[*]} -gt 1 ]; then
                     module="${tokens[0]}"
                fi
                currentOption=""    # Given that we're stripping versions, ignore this too

                Write "$color_primary" "Default"  "  -${description}..."

                pushd "$VIRTUAL_ENV/bin" > /dev/null
                if [ "${verbosity}" == "quiet" ]; then
                    ./python3 -m pip install $module $currentOption $pipFlags >/dev/null 2>/dev/null &
                    spin $!
                else
                    # echo python3 -m pip install $module $currentOption $pipFlags
                    ./python3 -m pip install $module $currentOption $pipFlags
                fi
                popd > /dev/null

                WriteLine "$color_success" "Default"  "Done"

            fi

            currentOption=""

        fi

    done
    unset IFS
else
    WriteLine "$color_success" "Default"  "present."
fi

# ============================================================================
# 3b. Install PIP packages for TextSummary

Write "$color_primary" "Default"  "Installing required Text Processing packages..."

pushd "$VIRTUAL_ENV/bin" > /dev/null
./python3 -m pip install -r "${textSummaryPath}/requirements.txt" $pipFlags # >/dev/null 2>/dev/null &
#spin $!
popd > /dev/null

WriteLine "$color_success" "Default"  "Done"

# ============================================================================
# ...and we're done.

WriteLine "$color_info" "Default"  "Development Environment setup complete" 
WriteLine "$color_primary" "Default"  ""
WriteLine "$color_primary" "Default"  ""

quit