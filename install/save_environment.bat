:: ===============================================================================================
::
:: CodeProject SenseAI Server script to load environment variables from store
::
:: Copyright CodeProject 2021
::
:: ===============================================================================================


@echo off
SETLOCAL EnableDelayedExpansion

:: The name of the Environment variable setup file
set envVariablesFile=set_environment.bat
if not "%1" == "" set envVariablesFile=%1
set envVariablesFile=!envVariablesFile:"=!

set envConfigFile=CodeProject.SenseAI.json
if not "%2" == "" set envConfigFile=%2
set envConfigFile=!envConfigFile:"=!

(
echo  {
REM SenseAI Application values
echo    "CPSENSEAI_ROOTDIR"     : "!CPSENSEAI_ROOTDIR!",
echo    "CPSENSEAI_APPDIR"      : "!CPSENSEAI_APPDIR!",
echo    "CPSENSEAI_APIDIR"      : "!CPSENSEAI_APIDIR!",
echo    "CPSENSEAI_ANALYSISDIR" : "!CPSENSEAI_ANALYSISDIR!",
echo    "CPSENSEAI_PORT"        : "!PORT!",
echo    "CPSENSEAI_PROFILE"     : "!PROFILE!",
echo    "CPSENSEAI_MODULES"     : "!CPSENSEAI_MODULES!",
echo    "CPSENSEAI_PRODUCTION"  : "!CPSENSEAI_PRODUCTION!",
echo    "CPSENSEAI_CONFIG"      : "!CPSENSEAI_CONFIG!",
echo    "CPSENSEAI_BUILDSERVER" : "!CPSENSEAI_BUILDSERVER!",

REM DeepStack compatible values
echo    "APPDIR"             : "!APPDIR!",
echo    "PROFILE"            : "!PROFILE!",
echo    "CUDA_MODE"          : "!CUDA_MODE!",
echo    "DATA_DIR"           : "!DATA_DIR!",
echo    "TEMP_PATH"          : "!TEMP_PATH!",
echo    "MODELS_DIR"         : "!MODELS_DIR!",
echo    "PORT"               : "!PORT!",
echo    "VISION_FACE"        : "!VISION_FACE!",
echo    "VISION_DETECTION"   : "!VISION_DETECTION!",
echo    "VISION_SCENE"       : "!VISION_SCENE!"
echo  }
) > "!envConfigFile!"

:: Also create a .BAT file for easy Starting

(
echo REM SenseAI Application values
echo if "%%CPSENSEAI_ROOTDIR%%" == "" set CPSENSEAI_ROOTDIR=!CPSENSEAI_ROOTDIR!
echo set CPSENSEAI_APPDIR=!CPSENSEAI_APPDIR!
echo set CPSENSEAI_APIDIR=!CPSENSEAI_APIDIR!
echo set CPSENSEAI_ANALYSISDIR=!CPSENSEAI_ANALYSISDIR!
echo set CPSENSEAI_PORT=!PORT!
echo set CPSENSEAI_PROFILE=!PROFILE!
echo set CPSENSEAI_MODULES=!CPSENSEAI_MODULES!
echo set CPSENSEAI_PRODUCTION=!CPSENSEAI_PRODUCTION!
echo set CPSENSEAI_CONFIG=!CPSENSEAI_CONFIG!
echo set CPSENSEAI_BUILDSERVER=!CPSENSEAI_BUILDSERVER!

echo REM DeepStack compatible values
echo set APPDIR=!APPDIR!
echo set PROFILE=!PROFILE!
echo set CUDA_MODE=!CUDA_MODE!
echo set DATA_DIR=!DATA_DIR!
echo set TEMP_PATH=!TEMP_PATH!
echo set MODELS_DIR=!MODELS_DIR!
echo set PORT=!PORT!
echo set VISION_FACE=!VISION_FACE!
echo set VISION_DETECTION=!VISION_DETECTION!
echo set VISION_SCENE=!VISION_SCENE!
) > "!envVariablesFile!"