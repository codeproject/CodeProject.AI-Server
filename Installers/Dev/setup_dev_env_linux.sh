#!/bin/bash
#
# CodeProject SenseAI Server 
# 
# Unix/Linux/macOS Development Environment install script
# 
# We assume we're in the source code /install directory.
# 

# Sub Routines :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::



# Returns a color code for the given foreground/background colors
# This code is echoed to the terminal before outputing text in
# order to generate a colored output.
#
# string foreground color name. Optional if no background provided.
#        Defaults to "Default" which uses the system default
# string background color name.  Optional. Defaults to $color_background
#        which is set based on the current terminal background
# returns a string
function Color () {

    local foreground=$1
    local background=$2

    if [ "$foreground" == "" ];  then foreground="Default"; fi
    if [ "$background" == "" ]; then background="$color_background"; fi

    if [ "$foreground" == "Contrast" ]; then
	    foreground=$(ContrastForeground ${background})
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

# Returns the name of a color that will providing a contrasting foreground
# color for the given background color. This function assumes $darkmode has
# been set globally.
#
# string background color name. 
# returns a string representing a contrasting foreground colour name
function ContrastForeground () {

    local color=$1
    if [ "$color" == "" ]; then color="Default"; fi

    if [ "$darkmode" == "true" ]; then
        case "$color" in
            "Default" )     echo "White";;
            "Black" )       echo "White";;
            "DarkRed" )     echo "White";;
            "DarkGreen" )   echo "White";;
            "DarkYellow" )  echo "White";;
            "DarkBlue" )    echo "White";;
            "DarkMagenta" ) echo "White";;
            "DarkCyan" )    echo "White";;
            "Gray" )        echo "Black";;
            "DarkGray" )    echo "White";;
            "Red" )         echo "White";;
            "Green" )       echo "White";;
            "Yellow" )      echo "Black";;
            "Blue" )        echo "White";;
            "Magenta" )     echo "White";;
            "Cyan" )        echo "Black";;
            "White" )       echo "Black";;
            *)              echo "White";;
        esac
    else
        case "$color" in
            "Default" )     echo "Black";;
            "Black" )       echo "White";;
            "DarkRed" )     echo "White";;
            "DarkGreen" )   echo "White";;
            "DarkYellow" )  echo "White";;
            "DarkBlue" )    echo "White";;
            "DarkMagenta" ) echo "White";;
            "DarkCyan" )    echo "White";;
            "Gray" )        echo "Black";;
            "DarkGray" )    echo "White";;
            "Red" )         echo "White";;
            "Green" )       echo "Black";;
            "Yellow" )      echo "Black";;
            "Blue" )        echo "White";;
            "Magenta" )     echo "White";;
            "Cyan" )        echo "Black";;
            "White" )       echo "Black";;
            *)              echo "White";;
        esac
    fi
    
    echo "${colorString}"
}


