#!/bin/bash

# :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
# CodeProject.AI Server Utilities
#
# Utilities for use with Linux/macOS Development Environment install scripts
# 
# TODO: Break this script up
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
# This code is echoed to the terminal before outputting text in
# order to generate a colored output.
#
# string foreground color name. Optional if no background provided.
#        Defaults to "Default" which uses the system default
# string background color name.  Optional. Defaults to "Default"
#        which is the system default
# string intense. Optional. If "true" then the intensity is turned up
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
        dir_error=$?

        if [ $dir_error = 1 ]; then 
            if [ "${verbosity}" = "loud" ]; then
                sudo mkdir -p "${path}"
            else
                sudo mkdir -p "${path}" >/dev/null 2>/dev/null
            fi
            dir_error=$?
        fi 

        if [ $dir_error = 0 ]; then 
            writeLine "done" $color_success
        else
            writeLine "Needs admin permission to create folder (error $dir_error)" $color_error
            displayMacOSDirCreatePermissionError
        fi
    fi

    if [ -d "${path}" ]; then
        checkForAdminRights
        if [ "$isAdmin" = true ]; then
            write "Setting permissions on ${desc} folder..." $color_primary 
            sudo chmod a+w "${path}" >/dev/null 2>/dev/null
            dir_error=$?
            if [ $dir_error = 0 ]; then 
                writeLine "done" $color_success
            else
                writeLine "Needs admin permission to set folder permissions (error $dir_error)" $color_error
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
        if [ "$os" = "macos" ]; then
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
            if [ "$architecture" = 'arm64' ]; then
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

                # We sometimes see errors when trying to install tools. Typically 
                # this has only been in docker, so we'll restrict this fix. 
                if [ "${systemName}" = "Docker" ]; then
                    sudo apt-get clean
                    sudo apt-get install -f -y
                    sudo dpkg --configure -a
                fi                
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
    if [ "$os" = "linux" ] && [ "$architecture" = "x86_64" ]; then

        if [ ! -f /usr/lib/x86_64-linux-gnu/libssl.so.1.1 ] || [ ! -e /usr/lib/libcrypto.so.1.1 ]; then

            # output a warning message if no admin rights and instruct user on manual steps
            install_instructions="cd ${setupScriptDirPath}${newline}sudo bash setup.sh"
            checkForAdminAndWarn "$install_instructions"

            if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then

                moduleInstallErrors=""

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

        # IFS=$'\n' # set the Internal Field Separator as end of line
        # while read -r line
        # do
        #     dotnet_version=$(echo "${line}" | cut -d ' ' -f 1)
        #     current_comparison=$(versionCompare $dotnet_version $highestDotNetVersion)
        # 
        #     if (( $current_comparison > $comparison )); then
        #         highestDotNetVersion="$dotnet_version"
        #         comparison=$current_comparison
        #     fi
        # done <<< "$(dotnet --list-sdks)"
        # unset IFS

    else    # runtimes

        # example output from 'dotnet --list-runtimes'
        # Microsoft.AspNetCore.App 7.0.11 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
        # Microsoft.NETCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

        comparison=-1

        if command -v dotnet >/dev/null 2>/dev/null; then

            IFS=$'\n' # set the Internal Field Separator as end of line
            while read -r line
            do
                if [[ $line == *'Microsoft.NETCore.App '* ]]; then
                    dotnet_version=$(echo "${line}" | cut -d ' ' -f 2)
                    # echo "GET: Found .NET runtime $dotnet_version" >&3

                    current_comparison=$(versionCompare $dotnet_version $highestDotNetVersion)
                    # echo "GET: current compare ${comparison}, new compare ${current_comparison}" >&3

                    if (( $current_comparison >= $comparison )); then
                        highestDotNetVersion="$dotnet_version"
                        comparison=$current_comparison
                        # echo "GET: Found new highest .NET runtime $highestDotNetVersion" >&3
                    # else
                    #    echo "GET: Found $dotnet_version runtime, which is not higher than $highestDotNetVersion" >&3
                    fi
                fi
            done <<< "$(dotnet --list-runtimes)"
            unset IFS
        fi
    fi

    if [ "$highestDotNetVersion" = "0" ]; then highestDotNetVersion=""; fi

    echo "$highestDotNetVersion"
}

function getMajorDotNetVersion() {

    majorminor_dotnet=$(getDotNetVersion "$requestedType")
    dotnet_major_version=$(echo "$majorminor_dotnet}" | cut -d '.' -f 1)

    echo "$dotnet_major_version"
}

