:: CodeProject.AI Server and Analysis modules: Replicates CustomDetection folder
:: to ensure backwards compatibility with this version
::
:: Usage:
::   Create_Custom_Folder.bat
::
:: We assume we're in the /src/AnalysisLAyer/ObjectDetectionYolo directory

@echo off
setlocal enabledelayedexpansion

REM Get the location where Blue Iris thinks the custom models reside

REM For the default location
REM pushd ..
REM set customDir=%cd%\CustomDetection\assets
REM popd

REM From the registry
set RegKey=HKLM\SOFTWARE\Perspective Software\Blue Iris\Options\AI
set RegValue=deepstack_custompath
set RegType=REG_SZ

FOR /F "usebackq tokens=1,2*" %%A in (`reg query "%RegKey%" /V %RegValue% ^|findstr /ri "%RegType%"`) do (
    set KeyName=%%A
    set ValueType=%%B
    set customDir=%%C
)

REM For testing
REM echo Found value %customDir%
REM goto:eof

REM Create this folder (if specified) and (if needed) copy into it empty copies of the actual custom models
if "!customDir!" neq "" (

    if not exist "!customDir!" mkdir "!customDir!"

    for %%I in (.\custom-models\*) do (
        set filename=%%~nxI
        if not exist "!customDir!\!filename!" copy NUL "!customDir!\!filename!" > NUL
    )
)