# Gets the terminal background color. It's a very naive guess 
# returns an RGB triplet, values from 0 - 64K
function getBackground () {

    if [[ $OSTYPE == 'darwin'* ]]; then
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

# Determines whether or not the current terminal is in dark mode (dark background, light text)
# returns "true" if running in dark mode; false otherwise
function isDarkMode () {

    local bgColor=$(getBackground)
    
    IFS=','; colors=($bgColor); IFS=' ';

    # Is the background more or less dark?
    if [ ${colors[0]} -lt 20000 ] && [ ${colors[1]} -lt 20000 ] && [ ${colors[2]} -lt 20000 ]; then
        echo "true"
    else
        echo "false"
    fi
}


# Outputs a line, including linefeed, to the terminal using the given foreground / background
# colors 
#
# string The text to output. Optional if no foreground provided. Default is just a line feed.
# string Foreground color name. Optional if no background provided. Defaults to "Default" which
#        uses the system default
# string Background color name.  Optional. Defaults to $color_background which is set based on the
#        current terminal background
function WriteLine () {

    local resetColor='\033[0m'

    local str=$1
    local forecolor=$2
    local backcolor=$3

    if [ "$str" == "" ]; then
        printf "\n"
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as strings without error
    if [ "$useColor" == "true" ]; then
        local colorString=$(Color ${forecolor} ${backcolor})
        printf "${colorString}%s${resetColor}\n" "${str}"
    else
        printf "%s\n" "${str}"
    fi
}

# Outputs a line without a linefeed to the terminal using the given foreground / background colors 
#
# string The text to output. Optional if no foreground provided. Default is just a line feed.
# string Foreground color name. Optional if no background provided. Defaults to "Default" which
#        uses the system default
# string Background color name.  Optional. Defaults to $color_background which is set based on the
#        current terminal background
function Write () {

    local resetColor="\033[0m"

    local str=$1
    local forecolor=$2
    local backcolor=$3

    if [ "$str" == "" ];  then
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as strings without error
    if [ "$useColor" == "true" ]; then
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

    WriteLine
    WriteLine
    WriteLine "------------------------------------------------------------------------"
    WriteLine "Error: ${name} is not installed on your system" $color_error

    if [ "$platform" == "osx" ]; then
        WriteLine "       Please run 'brew install ${name}'" $color_error

        if ! command -v brew &> /dev/null; then
            WriteLine
            WriteLine "Error: It looks like you don't have brew installed either" $color_warn
            WriteLine "       Please run:" $color_warn
            WriteLine "       /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)'" $color_warn
            quit
        fi
    else
        WriteLine "       Please run 'sudo apt install ${name}'" $color_error
    fi

    WriteLine
    WriteLine
    quit
}

function errorNoPython () {
    WriteLine
    WriteLine
    WriteLine "------------------------------------------------------------------------" $color_primary
    WriteLine "Default"  "Error: Python 3.7 not installed" $color_error
    WriteLine 
    WriteLine
    
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

    # WriteLine "Downloading ${fileToGet} to ${downloadToDir}/${dirToSave}" $color_primary

    Write $message $color_primary

    if [ -d "${downloadToDir}/${dirToSave}" ]; then
        WriteLine "Directory already exists" $color_info
        return 0 # This is ok and assumes it's already downloaded. Whether that's true or not...
    fi

    extension="${fileToGet:(-3)}"
    if [ ! "${extension}" == ".gz" ]; then
        extension="${fileToGet:(-4)}"
        if [ ! "${extension}" == ".zip" ]; then
            WriteLine "Unknown and unsupported file type for file ${fileToGet}" $color_error

            quit    # no point in carrying on
            # return 1
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        # WriteLine "Downloading ${fileToGet} to ${dirToSave}.zip in ${downloadToDir}"  $color_warn
        wget $wgetFlags --show-progress -O "${downloadToDir}/${fileToGet}" -P "${downloadToDir}" \
                                           "${storageUrl}${fileToGet}"
        
        status=$?    
        if [ $status -ne 0 ]; then
            WriteLine "The wget command failed for file ${fileToGet}." $color_error

            quit    # no point in carrying on
            # return 2
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        WriteLine "The downloaded file '${fileToGet}' doesn't appear to exist." $color_error

        quit    # no point in carrying on
        # return 3
    fi

    Write "Expanding..." $color_info

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
        WriteLine "Unable to extract download. Can you please check you have write permission to "${dirToSave}"." $color_error
        quit    # no point in carrying on
    fi
    
    popd >/dev/null

    # rm /s /f /q "${downloadToDir}/${fileToGet}" >/dev/null

    WriteLine "Done." $color_success
}

function getDisplaySize () {
    # See https://linuxcommand.org/lc3_adv_tput.php some great tips around this
    echo "Rows=$(tput lines) Cols=$(tput cols)"
}

function quit () {

    if [ "${useColor}" == "true" ] && "${darkmode}" == "true" ]; then
        # this resets the terminal, but also clears the screen which isn't great
        # tput reset
        echo
    fi
    exit
}

# Main script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

useColor="true"
darkmode=$(isDarkMode)

# Setup some predefined colours. Note that we can't reliably determine the background 
# color of the terminal so we avoid specifically setting black or white for the foreground
# or background. You can always just use "White" and "Black" if you specifically want
# this combo, but test thoroughly
if [ "$darkmode" == "true" ]; then
    color_primary="White"
    color_mute="Gray"
    color_info="Yellow"
    color_success="Green"
    color_warn="DarkYellow"
    color_error="Red"
else
    color_primary="Black"
    color_mute="Gray"
    color_info="Magenta"
    color_success="DarkGreen"
    color_warn="DarkYellow"
    color_error="Red"
fi

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

WriteLine "        Setting up CodeProject.SenseAI Development Environment          " "DarkYellow" 
WriteLine "                                                                        " "DarkGreen" 
WriteLine "========================================================================" "DarkGreen" 
WriteLine "                                                                        " "DarkGreen" 
WriteLine "                 CodeProject SenseAI Installer                          " "DarkGreen" 
WriteLine "                                                                        " "DarkGreen"
WriteLine "========================================================================" "DarkGreen" 
WriteLine "                                                                        " "DarkGreen"

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip

if [ "$platform" == "linux" ] && [ "$EUID" -ne 0 ]; then
    WriteLine "Please run this script as root: sudo bash setup_dev_env_linux.sh" $color_error
    exit
