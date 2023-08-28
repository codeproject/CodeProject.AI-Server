# :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# CodeProject.AI Server Utilities
#
# Utilities for use with Linux/macOS Development Environment install scripts
# 
# We assume we're in the source code /Installers/Dev directory.
# 
# :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# Quits the script and returns an optional error code (default is 0)
function quit () {

    local exit_code=$1
    if [ "$exit_code" == "" ]; then exit_code=0; fi

    if [ "${useColor}" == "true" ] && [ "${darkmode}" == "true" ]; then
        # this resets the terminal, but also clears all colours from the screen
        # which isn't great
        # tput reset
        echo
    fi

    exit $exit_code
}

# Returns a color code for the given foreground/background colors
# This code is echoed to the terminal before outputing text in
# order to generate a colored output.
#
# string foreground color name. Optional if no background provided.
#        Defaults to "Default" which uses the system default
# string background color name.  Optional. Defaults to "Default"
#        which is the system default
# string intense. Optional. If "true" then the insensity is turned up
# returns a string
function Color () {

    local foreground=$1
    local background=$2
    local intense=$3

    if [ "$foreground" == "" ]; then foreground='Default'; fi
    if [ "$background" == "" ]; then background='Default'; fi

    if [ "$foreground" == 'Contrast' ]; then
        foreground=$(ContrastForeground ${background})
    fi

    # Colour effect: <ESC>[(0|1)<code>m, where 0 = not intense / reset, 1 = intense    
    # See this most excellent answer: https://stackoverflow.com/a/33206814
    local colorString='\033['
    if [ "$intense" == "true" ]; then 
        colorString="${colorString}1;"
    else
        colorString="${colorString}0;"
    fi

    # Foreground Colours
    case "$foreground" in
        'Default')      colorString="${colorString}39m";;
        'Black' )       colorString="${colorString}30m";;
        'DarkRed' )     colorString="${colorString}31m";;
        'DarkGreen' )   colorString="${colorString}32m";;
        'DarkYellow' )  colorString="${colorString}33m";;
        'DarkBlue' )    colorString="${colorString}34m";;
        'DarkMagenta' ) colorString="${colorString}35m";;
        'DarkCyan' )    colorString="${colorString}36m";;
        'Gray' )        colorString="${colorString}37m";;
        'DarkGray' )    colorString="${colorString}90m";;
        'Red' )         colorString="${colorString}91m";;
        'Green' )       colorString="${colorString}92m";;
        'Yellow' )      colorString="${colorString}93m";;
        'Blue' )        colorString="${colorString}94m";;
        'Magenta' )     colorString="${colorString}95m";;
        'Cyan' )        colorString="${colorString}96m";;
        'White' )       colorString="${colorString}97m";;
        *)              colorString="${colorString}39m";;
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

function errorNoPython () {
    writeLine
    writeLine
    writeLine '----------------------------------------------------------------------' $color_primary
    writeLine 'Error: Python not installed' $color_error
    writeLine 
    writeLine
    
    quit 3 # required runtime missing, needs installing
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

# Outputs a line, including linefeed, to the terminal using the given foreground
# / background colors 
#
# string The text to output. Optional if no foreground provided. Default is 
#        just a line feed.
# string Foreground color name. Optional if no background provided. Defaults to
#        "Default" which uses the system default
# string background color name.  Optional. Defaults to "Default"
#        which is the system default
function writeLine () {

    local resetColor='\033[0m'

    local str=$1
    local forecolor=$2
    local backcolor=$3
    local width=$4

    if [ "$str" == "" ]; then
        printf '\n'
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as
    # strings without error
    if [ "$useColor" == "true" ]; then
        local colorString=$(Color ${forecolor} ${backcolor})

        if [ "$width" == "" ]; then
            printf "${colorString}%s${resetColor}\n" "${str}"
        else
            printf "${colorString}%-${width}s${resetColor}\n" "${str}"
        fi
    else
        if [ "$width" == "" ]; then
            printf "%s\n" "${str}"
        else
            printf "%-${width}s\n" "${str}"
        fi
    fi
}

# Outputs a line without a linefeed to the terminal using the given foreground
# / background colors 
#
# string The text to output. Optional if no foreground provided. Default is 
#        just a line feed.
# string Foreground color name. Optional if no background provided. Defaults to
#        "Default" which uses the system default
# string background color name.  Optional. Defaults to "Default"
#        which is the system default
function write () {

    local resetColor='\033[0m'

    local str=$1
    local forecolor=$2
    local backcolor=$3

    if [ "$str" == "" ];  then
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as
    # strings without error
    if [ "$useColor" == 'true' ]; then
        local colorString=$(Color ${forecolor} ${backcolor})
        printf "${colorString}%s${resetColor}" "${str}"
    else
        printf "%s" "$str"
    fi
}

function checkForTool () {

    # Install the tool and move on, or warn and quit?
    doInstall="true"

    local name=$1

    if command -v ${name} &> /dev/null; then
        return 0 # all good
    fi

    if [[ "${doInstall}" == "true" ]]; then
        if [ "$os" == "macos" ]; then
            # Ensure Brew is installed
            if ! command -v brew &> /dev/null; then
                writeLine "Installing brew..." $color_info
                /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)' > /dev/null

                if [ $? -ne 0 ]; then 
                    quit 10 # failed to install required tool
                fi
            fi

            writeLine "Installing ${name}..." $color_info
            brew install ${name} > /dev/null
        else
            writeLine "Installing ${name}..." $color_info
            sudo apt install ${name} -y > /dev/null
        fi

        if [ $? -ne 0 ]; then 
            quit 10 # failed to install required tool
        fi

    else
        writeLine
        writeLine
        writeLine '------------------------------------------------------------------------'
        writeLine "Error: ${name} is not installed on your system" $color_error

        if [ "$os" == "macos" ]; then
        
            writeLine "       Please run 'brew install ${name}'" $color_error

            if ! command -v brew &> /dev/null; then
                writeLine
                writeLine "Error: It looks like you don't have brew installed either" $color_warn
                writeLine '       Please run:' $color_warn
                writeLine "       /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)'" $color_warn
                quit 4 # required tool missing, needs installing
            fi
        else
            writeLine "       Please run 'sudo apt install ${name}'" $color_error
        fi

        writeLine
        writeLine
        quit 4 # required tool missing, needs installing
    fi

    return 0
}

