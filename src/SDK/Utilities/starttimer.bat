@echo off
setlocal EnableDelayedExpansion

set projectName=%~1
set projectName=!projectName: =!

REM Set the current time (the start of the build). Unfortunately this doesn't
REM persist across sessions so is lost
set "!projectName!_BuildStartTime=%time%"

rem setx variables are only available on next terminal session. Not helpful. You
rem can use the /M option to make the var system wide, but then you get permission
rem issues.
REM setx !projectName!_BuildStartTime %time%

rem Use a really, really big hammer. Powershell will do what we want. It just
rem takes forever (as in: a few seconds)
powershell -Command "& {[Environment]::SetEnvironmentVariable(\"%projectName%_BuildStartTime\", \"%time%\", \"User\")"}

REM echo "Time is %time%"
REM echo "Env var name is %projectName%_BuildStartTime"
REM echo "Value of !projectName!_BuildStartTime is !%projectName%_BuildStartTime!"

echo ------ !projectName! build started at !%projectName%_BuildStartTime! -----
