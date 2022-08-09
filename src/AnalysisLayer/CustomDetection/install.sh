# Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                            Custom Object Detection


# Install python and the required dependencies. If we find torch then asssume it's all there
setupPython 3.8
installPythonPackages 3.8 "${modulePath}" "torch"

# Download the models and store in /models
getFromServer "custom-models-yolo5-pt.zip" "assets" "Downloading Custom YOLO models..."



#                         -- Install script cheatsheet -- 
#
# Variables available:
#
#  rootPath          - the root path of the installation (def: C:\Program Files]\CodeProject\AI)
#  installBasePath   - the path to the installation utility scripts (def: rootPath\Installers\Dev)
#  analysisLayerPath - the path to the analysis modules base path (def: /src/AnalysisLayer)
#  modulePath        - the path to this module (def: analysisLayerPath\moduleDir
#  moduleDir         - the name of the directory containing this module (def: name of current dir)
#  downloadPath      - the path to where downloads will be stored (def: /Installers/downloads)
#
#  platform          - "linux", "macos", or "macos-arm"
#  verbosity         - quiet, info or loud. Use this to determines the noise level of output.
#  forceOverwrite    - if true then ensure you force a re-download and re-copy of downloads.
#                      GetFromServer will honour this value. Do it yourself for DownloadAndExtract 
#
# Methods available
#
#  write     text [foreground [background]] (eg call %utilsPath%\utils.bat WriteLine "Hi" "green")
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
#                        will be extracted and saved
#
#  setupPython Version 
#       Version - version number of python to setup. 3.8 and 3.9 currently supported. A virtual
#                 environment will be created in %analysisLayerPath%/bin/windows/python<version>/venv
#
#  installPythonPackages Version requirements-file-directory test-package-name
#       Version - version number, as per SetupPython
#       requirements-file-directory - directory containing the requirements.txt file
#       test-package-name - Name of a package which, if present, indicates the required packages
#                           have already been installed
