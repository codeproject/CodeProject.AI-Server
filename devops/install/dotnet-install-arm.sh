#!/bin/bash

darkred="\e[1;31m"
bold="\e[1m"
underline="\e[1;4m"
reset="\e[0m"

echo -e "${bold}.NET 7 Installer${reset}"
echo -e "${bold}Pete Codes / PJG Creations 2021${reset}"
echo -e "${bold}CodeProject.AI edition${reset}"   # Drastically trimming output / SDK is optional

echo -e "Latest update 01/10/2022"

if [ "$Admin" = "" ]; then
    if [[ $EUID -eq 0 ]]; then isAdmin=true; else isAdmin=false; fi
fi

if [ "$isAdmin" = false ]; then
   echo -e "${darkred}This script must be run as root (sudo $0)${reset}" 
   exit 1
fi

download() {
    [[ $downloadspage =~ $1 ]]
    linkpage=$(wget -qO - https://dotnet.microsoft.com${BASH_REMATCH[1]})

    matchdl='id="directLink" href="([^"]*)"'
    [[ $linkpage =~ $matchdl ]]
    wget -O $2 "${BASH_REMATCH[1]}"
}

detectArch() {
    arch=arm32
  
    if command -v uname > /dev/null; then
        machineCpu=$(uname -m)-$(uname -p)

        if [[ $machineCpu == *64* ]]; then
            arch=arm64
        fi
    fi
}


#dotnetver=7.0
#requestedType=runtime

# input parameters
dotnetver=$1
requestedType=$2

# Constants
dotnettype="dotnet-core"
always_install_runtime=true
sdkfile=/tmp/dotnetsdk.tar.gz
aspnetfile=/tmp/aspnetcore.tar.gz

# flags
quiet='false'
if [ "$3" = "quiet" ]; then quiet='true'; fi


echo -e "${bold}Fetching Latest .NET Versions${reset}"

if [ "$dotnetver" = "" ]; then
    versionspage=$(wget -qO - https://dotnet.microsoft.com/download/dotnet)
    matchrecommended='\.NET ([^]*) \(recommended\)'

    [[ $versionspage =~ $matchrecommended ]]
    dotnetver=${BASH_REMATCH[1]}
fi


echo -e "${bold}Installation information${reset}"

echo "This will install the latest versions of the following:"

if [ "$requestedType" = "sdk" ]; then
    echo "- .NET SDK $dotnetver"
fi
if [ "$requestedType" = "runtime" ] || [ "$always_install_runtime" = "true" ]; then
    echo "- ASP.NET Runtime $dotnetver"
fi
echo ""

# echo -e "Any suggestions or questions, email ${underline}pete@pjgcreations.co.uk${reset}"
# echo -e "Send me a tweet ${underline}@pete_codes${reset}"
# echo -e "Tutorials on ${underline}https://www.petecodes.co.uk${reset}"
# echo ""
# echo ""


echo -e "${bold}Installing Dependencies${reset}"

if [ "$quiet" = "true" ]; then
    apt-get -y install libunwind8 gettext > /dev/null
else
    apt-get -y install libunwind8 gettext
fi


echo -e "${bold}Remove Old Binaries${reset}"

if [ "$quiet" = "true" ]; then
    rm -f $sdkfile > /dev/null
    rm -f $aspnetfile > /dev/null
else
    rm -f $sdkfile
    rm -f $aspnetfile
fi

detectArch

echo -e "${bold}Getting download links for .NET ${dotnetver}${reset}"

# Download the downloads page to get some links
[[ "$dotnetver" > "5" ]] && dotnettype="dotnet" || dotnettype="dotnet-core"
downloadspage=$(wget -qO - https://dotnet.microsoft.com/download/$dotnettype/$dotnetver)

if [ "$requestedType" = "sdk" ]; then
    echo -e "${bold}Getting .NET SDK ${dotnetver}${reset}"
    download 'href="([^"]*sdk-[^"/]*linux-'$arch'-binaries)"' $sdkfile
fi

if [ "$requestedType" = "runtime" ] || [ "$always_install_runtime" = "true" ]; then
    echo -e "${bold}Getting ASP.NET Runtime ${dotnetver}${reset}"
    download 'href="([^"]*aspnetcore-[^"/]*linux-'$arch'-binaries)"' $aspnetfile
fi


echo -e "${bold}Creating Main Directory${reset}"

if [[ -d /opt/dotnet ]]; then
    echo "/opt/dotnet already  exists on your filesystem."
else
    echo "Creating Main Directory"
    mkdir /opt/dotnet
fi

if [ "$requestedType" = "sdk" ]; then

    echo -e "${bold}Extracting .NET SDK ${dotnetver}${reset}"

    if [ "$quiet" = "true" ]; then
        tar -xvf $sdkfile -C /opt/dotnet/ > /dev/null
    else
        tar -xvf $sdkfile -C /opt/dotnet/
    fi

fi

if [ "$requestedType" = "runtime" ] || [ "$always_install_runtime" = "true" ]; then

    echo -e "${bold}Extracting ASP.NET Runtime ${dotnetver}${reset}"

    if [ "$quiet" = "true" ]; then
        tar -xvf $aspnetfile -C /opt/dotnet/ > /dev/null
    else
        tar -xvf $aspnetfile -C /opt/dotnet/
    fi

fi

echo -e "${bold}Link Binaries to User Profile${reset}"
ln -s /opt/dotnet/dotnet /usr/local/bin

echo -e "${bold}Make Link Permanent${reset}"
if [ -d /home/pi/ ]; then
    if grep -q 'export DOTNET_ROOT=' /home/pi/.bashrc;  then
        echo 'Already added link to .bashrc'
    else
        echo 'Adding Link to .bashrc'
        echo 'export DOTNET_ROOT=/opt/dotnet' >> /home/pi/.bashrc
    fi
elif [ -f ~/.bashrc ]; then
    if grep -q 'export DOTNET_ROOT=' ~/.bashrc;  then
        echo 'Already added link to .bashrc'
    else
        echo 'Adding Link to .bashrc'
        echo 'export DOTNET_ROOT=/opt/dotnet' >> ~/.bashrc
    fi
elif [ -f ~/.bash_profile ]; then
    if grep -q 'export DOTNET_ROOT=' ~/.bash_profile;  then
        echo 'Already added link to .bash_profile'
    else
        echo 'Adding Link to .bash_profile'
        echo 'export DOTNET_ROOT=/opt/dotnet' >> ~/.bash_profile
    fi
elif [ -f ~/.zshrc ]; then
    if grep -q 'export DOTNET_ROOT=' ~/.zshrc;  then
        echo 'Already added link to .zshrc'
    else
        echo 'Adding Link to .zshrc'
        echo 'export DOTNET_ROOT=/opt/dotnet' >> ~/.zshrc
    fi
fi

# echo ""
# echo -e "${bold}Download Debug Stub${reset}"
# echo ""
#
# cd ~
# wget -O /home/pi/dotnetdebug.sh https://raw.githubusercontent.com/pjgpetecodes/dotnet7pi/master/dotnetdebug.sh
# chmod +x /home/pi/dotnetdebug.sh 

echo -e "${bold}Run dotnet --info${reset}"
dotnet --info

echo -e "${bold}ALL DONE!${reset}"
echo -e "${bold}Note: It's highly recommended that you perform a reboot at this point${reset}"

#echo -e "Go ahead and run ${bold}dotnet new console ${reset}in a new directory!"
#echo ""
#echo ""
#echo ""
#echo -e "Let me know how you get on by tweeting me at \e[1;5m@pete_codes${reset}"
#echo ""
#echo -e "${bold}----------------------------------------${reset}"
#echo ""
