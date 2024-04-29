@echo off
setlocal EnableDelayedExpansion
    
    set projectName=%~1
    set projectName=!projectName: =!

    rem This won't work. The variable isn't available until the next terminal session
    REM set startTime=!%projectName%_BuildStartTime!
    REM set "!projectName!_BuildStartTime="

    rem Use a really, really big hammer to get, and clear, the env variable. We
    rem use Powershell to get around the session and permission issues inherint 
    rem in set/setx
    for /f "delims=" %%i in ('powershell -Command "& { [Environment]::GetEnvironmentVariable(\"%projectName%_BuildStartTime\", \"User\")}"') do set startTime=%%i
    powershell -Command "& {[Environment]::SetEnvironmentVariable(\"%projectName%_BuildStartTime\", $null, \"User\")"}

    REM echo Env var name is %projectName%_BuildStartTime
    REM echo Value of !projectName!_BuildStartTime (startTime) is !startTime!

    set "startTime=!startTime: =0!"
    set "endTime=%time: =0%"

    set startTime=!startTime:,=.!
	 set endTime=!endTime:,=.!

    if "!startTime!" == "" set startTime=0
    if "!endTime!" == ""   set endTime=0

    rem Convert times to integers for easier calculations

    REM First we need to remove leading 0's else CMD thinks they are octal
    REM eg "08" -> 1"08" % 100 -> 108 % 100 = 8
    for /F "tokens=1-4 delims=:.," %%a in ("!startTime!") do (
       set /A "start=((((1%%a %% 100)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100"
    )
    for /F "tokens=1-4 delims=:.," %%a in ("!endTime!") do ( 
       REM If build went past midnight, add 24hrs to end time to correct time wrapping
       IF !endTime! GTR !startTime! set /A "end=((((1%%a %% 100)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
       IF !endTime! LSS !startTime! set /A "end=(((((1%%a %% 100)+24)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
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

    rem echo ------ !projectName! build start at !startTime! -----
    echo ------ !projectName! build complete at !endTime! -----
    echo        Build duration : %DURATION% 
