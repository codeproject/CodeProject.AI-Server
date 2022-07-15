:: CodeProject.AI Server 
::
:: Windows Development Environment install script
::
:: We assume we're in the source code /Installers/Dev directory.
::

@echo off
cls
setlocal enabledelayedexpansion

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: If files are already present, then don't overwrite if this is false
set forceOverwrite=false

:: Show output in wild, crazy colours
set useColor=true

:: Platform can define where things are located
set platform=windows

:: are we ready to support CUDA enabled GPUs?
set supportCUDA=false

:: Basic locations

:: The location of the solution root directory relative to this script
set rootPath=../..

:: CodeProject.AI Server specific :::::::::::::::::::::::::::::::::::::::::::::

:: The name of the dir holding the frontend API server
set APIDirName=API


:: Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: The location of large packages that need to be downloaded
:: a. From AWS
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/
:: b. Use a local directory rather than from online. Handy for debugging.
rem set storageUrl=C:\Dev\CodeProject\CodeProject.AI\install\cached_downloads\

:: The name of the source directory
set srcDir=src

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir holding the backend analysis services
set analysisLayerDir=AnalysisLayer

:: Absolute paths :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: The absolute path to the root directory of CodeProject.AI
set currentDir=%cd%
cd %rootPath%
set absoluteRootDir=%cd%
cd %currentDir%

:: The location of directories relative to the root of the solution directory
set analysisLayerPath=%absoluteRootDir%\%srcDir%\%analysisLayerDir%
set downloadPath=%absoluteRootDir%\Installers\%downloadDir%

if /i "%1" == "false" set useColor=false
if /i "%useColor%" == "true" call utils.bat setESC

:: Set Flags

set rmdirFlags=/q
set roboCopyFlags=/NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul

if /i "%verbosity%"=="info" (
    set rmdirFlags=/q
    set roboCopyFlags=/NFL /NDL /NJH
)

if /i "%verbosity%"=="loud" (
    set rmdirFlags=
    set roboCopyFlags=
)

call utils.bat WriteLine "          Setting up CodeProject.AI Development Environment             " "DarkYellow" 
call utils.bat WriteLine "                                                                        " "DarkGreen" 
call utils.bat WriteLine "========================================================================" "DarkGreen" 
call utils.bat WriteLine "                                                                        " "DarkGreen" 
call utils.bat WriteLine "                   CodeProject.AI Installer                             " "DarkGreen" 
call utils.bat WriteLine "                                                                        " "DarkGreen"
call utils.bat WriteLine "========================================================================" "DarkGreen" 
call utils.bat WriteLine "                                                                        " "DarkGreen"

:: ============================================================================
:: 1. Ensure directories are created and download required assets

call utils.bat WriteLine
call utils.bat WriteLine "General CodeProject.AI setup" "DarkGreen" 

:: Create some directories
call utils.bat Write "Creating Directories..."

:: For downloading assets
if not exist "%downloadPath%\" mkdir "%downloadPath%"
call utils.bat WriteLine "Done" "Green"


:: TextSummary specific ::::::::::::::::::::::::::::::::::::::::::::::::::::::: 

call utils.bat WriteLine
call utils.bat WriteLine "TextSummary setup" "DarkGreen" 
call utils.bat WriteLine

:: The name of the dir containing the TextSummary module
set moduleDir=TextSummary

:: Full path to the TextSummary dir
set modulePath=%analysisLayerPath%\%moduleDir%

call utils.bat SetupPython 3.7
call utils.bat InstallPythonPackages 3.7 "%modulePath%" "nltk"


:: Background Remover :::::::::::::::::::::::::::::::::::::::::::::::::::::::::

call utils.bat WriteLine
call utils.bat WriteLine "Background Remover setup" "DarkGreen" 
call utils.bat WriteLine

:: The name of the dir containing the Background Remover module
set moduleDir=BackgroundRemover

:: The full path of the background remover module
set modulePath=%analysisLayerPath%\%moduleDir%

:: The name of the dir containing the background remover models
set moduleAssetsDir=models

:: The name of the file in our S3 bucket containing the assets required for this module
set modelsAssetFilename=rembg-models.zip

:: Install python and the required dependencies
call utils.bat SetupPython 3.9
call utils.bat InstallPythonPackages 3.9 "%modulePath%" "onnxruntime"

:: Clean up directories to force a download and re-copy if necessary
if /i "%forceOverwrite%" == "true" (
    if exist "%downloadPath%\%moduleDir%"     rmdir /s %rmdirFlags% "%downloadPath%\%moduleDir%"
    if exist "%modulePath%\%moduleAssetsDir%" rmdir /s %rmdirFlags% "%modulePath%\%moduleAssetsDir%"
)

