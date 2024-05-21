#!/bin/bash

# :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

# CodeProject.AI Server Utilities
#
# Utilities for use with Linux/macOS Development Environment install scripts
# 
# We assume we're in the source code /Installers/Dev directory.
# 
# :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

newline=$'\n'

# Quits the script and returns an optional error code (default is 0)
function quit () {

    local exit_code=$1
    if [ "$exit_code" = "" ]; then exit_code=0; fi

    if [ "${useColor}" = true ] && [ "${darkmode}" = true ]; then
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

    if [ "$foreground" = "" ]; then foreground='Default'; fi
    if [ "$background" = "" ]; then background='Default'; fi

    if [ "$foreground" = 'Contrast' ]; then
        foreground=$(ContrastForeground ${background})
    fi

    # Colour effect: <ESC>[(0|1)<code>m, where 0 = not intense / reset, 1 = intense    
    # See this most excellent answer: https://stackoverflow.com/a/33206814
    local colorString='\033['
    if [ "$intense" = "true" ]; then 
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
    if [ "$color" = '' ]; then color='Default'; fi

    if [ "$darkmode" = true ]; then
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

    # spin[0]='⠏'
    # spin[1]='⠛'
    # spin[2]='⠹'
    # spin[3]='⢸'
    # spin[4]='⣰'
    # spin[5]='⣤'
    # spin[6]='⣆'
    # spin[7]='⡇'

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

    if [ "$str" = "" ]; then
        printf '\n'
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as
    # strings without error
    if [ "$useColor" = true ]; then
        local colorString=$(Color ${forecolor} ${backcolor})

        if [ "$width" = "" ]; then
            printf "${colorString}%s${resetColor}\n" "${str}"
        else
            printf "${colorString}%-${width}s${resetColor}\n" "${str}"
        fi
    else
        if [ "$width" = "" ]; then
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

    if [ "$str" = "" ];  then
        return;
    fi

    # Note the use of the format placeholder %s. This allows us to pass "--" as
    # strings without error
    if [ "$useColor" = true ]; then
        local colorString=$(Color ${forecolor} ${backcolor})
        printf "${colorString}%s${resetColor}" "${str}"
    else
        printf "%s" "$str"
    fi
}

function CreateWriteableDir () {
    local path=$1
    local desc=$2

    # for the runtimes
    if [ ! -d "${path}" ]; then
        write "Creating ${desc} folder..." $color_primary
        #sudo 
        if [ "${verbosity}" = "loud" ]; then
            mkdir -p "${path}"
        else
            mkdir -p "${path}" >/dev/null 2>/dev/null
        fi
        if [ $? -eq 0 ]; then 
            writeLine "done" $color_success
        else
            writeLine "Needs admin permission to create folder" $color_error
            displayMacOSDirCreatePermissionError
        fi
    fi

    if [ -d "${path}" ]; then
        checkForAdminRights
        if [ "$isAdmin" = true ]; then
            write "Setting permissions on ${desc} folder..." $color_primary 
            sudo chmod a+w "${path}" >/dev/null 2>/dev/null
            if [ $? -eq 0 ]; then 
                writeLine "done" $color_success
            else
                writeLine "Needs admin permission to set folder permissions" $color_error
            fi
        fi
    fi
}

function checkForAdminRights () {

    # Setting this to true will make this script show a popup prompt (at least in macOS) that will
    # ask for admin rights. The problem is the implementation is broken. Leaving this here as a sore
    # thumb.
    requestPassword=false

    # Be defensive
    isAdmin=false

    # Check if they are root
    if [[ $EUID = 0 ]]; then isAdmin=true; fi

    # On RPi, you get root access
    if [ "${edgeDevice}" = "Raspberry Pi" ] || [ "${edgeDevice}" = "Orange Pi" ] || [ "${edgeDevice}" = "Radxa ROCK" ]; then 
        isAdmin=true;
    fi

    # In Docker you have admin rights
    if [ "${systemName}" = "Docker" ]; then isAdmin=true; fi

    # Now check for sudo
    if [ "$isAdmin" = false ]; then
        # If there are two lines from the sudo call then it means sudo is available, otherwise not
        # See https://unix.stackexchange.com/a/692109
        # sudo_response=$(SUDO_ASKPASS=/bin/false sudo -A whoami 2>&1 | wc -l)
        # if [ $sudo_response = 2 ]; then isAdmin=true; fi

        # But this doesn't work on macOS, so we just look for "root"
        sudo_response=$(SUDO_ASKPASS=/bin/false sudo -A whoami 2>&1 | grep root)
        if [ "$sudo_response" = "root" ]; then isAdmin=true; fi
    fi

    if [ "$isAdmin" = false ] && [ "$requestPassword" = true ]; then
        if [ "$os" == "macos" ]; then
            # THIS DOES NOT WORK
            # This shows the password prompt, but the admin rights starts and ends with the "whoami"
            # call. Once that call finishes, admin rights no longer apply.
            /usr/bin/osascript -e 'do shell script "whoami 2>&1" with administrator privileges'

            sudo_response=$(SUDO_ASKPASS=/bin/false sudo -A whoami 2>&1 | grep root)
            if [ "$sudo_response" = "root" ]; then isAdmin=true; fi
        fi
    fi

    # echo "CHECK: $EUID, isAdmin ${isAdmin}"
}

function checkForAdminAndWarn () {

    local install_instructions=$1

    local indent="    "

    # Indent any text after a newline
    install_instructions="${install_instructions//${newline}/${newline}${indent}}"
    # indent the first line
    install_instructions="${indent}${install_instructions}"

    checkForAdminRights
    if [ "$isAdmin" = false ]; then
        writeLine " "
        writeLine "=======================================================================" $color_info
        writeLine "** We need to install some libraries with admin rights. Please run:  **" $color_info
        writeLine ""
        writeLine "${install_instructions}"                                                 $color_info
        writeLine ""
        writeLine "To allow this module to install successfully"                            $color_info
        writeLine "=======================================================================" $color_info

        if [ "$attemptSudoWithoutAdminRights" = true ]; then
            writeLine "We will attempt to run admin-only install commands. You may be prompted" "White" "Red"
            writeLine "for an admin password. If not then please run the script shown above.  " "White" "Red"
        fi

        writeLine " "
   fi
}

function checkForTool () {

    # Install the tool and move on, or warn and quit?
    doInstall=true

    local name=$1

    if command -v ${name} > /dev/null; then
        return 0 # all good
    fi

    if [ "${doInstall}" = true ]; then

        if [ "$os" = "macos" ]; then

            # Ensure Brew is installed. NOTE: macOS has curl built in, so no worries
            # about recursion if calling checkForTool "curl"
            if [ $"$architecture" = 'arm64' ]; then
                if [ ! -f /usr/local/bin/brew ]; then

                    checkForAdminAndWarn "arch -x86_64 /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)'"
                    if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                        writeLine "Installing brew (x64 for arm64)..." $color_info
                        arch -x86_64 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)"
                        # curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh --output install_brew.sh
                        # arch -x86_64 /bin/bash -c install_brew.sh
                        # rm install_brew.sh

                        if [ $? -ne 0 ]; then 
                            quit 10 # failed to install required tool
                        fi
                    fi
                fi
            else
                if ! command -v brew > /dev/null; then
                    writeLine "Installing brew..." $color_info

                    checkForAdminAndWarn "/bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)'"
                    if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                        curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh --output install_brew.sh
                        bash install_brew.sh
                        # /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
                        # rm install_brew.sh

                        if [ $? -ne 0 ]; then 
                            quit 10 # failed to install required tool
                        fi
                    fi
                fi
            fi

            writeLine "Installing ${name}..." $color_info
            if [ $"$architecture" = 'arm64' ]; then
                arch -x86_64 /usr/local/bin/brew install ${name} > /dev/null
            else
                brew install ${name} > /dev/null
            fi
        else
            checkForAdminAndWarn "sudo apt-get update -y && sudo apt-get install ${name} -y"

            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                writeLine "Installing ${name}..." $color_info
                sudo apt-get update -y > /dev/null & sudo apt-get install ${name} -y > /dev/null
                # Reset TTY. apt-get update can leave the console in a bad state
                stty sane > /dev/null 2>&1
            fi
        fi

        # Whilst a great idea, it doesn't work. A perfectly successful install 
        # of jq, for instance, returns > 0 error code. Who needs sanity?
        # if [ $? -ne 0 ]; then 
        #    quit 10 # failed to install required tool
        # fi

    else
        writeLine
        writeLine
        writeLine '------------------------------------------------------------------------'
        writeLine "Error: ${name} is not installed on your system" $color_error

        if [ "$os" = "macos" ]; then
        
            writeLine "       Please run 'brew install ${name}'" $color_error

            if ! command -v brew > /dev/null; then
                writeLine
                writeLine "Error: It looks like you don't have brew installed either" $color_warn
                writeLine 'Please run:' $color_warn
                writeLine "   /bin/bash -c '$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)'" $color_warn
                quit 4 # required tool missing, needs installing
            fi
        else
            writeLine "       Please run 'sudo apt-get install ${name}'" $color_error
        fi

        writeLine
        writeLine
        quit 4 # required tool missing, needs installing
    fi

    return 0
}

function setupSSL() {
    if [ "$os" = "linux" ] && [ "$architecture" == "x86_64" ]; then

        if [ ! -f /usr/lib/x86_64-linux-gnu/libssl.so.1.1 ] || [ ! -e /usr/lib/libcrypto.so.1.1 ]; then

            # output a warning message if no admin rights and instruct user on manual steps
            install_instructions="cd ${setupScriptDirPath}${newline}sudo bash setup.sh"
            checkForAdminAndWarn "$install_instructions"

            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then

                module_install_errors=""

                if [ "$os_name" != "debian" ]; then
                    echo "deb http://security.ubuntu.com/ubuntu focal-security main" | sudo tee /etc/apt/sources.list.d/focal-security.list
                fi
                installAptPackages "libssl1.1"

                write "Ensuring symlinks are created..." $color_info

                # LIBSSL: Add link at /usr/lib/libssl.so.1.1 that points to /lib/x86_64-linux-gnu/libssl.so.1.1
                if [ -f /lib/x86_64-linux-gnu/libssl.so.1.1 ] && [ ! -e /usr/lib/libssl.so.1.1 ]; then
                    if [ "${verbosity}" = "loud" ]; then
                        sudo ln -s /lib/x86_64-linux-gnu/libssl.so.1.1 /usr/lib/libssl.so.1.1
                    else
                        sudo ln -s /lib/x86_64-linux-gnu/libssl.so.1.1 /usr/lib/libssl.so.1.1 >/dev/null 2>/dev/null
                    fi
                fi

                # LIBRYPTO: Add link at /usr/lib/libcrypto.so.1.1 that points to /lib/x86_64-linux-gnu/libcrypto.so.1.1
                if [ -f /lib/x86_64-linux-gnu/libcrypto.so.1.1 ] && [ ! -e /usr/lib/libcrypto.so.1.1 ]; then
                    if [ "${verbosity}" = "loud" ]; then
                        sudo ln -s /lib/x86_64-linux-gnu/libcrypto.so.1.1 /usr/lib/libcrypto.so.1.1
                    else
                        sudo ln -s /lib/x86_64-linux-gnu/libcrypto.so.1.1 /usr/lib/libcrypto.so.1.1 >/dev/null 2>/dev/null
                    fi
                fi

                writeLine "Done" $color_success
            fi
        fi
    fi
}

