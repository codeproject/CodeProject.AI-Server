#!/bin/sh

## If you wish to have a single environment variable file for sharing between
## operating systems then this script will read the Windows .bat version and
## export the variables within a *nix shall
##
## Usage:
##   . ./set_environment.sh

unamestr=$(uname)
if [ "$unamestr" = 'Linux' ]; then
  export $(grep -v '^REM' set_environment.bat | xargs -d '\n')
elif [ "$unamestr" = 'FreeBSD' ]; then
  export $(grep -v '^REM' set_environment.bat | xargs -0)
fi