function setDotNetLocation () {

    local profile_location=$1
    local dotnet_path=$2

    if [ -f $location ]; then

        if grep -q "export DOTNET_ROOT=${profile_location}"; then
            write "Already added link to $profile_location"
        else
            write "Adding Link to $location"
            echo 'export DOTNET_ROOT=${dotnet_path}' >> $profile_location
            export DOTNET_ROOT=${dotnet_path}
        fi

        if [[ $PATH == *"${dotnet_path}"* ]]; then
            write "PATH contains location of .NET"
        else
            write "Adding location of .NET to PATH"
            echo "export PATH=${dotnet_path}${PATH:+:${PATH}}" >> $profile_location
            export PATH=${dotnet_path}${PATH:+:${PATH}}
        fi
    fi
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

    write "Checking for .NET ${requestedNetVersion} ${requestedType}..."

    highestDotNetVersion="(None)"
    comparison=-1
    haveRequested=false

    if [ "$requestedType" = "sdk" ]; then
        
        # example output from 'dotnet --list-sdks'
        # 7.0.119 [/usr/lib/dotnet/sdk]

        if command -v dotnet >/dev/null 2>/dev/null; then
            IFS=$'\n' # set the Internal Field Separator as end of line
            while read -r line
            do
                # echo "SET: Read line $line" >&3

                dotnet_version=$(echo "${line}" | cut -d ' ' -f 1)
                dotnet_major_version=$(echo "${dotnet_version}" | cut -d '.' -f 1)

                # echo "SET: Found .NET SDK $dotnet_version" >&3

                # Let's only compare major versions
                # current_comparison=$(versionCompare $dotnet_version $requestedNetVersion)
                current_comparison=$(versionCompare $dotnet_major_version $requestedNetMajorVersion)

                if (( $current_comparison >= $comparison )); then
                    highestDotNetVersion="$dotnet_version"
                    comparison=$current_comparison
                #     echo "SET: Found new highest .NET SDK $highestDotNetVersion" >&3
                # else
                #     echo "SET: Found $dotnet_version SDK, which is not higher than $requestedNetMajorVersion" >&3
                fi

                # We found the one we're after
                if (( $current_comparison == 0 )); then haveRequested=true; fi

            done <<< "$(dotnet --list-sdks)"
            unset IFS
        fi

    else    # runtimes

        # example output from 'dotnet --list-runtimes'
        # Microsoft.AspNetCore.App 7.0.11 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
        # Microsoft.NETCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

        if command -v dotnet >/dev/null 2>/dev/null; then
            IFS=$'\n' # set the Internal Field Separator as end of line
            while read -r line
            do
                if [[ $line == *'Microsoft.NETCore.App '* ]]; then

                    dotnet_version=$(echo "${line}" | cut -d ' ' -f 2)
                    dotnet_major_version=$(echo "${dotnet_version}" | cut -d '.' -f 1)
                    # echo "SET: Found .NET runtime $dotnet_version" >&3

                    # Let's only compare major versions
                    # current_comparison=$(versionCompare $dotnet_version $requestedNetVersion)
                    current_comparison=$(versionCompare $dotnet_major_version $requestedNetMajorVersion)
                    # echo "SET: current compare ${comparison}, new compare ${current_comparison}" >&3

                    if (( $current_comparison >= $comparison )); then
                        highestDotNetVersion="$dotnet_version"
                        comparison=$current_comparison

                    #     echo "SET: Found new highest .NET runtime $highestDotNetVersion" >&3
                    # else
                    #     echo "SET: Found $dotnet_version runtime, which is not higher than $requestedNetMajorVersion" >&3
                    fi

                    # We found the one we're after
                    if (( $current_comparison == 0 )); then haveRequested=true; fi

                fi
            done <<< "$(dotnet --list-runtimes)"
            unset IFS
        fi
    fi

    mustInstall="false"
    if [ "$haveRequested" = true ]; then
        writeLine "All good. .NET ${requestedType} is ${highestDotNetVersion}" $color_success
    elif (( $comparison == 0 )); then
        writeLine "All good. .NET ${requestedType} is ${highestDotNetVersion}" $color_success
    elif (( $comparison == -1 )); then 
        writeLine "Upgrading: .NET ${requestedType} is ${highestDotNetVersion}. Upgrading to ${requestedNetVersion}" $color_warn
        mustInstall=true
    else # (( $comparison == 1 )), meaning highestDotNetVersion > requestedNetVersion
        if [ "$requestedType" = "sdk" ]; then
            writeLine "Installing .NET ${requestedType} ${requestedNetVersion}" $color_warn
            mustInstall=true
        else
            writeLine "All good. .NET ${requestedType} is ${highestDotNetVersion}" $color_success
        fi
    fi 

    if [ "$mustInstall" = true ]; then 

        if [ "$offlineInstall" = true ]; then 
            writeLine "Offline Installation: Unable to download and install .NET." $color_error
            return 6 # unable to download required asset
        fi

        if [ "$os" = "linux" ]; then
            dotnet_basepath="/usr/lib/"
        else # macOS x64
            # dotnet_basepath="~/."
            # if [ "$architecture" = 'arm64' ]; then
            #     dotnet_basepath="/opt/"
            # else
                dotnet_basepath="/usr/local/share/"
            # fi
        fi
        dotnet_path="${dotnet_basepath}/dotnet/"

        useCustomDotNetInstallScript=false
        # No longer using the arm64 custom script: standard install script seems good enough now.
        # output a warning message if no admin rights and instruct user on manual steps
        # if [ "$architecture" = 'arm64' ]; then useCustomDotNetInstallScript=true; fi
        
        if [ "$useCustomDotNetInstallScript" = true ]; then
           install_instructions="sudo bash '${installScriptsDirPath}/dotnet-install-arm.sh' $requestedNetMajorMinorVersion $requestedType"
        else
            install_instructions="sudo bash '${installScriptsDirPath}/dotnet-install.sh' --install-dir '${dotnet_path}' --channel $requestedNetMajorMinorVersion --runtime $requestedType"
        fi
        checkForAdminAndWarn "$install_instructions"

        if [ "$isAdmin" = true ] || [ "$attemptSudoWithoutAdminRights" = true ]; then
            if [ "$os" = "linux" ]; then

                # Potentially not reliable. Only blessed by MS for Debian >= 12
                if [ "$os_name" = "debian" ] && [ ! "$os_vers" -lt "12" ]; then

                    if [ $verbosity = "quiet" ]; then
                        wget https://packages.microsoft.com/config/debian/${os_vers}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb >/dev/null
                        sudo dpkg -i packages-microsoft-prod.deb >/dev/null
                        rm packages-microsoft-prod.deb >/dev/null
                    else
                        wget https://packages.microsoft.com/config/debian/${os_vers}/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
                        sudo dpkg -i packages-microsoft-prod.deb
                        rm packages-microsoft-prod.deb
                    fi

                    if [ "$requestedType" = "sdk" ]; then
                        sudo apt-get update && sudo apt-get install -y dotnet-sdk-$requestedNetMajorMinorVersion >/dev/null
                    else
                        sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-$requestedNetMajorMinorVersion >/dev/null
                    fi

                # .NET 9 seems to settle down the "we do/we don't" that MS is doing with .NET 6,7 and 8. 
                elif [ "$requestedNetMajorVersion" = "9" ]; then   # ... && [ "$os_name" = "ubuntu" ]

                    # TODO: Change this to a " >= 24.10"
                    if [ "$os_name" = "ubuntu" ] && [ "$os_vers" = "24.10" ]; then 
                        if [ "$requestedType" = "sdk" ]; then
                            sudo apt-get update && sudo apt-get install -y dotnet-sdk-$requestedNetMajorMinorVersion >/dev/null
                        else
                            sudo apt-get update && sudo apt-get install -y aspnetcore-runtime-$requestedNetMajorMinorVersion >/dev/null
                        fi
                    else
                        # .NET 9 is still not fully released. But we know a guy who knows a guy...
                        # TODO: This also works for NET 6 & 7, Ubuntu 24.04, and .NET 9,  Ubuntu 22.04 & 24.04
                        sudo add-apt-repository ppa:dotnet/backports -y
                        sudo apt install "dotnet${requestedNetMajorVersion}" -y
                    fi

                # For everyone else, use The Script
                else

                    # Needed if we're installing .NET without an installer to help us
                    installAptPackages "ca-certificates libc6 libgcc-s1 libicu74 liblttng-ust1 libssl3 libstdc++6 libunwind8 zlib1g"

                    if [ "$useCustomDotNetInstallScript" = true ]; then
                        # installs in /opt/dotnet
                        if [ $verbosity = "quiet" ]; then
                            sudo bash "${installScriptsDirPath}/dotnet-install-arm.sh" "${requestedNetMajorVersion}.0" "$requestedType" "quiet"
                        else
                            sudo bash "${installScriptsDirPath}/dotnet-install-arm.sh" "${requestedNetMajorVersion}.0" "$requestedType"
                        fi               
                    else
                        if [ $verbosity = "quiet" ]; then
                            sudo bash "${installScriptsDirPath}/dotnet-install.sh" --install-dir "$dotnet_path" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType" "--quiet"
                        elif [ $verbosity = "loud" ]; then
                            sudo bash "${installScriptsDirPath}/dotnet-install.sh" --install-dir "$dotnet_path" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType" "--verbose"
                        else
                            sudo bash "${installScriptsDirPath}/dotnet-install.sh" --install-dir "$dotnet_path" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType"
                        fi
                    fi
                fi

            else
                # macOS
                
                # (Maybe) needed if we're installing .NET without an installer to help us
                # installAptPackages "ca-certificates libc6 libgcc-s1 libicu74 liblttng-ust1 libssl3 libstdc++6 libunwind8 zlib1g"

                if [ $verbosity = "quiet" ]; then
                    sudo bash "${installScriptsDirPath}/dotnet-install.sh" --install-dir "${dotnet_path}" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType" "--quiet"
                elif [ $verbosity = "loud" ]; then
                    sudo bash "${installScriptsDirPath}/dotnet-install.sh" --install-dir "${dotnet_path}" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType" "--verbose"
                else
                    sudo bash "${installScriptsDirPath}/dotnet-install.sh" --install-dir "${dotnet_path}" --channel "$requestedNetMajorMinorVersion" --runtime "$requestedType"
                fi
            fi
        fi

        # if (( $? -ne 0 )); then 
        #    return 2 # failed to install required runtime
        #fi

        # The install script is for CI/CD and doesn't actually register .NET. So add 
        # link and env variables
        writeLine "Link Binaries to /usr/local/bin..."
        if [ -e /usr/local/bin/dotnet ]; then
            rm /usr/local/bin/dotnet
        fi
        sudo ln -s ${dotnet_path}dotnet /usr/local/bin

        if [ -f " /home/pi/.bashrc" ]; then
            setDotNetLocation " /home/pi/.bashrc" "${dotnet_path}"
        elif [ -f " ~/.bashrc" ]; then 
            setDotNetLocation " ~/.bashrc" "${dotnet_path}"
        elif [ -f " ~/.bash_profile" ]; then
            setDotNetLocation " ~/.bash_profile" "${dotnet_path}"
        elif [ -f " ~/.zshrc" ]; then
            setDotNetLocation " ~/.zshrc" "${dotnet_path}"
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
            echo "sudo apt-get install dotnet-sdk-${requestedNetMajorMinorVersion}"
        else
            writeLine ".NET was not installed correctly. You may need to install .NET manually"       $color_error
            writeLine "See https://learn.microsoft.com/en-us/dotnet/core/install/macos for downloads" $color_error
        fi
    else
        writeLine ".NET ${requestedType} ${found_dotnet} is present." $color_mute
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

                # Download "${assetStorageUrl}runtimes/" "python3.7.12-osx64.tar.gz" \
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

        # macOS: With my M1 chip and Rosetta, I make installing Python a real PITA.
        # Raspberry Pi: Hold my beer 
        elif [ "${edgeDevice}" = "Raspberry Pi" ] || [ "${edgeDevice}" = "Orange Pi" ] || \
             [ "${edgeDevice}" = "Radxa ROCK"   ] || [ "${edgeDevice}" = "Jetson"    ] || \
             [ "$os_name" = "debian" ]; then

            # ensure gcc is installed
            if [ "$os_name" = "debian" ]; then 
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

            pushd "${rootDirPath}" > /dev/null

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
            cd "${downloadDirPath}"
            mkdir --parents "${platform_dir}/lib" >/dev/null
            cd "${os}/lib"

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

                if [ "$os_name" = "debian" ]; then 
                    # Native debian is giving us troubles. The instructions should be optimised down
                    # to just what's needed, but for now we'll just throw everything at the problem
                    # until we find a solution to the "SSLError("Can't connect to HTTPS URL because 
                    # the SSL module is not available.")' issue
                    sudo apt-get install libssl-dev libncurses5-dev libsqlite3-dev libreadline-dev libtk8.6 libgdm-dev libdb4o-cil-dev libpcap-dev -y
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
        # NOTE: ppa is an Ubuntu thing only. https://askubuntu.com/a/1481830
        else # if [ "$os_name" = "ubuntu" ]; then

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
    #if [ "$os" = "linux" ]; then
        write 'Installing updated setuptools in venv...' $color_primary
        "$venvPythonCmdPath" -m pip install -U setuptools >/dev/null 2>/dev/null &
        spin $!
        writeLine "done" $color_success
    #fi

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