function getDotNetVersion() {

    local requestedType=$1

    highestDotNetVersion="0"

    if [ "$requestedType" = "sdk" ]; then
        
        # dotnet --version gives the SDK version. 
        if command -v dotnet >/dev/null 2>/dev/null; then
            highestDotNetVersion=$(dotnet --version 2>/dev/null)
            if [ "$?" != "0" ]; then
                highestDotNetVersion="0"
            fi
        fi

    else    # runtimes

        # example output from 'dotnet --list-runtimes'
        # Microsoft.AspNetCore.App 7.0.11 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
        # Microsoft.NETCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

        comparison=-1

        IFS=$'\n' # set the Internal Field Separator as end of line
        while read -r line
        do
            if [[ ${line} == *'Microsoft.NETCore.App '* ]]; then
                dotnet_version=$(echo "$line}" | cut -d ' ' -f 2)
                current_comparison=$(versionCompare $dotnet_version $highestDotNetVersion)

                if (( $current_comparison > $comparison )); then
                    highestDotNetVersion="$dotnet_version"
                    comparison=$current_comparison
                fi
            fi
        done <<< "$(dotnet --list-runtimes)"
        unset IFS
    fi

    if [ "$highestDotNetVersion" = "0" ]; then highestDotNetVersion=""; fi

    echo "$highestDotNetVersion"
}

function setupDotNet () {

    # only major/minor versions accepted (eg 7.0)
    local requestedNetVersion=$1
    # allowed: sdk, dotnet, aspnetcore
    local requestedType=$2

    # echo "Setting up .NET ${requestedType}"

    local requestedNetMajorVersion=$(cut -d '.' -f 1 <<< "${requestedNetVersion}")
    local requestedNetMajorMinorVersion=${requestedNetMajorVersion}.$(cut -d '.' -f 2 <<< "${requestedNetVersion}")
    
    if [ "$requestedType" = "" ]; then requestedType="aspnetcore"; fi

    write "Checking for .NET ${requestedNetVersion}..."

    currentDotNetVersion="(None)"
    comparison=-1

    if [ "$requestedType" = "sdk" ]; then
        
        # dotnet --version gives the SDK version. 
        if command -v dotnet >/dev/null 2>/dev/null; then
            currentDotNetVersion=$(dotnet --version 2>/dev/null)
            if [ "$?" = "0" ]; then
                comparison=$(versionCompare $currentDotNetVersion $requestedNetVersion)
            else
                currentDotNetVersion="(None)"
            fi
        fi

    else

        # Let's test the runtimes only, since that's all we need
        # example output from 'dotnet --list-runtimes'
        # Microsoft.AspNetCore.App 7.0.11 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
        # Microsoft.NETCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

        IFS=$'\n' # set the Internal Field Separator as end of line
        while read -r line
        do
            if [[ ${line} == *'Microsoft.NETCore.App '* ]]; then
                dotnet_version=$(echo "$line}" | cut -d ' ' -f 2)
                current_comparison=$(versionCompare $dotnet_version $requestedNetVersion)

                if (( $current_comparison > $comparison )); then
                    currentDotNetVersion="$dotnet_version"
                    comparison=$current_comparison
                fi
            fi
        done <<< "$(dotnet --list-runtimes)"
        unset IFS
    fi

    if (( $comparison == 0 )); then
        writeLine "All good. .NET is ${currentDotNetVersion}" $color_success
    elif (( $comparison == -1 )); then 
        writeLine "Upgrading: .NET is ${currentDotNetVersion}" $color_warn
    else # (( $comparison == 1 ))
        writeLine "All good. .NET is ${currentDotNetVersion}" $color_success
    fi 

    if (( $comparison < 0 )); then

        if [ "$offlineInstall" = true ]; then 
            writeLine "Offline Installation: Unable to download and install .NET." $color_error
            return 6 # unable to download required asset
        fi

        # output a warning message if no admin rights and instruct user on manual steps
        if [ "$architecture" = 'arm64' ]; then
            install_instructions="sudo bash '${sdkScriptsDirPath}/dotnet-install-arm.sh' $requestedNetMajorMinorVersion $requestedType"
        else
            install_instructions="sudo bash '${sdkScriptsDirPath}/dotnet-install.sh' --channel $requestedNetMajorMinorVersion --runtime $requestedType"
        fi
        checkForAdminAndWarn "$install_instructions"

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
            if [ "$os" = "linux" ]; then

                if [ "$os_name" = "debian" ]; then
                    wget https://packages.microsoft.com/config/debian/${os_vers}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb >/dev/null
                    sudo dpkg -i packages-microsoft-prod.deb >/dev/null
                    rm packages-microsoft-prod.deb >/dev/null

                    if [ "$requestedType" = "sdk" ]; then
                        sudo apt-get update && sudo apt-get install -y dotnet-sdk-$requestedNetMajorMinorVersion >/dev/null
                    else
                        sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-$requestedNetMajorMinorVersion >/dev/null
                    fi

                else
                    if [ "$architecture" = 'arm64' ]; then
                        if [ $verbosity = "quiet" ]; then
                            sudo bash "${sdkScriptsDirPath}/dotnet-install-arm.sh" "$requestedNetMajorMinorVersion" "$requestedType" "quiet"
                        else
                            sudo bash "${sdkScriptsDirPath}/dotnet-install-arm.sh" "$requestedNetMajorMinorVersion" "$requestedType"
                        fi               
                    else
                        if [ $verbosity = "quiet" ]; then
                            sudo bash "${sdkScriptsDirPath}/dotnet-install.sh" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType" "--quiet"
                        else
                            sudo bash "${sdkScriptsDirPath}/dotnet-install.sh" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType"
                        fi
                    fi
                fi
            else
                if [ $verbosity = "quiet" ]; then
                    sudo bash "${sdkScriptsDirPath}/dotnet-install.sh" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType" "--quiet"
                else
                    sudo bash "${sdkScriptsDirPath}/dotnet-install.sh" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType"
                fi
            fi
        fi

        # if (( $? -ne 0 )); then 
        #    return 2 # failed to install required runtime
        #fi

        if [ "$os" = "linux" ] && [ "$architecture" != "arm64" ]; then

            # Add link
            # ln -s /opt/dotnet/dotnet /usr/local/bin
            
            # if [ "$os_name" = "debian" ]; then
            #    sudo ln ~/.dotnet/dotnet /usr/bin
            #fi

            # make link permanent
            if grep -q 'export DOTNET_ROOT=' ~/.bashrc;  then
                echo 'Already added link to .bashrc'
            else
                echo 'export DOTNET_ROOT=/usr/bin/' >> ~/.bashrc
            fi
        fi

        if [ "$os" == "macos" ]; then
            # The install script is for CI/CD and doesn't actually register .NET. So add 
            # link and env variable
            export DOTNET_ROOT=~/.dotnet
            export PATH=${DOTNET_ROOT}${PATH:+:${PATH}}

            if [ ! -e /usr/local/bin/dotnet ]; then
                ln -fs ~/.dotnet/dotnet /usr/local/bin/dotnet
            fi

            if grep -q 'export DOTNET_ROOT=' ~/.bashrc; then
                echo 'export DOTNET_ROOT=~/.dotnet'                >> ~/.bashrc
                echo "export PATH=${DOTNET_ROOT}${PATH:+:${PATH}}" >> ~/.bashrc
            fi
        fi
    fi

    found_dotnet=$(getDotNetVersion "$requestedType")
    if [ "$found_dotnet" = "" ]; then
        if [ "$os" = "linux" ]; then
            writeLine ".NET was not installed correctly. You may need to run the following:" $color_error
            echo "# Remove all .NET packages"
            echo "sudo apt-get remove 'dotnet*'"
            echo "sudo apt-get remove 'aspnetcore*'"
            echo "# Delete PMC repository from APT, by deleting the repo .list file"
            echo "sudo rm /etc/apt/sources.list.d/microsoft-prod.list"
            echo "sudo apt-get update"
            echo "# Install .NET SDK"
            echo "sudo apt-get install dotnet-sdk-${requestedNetVersion}"
        else
            writeLine ".NET was not installed correctly. You may need to install .NET manually"       $color_error
            writeLine "See https://learn.microsoft.com/en-us/dotnet/core/install/macos for downloads" $color_error
        fi
    else
        writeLine "Confirming .NET ${requestedType} install present. Version is ${found_dotnet}" $color_mute
    fi

    return 0
}

function setupGo () {

    # only major/minor versions accepted (eg 11.2)
    local requestedGoVersion=$1

    write "Checking for Go >= ${requestedGoVersion}..."

    currentGoVersion="(None)"
    comparison=-1

    goVersionOutput=$(go version 2>&1)
    if [[ $goVersionOutput =~ ([0-9]+\.[0-9]+\.[0-9]+) ]]; then
        currentGoVersion="${BASH_REMATCH[1]}"
        comparison=$(versionCompare $currentGoVersion $requestedGoVersion)

        if (( $comparison == 0 )); then
            writeLine "All good. Go is ${currentGoVersion}, requested was ${requestedGoVersion}" $color_success
        elif (( $comparison == -1 )); then 
            writeLine "Upgrading: Go is ${currentGoVersion}, requested was ${requestedGoVersion}" $color_warn
        else # (( $comparison == 1 ))
            writeLine "All good. Go is ${currentGoVersion}, requested was ${requestedGoVersion}" $color_success
        fi 
    fi

    if (( $comparison < 0 )); then

        if [ "${os}" = "linux" ]; then

            # Must remove old version before installing new
            rm -rf /usr/local/go

            if [ "${architecture}" = "arm64" ]; then
                wget -nv -O - https://storage.googleapis.com/golang/go${GOLANG_VERSION}.linux-arm64.tar.gz | tar -C /usr/local -xz
            else
                wget -nv -O - https://storage.googleapis.com/golang/go${GOLANG_VERSION}.linux-amd64.tar.gz | tar -C /usr/local -xz
            fi

            if grep -q 'export GOPATH=' ~/.bashrc;  then
                echo 'Already added GOPATH to .bashrc'
            else
                echo "export PATH=/usr/local/go/bin:${PATH:+:${PATH}}" >> ~/.bashrc
            fi

        else

            if [ "$currentGoVersion" = "(None)" ]; then
                brew install golang
            else
                brew update
                brew upgrade golang
            fi
            
        fi
    fi
}

