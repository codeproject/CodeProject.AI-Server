:: ===============================================================================================
::
:: CodeProject SenseAI Server script to load environment variables from store
::
:: Copyright CodeProject 2021
::
:: ===============================================================================================

@echo off
SETLOCAL EnableDelayedExpansion

set verbosity=loud

:: The name of the Environment variable setup file
set envVariablesFile=CodeProject.SenseAI.json
if not "%1" == "" set envVariablesFile=%1
set envVariablesFile=!envVariablesFile:"=!

if not exist !envVariablesFile! goto errorNoSettingsFile

:: Load Json string from settings file

REM Need to handle quotes around filenames. See https://stackoverflow.com/a/22636725
REM for /f "tokens=*" %%x in (%envVariablesFile%) do (

(
    for /f "tokens=*" %%x in (' more ^< "%envVariablesFile%" ') do (
        set line=%%x
        rem remove quotes, change " : " to "=", remove spaces
        set line=!line:"=!
        set line=!line: : ==!
        set line=!line: =!
	    if not "!line:~0,1!" == "{" (
    	    if not "!line:~0,1!" == "}" (
                if "!line:~-1!" == "," set line=!line:~0,-1!
                echo set !line!
            )
        )
    )
) > "!envVariablesFile!.bat"
call !envVariablesFile!.bat
del !envVariablesFile!.bat

goto:eof

:errorNoSettingsFile
Echo:
Echo:
Echo ---------------------------------------------------------------------------
Echo Error: %settingsFile% settings file not found
Echo Ensure you have run setup_dev_env_win.bat before running this script