# Thanks to https://stackoverflow.com/a/4025065/1128209
# Compares version numbers: compareVersions version1 version2
# Returns 1 if version1 > version2
#         0 if version1 = version2
#         2 if version1 < version2
compareVersions () {
    if [[ $1 == $2 ]]
    then
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
            return 1
        fi
        if ((10#${ver1[i]} < 10#${ver2[i]}))
        then
            return 2
        fi
    done

    return 0
}

function setupDotNet () {

    # only major versions accepted
    local requestedNetVersion=$1
    local requestedNetMajorVersion=$(cut -d '.' -f 1 <<< "${requestedNetVersion}")
    local sdkInstallVersion=${requestedNetMajorVersion}.$(cut -d '.' -f 2 <<< "${requestedNetVersion}")
    
    write "Checking for .NET >= ${requestedNetVersion}..."

    if command -v dotnet &> /dev/null; then
        currentDotNetVersion=$(dotnet --version) 2>/dev/null
        compareVersions $currentDotNetVersion $requestedNetVersion
        comparison=$?
    else
        currentDotNetVersion="(None)"
        comparison=2
    fi

    if [ "$comparison" == "2" ]; then

        if [ "$offlineInstall" == "true" ]; then 
            writeLine "Offline Installation: Unable to download and install .NET." $color_error
            return 4 # required tool missing, needs installing
        fi

        # The script provides the warning: "To set up a development environment or to run apps,
        # use installers rather than this script." This is because paths aren't set. No time to
        # futz around with this right now so going the dodgy route

        # writeLine "Current .NET version is ${currentDotNetVersion}. Installing newer version." $color_info
        # pushd "$sdkScriptsPath" >/dev/null
        # bash dotnet-install.sh --version $requestedNetVersion
        # popd

        if [ "$os" == "linux" ]; then 
            # Super naive
            local currentLinuxDistro=$(lsb_release -d | cut -f2 | cut -d ' ' -f1)
            currentLinuxDistro=`echo $currentLinuxDistro | tr '[:upper:]' '[:lower:]'`
            local currentLinuxVersion=$(lsb_release -r | cut -f2)

            if [ "${systemName}" == "Raspberry Pi" ]; then

                sudo bash "${sdkScriptsPath}/dotnet-install-rpi.sh" ${sdkInstallVersion}
                if [ $? -ne 0 ]; then 
                    return 2 # failed to install required runtime
                fi

            elif [ "$currentLinuxDistro" == "ubuntu" ] && [[ "$currentLinuxVersion" =~ ^(18\.04|20\.04|21\.04|22\.04|22\.10)$ ]]; then           

                writeLine "Current .NET version is ${currentDotNetVersion}. Installing newer version." $color_info
                wget https://packages.microsoft.com/config/${currentLinuxDistro}/${currentLinuxVersion}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
                sudo dpkg -i packages-microsoft-prod.deb
                rm packages-microsoft-prod.deb 
                sudo apt-get update && sudo apt-get install -y dotnet-sdk-${sdkInstallVersion}

                if [ $? -ne 0 ]; then 
                    return 2 # failed to install required runtime
                fi

            else

                writeLine "Current .NET version is ${currentDotNetVersion}. Please download and install the latest .NET SDK." $color_error
                writeLine "See https://learn.microsoft.com/en-au/dotnet/core/install/linux-ubuntu for details:" $color_error
                writeLine
                writeLine "wget https://packages.microsoft.com/config/ubuntu/<ubuntu-version>/packages-microsoft-prod.deb -O packages-microsoft-prod.deb" $color_info
                writeLine "sudo dpkg -i packages-microsoft-prod.deb" $color_info
                writeLine "rm packages-microsoft-prod.deb " $color_info
                writeLine "sudo apt-get update && sudo apt-get install -y dotnet-sdk-${sdkInstallVersion}" $color_info
                quit 3 # required runtime missing, needs installing

            fi
        else
            if [ "$architecture" == 'arm64' ]; then
                writeLine "Please download and install the .NET SDK. For macOS Arm64 (Apple Silicon) use:"
                writeLine "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-${requestedNetVersion}-macos-arm64-installer"
                quit 3 # required runtime missing, needs installing
            else
                writeLine "Please download and install the .NET SDK. For macOS Intel machines use:"
                writeLine "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-7.0.400-macos-x64-installer"
                # writeLine "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-${requestedNetVersion}-macos-x64-installer"
                quit 3 # required runtime missing, needs installing
            fi
            quit 100 # impossible code path
        fi
    else
        writeLine "Current .NET version is ${currentDotNetVersion}. Good to go." $color_success
    fi

    return 0
}

function setupPython () {

    if [ "$offlineInstall" == "true" ]; then 
        writeLine "Offline Installation: Unable to download and install Python." $color_error
        return 6 # unable to download required asset
    fi

    # M1 macs are trouble for python
    if [ "$platform" == "macos-arm64" ]; then
        writeLine "Arm64 (Apple silicon) Mac detected. Not running under Rosetta. " $color_warn
        if [ $(/usr/bin/pgrep oahd >/dev/null 2>&1; echo $?) -gt 0 ]; then
        #if [ "$(pkgutil --files com.apple.pkg.RosettaUpdateAuto)" == "" ]; then 
    	    writeLine 'Rosetta is not installed' $color_error
            needRosettaAndiBrew
        else
    	    writeLine 'Rosetta is, however, available. We can continue.' $color_success
        fi
    fi

    local pythonVersion=$1
    local installLocation=$2

    # HACK: Version 2.1 changed installLocation from LocalToModule to Local
    if [ "${installLocation}" == "LocalToModule" ]; then installLocation="Local"; fi

    if [ "${installLocation}" == "" ]; then installLocation="Shared"; fi

    if [ "${allowSharedPythonInstallsForModules}" == "false" ]; then
        if [[ "${modulePath}" == *"/modules/"* ]] && [[ "${installLocation}" == "Shared" ]]; then
            writeLine "Downloaded modules must have local Python install. Changing install location" $color_warn
            installLocation="Local"
        fi
    fi

    # Version with ".'s removed
    local pythonName="python${pythonVersion/./}"

    # For now we will force all docker installs to be local
    if [ "${installLocation}" == "Local" ]; then
        installPath="${modulePath}/bin/${os}/${pythonName}"
    else
        installPath="${runtimesPath}/bin/${os}/${pythonName}"
    fi

    if [ "${forceOverwrite}" == "true" ]; then

        writeLine "Cleaning Python directory to force re-install of Python VENV" $color_info
        writeLine "This will mean any previous PIP installs wwill be lost." $color_error

        # Force Re-download. Except we don't actually. We have a x64 version of
        # python for macOS in our S3 bucket but it's easier simply to install 
        # python natively
        # if [ -d "${downloadPath}/${platform}/${pythonName}" ]; then 
        #    rm -rf "${downloadPath}/${platform}/${pythonName}"
        # fi

        # Force overwrite
        if [ -d "${installPath}" ]; then 
            rm -rf "${installPath}"
        fi
    fi

    # =========================================================================
    # 1. Install Python. Using deadsnakes for Linux (not macOS), so be aware if
    #    you have concerns about potential late adoption of security patches.

    if [ $verbosity == "loud" ]; then
        writeLine "Python install path is ${installPath}" $color_info
    fi

    if [ ! -d "${installPath}" ]; then
        mkdir -p "${installPath}"
    fi

    globalPythonCmd="python${pythonVersion}"
    if command -v $globalPythonCmd &> /dev/null; then
         writeLine "Python ${pythonVersion} is already installed" $color_success
    else
        # For macOS we'll use brew to install python
        if [ "$os" == "macos" ]; then

            # We first need to ensure GCC is installed. 
            write "Checking for GCC and xcode tools..." $color_primary

            xcodetoolsVersion=$(getXcodeToolsVersion)
            if [ $? -ne 0 ] || [ $((xcodetoolsVersion)) -lt 2396 ]; then
                writeLine "Requesting install." $color_info
                xcode-select --install
            else
                writeLine "present" $color_success
            fi

            write "Installing Python ${pythonVersion}..." $color_primary

            if [ "$platform" == "macos-arm64" ]; then

                # Apple silicon requires Rosetta2 for python to run for Python < 3.9.1,
                # so use the x86 version of Brew we installed earlier
                if [ $(versionCompare "${pythonVersion}" '3.9.1') == "-1" ]; then
                    if [ "${verbosity}" == "quiet" ]; then
                        arch -x86_64 /usr/local/bin/brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                        spin $!
                    else
                        arch -x86_64 /usr/local/bin/brew install python@${pythonVersion}
                    fi
                else
                    if [ "${verbosity}" == "quiet" ]; then
                        brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                        spin $!
                    else
                        brew install python@${pythonVersion}
                    fi
                fi

            else

                # We have a x64 version of python for macOS (Intel) in our S3 bucket
                # but it's easier to simply install python natively

                # Download $storageUrl $downloadPath "python3.7.12-osx64.tar.gz" \
                #                   "${platform}/${pythonDir}" "Downloading Python interpreter..."
                # cp -R "${downloadPath}/${platform}/${pythonDir}" "${runtimesPath}/bin/${platform}"

                if [ "${verbosity}" == "quiet" ]; then
                    brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                    spin $!
                else
                    brew install python@${pythonVersion}
                fi

            fi

            # Note that we only need the system-wide location of the python 
            # interpreter to setup the virtual environment. After it's setup,
            # all python calls are made using the venv's python
            globalPythonCmd="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"

            writeLine "Done" $color_success

        # macOS: With my M1 chip and Rosetta I make installing Python a real PITA.
        # Raspberry Pi: Hold my beer 
        elif [ "${systemName}" == "Raspberry Pi" ]; then

            pushd "${pathToInstallerBase}" > /dev/null

            # Update at your leisure. 
            # See https://www.python.org/ftp/python/ for a complete list.
            case "${pythonVersion}" in
                "3.0")  pythonPatchVersion="3.0";;
                "3.1")  pythonPatchVersion="3.1";;
                "3.2")  pythonPatchVersion="3.2";;
                # 3.3.0, 3.4.0, 3.5.0, 3.6.0
                "3.7")  pythonPatchVersion="3.7.9";;
                "3.8")  pythonPatchVersion="3.8.10";;
                "3.9")  pythonPatchVersion="3.9.2";;
                "3.10") pythonPatchVersion="3.10.4";;
                "3.11") pythonPatchVersion="3.11.1";;
                "3.12") pythonPatchVersion="3.12.0";;
                *)      pythonPatchVersion="${pythonVersion}.0"
            esac

            # install the pre-requisites
            sudo apt-get update -y && sudo apt --yes --force-yes upgrade
            sudo apt-get dist-upgrade

            if [[ $(command -v checkinstall) ]]; then
                sudo apt -qq install -y wget build-essential < /dev/null
            else
                sudo apt -qq install -y wget build-essential checkinstall < /dev/null
            fi

            sudo apt-get install -y python3-dev python-setuptools # < /dev/null

            # Might not be needed
            sudo apt-get install -y python3-pip # < /dev/null
            sudo apt-get install -y python-smbus
            sudo apt-get install -y libncurses5-dev libgdbm-dev libc6-dev
            sudo apt-get install -y tk-dev libsqlite3-dev zlib1g-dev
            sudo apt-get install -y libssl-dev openssl
            sudo apt-get install -y libffi-dev
            sudo apt-get install -y libncursesw5-dev libreadline6-dev libdb5.3-dev libbz2-dev libexpat1-dev liblzma-dev

            # Get the Python tar ball and extract into our downloads dir

            mkdir --parents downloads/Python
            cd downloads/Python

            if [ ! -f "Python-${pythonPatchVersion}.tar.xz" ]; then
                # curl https://www.python.org/ftp/python/${pythonPatchVersion}/Python-${pythonPatchVersion}.tar.xz | tar -xf
                curl https://www.python.org/ftp/python/${pythonPatchVersion}/Python-${pythonPatchVersion}.tar.xz --output Python-${pythonPatchVersion}.tar.xz
            fi

            if [ ! -d "Python-${pythonPatchVersion}" ]; then
                tar -xf Python-${pythonPatchVersion}.tar.xz
            fi

            # Apply patch to enable SSL See https://joshspicer.com/python37-ssl--issue
            # sudo cp "../../Patch/Python${pythonVersion}/Setup" /Python-${pythonPatchVersion}/Modules

            # Install SSL
            # https://www.aliengen.com/blog/install-python-3-7-on-a-raspberry-pi-with-raspbian-8
            if [ ! -f "openssl-1.1.1c.tar.gz" ]; then
                curl https://www.openssl.org/source/openssl-1.1.1c.tar.gz  --output openssl-1.1.1c.tar.gz
            fi
            if [ ! -d "openssl-1.1.1c" ]; then
                tar -xf openssl-1.1.1c.tar.gz
            fi

            cd openssl-1.1.1c/
            ./config shared --prefix=/usr/local/
            make -j $(nproc)
            sudo make install
            cd ..
            sudo rm -r openssl-1.1.1c
            sudo apt-get install libssl-dev -y

            # Bulld Python
            cd Python-${pythonPatchVersion}
            sudo ./configure --enable-optimizations  --prefix=/usr
            make -j $(nproc) < /dev/null
            sudo make -j $(nproc) altinstall
            cd ..

            # Cleanup
            sudo rm -r Python-${pythonPatchVersion}
            
            #. ~/.bashrc

            popd

            # lsb_release is too short-sighted to handle multiple python packages
            # Modified from https://stackoverflow.com/a/61605955
            sudo ln -s /usr/share/pyshared/lsb_release.py /usr/lib/python{pythonPatchVersion}/site-packages/lsb_release.py

            # And sometims Pip just isn't here. So...
            curl -O https://bootstrap.pypa.io/get-pip.py
            sudo python3 get-pip.py pip==20.3.4
            pip3 install --upgrade pip

        # For Linux we'll use apt-get the deadsnakes PPA to get the old version
        # of python. Deadsnakes? Old python? Get it? Get it?! And who said 
        # developers have no sense of humour.
        else

            if [ ! "${verbosity}" == "loud" ]; then

                write "Installing Python ${pythonVersion}..." $color_primary

                if [ "${verbosity}" == "info" ]; then writeLine "Updating apt-get" $color_info; fi;
                sudo apt-get update -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Installing software-properties-common" $color_info; fi;
                sudo apt install software-properties-common -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Adding deadsnakes as a Python install source (PPA)" $color_info; fi;
                sudo add-apt-repository ppa:deadsnakes/ppa -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Updating apt" $color_info; fi;
                sudo apt update -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Upgrading apt" $color_info; fi;
                sudo apt upgrade  -y >/dev/null 2>/dev/null &
                spin $!

                if [ "${verbosity}" == "info" ]; then writeLine "Installing Python ${pythonVersion}" $color_info; fi;
                sudo apt-get install python${pythonVersion} -y >/dev/null 2>/dev/null &
                spin $!

                # apt-get install python3-pip
                writeLine "Done" $color_success
            else
                writeLine "Updating apt-get" $color_info
                sudo apt-get update -y
                writeLine "Installing software-properties-common" $color_info
                sudo apt install software-properties-common -y
                writeLine "Adding deadsnakes as a Python install source (PPA)" $color_info
                sudo add-apt-repository ppa:deadsnakes/ppa -y
                writeLine "Updating apt" $color_info
                sudo apt update -y
                writeLine "Upgrading apt" $color_info
                sudo apt upgrade -y
                writeLine "Installing Python ${pythonVersion}" $color_primary
                sudo apt-get install python${pythonVersion} -y
                sudo apt-get install python3-pip
                writeLine "Done" $color_success
            fi
        fi

        if ! command -v $globalPythonCmd &> /dev/null; then
            return 2 # failed to install required runtime
        fi

    fi

    # =========================================================================
    # 2. Create Virtual Environment

    if [ -d  "${installPath}/venv" ]; then
        writeLine "Virtual Environment already present" $color_success
    else

        # Before we start we need to ensure we can create a Virtual environment
        # that has the the correct tools, pip being the most important.
        #
        # The process is different between macOS and Ubuntu due to how we've 
        # installed python, The following code is messy (sorry) in order to 
        # handle both cases as well as quiet / not quiet output. Critical when 
        # trying to debug issues.
        #
        # If venv creation fails, ensure you remove the old venv folder before 
        # trying again

        if [ "$os" == "macos" ]; then
            if [ "${verbosity}" == "quiet" ]; then
                write "Installing Virtual Environment tools for mac..." $color_primary
                
                pip3 $pipFlags install setuptools virtualenv virtualenvwrapper >/dev/null &
                spin $!
                writeLine "Done" $color_success

            else
                writeLine "Installing Virtual Environment tools for mac..." $color_primary
        
                # regarding the warning: See https://github.com/Homebrew/homebrew-core/issues/76621
                if [ $(versionCompare "${pythonVersion}" '3.10.2') == "-1" ]; then
                    writeLine "Ignore the DEPRECATION warning. See https://github.com/Homebrew/homebrew-core/issues/76621 for details" $color_info
                fi

                pip3 $pipFlags setuptools install virtualenv virtualenvwrapper
            fi
        else
            if [ "${verbosity}" == "quiet" ]; then
                write 'Installing Virtual Environment tools for Linux...' $color_primary

                sudo apt install python3-pip python3-setuptools python${pythonVersion}-venv -y >/dev/null 2>/dev/null &
                spin $!
                writeLine "Done" $color_success
            else
                writeLine 'Installing Virtual Environment tools for Linux...' $color_primary
                sudo apt install python3-pip python3-setuptools python${pythonVersion}-venv -y
            fi
        fi

        # Create the virtual environments. All sorts of things can go wrong here
        # but if you have issues, make sure you delete the venv directory before
        # retrying.
        write "Creating Virtual Environment..." $color_primary
        
        if [ $verbosity == "loud" ]; then
            writeLine "Install path is ${installPath}"
        fi

        if [ "$os" == "macos" ]; then
            $globalPythonCmd -m venv "${installPath}/venv"
        else
            #echo $globalPythonCmd
            #echo $installPath
            
            $globalPythonCmd -m venv "${installPath}/venv" &
            spin $! # process ID of the python install call
        fi

        if [ ! -d "${installPath}/venv" ]; then
            return 5 # unable to create Python virtual environment
        fi

        writeLine "Done" $color_success
    fi

    # our DIY version of Python 'Activate' for virtual environments
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
    writeLine 'present' $color_success

    return 0
}