function setupPython () {

    # A number of global variables are assumed here
    #  - pythonVersion     - version in X.Y format
    #  - pythonName        - python name in "pythonXY" format
    #  - venvPythonCmdPath - the path to the python interpreter for this venv
    #  - virtualEnvDirPath - the path to the virtual environment for this module

    if [ "$offlineInstall" = true ]; then 
        writeLine "Offline Installation: Unable to download and install Python." $color_error
        return 6 # unable to download required asset
    fi

    # M1 macs are trouble for python
    if [ "$platform" = "macos-arm64" ]; then
        writeLine "Arm64 (Apple silicon) Mac detected. Not running under Rosetta. " $color_warn
        if [ $(/usr/bin/pgrep oahd >/dev/null 2>&1; echo $?) -gt 0 ]; then
        #if [ "$(pkgutil --files com.apple.pkg.RosettaUpdateAuto)" = "" ]; then 
            writeLine 'Rosetta is not installed' $color_error
            needRosettaAndiBrew
        else
            writeLine 'Rosetta is, however, available. We can continue.' $color_success
        fi
    fi

    if [ "${forceOverwrite}" = true ]; then

        writeLine "Cleaning Python directory to force re-install of Python VENV" $color_info
        writeLine "This will mean any previous PIP installs will be lost." $color_error

        # Force overwrite
        if [ -d "${pythonDirPath}" ]; then 
            rm -rf "${pythonDirPath}"
        fi
    fi

    # =========================================================================
    # 1. Install Python. Using deadsnakes for Linux (not macOS), so be aware if
    #    you have concerns about potential late adoption of security patches.

    if [ $verbosity = "loud" ]; then
        writeLine "Python install path is ${pythonDirPath}" $color_info
    fi

    if [ ! -d "${pythonDirPath}" ]; then
        mkdir -p "${pythonDirPath}"
    fi

    # We need to ensure the requested version Python is installed somewhere so
    # we can use it to create virtual environments. In Windows we install Python
    # in the /runtimes folder. In Linux/macOS we install Python directly into
    # the standard system folders. 

    # basePythonCmdPath is the path to the "base" python interpreter which will
    # then be used to create virtual environments. We will test if the given 
    # version of python is installed on the system, and if not we'll install it.
    basePythonCmdPath="python${pythonVersion}"

    if command -v "$basePythonCmdPath" > /dev/null; then

        # All good - we have python
        writeLine "Python ${pythonVersion} is already installed" $color_success

    else

        # output a warning message if no admin rights and instruct user on manual steps
        if [ "$setupMode" = 'SetupEverything' ]; then 
            install_instructions="cd ${setupScriptDirPath}${newline}sudo bash setup.sh"
        else
            install_instructions="cd ${moduleDirPath}${newline}sudo bash ../../setup.sh"
        fi
        checkForAdminAndWarn "$install_instructions"

        # For macOS we'll use brew to install python
        if [ "$os" = "macos" ]; then

            # We first need to ensure GCC is installed. 
            write "Checking for GCC and xcode tools..." $color_primary

            xcodetoolsVersion=$(getXcodeToolsVersion)
            if [ $? -ne 0 ] || [ $((xcodetoolsVersion)) -lt 2396 ]; then
                writeLine "Requesting install." $color_info
                xcode-select --install
            else
                writeLine "present" $color_success
            fi

            write "Installing Python ${pythonVersion} application and libraries..." $color_primary

            if [ "$platform" = "macos-arm64" ]; then

                # Apple silicon requires Rosetta2 for python to run for Python < 3.9.1,
                # so use the x86 version of Brew we installed earlier
                if [ $(versionCompare "${pythonVersion}" '3.9.1') = "-1" ]; then
                    if [ "${verbosity}" = "quiet" ]; then
                        arch -x86_64 /usr/local/bin/brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                        spin $!
                    else
                        arch -x86_64 /usr/local/bin/brew install python@${pythonVersion}
                    fi
                else
                    if [ "${verbosity}" = "quiet" ]; then
                        brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                        spin $!
                    else
                        brew install python@${pythonVersion}
                    fi
                fi

            else

                # We have a x64 version of python for macOS (Intel) in our S3 bucket
                # but it's easier to simply install python natively

                # Download "${storageUrl}runtimes/" "python3.7.12-osx64.tar.gz" \
                #          "${platform}/${pythonName}" "Downloading Python interpreter..."
                # cp -R "${downloadDirPath}/${platform}/${pythonName}" "${runtimesDirPath}/bin/${platform}"

                if [ "${verbosity}" = "quiet" ]; then
                    brew install python@${pythonVersion}  >/dev/null 2>/dev/null &
                    spin $!
                else
                    brew install python@${pythonVersion}
                fi

            fi

            # Note that we only need the system-wide location of the python 
            # interpreter to setup the virtual environment. After it's setup,
            # all python calls are made using the venv's python
            if ! command -v "$basePythonCmdPath" > /dev/null; then
                basePythonCmdPath="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"
            fi 

            writeLine "done" $color_success

        # macOS: With my M1 chip and Rosetta I make installing Python a real PITA.
        # Raspberry Pi: Hold my beer 
        elif [ "${edgeDevice}" = "Raspberry Pi" ] || [ "${edgeDevice}" = "Orange Pi" ] || \
             [ "${edgeDevice}" = "Radxa ROCK"   ] || [ "${edgeDevice}" = "Jetson"    ] || \
             [ "$os_name" = "debian" ]; then

            # ensure gcc is installed
            if [ "$os_name" == "debian" ]; then 
                # gcc and make
                installAptPackages "build-essential make"
                # to build python on Debian
                installAptPackages "openssl-dev libssl-dev libncurses5-dev libsqlite3-dev libreadline-dev libtk8.6 libgdm-dev libdb4o-cil-dev libpcap-dev"
            fi

            if [ "$launchedBy" = "server" ]; then
                writeLine "Installing Python needs to be done manually" $color_error
                writeLine "Please run this command in a terminal:" $color_error
                writeLine ""
                writeLine "   cd ${moduleDirPath} && sudo bash ../../setup.sh" $color_error
                writeLine ""
                writeLine "then restart CodeProject.AI Server" $color_error
                quit
            else
                if [ "$os_name" = "debian" ]; then
                    writeLine "Installing Python. THIS COULD TAKE 10-15 mins" "white" "red" 50
                else
                    writeLine "Installing Python. THIS COULD TAKE AN HOUR" "white" "red" 50
                fi
            fi

            pushd "${appRootDirPath}" > /dev/null

            # Update at your leisure. 
            # See https://www.python.org/ftp/python/ for a complete list.
            case "${pythonVersion}" in
                "3.0")  pythonPatchVersion="3.0.1";;
                "3.1")  pythonPatchVersion="3.1.5";;
                "3.2")  pythonPatchVersion="3.2.6";;
                "3.3")  pythonPatchVersion="3.3.7";;
                "3.4")  pythonPatchVersion="3.4.19";; 
                "3.5")  pythonPatchVersion="3.5.10";;
                "3.6")  pythonPatchVersion="3.6.15";;
                "3.7")  pythonPatchVersion="3.7.17";;
                "3.8")  pythonPatchVersion="3.8.18";;
                "3.9")  pythonPatchVersion="3.9.18";; 
                "3.10") pythonPatchVersion="3.10.13";; 
                "3.11") pythonPatchVersion="3.11.5";; 
                "3.12") pythonPatchVersion="3.12.0";; 
                *)      pythonPatchVersion="${pythonVersion}.0"
            esac

            # install the pre-requisites
            checkForAdminAndWarn "sudo apt-get update -y && sudo apt-get upgrade -y"
            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                sudo apt-get update -y >/dev/null && sudo apt-get upgrade -y >/dev/null
            fi

            # Build tools
            installAptPackages "dist-upgrade build-essential checkinstall python3-dev python-setuptools"
            installAptPackages "python3-pip libncurses5-dev libgdbm-dev libc6-dev"
            installAptPackages "zlib1g-dev libssl-dev openssl libffi-dev libncursesw5-dev"

            # TODO: Need info on why these are needed
            installAptPackages "libreadline6-dev libbz2-dev libexpat1-dev liblzma-dev"
            installAptPackages "python-smbus tk-dev libsqlite3-dev libdb5.3-dev "

            # Download, build and Install SSL
            # https://www.aliengen.com/blog/install-python-3-7-on-a-raspberry-pi-with-raspbian-8

            # Download
            cd $downloadDir 
            mkdir --parents "${os}/Lib" >/dev/null
            cd "${os}/Lib"

            if [ ! -f "openssl-1.1.1c.tar.gz" ]; then
                curl $curlFlags --remote-name https://www.openssl.org/source/openssl-1.1.1c.tar.gz
                # If using wget
                # wget $wgetFlags https://www.openssl.org/source/openssl-1.1.1c.tar.gz
            fi
            if [ ! -d "openssl-1.1.1c" ]; then
                tar -xf openssl-1.1.1c.tar.gz >/dev/null
            fi

            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                cd openssl-1.1.1c/
                # Build SSL
                sudo ./config shared --prefix=/usr/local/
                sudo make -j $(nproc) >/dev/null

                # Install
                sudo make install >/dev/null
                sudo apt-get install libssl-dev -y >/dev/null

                # cleanup
                cd ..
                sudo rm -rf openssl-1.1.1c
            fi
            
            # Get the Python tar ball and extract into our downloads dir
            mkdir "${pythonName}" >/dev/null
            cd "${pythonName}" >/dev/null

            if [ ! -f "Python-${pythonPatchVersion}.tar.xz" ]; then
                # curl https://www.python.org/ftp/python/${pythonPatchVersion}/Python-${pythonPatchVersion}.tar.xz | tar -xf
                curl $curlFlags --remote-name https://www.python.org/ftp/python/${pythonPatchVersion}/Python-${pythonPatchVersion}.tar.xz
                # If using wget
                # wget $wgetFlags https://www.python.org/ftp/python/${pythonPatchVersion}/Python-${pythonPatchVersion}.tar.xz
            fi

            if [ ! -d "Python-${pythonPatchVersion}" ]; then
                tar -xf Python-${pythonPatchVersion}.tar.xz
            fi

            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then

                # Build and install Python
                cd Python-${pythonPatchVersion}

                if [ "$os_name" == "debian" ]; then 
                    # Native debian is giving us troubles. The instructions should be optimised down
                    # to just what's needed, but for now we'll just throw everything at the problem
                    # until we find a solution to the "SSLError("Can't connect to HTTPS URL because 
                    # the SSL module is not available.")' issue
                    sudo apt-get install libssl-dev libncurses5-dev libsqlite3-dev libreadline-dev libtk8.6 libgdm-dev libdb4o-cil-dev libpcap-dev
                    sudo ./configure --enable-optimizations 
                    make
                    sudo make install
                else
                    sudo ./configure --enable-optimizations  --prefix=/usr >/dev/null
                    if [ "$nproc" = "" ]; then nproc=1; fi
                    make -j $(nproc) > /dev/null 
                    sudo make -j $(nproc) altinstall >/dev/null
                fi

                cd ..

                # Cleanup
                sudo rm -rf Python-${pythonPatchVersion}
            fi

            #. ~/.bashrc

            popd > /dev/null

            # lsb_release is too short-sighted to handle multiple python packages
            # Modified from https://stackoverflow.com/a/61605955
            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                sudo ln -s /usr/share/pyshared/lsb_release.py /usr/lib/python${pythonPatchVersion}/site-packages/lsb_release.py
            fi

            # This was done above, but let's force it here in case it's been changed since
            basePythonCmdPath="python${pythonVersion}"

            # And sometimes Pip just isn't here. So...
            curl --remote-name https://bootstrap.pypa.io/get-pip.py
            # If using wget
            # wget https://bootstrap.pypa.io/get-pip.py

            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
                sudo "${basePythonCmdPath}" get-pip.py pip==20.3.4
            fi
            rm get-pip.py

        # For Linux we'll use apt-get the deadsnakes PPA to get the old version
        # of python. Deadsnakes? Old python? Get it? Get it?! And who said 
        # developers have no sense of humour.
        else

            # https://askubuntu.com/a/1481830
            # if [ "$os_name" = "debian" ]; then
            #     This allows adding the deadsnakes PPA, but this ppa doesn't support debian
            #     sudo apt-get install python3-launchpadlib -y
            # fi

            if [ "${verbosity}" = "loud" ]; then
            
                writeLine "Updating apt-get" $color_info
                sudo apt-get update -y 
                
                writeLine "Installing software-properties-common" $color_info
                sudo apt-get install software-properties-common -y
                
                writeLine "Adding deadsnakes as a Python install source (PPA)" $color_info
                apt policy | grep deadsnakes/ppa > /dev/null
                if [ "$?" != "0" ]; then
                    sudo add-apt-repository ppa:deadsnakes/ppa -y
                else
                    writeLine "(Already added)" $color_success
                fi

                writeLine "Updating and upgrading apt-get" $color_info
                sudo apt-get update -y && sudo apt-get upgrade -y
                
                write "Installing Python ${pythonVersion} library..." $color_primary
                sudo apt-get install python${pythonVersion} -y
                writeLine "done" $color_success

            elif [ "${verbosity}" = "info" ]; then           

                write "Updating apt-get " $color_info
                sudo apt-get update -y >/dev/null &
                spin $!
                writeLine "done" $color_success

                write "Installing software-properties-common " $color_info
                sudo apt-get install software-properties-common -y >/dev/null &
                spin $!
                writeLine "done" $color_success

                write "Adding deadsnakes as a Python install source (PPA) " $color_info
                apt policy | grep deadsnakes/ppa > /dev/null
                if [ "$?" != "0" ]; then
                    sudo add-apt-repository ppa:deadsnakes/ppa -y >/dev/null &
                    spin $!
                    writeLine "done" $color_success
                else
                    writeLine "Already added" $color_success
                fi

                write "Updating apt-get " $color_info
                sudo apt-get update -y >/dev/null &
                spin $!
                writeLine "done" $color_success

                write "Upgrading apt " $color_info
                sudo apt-get upgrade  -y >/dev/null  &
                spin $!
                writeLine "done" $color_success

                write "Installing Python Library ${pythonVersion} " $color_info
                sudo apt-get install python${pythonVersion} -y >/dev/null  &
                spin $!
                writeLine "done" $color_success

            else

                write "Installing Python Library ${pythonVersion}..." $color_info

                sudo apt-get update -y >/dev/null 2>/dev/null &
                spin $!

                sudo apt-get install software-properties-common -y >/dev/null 2>/dev/null &
                spin $!

                apt policy | grep deadsnakes/ppa >/dev/null 2>/dev/null
                if [ "$?" != "0" ]; then
                    sudo add-apt-repository ppa:deadsnakes/ppa -y >/dev/null 2>/dev/null &
                    spin $!
                fi

                sudo apt-get update -y >/dev/null 2>/dev/null &
                spin $!

                sudo apt-get upgrade  -y >/dev/null 2>/dev/null &
                spin $!

                sudo apt-get install python${pythonVersion} -y >/dev/null 2>/dev/null &
                spin $!

                writeLine "done" $color_success

            fi
        fi

        if ! command -v "$basePythonCmdPath" > /dev/null; then
            return 2 # failed to install required runtime
        fi
    fi

    # Check permissions again. This check was done if python wasn't found, but 
    # was NOT done if python was found. Do it again just to be safe
    if [ "$setupMode" = 'SetupEverything' ]; then 
        install_instructions="cd ${setupScriptDirPath}${newline}sudo bash setup.sh"
    else
        install_instructions="cd ${moduleDirPath}${newline}sudo bash ../../setup.sh"
    fi

    # In WSL, getting: ModuleNotFoundError: No module named 'distutils.cmd'
    if [ "$os" = "linux" ]; then
        checkForAdminAndWarn "$install_instructions"

        write "Ensuring PIP in base python install..." $color_primary
        if [ "${verbosity}" = "quiet" ]; then
            sudo apt-get install --reinstall python${pythonVersion}-distutils -y >/dev/null 2>/dev/null  &
            spin $! # process ID of the python install call
            "$basePythonCmdPath" -m ensurepip >/dev/null 2>/dev/null  &
            spin $! # process ID of the python install call
        else
            sudo apt-get install --reinstall python${pythonVersion}-distutils -y
            "$basePythonCmdPath" -m ensurepip
        fi
        writeLine 'done' $color_success
    fi

    write "Upgrading PIP in base python install..." $color_primary

    #if [ "$os_name" != "debian" ]; then
    #    curl https://bootstrap.pypa.io/get-pip.py
    #    sudo "${basePythonCmdPath}" get-pip.py
    #else
        trustedHosts="--trusted-host pypi.org --trusted-host pypi.python.org --trusted-host files.pythonhosted.org" 
        if [ "${verbosity}" = "quiet" ]; then
            "${basePythonCmdPath}" -m pip install ${trustedHosts} --upgrade pip >/dev/null 2>/dev/null  &
            spin $! # process ID of the python install call
        else
            "${basePythonCmdPath}" -m pip install ${trustedHosts} --upgrade pip
        fi
    #fi
    writeLine 'done' $color_success

    # =========================================================================
    # 2. Create Virtual Environment

    if [ -d  "${virtualEnvDirPath}" ]; then
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

        if [ "$os" = "macos" ]; then
            if [ "${verbosity}" = "quiet" ]; then
                write "Installing Virtual Environment tools for macOS..." $color_primary
                "${basePythonCmdPath}" -m pip $pipFlags install setuptools virtualenv virtualenvwrapper >/dev/null 2>/dev/null &
                spin $!
                writeLine "done" $color_success
            else
                writeLine "Installing Virtual Environment tools for macOS..." $color_primary
                "${basePythonCmdPath}" -m pip $pipFlags install setuptools virtualenv virtualenvwrapper

                # regarding the warning: See https://github.com/Homebrew/homebrew-core/issues/76621
                if [ $(versionCompare "${pythonVersion}" '3.10.2') = "-1" ]; then
                    writeLine "Ignore the DEPRECATION warning above. See https://github.com/Homebrew/homebrew-core/issues/76621 for details" $color_info
                fi
            fi
        else
            writeLine 'Installing Virtual Environment tools for Linux...' $color_primary
            installAptPackages "python3-pip python3-setuptools python${pythonVersion}-venv"
        fi

        # Create the virtual environments. All sorts of things can go wrong here
        # but if you have issues, make sure you delete the venv directory before
        # retrying.
        write "Creating Virtual Environment (${runtimeLocation})..." $color_primary
        
        if [ $verbosity = "loud" ]; then
            writeLine "Install path is ${pythonDirPath}"
        fi

        if [ $verbosity = "quiet" ]; then
            "$basePythonCmdPath" -m venv "${virtualEnvDirPath}" >/dev/null &
            spin $! # process ID of the python install call
        else            
            "$basePythonCmdPath" -m venv "${virtualEnvDirPath}"
        fi

        if [ ! -d "${virtualEnvDirPath}" ]; then
            writeLine ""
            writeLine "Virtual Environment was not created" $color_error
            return 5 # unable to create Python virtual environment
        fi

        writeLine "done" $color_success
    fi

    # Ensure Python Exists in the venv
    write "Checking for Python ${pythonVersion}..." $color_primary
    pyVersion=$("$venvPythonCmdPath" --version)
    write "(Found ${pyVersion}) " $color_mute

    echo $pyVersion | grep "${pythonVersion}" >/dev/null
    if [ $? -ne 0 ]; then
        errorNoPython
    fi 
    writeLine 'All good' $color_success

    write "Upgrading PIP in virtual environment..." $color_primary
    "${venvPythonCmdPath}" -m pip remove --upgrade pip >/dev/null 2>/dev/null &

    "${venvPythonCmdPath}" -m pip install --upgrade pip >/dev/null 2>/dev/null &
    spin $!
    writeLine 'done' $color_success

    #hack
    if [ "$os" = "linux" ]; then
        write 'Installing updated setuptools in venv...' $color_primary
        "$venvPythonCmdPath" -m pip install -U setuptools >/dev/null 2>/dev/null &
        spin $!
        writeLine "done" $color_success
    fi

    return 0
}

