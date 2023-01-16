:: Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                          YOLOv5-3.1
::
:: This script is called from the YOLOv5-3.1 directory using: 
::
::    ..\..\..\Installers\Live\setup.bat
::
:: The setup.bat file will find this install.bat file and execute it.

@if "%1" NEQ "install" (
	echo This script is only called from /src/setup.bat
	@pause
	@goto:eof
)

:: Install python and the required dependencies
call "%sdkScriptsPath%\utils.bat" SetupPython 3.7 "LocalToModule"

:: Installing the old Torch versions can cause havoc. This should help
:: https://bobbyhadz.com/blog/python-no-module-named-setuptools
call "%sdkScriptsPath%\utils.bat" WriteLine "Re-installing python setuptools. Just in case."
"%modulePath%\bin\windows\python37\venv\Scripts\pip3" uninstall setuptools -y -q -q
"%modulePath%\bin\windows\python37\venv\Scripts\python" -m pip install setuptools -q -q

call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%modulePath%" "LocalToModule"

:: TODO: This needs to be a self contained 'SetupSDK Python 3.7 "LocalToModule"' call
::       We'd also need 'SetupSDK Python 3.7 "Shared"' 
::                      'SetupSDK .NET 7     "Global"'
call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%absoluteAppRootDir%\SDK\Python" "LocalToModule"

:: Download the YOLO models and custom models and store in /assets
call "%sdkScriptsPath%\utils.bat" GetFromServer "yolov5-5-models-pytorch.zip"        "assets" "Downloading Standard YOLOv5 models..."

:: TODO: Move this to %ProgramData%\CodeProject\AI\custom-models
call "%sdkScriptsPath%\utils.bat" GetFromServer "yolov5-5-custom-models-pytorch.zip" "custom-models" "Downloading Custom YOLOv5 models..."

:: HACK for previous Blue Iris install`
:: call "%modulePath%"\Create_Custom_Folder.bat

:: Cleanup if you wish
:: rmdir /S %downloadPath%


::                         -- Install script cheatsheet -- 
::
:: Variables available:
::
::  absoluteAppRootDir    - the root path of the app (eg: C:\Program Files]\CodeProject\AI\)
::  sdkScriptsPath       - the path to the installation utility scripts (%rootPath%\src\SDK\Scripts)
::  downloadPath          - the path to where downloads will be stored (%rootPath%\src\downloads)
::  installedModulesPath  - the path to the pre-installed AI modules (%rootPath%\src\AnalysisLayer)
::  downloadedModulesPath - the path to the download AI modules (%rootPath%\src\modules)
::  moduleDir             - the name of the directory containing this module
::  modulePath            - the path to this module (%installedModulesPath%\%moduleDir% or
::                          %downloadedModulesPath%\%moduleDir%, depending on whether pre-installed)
::  os                    - "windows"
::  architecture          - "x86_64" or "arm64"
::  platform              - "windows" or "windows-arm64"
::  verbosity             - quiet, info or loud. Use this to determines the noise level of output.
::  forceOverwrite        - if true then ensure you force a re-download and re-copy of downloads.
::                          GetFromServer will honour this value. Do it yourself for DownloadAndExtract 
::
:: Methods available (call by 'call %sdkScriptsPath%\utils.bat <method>')
::
::  Write     text [foreground [background]] (eg call %sdkScriptsPath%\utils.bat WriteLine "Hi" "green")
::  WriteLine text [foreground [background]]
::
::  GetFromServer filename moduleAssetDir message
::        filename       - Name of the compressed archive to be downloaded
::        moduleAssetDir - Name of folder inthe module's directory where archive will be extracted
::        message        - Message to display during download
::
::  DownloadAndExtract  storageUrl filename downloadPath dirNameToSave message
::        storageUrl    - Url that holds the compressed archive to Download
::        filename      - Name of the compressed archive to be downloaded
::        downloadPath  - Path to where the downloaded compressed archive should be downloaded
::        dirNameToSave - name of directory, relative to downloadPath, where contents of archive 
::                        will be extracted and saved
::        message       - Message to display during download
::
::  SetupPython Version [install-location]
::       Version - version number of python to setup. 3.7 and 3.9 currently supported. A virtual
::                 environment will be created in the module's local folder if install-location is
::                 "LocalToModule", otherwise in %installedModulesPath%/bin/windows/python<version>/venv.
::       install-location - [optional] "LocalToModule" or "Shared" (see above)
::
::  InstallPythonPackages Version requirements-file-directory [install-location]
::       Version - version number, as per SetupPython
::       requirements-file-directory - directory containing the requirements.txt file
::       install-location - [optional] "LocalToModule" (installed in the module's local folder) or 
::                          "Shared" (installed in the shared AnalysisLayer/bin directory)
