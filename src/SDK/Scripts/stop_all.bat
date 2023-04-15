@echo off
SetLocal EnableDelayedExpansion

set pwd=%cd%
pushd %pwd%\..\..\
set srcDir=%cd%
popd

set srcDir=%srcDir:\=\\%

rem wmic process where "ExecutablePath like '%srcDir%\\modules%%'" get Name, ProcessID, ExecutablePath, Description

set /a loops=1
:whileModules
if %loops% GEQ 50 goto endModules

    rem List processes
    rem wmic process where "ExecutablePath like '!srcDir!\\modules%%'" get ExecutablePath

    REM Kill processes
    wmic process where "ExecutablePath like '%srcDir%\\modules%%\\venv\\%%'" delete
    wmic process where "ExecutablePath like '%srcDir%\\modules%%'" delete

    REM Count how many are left.
    Set /a Number=0
    For /f %%j in ('wmic process where "ExecutablePath like ^'!srcDir!\\modules%%^'" get ExecutablePath ^| Find  /c "modules"') Do Set /a Number=%%j

    if %Number% EQU 0 goto endModules

    set /a loops+=1
    echo Running loop again: attempt !loops!

    goto whileModules

:endModules
