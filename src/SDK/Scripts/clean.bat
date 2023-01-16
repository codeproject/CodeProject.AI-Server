:: CodeProject.AI Server and Analysis modules: Cleans debris, properly, for clean build
::
:: Usage:
::   clean [build | install | installall | downloads | all]
::
:: We assume we're in the /Installers/Dev directory

@echo off
cls
setlocal enabledelayedexpansion

set pwd=%cd%
pushd ..\..\..
set rootDir=%cd%
popd

set useColor=true
set doDebug=false

if "%1" == "" (
    call "!pwd!\utils.bat" WriteLine "Solution Cleaner" "White"
    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "clean [build : install : installall : downloads : all]"
    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "  build      - cleans build output (bin / obj)"
    call "!pwd!\utils.bat" WriteLine "  install    - removes current OS installation stuff (Python, PIPs, downloads etc)"
    call "!pwd!\utils.bat" WriteLine "  installall - removes installation stuff for all platforms"
    call "!pwd!\utils.bat" WriteLine "  downloads  - removes downloads to force re-download"
    call "!pwd!\utils.bat" WriteLine "  all        - removes build and installation stuff for all platforms"
    call "!pwd!\utils.bat" WriteLine 
    exit /b
)


set cleanBuild=false
set cleanAssets=false
set cleanDownloads=false
set cleanInstallLocal=false
set cleanInstallAll=false
set cleanAll=false

if /i "%1" == "build"      set cleanBuild=true
if /i "%1" == "assets"     set cleanAssets=true
if /i "%1" == "downloads"  set cleanDownloads=true
if /i "%1" == "install"    set cleanInstallLocal=true
if /i "%1" == "installall" set cleanInstallAll=true
if /i "%1" == "all"        set cleanAll=true

REM if /i "!cleanAll!" == "true"          set cleanInstallAll=true
REM if /i "!cleanInstallAll!" == "true"   set cleanInstallLocal=true
REM if /i "!cleanInstallLocal!" == "true" set cleanBuild=true

if /i "!cleanAll!" == "true" (
    set cleanInstallAll=true
    set cleanBuild=true
    set cleanAssets=true
    set cleanDownloads=true
)

if /i "!cleanInstallLocal!" == "true" set cleanAssets=true
if /i "!cleanInstallAll!" == "true"   set cleanAssets=true


if /i "%cleanBuild%" == "true" (
       
    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Build                                                      " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src"   "bin" "\AnalysisLayer\bin"
    call :CleanSubDirs "!rootDir!\src"   "obj"
    call :CleanSubDirs "!rootDir!\Installers\Windows"  "bin"
    call :CleanSubDirs "!rootDir!\Installers\Windows"  "obj"
    call :CleanSubDirs "!rootDir!\demos" "bin"
    call :CleanSubDirs "!rootDir!\demos" "obj"
    call :CleanSubDirs "!rootDir!\tests" "bin"
    call :CleanSubDirs "!rootDir!\tests" "obj"
)

if /i "%cleanInstallLocal%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Windows Install                                            " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\bin"            "windows" 
    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\FaceProcessing" "datastore"
    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\FaceProcessing" "tempstore"
)

if /i "%cleanInstallAll%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning install for other platforms                                " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src\AnalysisLayer" "bin"
)

if /i "%cleanAssets%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Assets                                                     " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\BackgroundRemover"   "models"
    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\ObjectDetectionNet"  "assets"
    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\ObjectDetectionNet"  "custom-models"
    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\ObjectDetectionYolo" "assets"
    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\ObjectDetectionYolo" "custom-models"
    call :CleanSubDirs "!rootDir!\src\AnalysisLayer\FaceProcessing"      "assets"

    call :CleanSubDirs "!rootDir!\src\modules\ALPR"                      "paddleocr"
    call :CleanSubDirs "!rootDir!\src\modules\OCR"                       "paddleocr"
    call :CleanSubDirs "!rootDir!\src\modules\YOLOv5-3.1"                "assets"
    call :CleanSubDirs "!rootDir!\src\modules\YOLOv5-3.1"                "custom-models"

    REM In case you have an old install
    REM call :CleanSubDirs "!rootDir!\src\AnalysisLayer\CustomDetection" "assets" 

    rem debatable where this should go
    rem call :CleanFiles ".\..\downloads" "*.zip"
    rem call :CleanFiles ".\..\downloads\windows" "*.zip"
    rem call :CleanFiles ".\..\downloads\windows\python37" "*.zip"
    rem call :CleanFiles ".\..\downloads\windows\python39" "*.zip"
)

