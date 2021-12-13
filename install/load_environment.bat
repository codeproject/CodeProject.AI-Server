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

:: Easy way is to just run %envVariablesFile%, but that won't provide any summary

if not exist %envVariablesFile% goto errorNoSettingsFile

:: Load Json string from settings file
for /f "tokens=*" %%x in (%envVariablesFile%) do (
    set line=%%x
	if not "!line:~0,2!" == "::" (
    	if not "!line:~0,3!" == "REM" (
    	    if "%verbosity%" == "loud" echo Config File line: !line!
            !line!
        )
    )
)

goto:eof

:errorNoSettingsFile
call :WriteLine White ""
call :WriteLine White ""
call :WriteLine White "---------------------------------------------------------------------------"
call :WriteLine Red "Error: %settingsFile% settings file not found"
call :WriteLine ORange "Ensure you have run setup_dev_env_win.bat before running this script"