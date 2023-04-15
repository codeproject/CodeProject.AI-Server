:: Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
::
::                           .NET YOLO Object Detection
::
:: This script is only called from ..\..\setup.bat 


@if "%1" NEQ "install" (
	echo This script is only called from ..\..\setup.bat
	@pause
	@goto:eof
) 

REM Read the version from the modulesettings.json file 
call "!sdkScriptsPath!\utils.bat" GetVersionFromModuleSettings "modulesettings.json" "Version"
set version=!jsonValue!

:: Pull down the correct .NET image of ObjectDetectionNet based on this OS / GPU combo
if /i "!executionEnvironment!" == "Production" (
	set imageName=ObjectDetectionNet-CPU-!version!.zip
	if /i "!enableGPU!" == "true" (
		set imageName=ObjectDetectionNet-DirectML-!version!.zip

		REM We can use CUDA on Windows but DirectML has proven to be faster
		REM if /i "!supportCUDA!" == "true" (
		REM 	if /i "!hasCUDA!" == "true" set imageName=ObjectDetectionNet-CUDA-!version!.zip
		REM )
	)

	call "%sdkScriptsPath%\utils.bat" GetFromServer "!imageName!" "" "Downloading ObjectDetectionNet module..."
) else (
	call "%sdkScriptsPath%\utils.bat" GetFromServer "ObjectDetectionNetNugets.zip" "LocalNugets" "Downloading Nuget packages..."
)

:: Download the YOLO models and store in /assets
call "%sdkScriptsPath%\utils.bat" GetFromServer "yolonet-models.zip" "assets" "Downloading YOLO ONNX models..."
if errorlevel 1 exit /b 1

call "%sdkScriptsPath%\utils.bat" GetFromServer "yolonet-custom-models.zip" "custom-models" "Downloading Custom YOLO ONNX models..."
if errorlevel 1 exit /b 1



::                         -- Install script cheatsheet -- 
::
:: Variables available:
::
::  absoluteAppRootDir    - the root path of the app (eg: C:\Program Files]\CodeProject\AI\)
::  sdkScriptsPath        - the path to the installation utility scripts (%rootPath%\src\SDK\Scripts)
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