fi

# ============================================================================
# 1. Ensure directories are created and download required assets

# Create some directories
Write "Creating Directories..." $color_primary

# For downloading assets
mkdir -p "${downloadPath}"

# For Text Summary 
textSummaryPath="${analysisLayerPath}/${textSummaryDir}"

# For DeepStack
deepStackPath="${analysisLayerPath}/${deepstackDir}"
mkdir -p "${deepStackPath}/${tempstoreDir}"

# To do this properly we're going to use the standard directories for common application data
#mkdir -p "${deepStackPath}/${datastoreDir}"
mkdir -p /usr/share/CodeProject/SenseAI
chmod 777 /usr/share/CodeProject/SenseAI


# For Yolo.NET
yoloNetPath=${analysisLayerPath}/${yoloNetDir}

WriteLine "Done" $color_success

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
     WriteLine "Python 3.7 is already installed" $color_success
else

    WriteLine "Installing Python 3.7" $color_primary

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
    WriteLine "Python install complete" $color_success
fi

# We need to be sure on linux that pip/venv is available for python3.7 specifically
if [ "$platform" == "linux" ]; then
    Write "Installing PIP and venv to enable final Python environment setup..." $color_primary

    if [ "${verbosity}" == "quiet" ]; then
        apt-get install python3-pip -y  >/dev/null 2>/dev/null
        apt-get install python3.7-venv -y >/dev/null 2>/dev/null
    else
        apt-get install python3-pip -y 
        apt-get install python3.7-venv -y
    fi

    WriteLine "Done" $color_success
fi

Write "Downloading modules and models: " $color_primary
WriteLine "Starting" $color_mute

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

WriteLine "Modules and models downloaded" $color_success

# ============================================================================
# 2. Create & Activate Virtual Environment: DeepStack specific / Python 3.7

Write "Creating Virtual Environment..." $color_primary

if [ -d  "${pythonInstallPath}/venv"  ]; then
    WriteLine "Already present" $color_success
else

    python3.7 -m venv "${pythonInstallPath}/venv" &

    spin $! # process ID of the unzip/tar call
    WriteLine "Done" $color_success
fi

Write "Enabling our Virtual Environment..." $color_primary
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
WriteLine "Done" $color_success

# Ensure Python Exists
Write "Checking for Python 3.7..." $color_primary
pyVersion=$($pythonInterpreterPath --version)
Write "Found ${pyVersion}. " $color_mute

echo $pyVersion | grep "3.7" >/dev/null
if [ $? -ne 0 ]; then
    errorNoPython
fi 
WriteLine "present" $color_success

# ============================================================================
# 3a. Install PIP packages

Write "Installing Python package manager..." $color_primary
pushd "$VIRTUAL_ENV/bin" > /dev/null
./python3 -m pip install --upgrade pip $pipFlags &
spin $!
popd > /dev/null
WriteLine "Done" $color_success

Write "Checking for required packages..." $color_primary
# ASSUMPTION: If venv/Lib/python3.7/site-packages/torch exists then no need to do this
if [ ! -d "${VIRTUAL_ENV}/Lib/python3.7/site-packages/torch" ]; then

    WriteLine "Installing" $color_info

    # We'll do this the long way so we can see some progress
    # Write "Installing Packages into Virtual Environment..." $color_primary
    # pip install -r "${deepStackPath}/${intelligenceDir}/requirements.txt" $pipFlags
    # WriteLine "Success" $color_success

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

                Write "  -${description}..." $color_primary

                pushd "$VIRTUAL_ENV/bin" > /dev/null
                if [ "${verbosity}" == "quiet" ]; then
                    ./python3 -m pip install $module $currentOption $pipFlags >/dev/null 2>/dev/null &
                    spin $!
                else
                    # echo python3 -m pip install $module $currentOption $pipFlags
                    ./python3 -m pip install $module $currentOption $pipFlags
                fi
                popd > /dev/null

                WriteLine "Done" $color_success

            fi

            currentOption=""

        fi

    done
    unset IFS
else
    WriteLine "present." $color_success
fi

# ============================================================================
# 3b. Install PIP packages for TextSummary

Write "Installing required Text Processing packages..." $color_primary

pushd "$VIRTUAL_ENV/bin" > /dev/null
./python3 -m pip install -r "${textSummaryPath}/requirements.txt" $pipFlags >/dev/null 2>/dev/null &
spin $!
popd > /dev/null

WriteLine "Done" $color_success

# ============================================================================
# ...and we're done.

WriteLine 
WriteLine "                Development Environment setup complete                  " "White" "DarkGreen"
WriteLine 

quit