function installPythonPackages () {

    if [ "$offlineInstall" == "true" ]; then 
        writeLine "Offline Installation: Unable to download and install Python packages." $color_error
        return 6 # unable to download required asset
    fi

    pythonVersion=$1
    requirementsDir=$2

    # Either "Local" or "Shared"
    installLocation=$3

    # HACK: Version 2.1 changed installLocation from LocalToModule to Local
    if [ "${installLocation}" == "LocalToModule" ]; then installLocation="Local"; fi

    if [ "${installLocation}" == "" ]; then installLocation="Shared"; fi

    if [ "${allowSharedPythonInstallsForModules}" == "false" ]; then
        if [[ "${modulePath}" == *"/modules/"* ]] && [[ "${installLocation}" == "Shared" ]]; then
            writeLine "Downloaded modules must have local Python install. Changing install location" $color_warn
            installLocation="Local"
        fi
    fi

    # Version with ".'s removed
    local pythonName="python${pythonVersion/./}"
    pythonCmd="./python${pythonVersion}"

    # hasCUDA is actually already set in /src/setup.sh, but no harm in keeping this check here.
    # Note that CUDA is only available on non-macOS systems
    hasCUDA='false'
    if [ "$os" == "linux" ]; then
        if [ "$supportCUDA" == "true" ]; then
            write 'Checking for CUDA...'

            # nvidia=$(lspci | grep -i '.* vga .* nvidia .*')
            # if [[ ${nvidia,,} == *' nvidia '* ]]; then  # force lowercase compare

            if [ -x "$(command -v nvidia-smi)" ]; then
                nvidia=$(nvidia-smi | grep -i -E 'CUDA Version: [0-9]+.[0-9]+') > /dev/null 2>&1
                if [[ ${nvidia} == *'CUDA Version: '* ]]; then 
                    hasCUDA='true'
                fi
            fi
            if [ "$hasCUDA" == "true" ]; then
                writeLine 'CUDA Present' $color_success
            else 
                writeLine 'Not found' $color_mute
            fi
        fi
    fi

    # This is getting complicated. The order of priority for the requirements file is:
    #
    #  requirements.os.architecture.cuda.txt
    #  requirements.os.cuda.txt
    #  requirements.cuda.txt
    #  requirements.os.architecture.gpu.txt
    #  requirements.os.gpu.txt
    #  requirements.gpu.txt
    #  requirements.os.architecture.txt
    #  requirements.os.txt
    #  requirements.txt
    #
    # The logic here is that we go from most specific to least specific. The only
    # real tricky bit is the subtlety around .cuda vs .gpu. CUDA is a specific
    # type of card. We may not be able to support that, but may be able to support
    # other cards generically via OpenVINO or DirectML. So CUDA first, then GPU,
    # then CPU. With a query at each steo for OS and architecture.

    requirementsFilename=""

    if [ "$enableGPU" == "true" ]; then
        if [ "$hasCUDA" == "true" ]; then
            if [ -f "${requirementsDir}/requirements.${os}.${architecture}.cuda.txt" ]; then
                requirementsFilename="requirements.${os}.${architecture}.cuda.txt"
            elif [ -f "${requirementsDir}/requirements.${os}.cuda.txt" ]; then
                requirementsFilename="requirements.${os}.cuda.txt"
            elif [ -f "${requirementsDir}/requirements.cuda.txt" ]; then
                requirementsFilename="requirements.cuda.txt"
            fi
        fi

        if [ "$hasROCm" == "true" ]; then
            if [ -f "${requirementsDir}/requirements.${os}.${architecture}.rocm.txt" ]; then
                requirementsFilename="requirements.${os}.${architecture}.rocm.txt"
            elif [ -f "${requirementsDir}/requirements.${os}.rocm.txt" ]; then
                requirementsFilename="requirements.${os}.rocm.txt"
            elif [ -f "${requirementsDir}/requirements.rocm.txt" ]; then
                requirementsFilename="requirements.rocm.txt"
            fi
        fi

        if [ "$requirementsFilename" == "" ]; then
            if [ -f "${requirementsDir}/requirements.${os}.${architecture}.gpu.txt" ]; then
                requirementsFilename="requirements.${os}.${architecture}.gpu.txt"
            elif [ -f "${requirementsDir}/requirements.${os}.gpu.txt" ]; then
                requirementsFilename="requirements.${os}.gpu.txt"
            elif [ -f "${requirementsDir}/requirements.gpu.txt" ]; then
                requirementsFilename="requirements.gpu.txt"
            fi
        fi
    fi

    if [ "$requirementsFilename" == "" ]; then
        if [ -f "${requirementsDir}/requirements.${os}.${architecture}.txt" ]; then
            requirementsFilename="requirements.${os}.${architecture}.txt"
        elif [ -f "${requirementsDir}/requirements.${os}.txt" ]; then
            requirementsFilename="requirements.${os}.txt"
        elif [ -f "${requirementsDir}/requirements.txt" ]; then
            requirementsFilename="requirements.txt"
        fi
    fi

    if [ "$requirementsFilename" != "" ]; then
        requirementsPath="${requirementsDir}/${requirementsFilename}"
    fi

    if [ "$requirementsFilename" == "" ]; then
        writeLine "No suitable requirements.txt file found." $color_warn
        return
    fi

    if [ ! -f "$requirementsPath" ]; then
        writeLine "Can't find ${requirementsPath} file." $color_warn
        return
    fi

    if [ "${installLocation}" == "Local" ]; then
        virtualEnv="${modulePath}/bin/${os}/${pythonName}/venv"
    else
        virtualEnv="${runtimesPath}/bin/${os}/${pythonName}/venv"
    fi
    # echo "virtualEnv = ${virtualEnv}"

    # For speeding up debugging
    if [ "${skipPipInstall}" == "true" ]; then return; fi

    # We'll head into the venv's bin directory which should contain the python interpreter
    pushd "${virtualEnv}/bin"  >/dev/null

    if [ "$os" == "macos" ]; then
        # Running "PythonX.Y" should work, but may not. Check, and if it doesn't work then set the
        # pythonCmd var to point to the absolute pather where we think the python launcher should be
        $pythonCmd --version >/dev/null  2>/dev/null
        if [ $? -ne 0 ]; then
            writeLine "Setting python command to point to global install location"
            pythonCmd="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"
        fi
    fi

    # Before installing packages, check to ensure PIP is installed and up to 
    # date. This slows things down a bit, but it's worth it in the end.
    if [ "${verbosity}" == "quiet" ]; then

        # Ensure we have pip (no internet access - ensures we have the current
        # python compatible version).
        write 'Ensuring PIP is installed...' $color_primary
        $pythonCmd -m ensurepip >/dev/null 2>/dev/null &
        spin $!
        writeLine 'Done' $color_success

        write 'Updating PIP...' $color_primary
        $pythonCmd -m pip install --upgrade pip >/dev/null 2>/dev/null &
        spin $!
        writeLine 'Done' $color_success
    else
        writeLine 'Ensuring PIP is installed and up to date...' $color_primary
    
        # if [ "$os" == "macos" ]; then
        #     # regarding the warning: See https://github.com/Homebrew/homebrew-core/issues/76621
        #     if [ $(versionCompare "${pythonVersion}" '3.10.2') == "-1" ]; then
        #         writeLine "Ignore the DEPRECATION warning. See https://github.com/Homebrew/homebrew-core/issues/76621 for details" $color_info
        #     fi
        # fi
    
        if [ "$os" == "macos" ]; then
            # sudo $globalPythonCmd -m ensurepip
            $pythonCmd -m ensurepip
            $pythonCmd -m pip install --upgrade pip
        else
            sudo $pythonCmd -m ensurepip
            $pythonCmd -m pip install --upgrade pip
        fi
    fi 
    popd  >/dev/null

    # =========================================================================
    # Install PIP packages

    packagesPath="${virtualEnv}/lib/python${pythonVersion}/site-packages/"

    pushd "${virtualEnv}/bin" >/dev/null

    #hack
    write 'Installing setuptools...' $color_primary
    # pip3 install setuptools
    $pythonCmd -m pip install setuptools >/dev/null 2>/dev/null &
    spin $!
    writeLine "Done" $color_success

    writeLine "Choosing packages from ${requirementsFilename}" $color_info

    if [ "${oneStepPIP}" == "true" ]; then

        # Install the Python Packages in one fell swoop. Not much feedback, but it works
        write 'Installing Packages into Virtual Environment...' $color_primary
        if [ "${verbosity}" != "loud" ]; then
            # writeLine "${pythonCmd} -m pip install $pipFlags -r ${requirementsPath} --target ${packagesPath}" $color_info
            #hack
            #./pip3 install $pipFlags -r ${requirementsPath} --target ${packagesPath} # > /dev/null &
            $pythonCmd -m pip install $pipFlags -r ${requirementsPath} --target ${packagesPath} > /dev/null &
            spin $!
        else
            #hack
            # ./pip3 install $pipFlags -r ${requirementsPath} --target ${packagesPath}
            $pythonCmd -m pip install $pipFlags -r ${requirementsPath} --target ${packagesPath}
        fi
        writeLine 'Success' $color_success

    else

        # Open requirements.txt and grab each line. We need to be careful with --find-links lines
        # as this doesn't currently work in Linux
        currentOption=''

        IFS=$'\n' # set the Internal Field Separator as end of line
        cat "${requirementsPath}" | while read -r line
        do

            line="$(echo $line | tr -d '\r\n')"    # trim newlines / CRs

            if [ "${line}" == "" ]; then
                currentOption=''
            elif [ "${line:0:1}" == "#" ]; then
                currentOption=''
            elif [ "${line:0:1}" == "-" ]; then
                currentOption="${currentOption} ${line}"
            else
        
                module="${line}"
                description=''

                # breakup line into module name and description
                IFS='#'; tokens=($module); IFS=$'\n';

                if [ ${#tokens[*]} -gt 1 ]; then
                    module="${tokens[0]}"
                    description="${tokens[1]}"
                fi

                if [ "${description}" == "" ]; then
                    description="Installing ${module}"
                fi
    
                # remove all whitespaces
                # module="${module// /}"

                if [ "${module}" != "" ]; then

                    # writeLine "./pip install ${pipFlags} $module ${currentOption}" $color_error
                    write "  -${description}..." $color_primary

                    # TODO: We should test first. Alter the requirements file to provide the 
                    # name of a module (module_import) to be tested before we import
                    # if python3 -c "import ${module_import}"; then echo "Found ${module}. Skipping."; fi;

                    if [ "${verbosity}" != "loud" ]; then
                        # I have NO idea why it's necessary to use eval to get this to work without errors
                        # ./pip3 install ${module} ${currentOption} >/dev/null & # 2>/dev/null &

                        #hack
                        # eval "./pip3 install ${module} ${currentOption} -q -q -q" >/dev/null &
                        $pythonCmd -m pip install ${module} ${currentOption} --target ${packagesPath} >/dev/null  2>/dev/null &
                        spin $!
                    else
                        # ./pip3 install $module ${currentOption}
                        #hack
                        #eval "./pip3 install ${module} ${currentOption}"
                        $pythonCmd -m pip install ${module} ${currentOption}  --target ${packagesPath}
                    fi

                    status=$?    
                    if [ $status -eq 0 ]; then
                        writeLine "Done" $color_success
                    else
                        writeLine "Failed" $color_error
                    fi
                fi

                currentOption=''

            fi

        done
        unset IFS

    fi

    popd  >/dev/null

    return 0
}

function getFromServer () {

    # eg packages_for_gpu.zip
    local fileToGet=$1

    # eg assets
    local moduleAssetsDir=$2

    # output message
    local message=$3

    # Clean up directories to force a re-copy if necessary
    if [ "${forceOverwrite}" == "true" ]; then
        # if [ $verbosity -ne "quiet" ]; then echo "Forcing overwrite"; fi

        rm -rf "${downloadPath}/${moduleDir}"
        rm -rf "${modulePath}/${moduleAssetsDir}"
    fi

    # Download !$storageUrl$fileToGet to $downloadPath and extract into $downloadPath/$moduleDir
    # Params are: S3 storage bucket | fileToGet     | downloadToDir     | dirToSaveTo | message
    # eg           "$S3_bucket"   "rembg-models.zip" /downloads/module/"    "assets"    "Downloading Background Remover models..."
    downloadAndExtract $storageUrl $fileToGet "${downloadPath}" "${moduleDir}" "${message}"

    # Copy contents of downloadPath\moduleDir to runtimesPath\moduleDir\moduleAssetsDir
    if [ -d "${downloadPath}/${moduleDir}" ]; then

        if [ ! -d "${modulePath}/${moduleAssetsDir}" ]; then
            mkdir -p "${modulePath}/${moduleAssetsDir}"
        fi;

        # pushd then cp to stop "cannot stat" error
        pushd "${downloadPath}/${moduleDir}/" >/dev/null 2>/dev/null

        # This code will have issues if you download more than 1 zip to a download folder.
        # 1. Copy *everything* over (including the downloaded zip)        
        # 2. Remove the original download archive which was copied over along with everything else.
        # 3. Delete all but the downloaded archive from the downloads dir
        # cp * "${modulePath}/${moduleAssetsDir}/"
        # rm "${modulePath}/${moduleAssetsDir}/${fileToGet}"  #>/dev/null 2>/dev/null
        # ls | grep -xv *.zip | xargs rm

        # Safer.
        # 1. Copy all non-zip files to the module's installation dir
        # 2. Delete all non-zip files in the download dir
        if [ $verbosity == "quiet" ]; then 
            rsync -rav --exclude='*.zip' --exclude='.DS_Store' * "${modulePath}/${moduleAssetsDir}/"  >/dev/null 
            find . -type f -not -name '*.zip' | xargs rm >/dev/null 2>/dev/null
            find . -type d -not -name . -not -name ..| xargs rmdir >/dev/null 2>/dev/null
        else
            rsync -rav --exclude='*.zip' --exclude='.DS_Store' * "${modulePath}/${moduleAssetsDir}/"
            find . -type f -not -name '*.zip' | xargs rm  2>/dev/null
            find . -type d -not -name . -not -name ..| xargs rmdir  2>/dev/null
        fi

        popd >/dev/null 2>/dev/null
    else
        return 6 # unable to download required asset
    fi

    return 0
}

function downloadAndExtract () {

    local storageUrl=$1
    local fileToGet=$2
    local downloadToDir=$3
    local dirToSave=$4
    local message=$5

    # storageUrl = 'https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/'
    # downloadToDir = 'downloads/' - relative to the current directory
    # fileToGet = packages_for_gpu.zip
    # dirToSave = packages
   
    if [ "${fileToGet}" == "" ]; then
        writeLine 'No download file was specified' $color_error
        quit 9 # required parameter not supplied
    fi

    if [ "${message}" == "" ]; then
        message="Downloading ${fileToGet}..."
    fi

    if [ $verbosity != "quiet" ]; then 
        writeLine "Downloading ${fileToGet} to ${downloadToDir}/${dirToSave}" $color_info
    fi
    
    write "$message" $color_primary

    extension="${fileToGet:(-3)}"
    if [ ! "${extension}" == ".gz" ]; then
        extension="${fileToGet:(-4)}"
        if [ ! "${extension}" == ".zip" ]; then
            writeLine "Unknown and unsupported file type for file ${fileToGet}" $color_error
            quit    # no point in carrying on
        fi
    fi

    if [ -f  "${downloadToDir}/${dirToSave}/${fileToGet}" ]; then     # To check for the download itself
        write " already exists..." $color_info
    else

        if [ "$offlineInstall" == "true" ]; then 
            writeLine "Offline Installation: Unable to download ${fileToGet}." $color_error
            return 6  # unable to download required asset
        fi

        # writeLine "Downloading ${fileToGet} to ${dirToSave}.zip in ${downloadToDir}"  $color_warn
        # wget $wgetFlags --show-progress -O "${downloadToDir}/${dirToSave}/${fileToGet}" -P "${downloadToDir}/${dirToSave}" \
        #                                   "${storageUrl}${fileToGet}"

        wget $wgetFlags --progress=bar:force -P "${downloadToDir}/${dirToSave}" "${storageUrl}${fileToGet}"
        status=$?    
        if [ $status -ne 0 ]; then
            writeLine "The wget command failed for file ${fileToGet}." $color_error
            quit 6 # unable to download required asset
        fi
    fi

    if [ ! -f  "${downloadToDir}/${dirToSave}/${fileToGet}" ]; then
        writeLine "The downloaded file '${fileToGet}' doesn't appear to exist." $color_error
        quit 6 # unable to download required asset
    fi

    write 'Expanding...' $color_info

    pushd "${downloadToDir}/${dirToSave}" >/dev/null
  
    if [ $verbosity == "quiet" ]; then 
        if [ "${extension}" == ".gz" ]; then
            tar $tarFlags "${fileToGet}" >/dev/null &  # execute and continue
            spin $! # process ID of the unzip/tar call
        else
            unzip $unzipFlags "${fileToGet}" >/dev/null &  # execute and continue
            spin $! # process ID of the unzip/tar call
        fi
    else
        if [ "${extension}" == ".gz" ]; then
                tar $tarFlags "${fileToGet}"
        else
                unzip $unzipFlags "${fileToGet}"
        fi
    fi
    
    if [ ! "$(ls -A .)" ]; then # Is the download dir empty?
        writeLine "Unable to extract download. Can you please check you have write permission to "${dirToSave}"." $color_error
        popd >/dev/null
        quit 7  # unable to expand compressed archivep
    fi
    
    # Remove thw downloaded zip
    # rm -f "${fileToGet}" >/dev/null

    popd >/dev/null

    writeLine 'Done.' $color_success

    return 0
}

# Thanks: https://stackoverflow.com/a/4025065 with mods
# compares two version numbers (eg 3.9.12 < 3.10.1).
# Returns $1 == $2 -> 0 
#         $1 < $2 -> -1
#         $1 > $2 ->  1
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

# Call this, then test: if [ $online -eq 0 ]; then echo 'online'; fi
function checkForInternet () {
    nc -w 2 -z 8.8.8.8 53  >/dev/null 2>&1
    online=$?
}

function getVersionFromModuleSettings () { # jsonFile key

    # Code thanks to ChatGPT. Radically reworked to make it...work

    local json_file=$1
    local key=$2

    # echo jsonFile is $jsonFile
    # echo key is $key

    # Use the jq command to extract the value of the property from the JSON file.
    # ...except jq needs to be installed using sudo apt install jq
    #  jsonValue=$(jq -r ".$key" "$json_file")

    # or use inbuilt bash commands.
    jsonValue=$(grep -o "\"$key\":[^,}]*" "$json_file" | sed 's/.*: "\(.*\)".*/\1/')

    echo $jsonValue
}

function displayMacOSDirCreatePermissionError () {
    writeLine
    writeLine 'Unable to Create a Directory'  $color_error

    if [[ $OSTYPE == 'darwin'* ]]; then

        local commonDir=$1

        writeLine
        writeLine 'But we may be able to suggest something:'  $color_info

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

    quit 8 # unable to create file or directory
}

function needRosettaAndiBrew () {

    writeLine
    writeLine "You're on a Mac running Apple silicon but Python3 only works on Intel."  $color_error

    if [ "$offlineInstall" == "true" ]; then 
        writeLine "You will need to install Rosetta2 to continue, once you are back online." $color_error
        return 6 # unable to download required asset
    fi

    writeLine "You will need to install Rosetta2 to continue."  $color_error
    writeLine
    
    read -p 'Install Rosetta2 (Y/N)?' installRosetta
    if [ "${installRosetta}" == "y" ] || [ "${installRosetta}" == "Y" ]; then
        /usr/sbin/softwareupdate --install-rosetta --agree-to-license
    else
        quit 4 # required tool missing, needs installing
    fi

    writeLine "Then you need to install brew under Rosetta (We'll alias it as ibrew)"
    read -p 'Install brew for x86 (Y/N)?' installiBrew
    if [ "${installiBrew}" == "y" ] || [ "${installiBrew}" == "Y" ]; then
        arch -x86_64 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)"
    else
        quit 4 # required tool missing, needs installing
    fi
}

# =================================================================================================

# SETUP

# A NOTE ON PLATFORM.
# We use the full x86_64 for architecture, but follow the common convention of
# abbreviating this to x64 when used in conjuntion with OS (ie platform). So 
# macOS-x64 rather than macOS-x86_64. To simplify further, if the platform value
# doesn't have a suffix then it's assumed to be -x64. This may change in the future.

if [ $(uname -m) == 'arm64' ] || [ $(uname -m) == 'aarch64' ]; then
    architecture='arm64'
else
    architecture='x86_64'
fi

if [[ $OSTYPE == 'darwin'* ]]; then
    platform='macos'
    os="macos"
    os_name=$(awk '/SOFTWARE LICENSE AGREEMENT FOR macOS/' '/System/Library/CoreServices/Setup Assistant.app/Contents/Resources/en.lproj/OSXSoftwareLicense.rtf' | awk -F 'macOS ' '{print $NF}' | awk '{print substr($0, 0, length($0)-1)}') # eg "Big Sur"
    os_vers=$(sw_vers -productVersion) # eg "11.1" for macOS Big Sur

    systemName=$os

    if [[ "$architecture" == 'arm64' ]]; then platform='macos-arm64'; fi
else
    os='linux'
    platform='linux'
    os_name=$(. /etc/os-release;echo $ID) # eg "ubuntu"
    os_vers=$(. /etc/os-release;echo $VERSION_ID) # eg "22.04" for Ubuntu 22.04

    if [[ "$architecture" == 'arm64' ]]; then platform='linux-arm64'; fi

    modelInfo=""
    if [ -f "/sys/firmware/devicetree/base/model" ]; then
        modelInfo=$(tr -d '\0' </sys/firmware/devicetree/base/model) >/dev/null 2>&1
    fi

    if [[ "${modelInfo}" == *"Raspberry Pi"* ]]; then # elif [ $(uname -n) == "raspberrypi" ]; then
        systemName='Raspberry Pi'
    elif [[ "${modelInfo}" == *"Orange Pi"* ]]; then    # elif [ $(uname -n) == "orangepi5" ]; then
        systemName='Orange Pi'
    elif [ $(uname -n) == "nano" ]; then
        systemName='Jetson'
    elif [ "$inDocker" == "true" ]; then 
        systemName='Docker'
    elif [[ $(uname -a) =~ microsoft-standard-WSL ]]; then
        systemName='WSL'
    else
        systemName=$os
    fi
fi

# See if we can spot if it's a dark or light background
darkmode='false'
if [ "$os" == "macos" ]; then
    interfaceStyle=$(defaults read -g AppleInterfaceStyle 2>/dev/null)
    if [ $? -eq 0 ]; then
        if [ "${interfaceStyle}" == "Dark" ]; then
            darkmode='true'
        fi
    else
        termBg=$(osascript -e \
            'tell application "Terminal" 
               get background color of selected tab of window 1
            end tell') >/dev/null 2>&1

        if [[ $termBg ]]; then
            IFS=','; colors=($termBg); IFS=' ';
            if [ ${colors[0]} -lt 2000 ] && [ ${colors[1]} -lt 2000 ] && [ ${colors[2]} -lt 2000 ]; then
                darkmode='true'
            else
                darkmode='false'
            fi
        fi
    fi
else
    darkmode='true'
    terminalBg=$(gsettings get org.gnome.desktop.background primary-color)

    if [ "${terminalBg}" != "no schemas installed" ] && [ "${terminalBg}" != "" ]; then
        terminalBg="${terminalBg%\'}"                               # remove first '
        terminalBg="${terminalBg#\'}"                               # remove last '
        terminalBg=`echo $terminalBg | tr '[:lower:]' '[:upper:]'`  # uppercase-ing

        if [[ $terminalBg =~ ^\#[0-9A-F]{6}$ ]]; then   # if it's of the form #xxxxxx hex colour

            a=`echo $terminalBg | cut -c2-3`
            b=`echo $terminalBg | cut -c4-5`
            c=`echo $terminalBg | cut -c6-7`

            r=`echo "ibase=16; $a" | bc`
            g=`echo "ibase=16; $b" | bc`
            b=`echo "ibase=16; $c" | bc`

            luma=$(echo "(0.2126 * $r) + (0.7152 * $g) + (0.0722 * $b)" | bc)
            luma=${luma%.*}
            
            if (( luma > 127 )); then 
                darkmode='false'
            fi

            # echo "terminalBg = ${terminalBg}, darkmode = ${darkmode}, luminosity = ${luma}"
        fi
    else
        writeLine "(No schemas means: we can't detect if you're in light or dark mode)"  $color_info
    fi
fi

# echo "Darkmode = ${darkmode}"

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

# For VSCode, the terminal depends on the color theme installed, so who knows?
if [ "$TERM_PROGRAM" == "vscode" ]; then color_primary='Default'; fi


# Outputs the version of the currently installed xcode tools. It's placed at the bottom because
# this command completely screws up the colourisation of the rest of the script in Visual Studio.
function getXcodeToolsVersion() {
    xcode-select -v | sed 's/[^0-9]*//g'
}