if /i "%cleanDownloads%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Downloads                                                  " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\Installers"       "downloads"

    call :CleanSubDirs "!rootDir!\src\modules\OCR"  "downloads"
    call :CleanSubDirs "!rootDir!\src\modules\ALPR" "downloads"
)

goto:eof


:CleanSubDirs
    SetLocal EnableDelayedExpansion
	
    set BasePath=%~1
    set DirToFind=%~2
    set ExcludeDirFragment=%~3

    pushd "!BasePath!"  >nul 2>nul
    if not "%ErrorLevel%" == "0" (
        call "!pwd!\utils.bat" WriteLine "Can't navigate to %cd%!BasePath!" "!color_error!"
        cd %pwd%
        exit /b %ErrorLevel%
    )
    
    if /i "%doDebug%" == "true" (
        if "!ExcludeDirFragment!" == "" (
            call "!pwd!\utils.bat" WriteLine "Removing folders in %cd% that match !DirToFind!" "!color_info!"
        ) else (
            call "!pwd!\utils.bat" WriteLine "Removing folders in %cd% that match !DirToFind! without !ExcludeDirFragment!" "!color_info!"
        )
    )

    REM Loop through all subdirs recursively
    for /d /r %%i in (*!DirToFind!) do (

        set dirName=%%i

        REM Check for exclusions
        set dirMatched=true
        if "!ExcludeDirFragment!" neq "" (
            if "!dirName:%ExcludeDirFragment%=!" neq "!dirName!" set dirMatched="false"

            rem If we wanted more of a regular expression based fragment check we could use:
            rem echo !dirName! | FindStr /B "!ExcludeDirFragment!"
            rem IF %ErrorLevel% equ 0 set dirMatched="false"
        )

        if /i "!dirMatched!" == "true" (

            if /i "!doDebug!" == "true" (
                call "!pwd!\utils.bat" WriteLine "Marked for removal: !dirName!" "!color_error!"
            ) else (
                rmdir /s /q "!dirName!";
            
                if exist "!dirName!" (
                    call "!pwd!\utils.bat" WriteLine "Unable to remove !dirName!"  "!color_error!"
                ) else (
                    call "!pwd!\utils.bat" WriteLine "Removed !dirName!" "!color_success!"
                )
            )
        ) else (
            if /i "!doDebug!" == "true" (
                call "!pwd!\utils.bat" WriteLine "Not deleting !dirName!" "!color_success!"
            )
        )
    )

    exit /b

:CleanFiles
    SetLocal EnableDelayedExpansion
	
    set BasePath=%~1
    set ExcludeFilePattern=%~2

    pushd "!BasePath!"  >nul 2>nul
    if not "%errorlevel%" == "0" (
        call "!pwd!\utils.bat" WriteLine "Can't navigate to %cd%!BasePath! (but that's probably OK)" "!color_warn!"
        cd %pwd%
        exit /b %errorlevel%
    )
    
    if /i "%doDebug%" == "true" (
        if "!ExcludeFilePattern!" == "" (
            call "!pwd!\utils.bat" WriteLine "Removing all files in %cd%" "!color_info!"
        ) else (
            call "!pwd!\utils.bat" WriteLine "Removing files in %cd% that don't match !ExcludeFilePattern!" "!color_info!"
        )
    )

    REM Loop through all files in this dir
    for %%i in (*) do (

        set fileName=%%i

        REM Check for exclusions
        set fileMatched=true
        if not "!ExcludeFilePattern!" == "" (
            if not "!fileName:%ExcludeFilePattern%=!" == "!fileName!" set fileMatched=false
        )

        if /i "!fileMatched!" == "true" (

            if /i "!doDebug!" == "true" (
                call "!pwd!\utils.bat" WriteLine "Marked for removal: !fileName!" !color_error!
            ) else (
                del /q "!fileName!";
            
                if exist "!fileName!" (
                    call "!pwd!\utils.bat" WriteLine "Unable to remove !fileName!"  !color_error!
                ) else (
                    call "!pwd!\utils.bat" WriteLine "Removed !fileName!" !color_success!
                )
            )
        ) else (
            if /i "!doDebug!" == "true" (
                call "!pwd!\utils.bat" WriteLine "Not deleting !fileName!" !color_success!
            )
        )
    )

    exit /b
