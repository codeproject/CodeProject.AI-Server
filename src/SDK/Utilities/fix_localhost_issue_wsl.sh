#!/bin/bash

# To enable localhost host access from Windows into WSL. eg if testing a site in
# WSL by using a Windows browser. WSL changes the localhost IP on every boot.
# See the discussion at https://github.com/microsoft/WSL/issues/5298
#
# You can:
#  a) open Powershell terminal, and issue wsl --shutdown, re-open WSL terminal
#  b) remap the WSL ip into the Windows hosts file each time WSL starts up
#  c) install and run a browser from within WSL
#
# This script does (b). Make sure your user has the permission to modify / create
# files in the "/Windows/System32/drivers/etc/" folder because sed will create a
# temp file in this directory. Run this script everytime you start WSL.

if ! command -v ipconfig &> /dev/null; then
    apt install net-tools
fi

NEWIP=`ifconfig | grep eth0 -A1 | tail -n1 | cut -d ' '  -f10 `

# delete IP from windows hosts file
sed '/wsl/d' -i '/mnt/c/Windows/System32/drivers/etc/hosts' || true

echo "$NEWIP		wsl" >> '/mnt/c/Windows/System32/drivers/etc/hosts'

# OPTION c:
# 
# Google Chrome:
#
# sudo apt update && sudo apt -y upgrade && sudo apt -y autoremove
# wget https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
# sudo apt -y install ./google-chrome-stable_current_amd64.deb
#
# and to check: run 'google-chrome --version'
#