:: Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                          ALPR
::
:: This script is called from the ALPR directory using: 
::
::    ..\..\setup.bat
::
:: The setup.bat file will find this install.bat file and execute it.

@if "%1" NEQ "install" (
	echo This script is only called from /src/setup.bat
	@pause
	@goto:eof
)

:: Install python and the required dependencies
:: Note that PaddlePaddle requires Python <= 3.8
call "%sdkScriptsPath%\utils.bat" SetupPython 3.7 "LocalToModule"
call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%modulePath%" "LocalToModule"
call "%sdkScriptsPath%\utils.bat" InstallPythonPackages 3.7 "%absoluteAppRootDir%\SDK\Python" "LocalToModule"

:: Download the ALPR models and store in /paddleocr
call "%sdkScriptsPath%\utils.bat" GetFromServer "paddleocr-models.zip" "paddleocr" "Downloading ALPR models..."

:: We have a patch to apply!
call "!sdkScriptsPath!\utils.bat" WriteLine "Applying patch for PaddlePaddle" "!color_info!"
if /i "!hasCUDA!" == "true" (
	copy /Y "!modulePath!\patch\paddle2.3.2.post116\image.py" "!modulePath!\bin\%os%\python37\venv\Lib\site-packages\paddle\dataset\"
) else (
	copy /Y "!modulePath!\patch\paddle2.3.2\image.py"         "!modulePath!\bin\%os%\python37\venv\Lib\site-packages\paddle\dataset\"
)

:: Cleanup if you wish
:: rmdir /S %downloadPath%


::                         -- Install script cheatsheet -- 
::
:: Variables available:
::
::  absoluteAppRootDir    - the root path of the app (eg: C:\Program Files]\CodeProject\AI\)
::  sdkScriptsPath        - the path to the installation utility scripts (%rootPath%\src\SDK\Scripts)
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
