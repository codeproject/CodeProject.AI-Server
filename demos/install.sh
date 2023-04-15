# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            CodeProject.AI Demos
#
# This script is called from the Demos directory using: 
#
#    bash ../src/setup.sh
#
# The setup.sh script will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../src/setup.sh"
    echo
	exit 1 
fi


# Install python and the required dependencies. If we find imutils then asssume it's all there
setupPython 3.8 "Shared"
if [ $? -ne 0 ]; then quit 1; fi

installPythonPackages 3.8 "${modulePath}/Python" "Shared"
if [ $? -ne 0 ]; then quit 1; fi
