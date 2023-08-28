:: Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
::
::                    YOLO Object Detection Model Training
::
:: This script is only called from ..\..\setup.bat 

@if "%1" NEQ "install" (
	echo This script is only called from ..\..\setup.bat
	@pause
	@goto:eof
)

:: set verbosity=loud

:: Install python and the required dependencies
call "%sdkScriptsPath%\utils.bat" SetupPython 3.9 "Local"
if errorlevel 1 exit /b 1

REM We need to workaround a boto / urllib error. Pre-install urllib.
rem if "a" == "a" (   REM Comment out if this is causing issues
	:: -- Start -- To be provided in the SDK scripts for use everywhere in the future
	::
	:: To be set at start of install scripts
	set pythonVersion=3.9
	set installLocation=Local
	::
	:: Variables to provided to install scripts
	::
	set pythonName=python!pythonVersion:.=!
	if /i "!installLocation!" == "Local" (
		set virtualEnv=!modulePath!\bin\!os!\!pythonName!\venv
	) else (
		set virtualEnv=!runtimesPath!\bin\!os!\!pythonName!\venv
	)
	set venvPythonPath=!virtualEnv!\Scripts\python
	set packagesPath=%virtualEnv%\Lib\site-packages
	::
	REM call "%sdkScriptsPath%\utils.bat" WriteLine "pythonVersion !pythonVersion!" %color_info%
	REM call "%sdkScriptsPath%\utils.bat" WriteLine "pythonName !pythonName!" %color_info%
	REM call "%sdkScriptsPath%\utils.bat" WriteLine "location !installLocation!" %color_info%
	REM call "%sdkScriptsPath%\utils.bat" WriteLine "virtualEnv = !virtualEnv!" %color_info%
	REM call "%sdkScriptsPath%\utils.bat" WriteLine "venvPythonPath = !venvPythonPath!" %color_info%
	REM call "%sdkScriptsPath%\utils.bat" WriteLine "packagesPath = !packagesPath!" %color_info%
	::
	::
	:: InstallSinglePythonPackage to be provided to install scripts
	::
	:: (HACK due to botocore. See https://github.com/boto/botocore/issues/2926)
	set package="urllib3<1.27,>=1.25.4"
	set packageDesc=urllib3, the HTTP client for Python
	:: (to be called via: call "%sdkScriptsPath%\utils.bat" InstallSinglePythonPackage !package! !packageDesc!)
	::
	call "%sdkScriptsPath%\utils.bat" WriteLine "Installing !packageDesc!..."
	"!venvPythonPath!" -m pip install !package! --target "!packagesPath!" !pipFlags!
	call "%sdkScriptsPath%\utils.bat" WriteLine "Success" %color_success%
	::
	:: -- End --
rem )

call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.9 "%modulePath%" "Local"
if errorlevel 1 exit /b 1

REM This makes no sense. None. But: install these package by package to help work
REM around the boto/urllib error
REM set oneStepPIP=true - except this doesn't help
call "%sdkScriptsPath%\utils.bat" WriteLine "Ignore the 'urllib' error below. This issue is discussed at"  %color_info%
call "%sdkScriptsPath%\utils.bat" WriteLine "https://github.com/boto/botocore/issues/2926. It will not"  %color_info%
call "%sdkScriptsPath%\utils.bat" WriteLine "affect this module. We're all good!"  %color_info%

call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.9 "%absoluteAppRootDir%\SDK\Python" "Local"
if errorlevel 1 exit /b 1

:: Download the YOLO models and custom models and store in /assets
rem call "%sdkScriptsPath%\utils.bat" GetFromServer "models-yolo5-pt.zip" "assets" "Downloading Standard YOLO models..."
rem if errorlevel 1 exit /b 1

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
::  systemName            - "Windows"
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
::  InstallSinglePythonPackage Package
::       Package - package name/version and flags as per Python requirements.txt
::                 eg InstallSinglePythonPackage "torch>=1.7.0 -f https://download.pytorch.org/whl/torch_stable.html"
::
::  InstallPythonPackages Version requirements-file-directory [install-location]
::       Version - version number, as per SetupPython
::       requirements-file-directory - directory containing the requirements.txt file
::       install-location - [optional] "Local" (installed in the module's local folder) or 
::                          "Shared" (installed in the shared runtimes/bin directory)
