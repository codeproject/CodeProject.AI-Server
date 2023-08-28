:: Development mode setup script ::::::::::::::::::::::::::::::::::::::::::::::
::
::                         ObjectDetection (Coral)
::
:: This script is only called from ..\..\src\setup.bat

@if "%1" NEQ "install" (
	echo This script is only called from ..\..\src\setup.bat
	@pause
	@goto:eof
)

rem set verbosity=loud

REM Python setup
call "%sdkScriptsPath%\utils.bat" SetupPython 3.7 "Local"
if errorlevel 1 exit /b 1

REM Do SDK first, since it's a little fussy
call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%absoluteAppRootDir%\SDK\Python" "Local"
if errorlevel 1 exit /b 1

call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%modulePath%" "Local"
if errorlevel 1 exit /b 1

if not exist edgetpu_runtime (
	call "%sdkScriptsPath%\utils.bat" GetFromServer "edgetpu_runtime_20221024.zip" "." "Downloading edge TPU runtime..."

	rem call "%sdkScriptsPath%\utils.bat" ExtractToDirectory "edgetpu_runtime_20221024.zip"
	rem move edgetpu_runtime_20221024\edgetpu_runtime edgetpu_runtime
	rem rmdir edgetpu_runtime_20221024
)

if exist edgetpu_runtime (
	call "!sdkScriptsPath!\utils.bat" WriteLine "*** You may need to run !modulePath!\edgetpu_runtime\install.bat to complete this process. Attempting to run this script now." "!color_info!"

	pushd edgetpu_runtime
	call install.bat
	popd
)

:: Download the MobileNet TFLite models and store in /assets
call "%sdkScriptsPath%\utils.bat" GetFromServer "objectdetect-coral-models.zip" "assets" "Downloading MobileNet models..."
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
::  InstallPythonPackages Version requirements-file-directory [install-location]
::       Version - version number, as per SetupPython
::       requirements-file-directory - directory containing the requirements.txt file
::       install-location - [optional] "Local" (installed in the module's local folder) or 
::                          "Shared" (installed in the shared runtimes/bin directory)
