@echo off

set /a loops=1

:while
if %loops% lss 10 (

    rem To take a peek...
    rem wmic process where name="Python" get Name,ProcessID rem call echo

    echo Terminating dotnet
    wmic process where name="dotnet" call terminate | findstr /I /C:"Method execution successful" > /dev/null
    if %errorlevel% EQU 0 set foundProcess=true
   
    echo Terminating Python.exe
    wmic process where name="Python.exe" call terminate | findstr /I /C:"Method execution successful" > /dev/null
    if %errorlevel% EQU 0 set foundProcess=true

    echo Terminating Python
    wmic process where name="Python" call terminate | findstr /I /C:"Method execution successful" > /dev/null
    if %errorlevel% EQU 0 set foundProcess=true

    echo Terminating SentimentAnalysis
    wmic process where name="SentimentAnalysis.exe" call terminate | findstr /I /C:"Method execution successful" > /dev/null
    if %errorlevel% EQU 0 set foundProcess=true

    echo Terminating PortraitFilter
    wmic process where name="PortraitFilter.exe" call terminate | findstr /I /C:"Method execution successful" > /dev/null
    if %errorlevel% EQU 0 set foundProcess=true

    echo Terminating ObjectDetectionNet
    wmic process where name="ObjectDetectionNet.exe" call terminate | findstr /I /C:"Method execution successful" > /dev/null
    if %errorlevel% EQU 0 set foundProcess=true

    REM No longer finding anything to terminate?
    if /i "!enableGPU!" NEQ "true" goto:eof

    set /a loops+=1

    echo Running loop again: attempt %loops%
)