:: Location of models as per original repo
:: u2netp:          https://drive.google.com/uc?id=1tNuFmLv0TSNDjYIkjEdeH1IWKQdUA4HR
:: u2net:           https://drive.google.com/uc?id=1tCU5MM1LhRgGou5OpmpjBQbSrYIUoYab
:: u2net_human_seg: https://drive.google.com/uc?id=1ZfqwVxu-1XWC1xU1GHIP-FM_Knd_AX5j
:: u2net_cloth_seg: https://drive.google.com/uc?id=15rKbQSXQzrKCQurUjZFg8HqzZad8bcyz

if not exist "%modulePath%\%moduleAssetsDir%" (
    call utils.bat Download "%storageUrl%" "%downloadPath%\" "%modelsAssetFilename%" "%moduleDir%" ^
                   "Downloading Background Remover models..."
    if exist "%downloadPath%\%modulesDir%" (
        robocopy /e "%downloadPath%\%moduleDir% " "%modulePath%\%moduleAssetsDir% " !roboCopyFlags! > NUL
    )
)


:: For DeepStack Vision AI :::::::::::::::::::::::::::::::::::::::::::::::::

call utils.bat WriteLine
call utils.bat WriteLine "Vision toolkit setup" "DarkGreen" 
call utils.bat WriteLine

:: The name of the dir containing the Deepstack Vision modules
set moduleDir=Vision

:: The full path of the Deepstack Vision modules
set modulePath=%analysisLayerPath%\%moduleDir%

:: The name of the dir containing the AI models themselves
set moduleAssetsDir=assets

:: The name of the file in our S3 bucket containing the assets required for this module
set modelsAssetFilename=models.zip

:: Install python and the required dependencies
call utils.bat SetupPython 3.7
call utils.bat InstallPythonPackages 3.7 "%modulePath%\intelligencelayer" "torch"

:: Clean up directories to force a download and re-copy if necessary
if /i "%forceOverwrite%" == "true" (
    REM Force Re-download, then force re-copy of downloads to install dir
    if exist "%downloadPath%\%moduleDir%"     rmdir /s %rmdirFlags% "%downloadPath%\%moduleDir%"
    if exist "%modulePath%\%moduleAssetsDir%" rmdir /s %rmdirFlags% "%modulePath%\%moduleAssetsDir%"
)

if not exist "%modulePath%\%moduleAssetsDir%" (
    call utils.bat Download "%storageUrl%" "%downloadPath%\" "%modelsAssetFilename%" "%moduleDir%" ^
                   "Downloading Vision models..."
    if exist "%downloadPath%\%moduleDir%" (
        robocopy /e "%downloadPath%\%moduleDir% " "%modulePath%\%moduleAssetsDir% " !roboCopyFlags! > NUL
    )
)

:: Deepstack needs these to store temp and pesrsisted data
if not exist "%modulePath%\tempstore\" mkdir "%modulePath%\tempstore"
if not exist "%modulePath%\datastore\" mkdir "%modulePath%\datastore"


:: For CodeProject's YOLO ObjectDetector :::::::::::::::::::::::::::::::::::::::::::::

call utils.bat WriteLine
call utils.bat WriteLine "Object Detector setup" "DarkGreen" 
call utils.bat WriteLine

:: The name of the dir containing the Object Detector module. Yes, some brevity here would be good
set moduleDir=CodeProject.AI.AnalysisLayer.Yolo

:: The full path of the Object Detector module
set modulePath=%analysisLayerPath%\%moduleDir%

:: The name of the dir containing the AI models themselves
set moduleAssetsDir=assets

:: The name of the file in our S3 bucket containing the assets required for this module
set modelsAssetFilename=yolonet-models.zip

:: Clean up directories to force a download and re-copy if necessary
if /i "%forceOverwrite%" == "true" (
    REM Force Re-download, then force re-copy of downloads to install dir
    if exist "%downloadPath%\%moduleDir%"     rmdir /s %rmdirFlags% "%downloadPath%\%moduleDir%"
    if exist "%modulePath%\%moduleAssetsDir%" rmdir /s %rmdirFlags% "%modulePath%\%moduleAssetsDir%"
)

if not exist "%modulePath%\%moduleAssetsDir%" (
    call utils.bat Download "%storageUrl%" "%downloadPath%\" "%modelsAssetFilename%" "%moduleDir%" ^
                   "Downloading Vision models..."
    if exist "%downloadPath%\%moduleDir%" (
        robocopy /e "%downloadPath%\%moduleDir% " "%modulePath%\%moduleAssetsDir% " !roboCopyFlags! > NUL
    )
)

call utils.bat WriteLine
call utils.bat WriteLine "Modules and models downloaded" "Green"


:: ============================================================================
:: and we're done.

call utils.bat WriteLine 
call utils.bat WriteLine "                Development Environment setup complete                  " "White" "DarkGreen"
call utils.bat WriteLine 