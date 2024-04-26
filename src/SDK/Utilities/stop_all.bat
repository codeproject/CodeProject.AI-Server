@echo off
SetLocal EnableDelayedExpansion

set pwd=%cd%
pushd %pwd%\..\..\
set srcDir=%cd%
cd ..
set rootDir=%cd%
popd

set srcDir=%srcDir:\=\\%
set rootDir=%rootDir:\=\\%
set externalModulesDir=!rootDir!\..\CodeProject.AI-Modules

rem wmic process where "ExecutablePath like '%srcDir%\\modules%%'" get Name, ProcessID, ExecutablePath, Description

set /a loops=1
:whileModules
if %loops% GEQ 50 goto endModules

    rem List processes
    rem wmic process where "ExecutablePath like '!srcDir!\\modules%%'" get ExecutablePath

    REM Kill processes
    REM wmic process where "Name like 'python%' and ExecutablePath like '%src\\runtimes%%'" delete
    wmic process where "ExecutablePath like '!srcDir!\\runtimes%%'"        delete
    wmic process where "ExecutablePath like '!srcDir!\\modules%%'"         delete
    wmic process where "ExecutablePath like '!externalModulesDir!%%'"      delete
    wmic process where "ExecutablePath like '!rootDir!\\src\\demos\\modules%%'" delete

    REM Count how many are left.
    Set /a NumRuntimes=0
    For /f %%j in ('wmic process where "ExecutablePath like ^'!srcDir!\\runtimes%%^'" get ExecutablePath ^| Find  /c "runtimes"') Do Set /a NumRuntimes=%%j

    Set /a NumModules=0
    For /f %%j in ('wmic process where "ExecutablePath like ^'!srcDir!\\modules%%^'" get ExecutablePath ^| Find  /c "modules"') Do Set /a NumModules=%%j

    Set /a NumExternalModules=0
    For /f %%j in ('wmic process where "ExecutablePath like ^'!externalModulesDir!%%^'" get ExecutablePath ^| Find  /c "modules"') Do Set /a NumExternalModules=%%j

    Set /a NumDemos=0
    For /f %%j in ('wmic process where "ExecutablePath like ^'!rootDir!\\src\\demos\\modules%%^'" get ExecutablePath ^| Find  /c "demos"') Do Set /a NumDemos=%%j

    if %NumModules% EQU 0 if %NumExternalModules% EQU 0 if %NumRuntimes% EQU 0 if %NumDemos% EQU 0 goto endModules
    
    set /a loops+=1
    echo Running loop again: attempt !loops!

    goto whileModules

:endModules
