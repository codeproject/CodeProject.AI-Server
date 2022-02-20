:: ===============================================================================================
::
:: CodeProject SenseAI Server script to loasaved environment variables to a config file
::
:: Copyright CodeProject 2021
::
:: ===============================================================================================


@echo off
SETLOCAL EnableDelayedExpansion

set envConfigFile=CodeProject.SenseAI.json
if not "%1" == "" set envConfigFile=%1
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
echo  }
) > "!envConfigFile!"