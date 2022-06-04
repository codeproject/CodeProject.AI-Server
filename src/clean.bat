:: CodeProject SenseAI Server and Analysis modules: Cleans debris, properly, for clean build
::
:: Usage:
::   clean_for_build.bat
::
:: We assume we're in the /src directory

@echo off
cls
setlocal enabledelayedexpansion

set cleanBuild=false
set cleanInstallLocal=false
set cleanInstallAll=false
set cleanAll=false

if /i "%1" == "build"      set cleanBuild=true
if /i "%1" == "install"    set cleanInstallLocal=true
if /i "%1" == "installall" set cleanInstallAll=true
if /i "%1" == "all"        set cleanAll=true

if /i "!cleanAll!" == "true"          set cleanInstallAll=true
if /i "!cleanInstallAll!" == "true"   set cleanInstallLocal=true
if /i "!cleanInstallLocal!" == "true" set cleanBuild=true

if /i "%cleanBuild%" == "true" (
    
    echo.
    echo Cleaning Build
    echo.

    call :CleanSubDirs "." "bin" "AnalysisLayer\bin"
    call :CleanSubDirs "." "obj" "AnalysisLayer\bin"
    call :CleanSubDirs "..\Installers" "bin"
    call :CleanSubDirs "..\Installers" "obj"
    call :CleanSubDirs "..\demos" "bin"
    call :CleanSubDirs "..\demos" "obj"
    call :CleanSubDirs "..\tests" "bin"
    call :CleanSubDirs "..\tests" "obj"
)

if /i "%cleanInstallLocal%" == "true" (

    echo.
    echo Cleaning Windows install
    echo.

    call :CleanSubDirs "AnalysisLayer\bin" "windows" "linux"
    call :CleanSubDirs "AnalysisLayer\BackgroundRemover" "models"
    call :CleanSubDirs "AnalysisLayer\CodeProject.SenseAI.AnalysisLayer.Yolo" "assets"
    call :CleanSubDirs "AnalysisLayer\DeepStack" "assets"
    call :CleanSubDirs "AnalysisLayer\DeepStack" "datastore"
    call :CleanSubDirs "AnalysisLayer\DeepStack" "tempstore"
)

if /i "%cleanInstallAll%" == "true" (

    echo.
    echo Cleaning install for other platforms
    echo.

    call :CleanSubDirs "AnalysisLayer" "bin"
)

if /i "%cleanInstallAll%" == "true" (

    echo.
    echo Cleaning downloads
    echo.
    call :CleanSubDirs "..\Installers" "downloads"
)

goto:eof


:CleanSubDirs
    SetLocal EnableDelayedExpansion
	
    set BasePath=%~1
    pushd !BasePath!

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
            @rmdir /s /q "%%i"
            echo Removed !dirName!
        ) else (
            REM echo Not removing !dirName!
        )
    )

    popd

    exit /b
