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

    if [ "$foreground" == "" ];  then foreground='Default'; fi
    if [ "$background" == "" ]; then background="$color_background"; fi

    if [ "$foreground" == 'Contrast' ]; then
        foreground=$(ContrastForeground ${background})
    fi
    
    local colorString='\033['

    # Foreground Colours
    case "$foreground" in
        'Default')      colorString='\033[0;39m';;
        'Black' )       colorString='\033[0;30m';;
        'DarkRed' )     colorString='\033[0;31m';;
        'DarkGreen' )   colorString='\033[0;32m';;
        'DarkYellow' )  colorString='\033[0;33m';;
        'DarkBlue' )    colorString='\033[0;34m';;
        'DarkMagenta' ) colorString='\033[0;35m';;
        'DarkCyan' )    colorString='\033[0;36m';;
        'Gray' )        colorString='\033[0;37m';;
        'DarkGray' )    colorString='\033[1;90m';;
        'Red' )         colorString='\033[1;91m';;
        'Green' )       colorString='\033[1;92m';;
        'Yellow' )      colorString='\033[1;93m';;
        'Blue' )        colorString='\033[1;94m';;
        'Magenta' )     colorString='\033[1;95m';;
        'Cyan' )        colorString='\033[1;96m';;
        'White' )       colorString='\033[1;97m';;
        *)              colorString='\033[0;39m';;
    esac

    # Background Colours
    case "$background" in
        'Default' )     colorString="${colorString}\033[49m";;
        'Black' )       colorString="${colorString}\033[40m";;
        'DarkRed' )     colorString="${colorString}\033[41m";;
        'DarkGreen' )   colorString="${colorString}\033[42m";;
        'DarkYellow' )  colorString="${colorString}\033[43m";;
        'DarkBlue' )    colorString="${colorString}\033[44m";;
        'DarkMagenta' ) colorString="${colorString}\033[45m";;
        'DarkCyan' )    colorString="${colorString}\033[46m";;
        'Gray' )        colorString="${colorString}\033[47m";;
        'DarkGray' )    colorString="${colorString}\033[100m";;
        'Red' )         colorString="${colorString}\033[101m";;
        'Green' )       colorString="${colorString}\033[102m";;
        'Yellow' )      colorString="${colorString}\033[103m";;
        'Blue' )        colorString="${colorString}\033[104m";;
        'Magenta' )     colorString="${colorString}\033[105m";;
        'Cyan' )        colorString="${colorString}\033[106m";;
        'White' )       colorString="${colorString}\033[107m";;
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
    if [ "$color" == '' ]; then color='Default'; fi

    if [ "$darkmode" == 'true' ]; then
        case "$color" in
            'Default' )     echo 'White';;
            'Black' )       echo 'White';;
            'DarkRed' )     echo 'White';;
            'DarkGreen' )   echo 'White';;
            'DarkYellow' )  echo 'White';;
            'DarkBlue' )    echo 'White';;
            'DarkMagenta' ) echo 'White';;
            'DarkCyan' )    echo 'White';;
            'Gray' )        echo 'Black';;
            'DarkGray' )    echo 'White';;
            'Red' )         echo 'White';;
            'Green' )       echo 'White';;
            'Yellow' )      echo 'Black';;
            'Blue' )        echo 'White';;
            'Magenta' )     echo 'White';;
            'Cyan' )        echo 'Black';;
            'White' )       echo 'Black';;
            *)              echo 'White';;
        esac
    else
        case "$color" in
            'Default' )     echo 'Black';;
            'Black' )       echo 'White';;
            'DarkRed' )     echo 'White';;
            'DarkGreen' )   echo 'White';;
            'DarkYellow' )  echo 'White';;
            'DarkBlue' )    echo 'White';;
            'DarkMagenta' ) echo 'White';;
            'DarkCyan' )    echo 'White';;
            'Gray' )        echo 'Black';;
            'DarkGray' )    echo 'White';;
            'Red' )         echo 'White';;
            'Green' )       echo 'Black';;
            'Yellow' )      echo 'Black';;
            'Blue' )        echo 'White';;
            'Magenta' )     echo 'White';;
            'Cyan' )        echo 'Black';;
            'White' )       echo 'Black';;
            *)              echo 'White';;
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

        echo '0,0,0' # we're making assumptions here
    fi
}

