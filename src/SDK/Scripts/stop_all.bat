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
    REM wmic process where "Name like 'python%' and ExecutablePath like '%src\\runtimes%%'" delete
    wmic process where "ExecutablePath like '%srcDir%\\runtimes%%'" delete
    wmic process where "ExecutablePath like '%srcDir%\\modules%%'" delete

    REM Count how many are left.
    Set /a NumModules=0
    For /f %%j in ('wmic process where "ExecutablePath like ^'!srcDir!\\modules%%^'" get ExecutablePath ^| Find  /c "modules"') Do Set /a NumModules=%%j

    Set /a NumRuntimes=0
    For /f %%j in ('wmic process where "ExecutablePath like ^'!srcDir!\\runtimes%%^'" get ExecutablePath ^| Find  /c "runtimes"') Do Set /a NumRuntimes=%%j

    if %NumModules% EQU 0 (
        if %NumRuntimes% EQU 0 goto endModules
    )
    
    set /a loops+=1
    echo Running loop again: attempt !loops!

    goto whileModules

:endModules
