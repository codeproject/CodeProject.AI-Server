@echo off
setlocal EnableDelayedExpansion

set projectName=%~1
set projectName=!projectName: =!

set "!projectName!_BuildStartTime=%time%"

rem setx vars are only available on next terminal session. Note helpful
rem setx !projectName!_BuildStartTime %time%

rem Use a really, really big hammer
powershell -Command "& {[Environment]::SetEnvironmentVariable(\"%projectName%_BuildStartTime\", \"%time%\", \"User\")"}

echo ------ !projectName! build started at !%projectName%_BuildStartTime! -----
