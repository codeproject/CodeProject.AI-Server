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
    call "!pwd!\utils.bat" WriteLine "clean [build : assets : install : installall : downloads : all]"
    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "  build          - cleans build output (bin / obj)"
    call "!pwd!\utils.bat" WriteLine "  install        - removes installation stuff, current OS (Python, PIPs, downloads etc)"
    call "!pwd!\utils.bat" WriteLine "  installall     - removes installation stuff for all OSs"
    call "!pwd!\utils.bat" WriteLine "  assets         - removes assets that were downloaded and moved into place"
    call "!pwd!\utils.bat" WriteLine "  download-cache - removes download cache to force re-download"
    call "!pwd!\utils.bat" WriteLine "  all            - removes build and installation stuff for all OSs"
    call "!pwd!\utils.bat" WriteLine 
    exit /b
)


set cleanBuild=false
set cleanAssets=false
set cleanDownloadCache=false
set cleanInstallCurrentOS=false
set cleanInstallAll=false
set cleanAll=false

if /i "%1" == "build"          set cleanBuild=true
if /i "%1" == "assets"         set cleanAssets=true
if /i "%1" == "download-cache" set cleanDownloadCache=true
if /i "%1" == "install"        set cleanInstallCurrentOS=true
if /i "%1" == "installall"     set cleanInstallAll=true
if /i "%1" == "all"            set cleanAll=true

REM if /i "!cleanAll!" == "true"          set cleanInstallAll=true
REM if /i "!cleanInstallAll!" == "true"   set cleanInstallCurrentOS=true
REM if /i "!cleanInstallCurrentOS!" == "true" set cleanBuild=true

if /i "!cleanAll!" == "true" (
    set cleanInstallAll=true
    set cleanBuild=true
    set cleanAssets=true
    set cleanDownloadCache=true
)

if /i "!cleanInstallCurrentOS!" == "true" set cleanAssets=true
if /i "!cleanInstallAll!" == "true"       set cleanAssets=true


if /i "%cleanBuild%" == "true" (
       
    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Build                                                      " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src"   "obj"
    call :CleanSubDirs "!rootDir!\Installers\Windows"  "bin"
    call :CleanSubDirs "!rootDir!\Installers\Windows"  "obj"
    call :CleanSubDirs "!rootDir!\demos" "bin"
    call :CleanSubDirs "!rootDir!\demos" "obj"
    call :CleanSubDirs "!rootDir!\tests" "bin"
    call :CleanSubDirs "!rootDir!\tests" "obj"
)

if /i "%cleanInstallCurrentOS%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Windows Install                                            " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src\runtimes\bin"           "windows" 
)

if /i "%cleanInstallAll%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning install for other platforms                                " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src\runtimes" "bin"
)

if /i "%cleanAssets%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Assets                                                     " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src\modules\ALPR"                  "paddleocr"
    call :CleanSubDirs "!rootDir!\src\modules\BackgroundRemover"     "models"
    call :CleanSubDirs "!rootDir!\src\modules\Cartooniser"           "weights"
    call :CleanSubDirs "!rootDir!\src\modules\FaceProcessing"        "assets"
    call :CleanSubDirs "!rootDir!\src\modules\FaceProcessing"        "datastore"
    call :CleanSubDirs "!rootDir!\src\modules\ObjectDetectionTFLite" "assets"
    call :CleanSubDirs "!rootDir!\src\modules\ObjectDetectionNet"    "assets"
    call :CleanSubDirs "!rootDir!\src\modules\ObjectDetectionNet"    "custom-models"
    call :CleanSubDirs "!rootDir!\src\modules\ObjectDetectionNet"    "LocalNugets"
    call :CleanSubDirs "!rootDir!\src\modules\ObjectDetectionYolo"   "assets"
    call :CleanSubDirs "!rootDir!\src\modules\ObjectDetectionYolo"   "custom-models"
    call :CleanSubDirs "!rootDir!\src\modules\OCR"                   "paddleocr"
    call :CleanSubDirs "!rootDir!\src\modules\SceneClassifier"       "assets"
    call :CleanSubDirs "!rootDir!\src\modules\YOLOv5-3.1"            "assets"
    call :CleanSubDirs "!rootDir!\src\modules\YOLOv5-3.1"            "custom-models"

    rem debatable where this should go
    rem call :CleanFiles ".\..\downloads" "*.zip"
    rem call :CleanFiles ".\..\downloads\windows" "*.zip"
    rem call :CleanFiles ".\..\downloads\windows\python37" "*.zip"
    rem call :CleanFiles ".\..\downloads\windows\python39" "*.zip"
)

if /i "%cleanDownloadCache%" == "true" (

    call "!pwd!\utils.bat" WriteLine 
    call "!pwd!\utils.bat" WriteLine "Cleaning Downloads                                                  " "White" "Blue"
    call "!pwd!\utils.bat" WriteLine 

    call :CleanSubDirs "!rootDir!\src\downloads"  "ALPR"
    call :CleanSubDirs "!rootDir!\src\downloads"  "BackgroundRemover"
    call :CleanSubDirs "!rootDir!\src\downloads"  "Cartooniser"
    call :CleanSubDirs "!rootDir!\src\downloads"  "FaceProcessing"
    call :CleanSubDirs "!rootDir!\src\downloads"  "ObjectDetectionNet"
    call :CleanSubDirs "!rootDir!\src\downloads"  "ObjectDetectionTFLite"
    call :CleanSubDirs "!rootDir!\src\downloads"  "ObjectDetectionYolo"
    call :CleanSubDirs "!rootDir!\src\downloads"  "OCR"
    call :CleanSubDirs "!rootDir!\src\downloads"  "SceneClassifier"
    call :CleanSubDirs "!rootDir!\src\downloads"  "YOLOv5-3.1"
)

goto:eof


:CleanSubDirs
    SetLocal EnableDelayedExpansion
	
    set BasePath=%~1
    set DirToFind=%~2
    set ExcludeDirFragment=%~3

    pushd "!BasePath!"  >nul 2>nul
    if not "%ErrorLevel%" == "0" (
        call "!pwd!\utils.bat" WriteLine "Can't navigate to %cd%!BasePath! (but this is probably OK)" "!color_warn!"
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
        call "!pwd!\utils.bat" WriteLine "Can't navigate to %cd%!BasePath! (but this is probably OK)" "!color_warn!"
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
