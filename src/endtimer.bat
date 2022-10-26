@echo off
setlocal EnableDelayedExpansion
    
    set projectName=%~1
    set projectName=!projectName: =!
    rem echo [Project Name: !projectName!]

    rem This variable isn't available until the next terminal session
    rem set starttime=!%projectName%_BuildStartTime!

    rem Use a really, really big hammer
    for /f "delims=" %%i in ('powershell -Command "& { [Environment]::GetEnvironmentVariable(\"%projectName%_BuildStartTime\", \"User\")}"') do set starttime=%%i
    rem cleanup
    powershell -Command "& {[Environment]::SetEnvironmentVariable(\"%projectName%_BuildStartTime\", $null, \"User\")"}

    REM echo [Start time: !starttime!]

    set "starttime=!starttime: =0!"
    set "endTime=%time: =0%"
    
    if "!starttime!" == "" set starttime=0
    if "!endTime!" == ""   set endTime=0

    rem Convert times to integers for easier calculations
    for /F "tokens=1-4 delims=:.," %%a in ("%starttime%") do (
       REM we need to remove leading 0's else CMD thinks they are octal
       REM eg "08" -> 1"08" % 100 -> 108 % 100 = 8
       set /A "start=((((1%%a %% 100)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100"
    )
    for /F "tokens=1-4 delims=:.," %%a in ("%endTime%") do ( 
       IF %endTime% GTR %starttime% set /A "end=((((1%%a %% 100)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
       IF %endTime% LSS %starttime% set /A "end=(((((1%%a %% 100)+24)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
    )

    rem Calculate the elapsed time 
    set /A elapsed=end-start

    rem Convert back to hr, min, sec
    set /A hh=elapsed/(60*60*100), rest=elapsed%%(60*60*100), mm=rest/(60*100), rest%%=60*100, ss=rest/100, cc=rest%%100
    if %hh% lss 10 set hh=0%hh%
    if %mm% lss 10 set mm=0%mm%
    if %ss% lss 10 set ss=0%ss%
    if %cc% lss 10 set cc=0%cc%

    set DURATION=%hh%:%mm%:%ss%.%cc%

    rem echo ------ !projectName! build start at !starttime! -----
    echo ------ !projectName! build complete at !endTime! -----
    echo        Build duration : %DURATION% 
