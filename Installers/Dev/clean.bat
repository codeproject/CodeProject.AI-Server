:: CodeProject.AI Server and Analysis modules: Cleans debris, properly, for clean build
::
:: Usage:
::   clean_for_build.bat
::
:: We assume we're in the /src directory

@echo off
cls
setlocal enabledelayedexpansion

set pwd=%cd%
set useColor=true
set doDelete=true

if "%1" == "" (
    call utils.bat WriteLine "Solution Cleaner" "White"
    call utils.bat WriteLine 
    call utils.bat WriteLine "clean [build : install : installall : all]"
    call utils.bat WriteLine 
    call utils.bat WriteLine "  build      - cleans build output (bin / obj)"
    call utils.bat WriteLine "  install    - removes current OS installation stuff (Python, PIPs, downloads etc)"
    call utils.bat WriteLine "  installall - removes installation stuff for all platforms"
    call utils.bat WriteLine "  all        - removes build and installation stuff for all platforms"
    call utils.bat WriteLine 
    exit /b
)


set cleanBuild=false
set cleanInstallLocal=false
set cleanInstallAll=false
set cleanAll=false


if /i "%1" == "build"      set cleanBuild=true
if /i "%1" == "install"    set cleanInstallLocal=true
if /i "%1" == "installall" set cleanInstallAll=true
if /i "%1" == "all"        set cleanAll=true

REM if /i "!cleanAll!" == "true"          set cleanInstallAll=true
REM if /i "!cleanInstallAll!" == "true"   set cleanInstallLocal=true
REM if /i "!cleanInstallLocal!" == "true" set cleanBuild=true

if /i "!cleanAll!" == "true" (
    set cleanInstallAll=true
    set cleanBuild=true
)

if /i "%cleanBuild%" == "true" (
       
    call utils.bat WriteLine 
    call utils.bat WriteLine "Cleaning Build                                                      " "White" "Blue"
    call utils.bat WriteLine 

    call :CleanSubDirs ".\..\..\src" "bin" "AnalysisLayer\bin"
    call :CleanSubDirs ".\..\..\src" "obj" "ObjectDetectionNet"
    call :CleanSubDirs ".\..\Windows" "bin"
    call :CleanSubDirs ".\..\Windows" "obj"
    call :CleanSubDirs ".\..\..\demos" "bin"
    call :CleanSubDirs ".\..\..\demos" "obj" "Objects"
    call :CleanSubDirs ".\..\..\tests" "bin"
    call :CleanSubDirs ".\..\..\tests" "obj"
)

if /i "%cleanInstallLocal%" == "true" (

    call utils.bat WriteLine 
    call utils.bat WriteLine "Cleaning Windows Install                                            " "White" "Blue"
    call utils.bat WriteLine 

    call :CleanSubDirs "..\..\src\AnalysisLayer\bin" "windows" 
    call :CleanSubDirs "..\..\src\AnalysisLayer\BackgroundRemover" "models"
    call :CleanSubDirs "..\..\src\AnalysisLayer\ObjectDetectionNet" "assets"
    call :CleanSubDirs "..\..\src\AnalysisLayer\Vision" "assets"
    call :CleanSubDirs "..\..\src\AnalysisLayer\Vision" "datastore"
    call :CleanSubDirs "..\..\src\AnalysisLayer\Vision" "tempstore"
)

if /i "%cleanInstallAll%" == "true" (

    call utils.bat WriteLine 
    call utils.bat WriteLine "Cleaning install for other platforms                                " "White" "Blue"
    call utils.bat WriteLine 

    call :CleanSubDirs "..\..\src\AnalysisLayer" "bin"
)

if /i "%cleanInstallAll%" == "true" (

    call utils.bat WriteLine 
    call utils.bat WriteLine "Cleaning Downloads                                                  " "White" "Blue"
    call utils.bat WriteLine 

    call :CleanSubDirs ".." "downloads"
)

goto:eof


:CleanSubDirs
    SetLocal EnableDelayedExpansion
	
    set BasePath=%~1

    REM echo Moving from %cd% to !BasePath!

    pushd !BasePath!  >nul 2>nul
    if not "%errorlevel%" == "0" (
        call %pwd%\utils.bat WriteLine "Can't navigate to %cd%!BasePath!" "Red"
        popd
        exit /b %errorlevel%
    )
    
    REM  echo Checking %cd%

    set DirPattern=%~2
    set ExcludeDirPattern=%~3

    REM Loop through all subdirs recursively
    for /d /r %%i in (*!DirPattern!*) do (

        set dirName=%%i

        REM Check for exclusions
        set remove=true
        if not "!ExcludeDirPattern!" == "" (
            if not "!dirName:%ExcludeDirPattern%=!" == "!dirName!" set remove=false
        )

        REM Do the deed
        if /i "!remove!" == "true" (
            if /i "!doDelete!" == "true" @rmdir /s /q "%%i"
            if "%errorlevel%" == "0" (
                call %pwd%\utils.bat WriteLine "Removed !dirName!" "DarkGreen"
            ) else (
                call %pwd%\utils.bat WriteLine "Unable to remove!dirName!" "DarkYellow"
            )
        ) else (
             REM call %pwd%\utils.bat WriteLine "Not removing remove!dirName!" "Gray"
        )
    )

    if /i "!doPopDirectory!"=="true" popd

    exit /b