function installPythonPackagesByName () {

    # A number of global variables are assumed here
    #  - pythonVersion     - version in X.Y format
    #  - venvPythonCmdPath - the path to the python interpreter for this venv
    #  - virtualEnvDirPath - the path to the virtual environment for this module
    #  - packagesDirPath   - site-packages location for this module

    # List of packages to install separate by spaces
    packages=$1
    # Description to use when describing the packages. Can be null.
    packages_desc=$2 
    # Options to specify for the pip command (eg --index-url ...). Can be null.
    pip_options=$3 

    if [ "$offlineInstall" = true ]; then 
        writeLine "Offline Installation: Unable to download and install Python packages." $color_error
        return 6 # unable to download required asset
    fi

    # For speeding up debugging
    if [ "${skipPipInstall}" = true ]; then return; fi

    if ! command -v "${venvPythonCmdPath}" > /dev/null; then
        writeLine "Virtual Environment was not created successfully." $color_error
        writeLine "You may need to run 'sudo dpkg --configure -a' to correct apt-get errors" $color_warn
        return 1
    fi

    # NOTE: This code will never be run because we bail before we get here if isn't working.
    if [ "$os" = "macos" ]; then
        # Running "PythonX.Y" should work, but may not. Check, and if it doesn't work then set the
        # venvPythonCmdPath var to point to the absolute pathe where we think the python launcher
        # should be
        "$venvPythonCmdPath" --version >/dev/null  2>/dev/null
        if [ $? -ne 0 ]; then
            writeLine "Setting python command to point to global install location" $color_warn
            venvPythonCmdPath="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"
        fi
    fi

    # Before installing packages, check to ensure PIP is installed and up to 
    # date. This slows things down a bit, but it's worth it in the end.
    if [ "${verbosity}" = "quiet" ]; then

        if [ "$os" = "linux" ]; then
            installAptPackages "python3-pip"
        fi

        # Ensure we have the current python compatible version of pip
        write 'Ensuring PIP compatibility...' $color_primary
        "$venvPythonCmdPath" -m ensurepip >/dev/null 2>/dev/null &
        spin $!
        writeLine 'done' $color_success

    else
        
        writeLine 'Ensuring PIP is installed and up to date...' $color_primary
    
        if [ "$os" = "linux" ]; then
            installAptPackages "python3-pip"
        fi

        writeLine 'Ensuring PIP compatibility ...' $color_primary
        "$venvPythonCmdPath" -m ensurepip
    fi 

    # =========================================================================
    # Install PIP package

    pushd "${virtualEnvDirPath}/bin" >/dev/null

    for package_name in $packages
    do
        # If the module specifier isn't a URL or .whl then extract the module's name
        module_name=""
        if [ "${module:0:4}" != "http" ] && [ "${package_name:(-4)}" != ".whl" ]; then
            module_name=$(echo "$package_name" | sed 's/[<=>,~].*//g')
            # Now remove any string from [ onwards to get 'module' from module[variant]
            module_name=${module_name%%[*}
        fi

        package_desc=$packages_desc
        if [ "$package_desc" = "" ]; then package_desc="$module_name";  fi
        if [ "$package_desc" = "" ]; then package_desc="$package_name"; fi

        write "Installing ${package_desc}..." $color_primary

        # Check if the module name is already installed
        module_installed=false

        if [ "${module_name}" != "" ]; then
            if [ "${verbosity}" != "quiet" ]; then
                write "Processing ${module_name}..." $color_info
            fi
            "$venvPythonCmdPath" -m pip show ${module_name} >/dev/null 2>/dev/null
            if [ $? -eq 0 ]; then module_installed=true; fi
        fi

        if [ "$module_installed" = false ]; then

            if [ "${verbosity}" != "quiet" ]; then
                write "Installing ${package_name}..." $color_info
            fi
        
            # echo "[DEBUG] '${venvPythonCmdPath}' -m pip install ${pipFlags} '${package_name}' --target '${packagesDirPath}' ${pip_options}" 
            if [ "${os}" = "linux" ] || [ "${os}" = "macos" ]; then
                if [ "${verbosity}" = "loud" ]; then
                    eval "$venvPythonCmdPath" -m pip install "${package_name}" --target "${packagesDirPath}" ${pip_options} ${pipFlags} 
                else
                    eval "$venvPythonCmdPath" -m pip install "${package_name}" --target "${packagesDirPath}" ${pip_options} ${pipFlags} >/dev/null  2>/dev/null &
                    spin $!
                fi
            else
                if [ "${verbosity}" = "loud" ]; then
                    "$venvPythonCmdPath" -m pip install "${package_name}" --target "${packagesDirPath}" ${pip_options} ${pipFlags} 
                else
                    "$venvPythonCmdPath" -m pip install "${package_name}" --target "${packagesDirPath}" ${pip_options} ${pipFlags} >/dev/null  2>/dev/null &
                    spin $!
                fi
            fi

            # Get the return value of the install op
            status=$?

            # Now check if it actually worked
            if [ "${module_name}" != "" ]; then

                "$venvPythonCmdPath" -m pip show "${module_name}" >/dev/null  2>/dev/null
                if [ $? -eq 0 ]; then
                    write "(✅ checked) " $color_info
                else
                    write "(❌ failed check) " $color_error
                fi
            else
                write "(not checked) " $color_mute
            fi

            if [ $status -eq 0 ]; then
                writeLine "done" $color_success
            else
                writeLine "Failed" $color_error
            fi
        else
            writeLine "Already installed" $color_success
        fi
    done

    popd  >/dev/null

    return 0
}

function installRequiredPythonPackages () {

    # A number of global variables are assumed here
    #  - pythonVersion     - version in X.Y format
    #  - venvPythonCmdPath - the path to the python interpreter for this venv
    #  - virtualEnvDirPath - the path to the virtual environment for this module
    #  - packagesDirPath   - site-packages location for this module

    if [ "$offlineInstall" = true ]; then 
        writeLine "Offline Installation: Unable to download and install Python packages." $color_error
        return 6 # unable to download required asset
    fi

    # For speeding up debugging
    if [ "${skipPipInstall}" = true ]; then return; fi

    if ! command -v "${venvPythonCmdPath}" > /dev/null; then
        writeLine "Virtual Environment was not created successfully." $color_error
        writeLine "You may need to run 'sudo dpkg --configure -a' to correct apt-get errors" $color_warn
        return 1
    fi

    # NOTE: This code will never run because we bail before we get here.
    if [ "$os" = "macos" ]; then
        # Running "PythonX.Y" should work, but may not. Check, and if it doesn't work then set the
        # venvPythonCmdPath var to point to the absolute pather where we think the python launcher should be
        "$venvPythonCmdPath" --version >/dev/null  2>/dev/null
        if [ $? -ne 0 ]; then
            writeLine "Setting python command to point to global install location" $color_warn
            venvPythonCmdPath="/usr/local/opt/python@${pythonVersion}/bin/python${pythonVersion}"
        fi
    fi

    # ==========================================================================
    # Install pre-requisites

    # Before installing packages, check to ensure PIP is installed and up to 
    # date. This slows things down a bit, but it's worth it in the end.
    if [ "${verbosity}" = "quiet" ]; then

        if [ "$os" = "linux" ]; then
            installAptPackages "python3-pip"
        fi

        # Ensure we have the current python compatible version of pip
        write 'Ensuring PIP compatibility...' $color_primary
        "$venvPythonCmdPath" -m ensurepip >/dev/null 2>/dev/null &
        spin $!
        writeLine 'done' $color_success

    else
        
        writeLine 'Ensuring PIP is installed and up to date...' $color_primary
    
        if [ "$os" = "linux" ]; then
            installAptPackages "python3-pip"
        fi

        writeLine 'Ensuring PIP compatibility ...' $color_primary
        "$venvPythonCmdPath" -m ensurepip
    fi 

    # =========================================================================
    # Install PIP packages

    pushd "${virtualEnvDirPath}/bin" >/dev/null

    # Getting the correct requirements file ------------------------------------

    requirementsDir=$1
    if [ "${requirementsDir}" = "" ]; then requirementsDir=$moduleDirPath; fi

    # This is getting complicated. The order of priority for the requirements file is:
    #
    #  requirements.device.txt                            (device = "raspberrypi", "orangepi", "radxarock" or "jetson" )
    #  requirements.os.architecture.cudaMajor_Minor.txt   (eg cuda12_0)
    #  requirements.os.architecture.cudaMajor.txt         (eg cuda12)
    #  requirements.os.architecture.(cuda|rocm).txt
    #  requirements.os.cudaMajor_Minor.txt
    #  requirements.os.cudaMajor.txt
    #  requirements.os.(cuda|rocm).txt
    #  requirements.cudaMajor_Minor.txt
    #  requirements.cudaMajor.txt
    #  requirements.(cuda|rocm).txt
    #  requirements.os.architecture.gpu.txt
    #  requirements.os.gpu.txt
    #  requirements.gpu.txt
    #  requirements.os.architecture.txt
    #  requirements.os.txt
    #  requirements.txt
    #
    # The logic here is that we go from most specific to least specific. The only
    # real tricky bit is the subtlety around .cuda vs .gpu. CUDA / ROCm are specific
    # types of card. We may not be able to support that, but may be able to support
    # other cards generically via OpenVINO or DirectML. So CUDA or ROCm first,
    # then GPU, then CPU. With a query at each step for OS and architecture.

    requirementsFilename=""

    # NOTE: edgeDevice would be "Raspberry Pi" (or blank if non-edge) and platform would be "raspberrypi"
    if [ "$edgeDevice" != "" ] && [ -f "${requirementsDir}/requirements.${platform}.txt" ]; then
        requirementsFilename="requirements.${platform}.txt"
    fi

    if [ "$requirementsFilename" = "" ]; then
        if [ "$installGPU" = "true" ]; then

            if [ "$cuda_version" != "" ]; then

                cuda_major_version=${cuda_version%%.*}
                cuda_major_minor=$(echo "$cuda_version" | sed 's/\./_/g')
                
                if [ "${verbosity}" != "quiet" ]; then
                    writeLine "CUDA version is $cuda_version (${cuda_major_minor} / ${cuda_major_version})" $color_info
                fi

                if [ -f "${requirementsDir}/requirements.${os}.${architecture}.cuda${cuda_major_minor}.txt" ]; then
                        requirementsFilename="requirements.${os}.${architecture}.cuda${cuda_major_minor}.txt"
                elif [ -f "${requirementsDir}/requirements.${os}.${architecture}.cuda${cuda_major_version}.cuda.txt" ]; then
                        requirementsFilename="requirements.${os}.${architecture}.cuda${cuda_major_version}.txt"
                elif [ -f "${requirementsDir}/requirements.${os}.${architecture}.cuda.txt" ]; then
                        requirementsFilename="requirements.${os}.${architecture}.cuda.txt"
                elif [ -f "${requirementsDir}/requirements.${os}.cuda${cuda_major_minor}.txt" ]; then
                        requirementsFilename="requirements.${os}.cuda${cuda_major_minor}.txt"
                elif [ -f "${requirementsDir}/requirements.${os}.cuda${cuda_major_version}.txt" ]; then
                        requirementsFilename="requirements.${os}.cuda${cuda_major_version}.txt"
                elif [ -f "${requirementsDir}/requirements.${os}.cuda.txt" ]; then
                        requirementsFilename="requirements.${os}.cuda.txt"
                elif [ -f "${requirementsDir}/requirements.cuda${cuda_major_minor}.txt" ]; then
                        requirementsFilename="requirements.cuda${cuda_major_minor}.txt"
                elif [ -f "${requirementsDir}/requirements.cuda${cuda_major_version}.txt" ]; then
                        requirementsFilename="requirements.cuda${cuda_major_version}.txt"
                elif [ -f "${requirementsDir}/requirements.cuda.txt" ]; then
                        requirementsFilename="requirements.cuda.txt"
                fi
            fi

            if [ "$hasROCm" = true ]; then
                if [ -f "${requirementsDir}/requirements.${os}.${architecture}.rocm.txt" ]; then
                    requirementsFilename="requirements.${os}.${architecture}.rocm.txt"
                elif [ -f "${requirementsDir}/requirements.${os}.rocm.txt" ]; then
                    requirementsFilename="requirements.${os}.rocm.txt"
                elif [ -f "${requirementsDir}/requirements.rocm.txt" ]; then
                    requirementsFilename="requirements.rocm.txt"
                fi
            fi

            if [ "$hasMPS" = true ]; then
                if [ -f "${requirementsDir}/requirements.mps.txt" ]; then
                    requirementsFilename="requirements.mps.txt"
                fi
            fi

            if [ "$requirementsFilename" = "" ]; then
                if [ -f "${requirementsDir}/requirements.${os}.${architecture}.gpu.txt" ]; then
                    requirementsFilename="requirements.${os}.${architecture}.gpu.txt"
                elif [ -f "${requirementsDir}/requirements.${os}.gpu.txt" ]; then
                    requirementsFilename="requirements.${os}.gpu.txt"
                elif [ -f "${requirementsDir}/requirements.gpu.txt" ]; then
                    requirementsFilename="requirements.gpu.txt"
                fi
            fi
        fi
    fi

    if [ "$requirementsFilename" = "" ]; then
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

    if [ "$requirementsFilename" = "" ]; then
        writeLine "No suitable requirements.txt file found." $color_warn
        return
    fi

    if [ ! -f "$requirementsPath" ]; then
        writeLine "Can't find ${requirementsPath} file." $color_warn
        return
    fi
    # --------------------------------------------------------------------------

    writeLine "Python packages will be specified by ${requirementsFilename}" $color_info

    if [ "${oneStepPIP}" = true ]; then

        # Install the Python Packages in one fell swoop. Not much feedback, but it works
        # writeLine "${venvPythonCmdPath} -m pip install $pipFlags -r ${requirementsPath} --target ${packagesDirPath}" $color_info
        write 'Installing Packages into Virtual Environment...' $color_primary
        if [ "${verbosity}" = "loud" ]; then
            "$venvPythonCmdPath" -m pip install $pipFlags -r "${requirementsPath}" --target "${packagesDirPath}"
        else
            "$venvPythonCmdPath" -m pip install $pipFlags -r "${requirementsPath}" --target "${packagesDirPath}" > /dev/null &
            spin $!
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

            if [ "${line}" = "" ]; then
                currentOption=''
            elif [ "${line:0:1}" = "#" ]; then
                currentOption=''
            elif [ "${line:0:1}" = "-" ]; then
                currentOption="${currentOption} ${line}"
            else
        
                package_name="${line}"
                description=''

                # breakup line into package name and description
                IFS='#'; tokens=($package_name); IFS=$'\n';

                if [ ${#tokens[*]} -gt 1 ]; then
                    package_name="${tokens[0]}"
                    description="${tokens[1]}"
                fi

                if [ "${description}" = "" ]; then
                    description="Installing ${package_name}"
                fi
    
                # remove all whitespaces
                package_name=$(trim "$package_name")
                # echo "Package = [${package_name}]"

                if [ "${package_name}" != "" ]; then

                    write "  -${description}..." $color_primary

                    # If the module specifier isn't a URL or .whl then extract the package's name
                    module_name=""
                    if [ "${package_name:0:4}" != "http" ] && [ "${package_name:(-4)}" != ".whl" ]; then
                        module_name=$(echo "$package_name" | sed 's/[<=>,~].*//g')
                        # Now remove any string from [ onwards to get 'module' from module[variant]
                        module_name=${module_name%%[*}
                    fi

                    # Check if the module name is already installed
                    module_installed=false
                    if [ "${module_name}" != "" ]; then
                        if [ "${verbosity}" != "quiet" ]; then write "Checking ..." $color_info; fi
                        # echo "[DEBUG] ${venvPythonCmdPath} -m pip show ${module_name}"
                        "${venvPythonCmdPath}" -m pip show ${module_name} >/dev/null 2>/dev/null
                        if [ $? -eq 0 ]; then module_installed=true; fi
                        if [ "${verbosity}" != "quiet" ]; then write "Check done..." $color_info; fi
                    fi

                    if [ "$module_installed" = false ]; then
                       
                        if [ "${verbosity}" != "quiet" ]; then write "Installing ${package_name}..." $color_info; fi

                        # echo "[DEBUG] '${venvPythonCmdPath}' -m pip install '${package_name}' --target '${packagesDirPath}' ${currentOption} ${pipFlags}"
                        if [ "${os}" = "linux" ] || [ "${os}" = "macos" ]; then
                            # No, I don't know why eval is needed in Linux but not elsewhere
                            if [ "${verbosity}" = "loud" ]; then
                                eval "\${venvPythonCmdPath}" -m pip install "${package_name}" --target "\${packagesDirPath}" ${currentOption} ${pipFlags}
                            else
                                eval "\${venvPythonCmdPath}" -m pip install "${package_name}" --target "\${packagesDirPath}" ${currentOption} ${pipFlags} >/dev/null 2>/dev/null &
                                spin $!
                            fi
                        else
                            if [ "${verbosity}" = "loud" ]; then
                                "${venvPythonCmdPath}" -m pip install "${package_name}" --target "${packagesDirPath}" ${currentOption} ${pipFlags}
                            else
                                "${venvPythonCmdPath}" -m pip install "${package_name}" --target "${packagesDirPath}" ${currentOption} ${pipFlags} >/dev/null 2>/dev/null &
                                spin $!
                            fi
                        fi

                        # Get the return value of the install op
                        status=$?

                        # If the module's name isn't simply a URL or .whl then actually check it worked
                        if [ "${module_name}" != "" ]; then
                            "$venvPythonCmdPath" -m pip show ${module_name} >/dev/null  2>/dev/null
                            if [ $? -eq 0 ]; then
                                write "(✅ checked) " $color_info
                            else
                                write "(❌ failed check) " $color_error
                            fi
                        else
                            write "(not checked) " $color_mute
                        fi

                        if [ $status -eq 0 ]; then
                            writeLine "done" $color_success
                        else
                            writeLine "Failed" $color_error
                        fi
                    else
                        writeLine "Already installed" $color_success
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

function installAptPackages () {

    # If you get a 'Could not get lock' error, then do this:
    #
    # Step 1: stop the processes locking dpkg
    #   sudo fuser -vki -TERM /var/lib/dpkg/lock /var/lib/dpkg/lock-frontend
    # Step 2: clean up the mess
    #   sudo dpkg --configure --pending

    local packageList=$1
    local options=$2

    pkgs_to_install=""
    if [ "${verbosity}" = "quiet" ]; then
        write "Searching for ${packageList:0:40}..."
    else
        writeLine "Searching for installed dependencies:"
        write " -> ${packageList}"
    fi

    for pkg in $packageList; do
        apt list "${pkg}" 2>/dev/null | grep installed >/dev/null 2>/dev/null
        if [ "$?" != "0" ]; then
            pkgs_to_install+=" ${pkg}"
        fi
    done

    if [ "${verbosity}" != "quiet" ]; then
        writeLine " done" $color_success
    fi

    if [[ -n "${pkgs_to_install}" ]]; then
    
        checkForAdminRights
        if [ "$isAdmin" = false ]; then
             writeLine "=================================================================" $color_info
             writeLine "This script does not have sufficient permissions. Please run:"     $color_info
             writeLine ""
             writeLine "  sudo apt-get update -y && sudo apt-get install -y ${options} ${pkgs_to_install}" $color_info
             writeLine ""
             writeLine "to complete this installation"                                     $color_info
             writeLine "=================================================================" $color_info

            if [ "$attemptSudoWithoutAdminRights" = true ]; then
                writeLine "We will attempt to run admin-only install commands. You may be prompted" "White" "Red"
                writeLine "for an admin password. If not then please run the script shown above."   "White" "Red"
            fi
        fi

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
            if [ "${verbosity}" = "quiet" ]; then
                write "installing..."
                sudo apt-get update -y -qq >/dev/null 2>/dev/null  &
                spin $!
                sudo apt-get install -y -qq ${options} ${pkgs_to_install} >/dev/null 2>/dev/null &
                spin $!
                writeLine " done" $color_success
            else
                writeLine "Installing missing dependencies:"
                writeLine " -> ${pkgs_to_install}"

                if [ "${verbosity}" = "loud" ]; then
                    sudo apt-get update -y && apt-get install -y --no-install-recommends ${options} ${pkgs_to_install} 
                else
                    sudo apt-get update -y 2>/dev/null && apt-get install -y --no-install-recommends ${options} ${pkgs_to_install} >/dev/null 
                fi
            fi
        fi

    else
        if [ "${verbosity}" = "quiet" ]; then
            writeLine "All good." $color_success
        else
            writeLine "All dependencies already installed." $color_success
        fi
    fi

    # Reset TTY. apt update can leave the console in a bad state
    stty sane > /dev/null 2>&1
}

function downloadModels () {
    # ASSUMPTION: moduleId and moduleDirPath are set

    write "Scanning modulesettings for downloadable models..."

    foundModels=false

    index=0
    while [ true ]; do
        modelName=$(getValueFromModuleSettingsFile "$moduleDirPath" "$moduleId" "InstallOptions.DownloadableModels[${index}].Name")
        if [ "$modelName" = "" ]; then break; fi
    
        preinstall=$(getValueFromModuleSettingsFile "$moduleDirPath" "$moduleId" "InstallOptions.DownloadableModels[${index}].PreInstall")
        if [ "$preinstall" = true ]; then

            if [ "$foundModels" = false ]; then writeLine "Processing model list"; fi
            foundModels=true

            modelFileName=$(getValueFromModuleSettingsFile   "$moduleDirPath" "$moduleId" "InstallOptions.DownloadableModels[${index}].Filename")
            modelFolderName=$(getValueFromModuleSettingsFile "$moduleDirPath" "$moduleId" "InstallOptions.DownloadableModels[${index}].Folder")
            
            getFromServer "models/" "$modelFileName" "$modelFolderName" "Downloading ${modelName}..."
        fi

        let index=index+1 
    done

    if [ "$foundModels" = false ]; then writeLine "No models specified" $color_mute; fi
}

function getFromServer () {

    # This method downloads a zip file from our S3 storage, stores in the downloads
    # folder within a subfolder specific to the current module, then expands the
    # zip and copies the contents over to module itself. The zip that was downloaded
    # will be saved in order to cache downloads

    # Name of the folder in which to look for this file on S3 eg "models/"
    local folder=$1

    # Name of the file to get eg packages_for_gpu.zip
    local fileToGet=$2

    # Folder within the current Module dir where this zip should be expanded. eg assets
    local moduleAssetsDirName=$3

    # output message
    local message=$4

    # Clean up directories to force a re-copy if necessary
    if [ "${forceOverwrite}" = true ]; then
        # if [ $verbosity -ne "quiet" ]; then echo "Forcing overwrite"; fi

        rm -rf "${downloadDirPath}/${modulesDir}/${moduleDirName}/${fileToGet}"
        rm -rf "${moduleDirPath}/${moduleAssetsDirName}"
    fi

    # Download !$storageUrl$folder$fileToGet to $downloadDirPath and extract into $downloadDirPath/${modulesDir}/$moduleDirName/$moduleAssetsDirName
    # Params are: S3 storage bucket | fileToGet     | zip lives in...      | zip expanded to moduleDir/... | message
    # eg               "S3_bucket/folder"    "rembg-models.zip"    /downloads/myModuleDir/"        "assets"             "Downloading models..."
    downloadAndExtract "${storageUrl}${folder}" "$fileToGet" "${downloadDirPath}/${modulesDir}/${moduleDirName}" "${moduleAssetsDirName}" "${message}"

    # Copy downloadDirPath/modules/moduleDirName/moduleAssetsDirName folder to modules/moduleDirName/moduleAssetsDirName
    if [ -d "${downloadDirPath}/${modulesDir}/${moduleDirName}/${moduleAssetsDirName}" ]; then

        if [ ! -d "${moduleDirPath}/${moduleAssetsDirName}" ]; then
            mkdir -p "${moduleDirPath}/${moduleAssetsDirName}"
        fi;

        # pushd then cp to stop "cannot stat" error
        pushd "${downloadDirPath}/${modulesDir}/${moduleDirName}" >/dev/null

        write "Moving contents of ${fileToGet} to ${moduleAssetsDirName}..."
        # mv -f "${downloadDirPath}/${modulesDir}/${moduleDirName}/${moduleAssetsDirName}/*" "${moduleDirPath}/${moduleAssetsDirName}/"
        # rsync --remove-source-files "${downloadDirPath}/${modulesDir}/${moduleDirName}/${moduleAssetsDirName}" "${moduleDirPath}/${moduleAssetsDirName}/"
        move_recursive "${downloadDirPath}/${modulesDir}/${moduleDirName}/${moduleAssetsDirName}" "${moduleDirPath}/${moduleAssetsDirName}"
        rm -rf "${downloadDirPath}/${modulesDir}/${moduleDirName}/${moduleAssetsDirName}"

        if [ "$?" != "0" ]; then
            writeLine "Failed." $color_error
            return 11 # Unable to copy file or directory
        fi

        writeLine "done." $color_success

        popd >/dev/null 
    else
        return 6 # unable to download required asset
    fi

    return 0
}

move_recursive() {
    if [ ! -d "$1" ] || [ ! -e "$2" ]; then
        mv -f "$1" "$2" || echo "Unable to move $1 to $2"
        return
    fi
    for entry in "$1/"* "$1/."[!.]* "$1/.."?*; do
        if [ -e "$entry" ]; then
            move_recursive "$entry" "$2/${entry##"$1/"}"
        fi
    done
}

function downloadAndExtract () {

    local storageUrl=$1
    local fileToGet=$2
    local downloadToDir=$3
    local dirToExtract=$4
    local message=$5

    # eg.
    # storageUrl    = 'https://codeproject-ai.s3.ca-central-1.amazonaws.com/server/assets/models'
    # fileToGet     = packages_for_gpu.zip
    # downloadToDir = 'downloads/moduleId'
    # dirToExtract  = assets (relative to downloadToDir)
   
    if [ "${fileToGet}" = "" ]; then
        writeLine 'No download file was specified' $color_error
        return 9 # required parameter not supplied
    fi

    if [ "${message}" = "" ]; then message="Downloading ${fileToGet}..."; fi

    if [ $verbosity != "quiet" ]; then 
        writeLine "Downloading ${fileToGet} to ${downloadToDir}" $color_info
        writeLine "Extracting to ${dirToExtract} in this folder" $color_info
    fi
    
    write "$message" $color_primary

    extension="${fileToGet:(-3)}"
    if [ ! "${extension}" = ".gz" ]; then
        extension="${fileToGet:(-4)}"
        if [ ! "${extension}" = ".zip" ]; then
            writeLine "Unknown and unsupported file type for file ${fileToGet}" $color_error
            return 12 - parameter value invalid
        fi
    fi

    if [ -f "${downloadToDir}/${fileToGet}" ]; then     # To check for the download itself
        write " already exists..." $color_info
    else

        if [ "$offlineInstall" = true ]; then 
            writeLine "Offline Installation: Unable to download ${fileToGet}." $color_error
            return 6  # unable to download required asset
        fi

        # create folder if needed and ensure permissions set
        if [ ! -d "${downloadToDir}/${dirToExtract}" ]; then
            mkdir -p "${downloadToDir}/${dirToExtract}"
            chmod -R a+w "${downloadToDir}/${dirToExtract}"
        fi

        wget $wgetFlags -P "${downloadToDir}" "${storageUrl}${fileToGet}"
        status=$?    
        if [ $status -ne 0 ]; then
            writeLine "The wget command failed for file ${fileToGet}." $color_error
            return 6 # unable to download required asset
        fi
    fi

    if [ ! -f "${downloadToDir}/${fileToGet}" ]; then
        writeLine "The downloaded file '${fileToGet}' doesn't appear to exist." $color_error
        return 6 # unable to download required asset
    fi

    write 'Expanding...' $color_info

    pushd "${downloadToDir}" >/dev/null
    
    if [ $verbosity = "quiet" ]; then 
        if [ "${extension}" = ".gz" ]; then
            tar $tarFlags "${fileToGet}" --directory ${dirToExtract} >/dev/null &  # execute and continue
            spin $! # process ID of the unzip/tar call
        else
            unzip $unzipFlags "${fileToGet}" -d ${dirToExtract} >/dev/null &  # execute and continue
            spin $! # process ID of the unzip/tar call
        fi
    else
        if [ "${extension}" = ".gz" ]; then
            tar $tarFlags "${fileToGet}" --directory ${dirToExtract} 
        else
            unzip $unzipFlags "${fileToGet}" -d ${dirToExtract} 
        fi
    fi
    
    if [ ! "$(ls -A .)" ]; then # Is the download dir empty?
        writeLine "Unable to extract download. Can you please check you have write permission to "${dirToExtract}"." $color_error
        popd >/dev/null
        quit 7  # unable to expand compressed archivep
    fi
    
    # Remove thw downloaded zip
    # rm -f "${fileToGet}" >/dev/null

    popd >/dev/null

    writeLine 'done.' $color_success

    return 0
}


# TO BE DONE
# function getRequirementsFile () {
# }


function getCudaVersion () { 

    if [ "${edgeDevice}" = "Jetson" ]; then
        cuda_version="${JETSON_CUDA}"
        if [ "$cuda_version" = "" ]; then
            # Contains something like "CUDA Version 10.2.300"We'll just grab the '10.2'
            cuda_version=$( cat /usr/local/cuda/version.txt | grep -o 'CUDA Version [0-9]*\.[0-9]*' | cut  -d ' ' -f 3 )
        fi
    else
        if command -v nvcc >/dev/null; then
       
            # Search for the line containing "release" to extract the CUDA version
            cuda_version_line=$(nvcc --version | grep -i "release")
            if [[ ${cuda_version_line} == *'release'* ]]; then 
                # example from nvcc:
                #   nvcc: NVIDIA (R) Cuda compiler driver
                #   Copyright (c) 2005-2021 NVIDIA Corporation
                #   Built on Thu_Nov_18_09:45:30_PST_2021
                #   Cuda compilation tools, release 11.5, V11.5.119
                #   Build cuda_11.5.r11.5/compiler.30672275_0
                cuda_version=$(echo "$cuda_version_line" | cut -d ' ' -f 5 | cut -d ',' -f 1)
            fi

        elif command -v /usr/local/cuda/bin/nvcc >/dev/null; then
       
            # Search for the line containing "release" to extract the CUDA version
            cuda_version_line=$(/usr/local/cuda/bin/nvcc --version | grep -i "release")
            if [[ ${cuda_version_line} == *'release'* ]]; then 
                # example from nvcc:
                #   nvcc: NVIDIA (R) Cuda compiler driver
                #   Copyright (c) 2005-2021 NVIDIA Corporation
                #   Built on Thu_Nov_18_09:45:30_PST_2021
                #   Cuda compilation tools, release 11.5, V11.5.119
                #   Build cuda_11.5.r11.5/compiler.30672275_0
                cuda_version=$(echo "$cuda_version_line" | cut -d ' ' -f 5 | cut -d ',' -f 1)
            fi
            
        elif command -v nvidia-smi >/dev/null; then

            cuda_version_line=$(nvidia-smi | grep -i -E 'CUDA Version: [0-9]+.[0-9]+') > /dev/null 2>&1
            if [[ ${cuda_version_line} == *'CUDA Version: '* ]]; then 
                # "| NVIDIA-SMI 510.39.01    Driver Version: 510.39.01    CUDA Version: 11.4     |"
                # -> " 11.4     |"
                # -> "11.4"
                cuda_version=$(echo "$cuda_version_line" | cut -d ':' -f 3 | cut -d ' ' -f 2)
            fi

        fi
    fi

    echo $cuda_version
}

function getcuDNNVersion () { 

    cuDNN_version=$(dpkg -l 2>/dev/null | grep cudnn | head -n 1 | grep -oP '\d+\.\d+\.\d+')
    echo $cuDNN_version
}

# Gets a value from the correct modulesettings.json file based on the current OS
# and architecture, based purely on the name of the propery. THIS METHOD DOES NOT
# TAKE INTO ACCOUNT THE DEPTH OF A PROPERTY. If the property is at the root level
# or 10 levels down, it's all the same. The extraction is done purely by grep/sed,
# so is very niaive. 
function getValueFromModuleSettingsFile () {

    local moduleDirPath=$1
    local moduleId=$2
    local property=$3

    if [ "$verbosity" = "loud" ] && [ "$debug_json_parse" == "true" ]; then
       echo "Searching for '${property}' in a suitable modulesettings.json file in ${moduleDirPath}" >&3
    fi

    # Module settings files are loaded in this order. Each file will overwrite (but not delete)
    # settings of the previous file. Becuase of this, we're going to search the files in REVERSE
    # order until we find the first value based on the most specific to least specific file.
    #   modulesettings.json
    #   modulesettings.development.json 
    #   modulesettings.os.json
    #   modulesettings.os.development.json
    #   modulesettings.os.architecture.json
    #   modulesettings.os.architecture.development.json
    #   (not searched) modulesettings.docker.json
    #   (not searched) modulesettings.docker.development.json
    #   modulesettings.device.json (device = raspberrypi, orangepi, radxarock, jetson)
    #   modulesettings.device.development.json
    # So we need to check each modulesettings file in reverse order until we find a value for 'key'
    
    dev_specifier=""
    if [ "$executionEnvironment" = "Development" ]; then dev_specifier="development"; fi

    settings_file_used=""

    if [ "$edgeDevice" != "" ] && [ "$dev_specifier" != "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.${edgeDevice}.${dev_specifier}.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.${edgeDevice}.${dev_specifier}.json"; fi
    fi
    if [ "$edgeDevice" != "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.${edgeDevice}.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.${edgeDevice}.json"; fi
    fi
    if [ "${moduleSettingValue}" = "" ] && [ "$dev_specifier" != "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.${os}.${architecture}.${dev_specifier}.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.${os}.${architecture}.${dev_specifier}.json"; fi
    fi
    if [ "${moduleSettingValue}" = "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.${os}.${architecture}.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.${os}.${architecture}.json"; fi
    fi
    if [ "${moduleSettingValue}" = "" ] && [ "$dev_specifier" != "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.${os}.${dev_specifier}.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.${os}.${dev_specifier}.json"; fi
    fi
    if [ "${moduleSettingValue}" = "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.${os}.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.${os}.json"; fi
    fi
    if [ "${moduleSettingValue}" = "" ] && [ "$dev_specifier" != "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.${dev_specifier}.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.${dev_specifier}.json"; fi
    fi
    if [ "${moduleSettingValue}" = "" ]; then
        moduleSettingValue=$(getValueFromModuleSettings "${moduleDirPath}/modulesettings.json" "${moduleId}" "${property}")
        if [ "${moduleSettingValue}" != "" ]; then settings_file_used="modulesettings.json"; fi
    fi

    if [ "$verbosity" = "loud" ] && [ "$debug_json_parse" == "true" ]; then
       if [ "${moduleSettingValue}" = "" ]; then
           echo "Cannot find ${moduleId}.${property} in modulesettings in ${moduleDirPath}" >&3
       else
           echo "${moduleId}.${property} is ${moduleSettingValue//[$'\r\n']/ } in ${settings_file_used}" >&3
       fi
    fi

    echo $moduleSettingValue
}

# Gets a value from the modulesettings.json file (any JSON file, really) based
# purely on the name of the propery. 
# If use_jq=true then key can be whatever the jq command accepts, with the 
# caveat that a "." is prefixed to "key" when doing the search (so don't add a
# "." to key yourself).
# if use_jq=false then the extraction is done purely by grep/sed, so is very
# niaive, and does NOT take into account the depth of a property. If the
# property is at the root level or 10 levels down, it's all the same. 
function getValueFromModuleSettings () { 

    local json_file=$1
    local moduleId=$2
    local property=$3

    # escape '-'s
    if [[ $property == *-* ]]; then property="\"${property}\""; fi
    if [[ $moduleId == *-* ]]; then moduleId="\"${moduleId}\""; fi

    # Correct path
    json_file=${json_file//\\//}
    
    # RANT: Douglas Crockford decided that people were abusing the comment 
    # syntax in JSON and so he removed it. Devs immediately added hack work-
    # arounds which nullified his 'fix' and instead has left us with a crippled
    # data format that has wasted countless developer hours. Explaining one's
    # work so the next person can maintain it is critical. J in JSON stands for
    # Javascript. The Industry needs to stop honouring a pointless short-sighted
    # decision and standardise Javascript-style comments in JSON. 

    # parse_mode can be 'jq' for the jq utility, 'parsejson' for our .NET utility, or
    # 'sed', which is what one would use if they have given up all hope for 
    # humanity. jq is solid but doesn't do comments. See above. ParseJSON does
    # some comments but not all, so not helpful enough for the overhead. So we'll
    # use jq and strip comments. What a waste of time.
    if [ "${useJq}" = true ]; then
        parse_mode='jq' 
        key=".Modules.${moduleId}.${property}"
    else
        parse_mode='parsejson' 
        key=$".Modules.${moduleId}.${property}"
    fi

    if [ "$verbosity" = "loud" ] && [ "$debug_json_parse" == "true" ]; then
        echo jsonFile is $json_file >&3
        echo parse_mode is $parse_mode >&3
    fi

    if [ -f "$json_file" ]; then
        if [ "$parse_mode" = "jq" ]; then

            # jq can't deal with comments so let's strip comments first.
            
            # remove single line comments from the file text
            # file_contents=$(cat "$json_file" | sed -e 's|//[^"]*||')

            # We run perl in "slurp" mode (-0777), treating the entire file as a single string. This 
            # allows multiline processing. The regex looks for two patterns:
            # "(?:\\. | [^"\\])*" matches a double-quoted string, allowing for escaped characters 
            # within the string.
            # //[^\n]* matches // and any following characters until the end of the line.
            # The replacement part {$1} ensures that only the first pattern (double-quoted strings) 
            # is kept, while occurrences of // outside quotes are removed.
            # g = all occurrences, e = replacement is an expression, x = ignore whitespace in pattern

            # Remove single line comments
            file_contents=$(perl -0777 -pe 's{ ( " (?: \\. | [^"\\] )* " ) | // [^\n]* }{$1}gex' "$json_file")
            # echo "file_contents = $file_contents" >&3

            # Remove /* ...*/ multiline comments. (split to make debug easier)
            file_contents=$(echo "$file_contents" | perl -0777 -pe 's#/\*.*?\*/|##gs')
            # echo "file_contents = $file_contents" >&3

            # Do the extraction
            jsonValue=$(echo "$file_contents" | jq -r "$key")
            
        elif [ "$parse_mode" = "parsejson" ]; then

            # Let's back in this huge earth mover to plant my flower pots. Even
            # more fun: System.Text.Json.JsonSerializer.Deserialize will handle
            # comments when deserialising a strongly typed object, but (at least
            # in Linux) it can't deal with "//" comments when deserialising to
            # a JsonNode object. See also:  monumental waste of dev's time.

            # remove single line comments from the file text
            file_contents=$(cat "$json_file" | sed -e 's|//[^"]*||')
            jsonValue=$(echo "${file_contents}" | dotnet "${sdkPath}/Utilities/ParseJSON/ParseJSON.dll" "$key")

        else # I have given up all hope. I will use regex and abandon myself to the fates. May God have mercy on my soul

            # 1. look for "name" : "text" (with text having optional quotes)
            # 2. pipe the result to sed and replace the entire string with whatever is
            #    after the '"name" : ' part. So '"name" : "value" // comment' becomes
            #    '"value" // comment'
            # 3. Trim "// ..." off the string if present 
            # 4. remove quotes
            jsonValue=$( grep -o "\"${key}\"\s*:\s*[^,}]*" "$json_file" | sed 's/.*: "\{0,1\}\(.*\)"\{0,1\}.*/\1/' | sed 's#//.*##' | tr -d '"' )

            # if [ "$verbosity" = "loud" ]; then grep -o "\"${key}\"\s*:\s*[^,}]*" "$json_file" >&3; fi
        fi
    else
        jsonValue=""
    fi

    # Really?? 
    if [ "$jsonValue" == "null" ]; then jsonValue=""; fi

    if [ "$verbosity" = "loud" ] && [ "$debug_json_parse" == "true" ]; then
        echo "${key} = $jsonValue" >&3;
    fi
    
    echo $jsonValue
}

# Gets the module ID value from the given modulesettings.json file.
# See comments from above method.
function getModuleIdFromModuleSettings () { 

    local json_file=$1
    json_file=${json_file//\\//}

    if [ "${useJq}" = true ]; then
        parse_mode='jq' 
        key=".Modules | keys[0]"
    else
        parse_mode='parsejson' 
        key=$".Modules.#keys[0]"
    fi
  
    if [ -f "$json_file" ]; then
        if [ "$parse_mode" = "jq" ]; then

            # jq can't deal with comments so let's strip singleline comments then multiline.
            file_contents=$(perl -0777 -pe 's{ ( " (?: \\. | [^"\\] )* " ) | // [^\n]* }{$1}gex' "$json_file")
            file_contents=$(echo "$file_contents" | perl -0777 -pe 's#/\*.*?\*/|##gs')
            jsonValue=$(echo "$file_contents" | jq -r "$key")

        else # [ "$parse_mode" = "parsejson" ]; then

            # Strip comments
            file_contents=$(cat "$json_file" | sed -e 's|//[^"]*||')
            jsonValue=$(echo "${file_contents}" | dotnet "${sdkPath}/Utilities/ParseJSON/ParseJSON.dll" "$key")
        fi
    else
        jsonValue=""
    fi

    # Really?? A literal "null"?
    if [ "$jsonValue" == "null" ]; then jsonValue=""; fi

    # debug
    # if [ "$verbosity" = "loud" ]; then echo "${key} = $jsonValue" >&3; fi

    echo $jsonValue
}


# Call this, then test: if [ $online -eq 0 ]; then echo 'online'; fi
function checkForInternet () {
    nc -w 2 -z 8.8.8.8 53  >/dev/null 2>&1
    online=$?
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

function trim() {
    local var="$*"
    # remove leading whitespace characters
    var="${var#"${var%%[![:space:]]*}"}"
    # remove trailing whitespace characters
    var="${var%"${var##*[![:space:]]}"}"
    printf '%s' "$var"
}

# Converts bytes value to human-readable string based on 1024 units (binary) [$1: bytes value]
function bytesToHumanReadableBinary() {
    local i=${1:-0} d="" s=0 S=("Bytes" "KiB" "MiB" "GiB" "TiB" "PiB" "EiB" "YiB" "ZiB")
    while ((i > 1024 && s < ${#S[@]}-1)); do
        printf -v d ".%02d" $((i % 1024 * 100 / 1024))
        i=$((i / 1024))
        s=$((s + 1))
    done
    echo "$i$d ${S[$s]}"
}

# Converts bytes value to human-readable string base on 1000 units [$1: bytes value]
function bytesToHumanReadableKilo() {

    local i=${1:-0} d="" s=0 S=("KiB" "MiB" "GiB" "TiB" "PiB" "EiB" "YiB" "ZiB")

    if ((i < 1024)); then
        echo "$i Bytes"
    else
        i=$((i / 1024))
        while ((i > 1000 && s < ${#S[@]}-1)); do
            printf -v d ".%02d" $((i % 100 * 100 / 1000))
            i=$((i / 1000))
            s=$((s + 1))
        done
        echo "$i$d ${S[$s]}"
    fi
}

function getDisplaySize () {
    # See https://linuxcommand.org/lc3_adv_tput.php some great tips around this
    echo "Rows=$(tput lines) Cols=$(tput cols)"
}

haveDisplayedMacOSDirCreatePermissionError=false
function displayMacOSDirCreatePermissionError () {

    if [[ $OSTYPE == 'darwin'* ]] && [ "$haveDisplayedMacOSDirCreatePermissionError" = false ]; then

        haveDisplayedMacOSDirCreatePermissionError=true

        writeLine ''
        writeLine ''
        writeLine 'We may be able to suggest something:'  $color_info

        if [ "$os_name" = "Sonoma" ]; then   # macOS 14 / Kernal 23
            # Note that  will appear as the Apple symbol on macOS, but probably not on Windows or Linux
            writeLine '1. Pull down the  Apple menu and choose "System Settings"'
            writeLine '2. Choose “Privacy & Security"'
            writeLine '3. Scroll down to “Full Disk Access” and click the right arrow >'
            writeLine '4. Click the [+] plus button, and in the popup, navigate to the'
            writeLine '   /Applications/Utilities/ folder and choose "Terminal"'
            writeLine '5. Relaunch Terminal. The “Operation not permitted” error messages should'
            writeLine '   be gone'
        else
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
        fi
        
        writeLine ''
    fi

    # quit 8 # unable to create file or directory
}

function needRosettaAndiBrew () {

    writeLine
    writeLine "You're on a Mac running Apple silicon but Python3 only works on Intel."  $color_error

    if [ "$offlineInstall" = true ]; then 
        writeLine "You will need to install Rosetta2 to continue, once you are back online." $color_error
        return 6 # unable to download required asset
    fi

    writeLine "You will need to install Rosetta2 to continue."  $color_error
    writeLine
    
    read -p 'Install Rosetta2 (Y/N)?' installRosetta
    if [ "${installRosetta}" = "y" ] || [ "${installRosetta}" = "Y" ]; then
        /usr/sbin/softwareupdate --install-rosetta --agree-to-license
    else
        quit 4 # required tool missing, needs installing
    fi

    writeLine "Then you need to install brew under Rosetta (We'll alias it as ibrew)"
    read -p 'Install brew for x86 (Y/N)?' installiBrew
    if [ "${installiBrew}" = "y" ] || [ "${installiBrew}" = "Y" ]; then
        arch -x86_64 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)"
    else
        quit 4 # required tool missing, needs installing
    fi
}

function extract_dpkg_version() {
    echo $(dpkg -l | grep $1 | awk '{print $3}')
}

# =================================================================================================

# SETUP

# A NOTE ON PLATFORM.
# We use the full x86_64 for architecture, but follow the common convention of
# abbreviating this to x64 when used in conjuntion with OS (ie platform). So 
# macOS-x64 rather than macOS-x86_64. To simplify further, if the platform value
# doesn't have a suffix then it's assumed to be -x64. This may change in the future.

if [ $(uname -m) = 'arm64' ] || [ $(uname -m) = 'aarch64' ]; then
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
    edgeDevice=''

    if [ "$architecture" = 'arm64' ]; then platform='macos-arm64'; fi
else
    os='linux'
    edgeDevice=''
    platform='linux'
    os_name=$(. /etc/os-release;echo $ID) # eg "ubuntu", "debian12"
    os_vers=$(. /etc/os-release;echo $VERSION_ID) # eg "22.04" for Ubuntu 22.04, "12" for Debian 12

    if [ "$architecture" = 'arm64' ]; then platform='linux-arm64'; fi

    modelInfo=""
    if [ -f "/sys/firmware/devicetree/base/model" ]; then
        modelInfo=$(tr -d '\0' </sys/firmware/devicetree/base/model) >/dev/null 2>&1
    fi

    if [[ "${modelInfo}" == *"Raspberry Pi"* ]]; then       # elif [ $(uname -n) = "raspberrypi" ]; then
        systemName='Raspberry Pi'
        edgeDevice='Raspberry Pi'
        platform='raspberrypi'
    elif [[ "${modelInfo}" == *"Orange Pi"* ]]; then        # elif [ $(uname -n) = "orangepi5" ]; then
        systemName='Orange Pi'
        edgeDevice='Orange Pi'
        platform='orangepi'
    elif [[ "${modelInfo}" == *"Radxa ROCK"* ]]; then
        systemName='Radxa ROCK'
        edgeDevice='Radxa ROCK'
        platform='radxarock'        
    elif [[ "${modelInfo}" == *"NVIDIA Jetson"* ]]; then    # elif [ $(uname -n) = "nano" ]; then
        systemName='Jetson'
        edgeDevice='Jetson'
        platform='jetson'
        # Get the good stuff
        source "${sdkScriptsDirPath}/jetson_libraries.sh"
        source "${sdkScriptsDirPath}/jetson_variables.sh"
    elif [ "$inDocker" = true ]; then 
        systemName='Docker'
    elif [[ $(uname -a) =~ microsoft-standard-WSL ]]; then
        systemName='WSL'
    else
        systemName=$os
    fi
fi

# See if we can spot if it's a dark or light background
darkmode=false
if [ "$os" = "macos" ]; then
    interfaceStyle=$(defaults read -g AppleInterfaceStyle 2>/dev/null)
    if [ $? -eq 0 ]; then
        if [ "${interfaceStyle}" = "Dark" ]; then
            darkmode=true
        fi
    else
        termBg=$(osascript -e \
            'tell application "Terminal" 
               get background color of selected tab of window 1
            end tell' 2>/dev/null)

        if [[ $termBg ]]; then
            IFS=','; colors=($termBg); IFS=' ';
            if [ ${colors[0]} -lt 2000 ] && [ ${colors[1]} -lt 2000 ] && [ ${colors[2]} -lt 2000 ]; then
                darkmode=true
            else
                darkmode=false
            fi
        fi
    fi
else
    darkmode=true
    terminalBg=$(gsettings get org.gnome.desktop.background primary-color 2>/dev/null)

    if [ "${terminalBg}" != "no schemas installed" ] && [ "${terminalBg}" != "" ]; then
        terminalBg="${terminalBg%\'}"                               # remove first '
        terminalBg="${terminalBg#\'}"                               # remove last '
        terminalBg=`echo $terminalBg | tr '[:lower:]' '[:upper:]'`  # uppercase-ing

        if [[ $terminalBg =~ ^\#[0-9A-F]{6}$ ]]; then   # if it's of the form #xxxxxx hex colour

            a=`echo $terminalBg | cut -c2-3`
            b=`echo $terminalBg | cut -c4-5`
            c=`echo $terminalBg | cut -c6-7`

            # convert from hex to decimal
            # checkForTool bc
            # if command -v bc > /dev/null; then
            # r=`echo "ibase=16; $a" | bc`
            # g=`echo "ibase=16; $b" | bc`
            # b=`echo "ibase=16; $c" | bc`
            # else
            r=`echo $((16#${a}))`
            g=`echo $((16#${b}))`
            b=`echo $((16#${c}))`
            # fi

            # calculate luminosity
            # luma=$(echo "(0.2126 * $r) + (0.7152 * $g) + (0.0722 * $b)" | bc)

            # Whatever warped version of Ubuntu WSL has, it doesn't like decimal points
            # luma=`$(( (0.2126 * ${r}) + (0.7152 * ${g}) + (0.0722 * ${b}) ))`
            luma=$(( ( (21 * ${r}) + (72 * ${g}) + (7 * ${b}) ) / 100 ))

            # remove everything after the decimal point
            luma=${luma%.*}
            
            if (( luma > 127 )); then 
                darkmode=false
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
if [ "$darkmode" = true ]; then
    color_primary='White'
    color_mute='Gray'
    color_info='DarkMagenta'
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
if [ "$TERM_PROGRAM" = "vscode" ]; then color_primary='Default'; fi


# Outputs the version of the currently installed xcode tools. It's placed at the bottom because
# this command completely screws up the colourisation of the rest of the script in Visual Studio.
function getXcodeToolsVersion() {
    xcode-select -v | sed 's/[^0-9]*//g'
}