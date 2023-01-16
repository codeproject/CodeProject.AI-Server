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
setupDotNet 7.0.100

# Install python and the required dependencies.
setupPython 3.8
installPythonPackages 3.8 "${modulePath}/Python"

setupPython 3.9
installPythonPackages 3.9 "${modulePath}/Python"

# Ensure cuDNN is installed. Note this is only for linux since macs no longer support nVidia
hasCUDA="false" # (disabled for now, pending testing)
if [ "$os" == "linux" ] && [ "$hasCUDA" == "true" ]; then

	# Ensure zlib is installed
	sudo apt-get install zlib1g

	# Download tar from https://developer.nvidia.com/cudnn
	tar -xvf cudnn-linux-x86_64-8.x.x.x_cudaX.Y-archive.tar.xz
	sudo cp cudnn-*-archive/include/cudnn*.h /usr/local/cuda/include 
	sudo cp -P cudnn-*-archive/lib/libcudnn* /usr/local/cuda/lib64 
	sudo chmod a+r /usr/local/cuda/include/cudnn*.h /usr/local/cuda/lib64/libcudnn*

	# Ensure nVidia Project Manager is installed

	# Enable the repo
	OS="ubuntu2004" # ubuntu1804, ubuntu2004, or ubuntu2204.
	wget https://developer.download.nvidia.com/compute/cuda/repos/${OS}/x86_64/cuda-${OS}.pin 

	sudo mv cuda-${OS}.pin /etc/apt/preferences.d/cuda-repository-pin-600
	sudo apt-key adv --fetch-keys https://developer.download.nvidia.com/compute/cuda/repos/${OS}/x86_64/3bf863cc.pub
	sudo add-apt-repository "deb https://developer.download.nvidia.com/compute/cuda/repos/${OS}/x86_64/ /"
	sudo apt-get update

	# install the cuDNN library
	cudnn_version="8.5.0.*"
	cuda_version="cuda11.7" # cuda10.2, cuda11.7 or cuda11.8

	sudo apt-get install libcudnn8=${cudnn_version}-1+${cuda_version}
	sudo apt-get install libcudnn8-dev=${cudnn_version}-1+${cuda_version}
fi


#                         -- Install script cheatsheet -- 
#
# Variables available:
#
#  absoluteRootDir       - the root path of the installation (eg: ~/CodeProject/AI)
#  sdkScriptsPath       - the path to the installation utility scripts ($rootPath/Installers)
#  downloadPath          - the path to where downloads will be stored ($sdkScriptsPath/downloads)
#  installedModulesPath  - the path to the pre-installed AI modules ($rootPath/src/AnalysisLayer)
#  downloadedModulesPath - the path to the download AI modules ($rootPath/src/modules)
#  moduleDir             - the name of the directory containing this module
#  modulePath            - the path to this module ($installedModulesPath/$moduleDir or
#                          $downloadedModulesPath/$moduleDir, depending on whether pre-installed)
#  os                    - "linux" or "macos"
#  architecture          - "x86_64" or "arm64"
#  platform              - "linux", "linux-arm64", "macos" or "macos-arm64"
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
#                 "LocalToModule", otherwise in $installedModulesPath/bin/$platform/python<version>/venv.
#       install-location - [optional] "LocalToModule" or "Shared" (see above)
#
#  installPythonPackages Version requirements-file-directory
#       Version - version number, as per SetupPython
#       requirements-file-directory - directory containing the requirements.txt file
#       install-location - [optional] "LocalToModule" (installed in the module's local venv) or 
#                          "Shared" (installed in the shared $installedModulesPath/bin venv folder)