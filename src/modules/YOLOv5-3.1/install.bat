:: Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                          YOLOv5-3.1
::
:: This script is called from the YOLOv5-3.1 directory using: 
::
::    ..\..\setup.bat
::
:: The setup.bat file will find this install.bat file and execute it.

@if "%1" NEQ "install" (
	echo This script is only called from ..\..\setup.bat
	@pause
	@goto:eof
)

:: Install python and the required dependencies
call "%sdkScriptsPath%\utils.bat" SetupPython 3.7 "Local"
if errorlevel 1 exit /b 1

:: Installing the old Torch versions can cause havoc. This should help
:: https://bobbyhadz.com/blog/python-no-module-named-setuptools
call "%sdkScriptsPath%\utils.bat" WriteLine "Re-installing python setuptools. Just in case."
"%modulePath%\bin\windows\python37\venv\Scripts\pip3" uninstall setuptools -y -q -q
"%modulePath%\bin\windows\python37\venv\Scripts\python" -m pip install setuptools -q -q

call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%modulePath%" "Local"
if errorlevel 1 exit /b 1

:: TODO: This needs to be a self contained 'SetupSDK Python 3.7 "Local"' call
::       We'd also need 'SetupSDK Python 3.7 "Shared"' 
call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%absoluteAppRootDir%\SDK\Python" "Local"
if errorlevel 1 exit /b 1

:: Download the YOLO models and custom models and store in /assets
call "%sdkScriptsPath%\utils.bat" GetFromServer "models-yolo5-31-pt.zip"        "assets" "Downloading Standard YOLOv5 models..."

:: TODO: Move this to %ProgramData%\CodeProject\AI\custom-models
call "%sdkScriptsPath%\utils.bat" GetFromServer "custom-models-yolo5-31-pt.zip" "custom-models" "Downloading Custom YOLOv5 models..."


::                         -- Install script cheatsheet -- 
::
:: Variables available:
::
::  absoluteAppRootDir    - the root path of the app (eg: C:\Program Files]\CodeProject\AI\)
::  sdkScriptsPath       - the path to the installation utility scripts (%rootPath%\src\SDK\Scripts)
::  downloadPath          - the path to where downloads will be stored (%rootPath%\src\downloads)
::  runtimesPath          - the path to the installed runtimes (%rootPath%\src\runtimes)
::  modulesPath           - the path to all the AI modules (%rootPath%\src\modules)
::  moduleDir             - the name of the directory containing this module
::  modulePath            - the path to this module (%modulesPath%\%moduleDir%)
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
::                 "Local", otherwise in %runtimesPath%/bin/windows/python<version>/venv.
::       install-location - [optional] "Local" or "Shared" (see above)
::
::  InstallPythonPackages Version requirements-file-directory [install-location]
::       Version - version number, as per SetupPython
::       requirements-file-directory - directory containing the requirements.txt file
::       install-location - [optional] "Local" (installed in the module's local folder) or 
::                          "Shared" (installed in the shared runtimes/bin directory)
