#!/bin/sh

## CodeProject.AI Server and Analysis modules startup script for Linux and macOS
##
## Usage:
##   . ./start.sh
##
## We assume we're in the /src directory

clear

# Do we want the CodeProject.AI server to start the backebnd analysis services, or have the services
# be started separately (aids in debugging)
codeprojectAI_starts_analysis="true"

# Move into the working directory
if [ "$1" != "" ]; then
    cd "$1"
fi

ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT

if [ "${codeprojectAI_starts_analysis}" == "true" ]; then

    echo "Starting API Server (with analysis)"
    cd ./API/Server/Frontend/bin/Debug/net6.0/
    ./CodeProject.AI.Server

else

    echo "Starting API Server (no analysis)"
    ./API/Server/Frontend/bin/Debug/net6.0/CodeProject.AI.Server --LaunchAnalysisServices=false &

    # Start analysis services
    # ISSUE: Environment variables that are set in CodeProject.AI Server are not being exposed / 
    #        made available to other processes. start-analysis.sh will run properly, but the 
    #        processes it launches do not have access to the variables that were set.

    echo "Starting Analysis"
    pushd ./AnalysisLayer >/dev/null
    bash ./start-analysis.sh --embedded
    popd >/dev/null

fi

# Wait forever. We need these processes to stay alive
sleep 5
