# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            CodeProject.AI SDK
#
# This script is called from the SDK directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh file will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    echo
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
	exit 1 
fi

# Setup .NET for the server and any .NET modules that may need it
setupDotNet 7.0
if [ $? -ne 0 ]; then quit 1; fi

# Install python and the required dependencies.
setupPython 3.8 "Shared"
if [ $? -ne 0 ]; then quit 1; fi
installPythonPackages 3.8 "${modulePath}/Python" "Shared"
if [ $? -ne 0 ]; then quit 1; fi

setupPython 3.9 "Shared"
if [ $? -ne 0 ]; then quit 1; fi
installPythonPackages 3.9 "${modulePath}/Python" "Shared"
if [ $? -ne 0 ]; then quit 1; fi

# Ensure CUDA and cuDNN is installed. Note this is only for native linux since 
# macOS no longer supports NVIDIA, WSL (Linux under Windows) uses the Windows 
# drivers, and docker images already contain the necessary SDKs and libraries
if [ "$os" == "linux" ] && [ "$hasCUDA" == "true" ] && [ "${inDocker}" == "false" ] && \
   [ "${systemName}" != "Jetson" ] && [ "${systemName}" != "Raspberry Pi" ] && \
   [ "${systemName}" != "Orange Pi" ] ; then

	# Install CUDA and cuDNN
	correctLineEndings "${sdkScriptsPath}/install_cuDNN.sh"
	source "${sdkScriptsPath}/install_cuDNN.sh"
fi


#                         -- Install script cheatsheet -- 
#
# Variables available:
#
#  absoluteRootDir       - the root path of the installation (eg: ~/CodeProject/AI)
#  sdkScriptsPath        - the path to the installation utility scripts ($rootPath/SDK/Scripts)
#  downloadPath          - the path to where downloads will be stored ($sdkScriptsPath/downloads)
#  runtimesPath          - the path to the installed runtimes ($rootPath/src/runtimes)
#  modulesPath           - the path to all the AI modules ($rootPath/src/modules)
#  moduleDir             - the name of the directory containing this module
#  modulePath            - the path to this module ($modulesPath/$moduleDir)
#  os                    - "linux" or "macos"
#  architecture          - "x86_64" or "arm64"
#  platform              - "linux", "linux-arm64", "macos" or "macos-arm64"
#  systemName            - General name for the system. "Linux", "macOS", "Raspberry Pi", "Orange Pi"
#                          "Jetson" or "Docker"
#  verbosity             - quiet, info or loud. Use this to determines the noise level of output.
#  forceOverwrite        - if true then ensure you force a re-download and re-copy of downloads.
#                          getFromServer will honour this value. Do it yourself for downloadAndExtract 
#
# Methods available
#
#  write     text [foreground [background]] (eg write "Hi" "green")
#  writeLine text [foreground [background]]
#  Download  storageUrl downloadPath filename moduleDir message
#        storageUrl    - Url that holds the compressed archive to Download
#        downloadPath  - Path to where the downloaded compressed archive should be downloaded
#        filename      - Name of the compressed archive to be downloaded
#        dirNameToSave - name of directory, relative to downloadPath, where contents of archive 
#                        will be extracted and saved
#
#  getFromServer filename moduleAssetDir message
#        filename       - Name of the compressed archive to be downloaded
#        moduleAssetDir - Name of folder in module's directory where archive will be extracted
#        message        - Message to display during download
#
#  downloadAndExtract  storageUrl filename downloadPath dirNameToSave message
#        storageUrl    - Url that holds the compressed archive to Download
#        filename      - Name of the compressed archive to be downloaded
#        downloadPath  - Path to where the downloaded compressed archive should be downloaded
#        dirNameToSave - name of directory, relative to downloadPath, where contents of archive 
#                        will be extracted and saved
#        message       - Message to display during download
#
#  setupPython Version [install-location]
#       Version - version number of python to setup. 3.8 and 3.9 currently supported. A virtual
#                 environment will be created in the module's local folder if install-location is
#                 "Local", otherwise in $runtimesPath/bin/$platform/python<version>/venv.
#       install-location - [optional] "Local" or "Shared" (see above)
#
#  installPythonPackages Version requirements-file-directory
#       Version - version number, as per SetupPython
#       requirements-file-directory - directory containing the requirements.txt file
#       install-location - [optional] "Local" (installed in the module's local venv) or 
#                          "Shared" (installed in the shared $runtimesPath/bin venv folder)