# Determines whether or not the current terminal is in dark mode (dark background, light text)
# returns "true" if running in dark mode; false otherwise
function isDarkMode () {

    local bgColor=$(getBackground)
    
    IFS=','; colors=($bgColor); IFS=' ';

    # Is the background more or less dark?
    if [ ${colors[0]} -lt 20000 ] && [ ${colors[1]} -lt 20000 ] && [ ${colors[2]} -lt 20000 ]; then
        echo 'true'
    else
        echo 'false'
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
function writeLine () {

    local resetColor='\033[0m'

    local str=$1
    local forecolor=$2
    local backcolor=$3

    if [ "$str" == "" ]; then
        printf '\n'
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
function write () {

    local resetColor='\033[0m'

    local str=$1
    local forecolor=$2
    local backcolor=$3

    if [ "$str" == "" ];  then
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as strings without error
    if [ "$useColor" == 'true' ]; then
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

    writeLine
    writeLine
    writeLine "------------------------------------------------------------------------"
    writeLine "Error: ${name} is not installed on your system" $color_error

    if [ "$platform" == "macos" ]; then
        writeLine "       Please run 'brew install ${name}'" $color_error

        if ! command -v brew &> /dev/null; then
            writeLine
            writeLine "Error: It looks like you don't have brew installed either" $color_warn
            writeLine "       Please run:" $color_warn
            writeLine "       /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)'" $color_warn
            quit
        fi
    else
        writeLine "       Please run 'sudo apt install ${name}'" $color_error
    fi

    writeLine
    writeLine
    quit
}

function setupPython () {

    # M1 macs are trouble for python
    if [ "$platform" == "macos" ] && [[ $(uname -p) == 'arm' ]]; then
        write "ARM (Apple silicon) Mac detected, but we are not running under Rosetta. " $color_warn
        if [ $(/usr/bin/pgrep oahd >/dev/null 2>&1; echo $?) -gt 0 ]; then
        #if [ "$(pkgutil --files com.apple.pkg.RosettaUpdateAuto)" == "" ]; then 
    	    writeLine 'Rosetta is not installed' $color_error
            needRosettaAndiBrew
        else
    	    writeLine 'Rosetta is installed. We can continue.' $color_success
        fi
    fi

    local pythonVersion=$1

    # Version with ".'s removed
    local pythonName="python${pythonVersion/./}"

    installPath="${analysisLayerPath}/bin/${platform}/${pythonName}"

    if [ "${forceOverwrite}" == "true" ]; then

        if [ ! $verbosity == "quiet" ]; then
            writeLine "Cleaning download directory to force re-install of Python" $color_info
        fi

        # Force Re-download
        if [ -d "${downloadPath}/${platform}/${pythonName}" ]; then 
            rm -rf "${downloadPath}/${platform}/${pythonName}"
        fi

        # Force overwrite
        if [ -d "${installPath}" ]; then 
            rm -rf "${installPath}"
        fi
    fi

    # ============================================================================
    # 1. Install Python. Using deadsnakes for Linux (not macOS), so be aware if you have concerns
    #                    about potential late adoption of security patches.

     if [ $verbosity == "loud" ]; then
        writeLine "Python install path is ${installPath}" $color_info
     fi

     if [ ! -d "${installPath}" ]; then
        if [ "$platform" == "macos" ]; then
            mkdir -p "${installPath}"
        else
            mkdir -p "${installPath}"
        fi
     fi

     pythonCmd="python${pythonVersion}"
     if command -v $pythonCmd &> /dev/null; then
         writeLine "Python ${pythonVersion} is already installed" $color_success
     else

        # For macOS we'll use brew to install python
        if [ "$platform" == "macos" ]; then

            write "Installing Python ${pythonVersion}..." $color_primary

            if [[ $(uname -p) == 'arm' ]]; then

                # Apple silicon requires Rosetta2 for python to run, so use the x86 version of Brew
                # we installed earlier
                if [ "${verbosity}" == "quiet" ]; then
                    arch -x86_64 /usr/local/bin/brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                    spin $!
                else
                    arch -x86_64 /usr/local/bin/brew install python@${pythonVersion}
                fi

                # Note that we only need the specific location of the python interpreter to setup the
                # virtual environment. After it's setup, all python calls are relative to the same venv
                # no matter the location of the original python interpreter
                pythonCmd="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"

            else

                # We have a x64 version of python for macOS in our S3 bucket but it's easier simply to
                # install python natively

                # Download $storageUrl $downloadPath "python3.7.12-osx64.tar.gz" "${platform}/${pythonDir}" "Downloading Python interpreter..."
                # cp -R "${downloadPath}/${platform}/${pythonDir}" "${analysisLayerPath}/bin/${platform}"

                if [ "${verbosity}" == "quiet" ]; then
                    brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                    spin $!
                else
                    brew install python@${pythonVersion}
                fi

                # Brew specific path
                pythonCmd="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"

            fi

            writeLine "Done" $color_success

        # For Linux we'll use apt-get the deadsnakes PPA to get the old version of python. Deadsnakes?
        # Old python? Get it? Get it?! And who said developers have no sense of humour.
        else

            if [ ! "${verbosity}" == "loud" ]; then

                write "Installing Python ${pythonVersion}..." $color_primary

                if [ "${verbosity}" == "info" ]; then writeLine "Updating apt-get" $color_info; fi;
                apt-get update -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Installing software-properties-common" $color_info; fi;
                apt install software-properties-common -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Adding deadsnakes as a Python install source (PPA)" $color_info; fi;
                add-apt-repository ppa:deadsnakes/ppa -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Updating apt" $color_info; fi;
                apt update -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Installing Python ${pythonVersion}" $color_info; fi;
                apt-get install python${pythonVersion} -y >/dev/null 2>/dev/null &
                spin $!

                # apt-get install python3-pip
                writeLine "Done" $color_success
            else
                writeLine "Updating apt-get" $color_info
                apt-get update -y
                writeLine "Installing software-properties-common" $color_info
                apt install software-properties-common -y
                writeLine "Adding deadsnakes as a Python install source (PPA)" $color_info
                add-apt-repository ppa:deadsnakes/ppa -y
                writeLine "Updating apt" $color_info
                apt update -y
                writeLine "Installing Python ${pythonVersion}" $color_primary
                apt-get install python${pythonVersion} -y
                # apt-get install python3-pip
                writeLine "Done" $color_success
            fi
        fi
    fi

    # ============================================================================
    # 2. Create Virtual Environment

    if [ -d  "${installPath}/venv" ]; then
        writeLine "Virtual Environment already present" $color_success
    else

        # Make sure we have pythonNN-env installed
        if [ "$platform" == "macos" ]; then
            if [ "${verbosity}" == "quiet" ]; then
                write "Installing Virtual Environment tools for mac..." $color_primary
                pip3 install virtualenv virtualenvwrapper >/dev/null 2>/dev/null &
                spin $!
                writeLine "Done" $color_success
            else
                writeLine "Installing Virtual Environment tools for mac..." $color_primary
        
                # regarding the warning: See https://github.com/Homebrew/homebrew-core/issues/76621
                if [ "$platform" == "macos" ] && [ $(versionCompare "${pythonVersion}" '3.10.2') == "-1" ]; then
                    writeLine "Ignore the DEPRECATION warning. See https://github.com/Homebrew/homebrew-core/issues/76621 for details" $color_info
                fi

                pip3 install virtualenv virtualenvwrapper
            fi
        else
            if [ "${verbosity}" == "quiet" ]; then
                write "Installing Virtual Environment tools for Linux..." $color_primary

                # just in case - but doesn't seem to be effective
                # writeLine
                # writeLine "First: correcting broken installs".
                # apt --fix-broken install

                apt install python${pythonVersion}-venv >/dev/null 2>/dev/null &
                spin $!
                writeLine "Done" $color_success
            else
                writeLine "Installing Virtual Environment tools for Linux..." $color_primary
                apt install python${pythonVersion}-venv
            fi
        fi

        # Create the virtual env
        write "Creating Virtual Environment..." $color_primary
        
        if [ $verbosity == "loud" ]; then
            writeLine "Install path is ${installPath}"
        fi

        if [ "$platform" == "macos" ]; then
            ${pythonCmd} -m venv "${installPath}/venv"
        else
            ${pythonCmd} -m venv "${installPath}/venv" &
            spin $! # process ID of the unzip/tar call
        fi
        writeLine "Done" $color_success
    fi

    pushd "${installPath}" >/dev/null
    venvPath="$(pwd)/venv"
    pythonInterpreterPath="${venvPath}/bin/python3"
    popd >/dev/null

    # Ensure Python Exists
    write "Checking for Python ${pythonVersion}..." $color_primary
    pyVersion=$($pythonInterpreterPath --version)
    write "Found ${pyVersion}. " $color_mute

    echo $pyVersion | grep "${pythonVersion}" >/dev/null
    if [ $? -ne 0 ]; then
        errorNoPython
    fi 
    writeLine "present" $color_success
}

function installPythonPackages () {

    # Whether or not to install all python packages in one step (-r requirements.txt) or step by step
    oneStepPIP="true"

    pythonVersion=$1
    # Version with ".'s removed
    local pythonName="python${pythonVersion/./}"

    pythonCmd="python${pythonVersion}"

    # Brew doesn't set PATH by default (nor do we need it to) which means we just have to be careful
    if [ "$platform" == "macos" ]; then
        
        # If running "PythonX.Y" doesn't actually work, then let's adjust the python command
        # to point to where we think the python launcher should be
        python${pythonVersion} --version >/dev/null  2>/dev/null
        if [ $? -ne 0 ]; then
            # writeLine "Did not find python in default location"
            pythonCmd="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"
        fi
    fi

    # Quick check to ensure PIP is upo to date
    if [ "${verbosity}" == "quiet" ]; then
        write "Updating Python PIP..."
        ${pythonCmd} -m pip install --upgrade pip >/dev/null 2>/dev/null &
        spin $!
        writeLine "Done" $color_success
    else
        writeLine "Updating Python PIP..."
        # regarding the warning: See https://github.com/Homebrew/homebrew-core/issues/76621
        if [ "$platform" == "macos" ] && [ $(versionCompare "${pythonVersion}" '3.10.2') == "-1" ]; then
            writeLine "Ignore the DEPRECATION warning. See https://github.com/Homebrew/homebrew-core/issues/76621 for details" $color_info
        fi
        ${pythonCmd} -m pip install --upgrade pip
    fi

    requirementsPath=$2

    testForPipExistanceName=$3

    virtualEnv="${analysisLayerPath}/bin/${platform}/${pythonName}/venv"

    # ============================================================================
    # Install PIP packages

    write "Checking for required packages..." $color_primary

    # ASSUMPTION: If a folder by the name of "testForPipExistanceName" exists in the site-packages
    # directory then we assume the requirements.txt file has already been processed.

    packagesPath="${virtualEnv}/lib/python${pythonVersion}/site-packages/"

    if [ ! -d "${packagesPath}/${testForPipExistanceName}" ]; then

        if [ ! "${verbosity}" == "quiet" ]; then
            writeLine "Installing packages from ${requirementsPath}" $color_info
        fi
        writeLine "Packages missing. Installing..." $color_info

        if [ "${oneStepPIP}" == "true" ]; then

            # Install the Python Packages in one fell swoop. Not much feedback, but it works
            write "Installing Packages into Virtual Environment..." $color_primary
            if [ "${verbosity}" == "quiet" ]; then
                ${pythonCmd} -m pip install -r ${requirementsPath} --target ${packagesPath} > /dev/null &
                spin $!
            else
                ${pythonCmd} -m pip install -r ${requirementsPath} --target ${packagesPath}
            fi
            writeLine "Success" $color_success

        else

            # Open requirements.txt and grab each line. We need to be careful with --find-links lines
            # as this doesn't currently work in Linux
            currentOption=""

            IFS=$'\n' # set the Internal Field Separator as end of line
            cat "${requirementsPath}" | while read -r line
            do

                line="$(echo $line | tr -d '\r\n')"    # trim newlines / CRs

                if [ "${line}" == "" ]; then
                    currentOption=""
                elif [ "${line:0:2}" == "##" ]; then
                    currentOption=""
                elif [ "${line:0:2}" == "#!" ]; then
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

                        write "  -${description}..." $color_primary

                        pushd "${virtualEnv}/bin" > /dev/null
                        if [ "${verbosity}" == "quiet" ]; then
                            ./python${pythonVersion} -m pip install $module $currentOption $pipFlags >/dev/null 2>/dev/null &
                            spin $!
                        else
                            # echo python3 -m pip install $module $currentOption $pipFlags
                            ./python${pythonVersion} -m pip install $module $currentOption $pipFlags
                        fi
                        popd > /dev/null

                        writeLine "Done" $color_success

                    fi

                    currentOption=""

                fi

            done
            unset IFS
        fi
    else
        writeLine "present." $color_success
    fi
}

function errorNoPython () {
    writeLine
    writeLine
    writeLine "------------------------------------------------------------------------" $color_primary
    writeLine "Error: Python not installed" $color_error
    writeLine 
    writeLine
    
    quit
}

function spin () {

    local pid=$1

    spin[0]='-'
    spin[1]='\\'
    spin[2]='|'
    spin[3]='/'

    while kill -0 $pid 2> /dev/null; do
        for i in "${spin[@]}"
        do
            echo -ne "$i\b"
            sleep 0.1
        done
    done

    echo -ne ' \b'
}

function Download () {

    local storageUrl=$1
    local downloadToDir=$2
    local fileToGet=$3
    local dirToSave=$4
    local message=$5

    # storageUrl = 'https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/'
    # downloadToDir = 'downloads/' - relative to the current directory
    # fileToGet = packages_for_gpu.zip
    # dirToSave = packages

    if [ "${fileToGet}" == "" ]; then
        writeLine 'No download file was specified' $color_error
        quit    # no point in carrying on
    fi

    if [ "${message}" == "" ]; then
        message="Downloading ${fileToGet}..."
    fi

    # writeLine "Downloading ${fileToGet} to ${downloadToDir}/${dirToSave}" $color_primary

    write $message $color_primary

    if [ -d "${downloadToDir}/${dirToSave}" ]; then
        writeLine " Directory already exists" $color_info
        return 0 # This is ok and assumes it's already downloaded. Whether that's true or not...
    fi

    extension="${fileToGet:(-3)}"
    if [ ! "${extension}" == ".gz" ]; then
        extension="${fileToGet:(-4)}"
        if [ ! "${extension}" == ".zip" ]; then
            writeLine "Unknown and unsupported file type for file ${fileToGet}" $color_error

            quit    # no point in carrying on
            # return 1
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        # writeLine "Downloading ${fileToGet} to ${dirToSave}.zip in ${downloadToDir}"  $color_warn
        wget $wgetFlags --show-progress -O "${downloadToDir}/${fileToGet}" -P "${downloadToDir}" \
                                           "${storageUrl}${fileToGet}"
        
        status=$?    
        if [ $status -ne 0 ]; then
            writeLine "The wget command failed for file ${fileToGet}." $color_error

            quit    # no point in carrying on
            # return 2
        fi
    fi

    if [ ! -f  "${downloadToDir}/${fileToGet}" ]; then
        writeLine "The downloaded file '${fileToGet}' doesn't appear to exist." $color_error

        quit    # no point in carrying on
        # return 3
    fi

    write 'Expanding...' $color_info

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
        writeLine "Unable to extract download. Can you please check you have write permission to "${dirToSave}"." $color_error
        quit    # no point in carrying on
    fi
    
    popd >/dev/null

    # rm /s /f /q "${downloadToDir}/${fileToGet}" >/dev/null

    writeLine 'Done.' $color_success
}

# Thanks: https://stackoverflow.com/a/4025065 with mods
# compares two version numbers (eg 3.9.12 < 3.10.1)
versionCompare () {
 
      # trivial equal case
    if [[ $1 == $2 ]]; then
        echo "0"
        return 0
    fi
 
    local IFS=.
    local i ver1=($1) ver2=($2)

    # fill empty fields in ver1 with zeros
    for ((i=${#ver1[@]}; i<${#ver2[@]}; i++))
    do
        ver1[i]=0
    done

    for ((i=0; i<${#ver1[@]}; i++))
    do
        if [[ -z ${ver2[i]} ]]
        then
            # fill empty fields in ver2 with zeros
            ver2[i]=0
        fi

        if ((10#${ver1[i]} > 10#${ver2[i]}))
        then
            echo "1" # $1 > $2
            return 0
        fi
        if ((10#${ver1[i]} < 10#${ver2[i]}))
        then
            echo "-1" # $1 < $2
            return 0
        fi
    done

    echo "0"
}

function getDisplaySize () {
    # See https://linuxcommand.org/lc3_adv_tput.php some great tips around this
    echo "Rows=$(tput lines) Cols=$(tput cols)"
}

function displayMacOSPermissionError () {
    writeLine
    writeLine "Unable to Create a Directory"  $color_error

    if [[ $OSTYPE == 'darwin'* ]]; then

       local commonDir=$1

        writeLine
        writeLine "But we may be able to suggest something:"  $color_info

        # Note that  will appear as the Apple symbol on macOS, but probably not on Windows or Linux
        writeLine '1. Pull down the  Apple menu and choose "System Preferences"'
        writeLine '2. Choose “Security & Privacy” control panel'
        writeLine '3. Now select the “Privacy” tab, then from the left-side menu select'
        writeLine '   “Full Disk Access”'
        writeLine '4. Click the lock icon in the lower left corner of the preference '
        writeLine '   panel and authenticate with an admin level login'
        writeLine '5. Now click the [+] plus button so we can full disk access to Terminal'
        writeLine "6. Navigate to the /Applications/Utilities/ folder and choose 'Terminal'"
        writeLine '   to grant Terminal Full Disk Access privileges'
        writeLine '7. Relaunch Terminal, the “Operation not permitted” error messages should'
        writeLine '   be gone'
        writeLine
        writeLine 'Thanks to https://osxdaily.com/2018/10/09/fix-operation-not-permitted-terminal-error-macos/'
    fi

    quit
}

function needRosettaAndiBrew () {

    writeLine
    writeLine "You're on an Mx Mac running ARM but Python3 only works on Intel."  $color_error
    writeLine "You will need to install Rosetta2 to continue."  $color_error
    writeLine
    read -p 'Install Rosetta2 (Y/N)?' installRosetta
    if [ "${installRosetta}" == "y" ] || [ "${installRosetta}" == "Y" ]; then
        /usr/sbin/softwareupdate --install-rosetta --agree-to-license
    else
        quit
    fi

    writeLine "Then you need to install brew under Rosetta (We'll alias it as ibrew)"
    read -p 'Install brew for x86 (Y/N)?' installiBrew
    if [ "${installiBrew}" == "y" ] || [ "${installiBrew}" == "Y" ]; then
        arch -x86_64 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)"
    else
        quit
    fi
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
    color_primary='White'
    color_mute='Gray'
    color_info='Yellow'
    color_success='Green'
    color_warn='DarkYellow'
    color_error='Red'
else
    color_primary='Black'
    color_mute='Gray'
    color_info='Magenta'
    color_success='DarkGreen'
    color_warn='DarkYellow'
    color_error='Red'
fi

clear

# verbosity can be: quiet | info | loud
verbosity="quiet"

# If files are already present, then don't overwrite if this is false
forceOverwrite=false

# Platform can define where things are located
if [[ $OSTYPE == 'darwin'* ]]; then
    platform='macos'
else
    platform='linux'
fi


# Basic locations

# The location of the solution root directory relative to this script
rootPath='../..'

# SenseAI Server specific ::::::::::::::::::::::::::::::::::::::::::::::::::::

# The name of the dir holding the frontend API server
senseAPIDir='API'


# Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# The location of large packages that need to be downloaded
# a. From AWS
storageUrl='https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/'
# b. Use a local directory rather than from online. Handy for debugging.
# storageUrl='/mnt/c/dev/CodeProject/CodeProject.SenseAI/install/cached_downloads/'

# The name of the source directory
srcDir='src'

# The name of the dir, within the current directory, where install assets will
# be downloaded
downloadDir='downloads'

# The name of the dir holding the backend analysis services
analysisLayerDir='AnalysisLayer'

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

wgetFlags='-q --no-check-certificate'
pipFlags='-q'
copyFlags='/NFL /NDL /NJH /NJS /nc /ns  >/dev/null'
unzipFlags='-qq'
tarFlags='xf'

if [ $verbosity == "info" ]; then
    wgetFlags='--no-verbose --no-check-certificate'
    pipFlags='-q'
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

writeLine '        Setting up CodeProject.SenseAI Development Environment          ' 'DarkYellow'
writeLine '                                                                        ' 'DarkGreen'
writeLine '========================================================================' 'DarkGreen'
writeLine '                                                                        ' 'DarkGreen'
writeLine '                 CodeProject SenseAI Installer                          ' 'DarkGreen'
writeLine '                                                                        ' 'DarkGreen'
writeLine '========================================================================' 'DarkGreen'
writeLine '                                                                        ' 'DarkGreen'

# ============================================================================
# House keeping

checkForTool wget
checkForTool unzip

if [ "$platform" == "linux" ] && [ "$EUID" -ne 0 ]; then
    writeLine "Please run this script as root: sudo bash setup_dev_env_linux.sh" $color_error
    exit
fi

# ============================================================================
# 1. Ensure directories are created and download required assets

writeLine
writeLine 'General SenseAI setup                                                   ' "White" "Blue"

# Create some directories

# For downloading assets
write "Creating Directories..." $color_primary
if [ $verbosity == "loud" ]; then writeLine "downloadPath is ${downloadPath}"; fi;

mkdir -p "${downloadPath}"
if [ "$platform" == "macos" ]; then 
    write "We'll need to run under root to set permissions. " $color_warn
    sudo chmod 777 "${downloadPath}"
else
    write "Creating Directories..." $color_primary
fi
writeLine "Done" $color_success


# TextSummary specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'TextSummary setup                                                       ' "White" "Blue"

# The name of the dir containing the TextSummary module
moduleDir='TextSummary'

# Full path to the TextSummary dir
modulePath="${analysisLayerPath}/${moduleDir}"

setupPython 3.8
installPythonPackages 3.8 "${modulePath}/requirements.txt" "nltk"


# Background Remover :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'Background Remover setup                                                ' "White" "Blue"

# The name of the dir containing the background remover module
moduleDir='BackgroundRemover'

# The name of the dir containing the background remover models
modulePath="${analysisLayerPath}/${moduleDir}"

# The name of the dir containing the background remover models
moduleAssetsDir='models'

# The name of the file in our S3 bucket containing the assets required for this module
modelsAssetFilename='rembg-models.zip'

setupPython 3.9
installPythonPackages 3.9 "${modulePath}/requirements.txt" "onnxruntime"

# Clean up directories to force a re-copy if necessary
if [ "${forceOverwrite}" == "true" ]; then
    rm -rf "${downloadPath}/${moduleDir}"
    rm -rf "${modulePath}/${moduleAssetsDir}"
fi

if [ ! -d  "${modulePath}/${moduleAssetsDir}" ]; then
    Download $storageUrl "${downloadPath}" $modelsAssetFilename "${moduleDir}" "Downloading models..."
    if [ -d "${downloadPath}/${moduleDir}" ]; then
        mv -f "${downloadPath}/${moduleDir}" "${modulePath}/${moduleAssetsDir}"
    fi
fi


# DeepStack specific :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'Vision toolkit setup                                                    ' "White" "Blue"

# The name of the dir containing the background remover module
moduleDir='DeepStack'

# The name of the dir containing the background remover models
modulePath="${analysisLayerPath}/${moduleDir}"

# The name of the dir containing the background remover models
moduleAssetsDir='assets'

# The name of the file in our S3 bucket containing the assets required for this module
modelsAssetFilename='models.zip'

setupPython 3.8
if [ "$platform" == "macos" ]; then
    installPythonPackages 3.8 "${modulePath}/intelligencelayer/requirements.macos.txt" "torch"
else
    installPythonPackages 3.8 "${modulePath}/intelligencelayer/requirements.txt" "torch"
fi

# Clean up directories to force a re-copy if necessary
if [ "${forceOverwrite}" == "true" ]; then
    rm -rf "${downloadPath}/${moduleDir}"
    rm -rf "${modulePath}/${moduleAssetsDir}"
fi

if [ ! -d  "${modulePath}/${moduleAssetsDir}" ]; then
    Download $storageUrl "${downloadPath}" $modelsAssetFilename "${moduleDir}" "Downloading models..."
    if [ -d "${downloadPath}/${moduleDir}" ]; then
        mv -f "${downloadPath}/${moduleDir}" "${modulePath}/${moduleAssetsDir}"
    fi
fi

# Deepstack needs these to store temp and pesrsisted data
mkdir -p "${deepStackPath}/${tempstoreDir}"

# To do this properly we're going to use the standard directories for common application data
# mkdir -p "${deepStackPath}/${datastoreDir}"
commonDataDir='/usr/share/CodeProject/SenseAI'
if [ "$platform" == "macos" ]; then 
    commonDataDir="/Library/Application Support/CodeProject/SenseAI"
fi

if [ ! -d "${commonDataDir}" ]; then
    if [ "$platform" == "macos" ]; then 
        if [[ $EUID > 0 ]]; then
            writeLine "Creating data directory at ${commonDataDir}. We'll need admin access..." $color_info
        fi

        sudo mkdir -p "${commonDataDir}"   
        if [ $? -ne 0 ]; then
            displayMacOSPermissionError "${commonDataDir}"
        fi
        sudo chmod 777 "${commonDataDir}"
    else
        mkdir -p "${commonDataDir}"
    fi
fi

# For Yolo.NET :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

writeLine
writeLine 'Object Detector setup                                                   ' "White" "Blue"

# The name of the dir containing the background remover module
moduleDir='CodeProject.SenseAI.AnalysisLayer.Yolo'

# The name of the dir containing the background remover models
modulePath="${analysisLayerPath}/${moduleDir}"

# The name of the dir containing the background remover models
moduleAssetsDir='assets'

# The name of the file in our S3 bucket containing the assets required for this module
modelsAssetFilename='yolonet-models.zip'

# Clean up directories to force a re-copy if necessary
if [ "${forceOverwrite}" == "true" ]; then
    rm -rf "${downloadPath}/${moduleDir}"
    rm -rf "${modulePath}/${moduleAssetsDir}"
fi

if [ ! -d  "${modulePath}/${moduleAssetsDir}" ]; then
    Download $storageUrl "${downloadPath}" $modelsAssetFilename "${moduleDir}" "Downloading models..."
    if [ -d "${downloadPath}/${moduleDir}" ]; then
        mv -f "${downloadPath}/${moduleDir}" "${modulePath}/${moduleAssetsDir}"
    fi
fi

# libfontconfig1 is required for SkiaSharp, libgdplus is required for System.Drawing
write "Installing supporting image libraries..."
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
        brew install mono-libgdiplus  >/dev/null 2>/dev/null &
        spin $!
    else
        brew install fontconfig
        brew install mono-libgdiplus
    fi
fi
writeLine "Done" $color_success


# ============================================================================
# ...and we're done.

writeLine 
writeLine '                Development Environment setup complete                  ' 'White' 'DarkGreen'
writeLine 

quit