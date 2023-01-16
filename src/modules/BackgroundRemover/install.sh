# Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
#
#                            Background Remover
#
# This script is called from the BackgroundRemover directory using: 
#
#    bash ../../setup.sh
#
# The setup.sh script will find this install.sh file and execute it.

if [ "$1" != "install" ]; then
    read -t 3 -p "This script is only called from: bash ../../setup.sh"
    echo
	exit 1 
fi


# Install python and the required dependencies. If we find onnxruntime then asssume it's all there
setupPython 3.9 "Shared"
installPythonPackages 3.9 "${modulePath}" "Shared"
installPythonPackages 3.9 "${absoluteAppRootDir}/SDK/Python" "Shared"

# Location of models as per original repo
# u2netp:          https://drive.google.com/uc?id=1tNuFmLv0TSNDjYIkjEdeH1IWKQdUA4HR
# u2net:           https://drive.google.com/uc?id=1tCU5MM1LhRgGou5OpmpjBQbSrYIUoYab
# u2net_human_seg: https://drive.google.com/uc?id=1ZfqwVxu-1XWC1xU1GHIP-FM_Knd_AX5j
# u2net_cloth_seg: https://drive.google.com/uc?id=15rKbQSXQzrKCQurUjZFg8HqzZad8bcyz

# Download the models and store in /models
getFromServer "rembg-models.zip" "models" "Downloading Background Remover models..."



#                         -- Install script cheatsheet -- 
#
# Variables available:
#
#  absoluteAppRootDir    - the root path of the app (eg: ~/CodeProject/AI)
#  sdkScriptsPath        - the path to the installation utility scripts ($rootPath/src/SDK/Scripts)
#  downloadPath          - the path to where downloads will be stored ($rootPath/src/downloads)
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
