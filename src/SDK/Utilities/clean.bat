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
pushd ..\Scripts
set sdkDir=%cd%
popd
pushd ..\..\..
set rootDir=%cd%
popd

set externalModulesDir=!rootDir!\..\CodeProject.AI-Modules

set useColor=true
set doDebug=false
set lineWidth=70

set dotNetModules=ObjectDetectionYOLOv5Net PortraitFilter SentimentAnalysis
set pythonModules=ALPR ALPR-RKNN BackgroundRemover Cartooniser FaceProcessing LlamaChat       ^
                  ObjectDetectionCoral ObjectDetectionYOLOv5-3.1 ObjectDetectionYOLOv5-6.2    ^
                  ObjectDetectionYOLOv8 ObjectDetectionYoloRKNN TrainingObjectDetectionYOLOv5 ^
                  OCR SceneClassifier SoundClassifierTF SuperResolution TextSummary Text2Image

set dotNetExternalModules=
set pythonExternalModules=CodeProject.AI-ALPR CodeProject.AI-ObjectDetectionCoral
                          CodeProject.AI-ALPR-RKNN CodeProject.AI-ObjectDetectionYoloRKNN

set dotNetDemoModules=DotNetLongProcess DotNetSimple
set pythonDemoModules=PythonLongProcess PythonSimple

if "%1" == "" (
    call "!sdkDir!\utils.bat" WriteLine "Solution Cleaner" "White"
    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "clean [build : assets : install : installall : downloads : all]"
    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "  build          - cleans build output (bin / obj)"
    call "!sdkDir!\utils.bat" WriteLine "  install        - removes installation stuff, current OS (Python, PIPs, downloads etc)"
    call "!sdkDir!\utils.bat" WriteLine "  installall     - removes installation stuff for all OSs"
    call "!sdkDir!\utils.bat" WriteLine "  assets         - removes assets that were downloaded and moved into place"
    call "!sdkDir!\utils.bat" WriteLine "  data           - removes user data stored by modules"
    call "!sdkDir!\utils.bat" WriteLine "  download-cache - removes download cache to force re-download"
    call "!sdkDir!\utils.bat" WriteLine "  all            - removes build and installation stuff for all OSs"
    call "!sdkDir!\utils.bat" WriteLine 
    exit /b
)


set cleanBuild=false
set cleanAssets=false
set cleanUserData=false
set cleanDownloadCache=false
set cleanInstallCurrentOS=false
set cleanInstallAll=false
set cleanAll=false

if /i "%1" == "build"          set cleanBuild=true
if /i "%1" == "install"        set cleanInstallCurrentOS=true
if /i "%1" == "installall"     set cleanInstallAll=true
if /i "%1" == "assets"         set cleanAssets=true
if /i "%1" == "data"           set cleanUserData=true
if /i "%1" == "download-cache" set cleanDownloadCache=true
if /i "%1" == "all"            set cleanAll=true

REM if /i "!cleanAll!" == "true"          set cleanInstallAll=true
REM if /i "!cleanInstallAll!" == "true"   set cleanInstallCurrentOS=true
REM if /i "!cleanInstallCurrentOS!" == "true" set cleanBuild=true

if /i "!cleanAll!" == "true" (
    set cleanInstallAll=true
    set cleanBuild=true
    set cleanUserData=true
    set cleanAssets=true
    set cleanDownloadCache=true
)

if /i "!cleanInstallCurrentOS!" == "true" (
    set cleanBuild=true
    set cleanAssets=true
    set cleanUserData=true
)

if /i "!cleanInstallAll!" == "true" (
    set cleanBuild=true
    set cleanAssets=true
    set cleanUserData=true
)


if /i "%cleanBuild%" == "true" (
       
    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "Cleaning Build" "White" "Blue" !lineWidth!
    call "!sdkDir!\utils.bat" WriteLine 

    call :RemoveDir "!rootDir!\src\server\bin\"
    call :RemoveDir "!rootDir!\src\server\obj"

    call :RemoveDir "!rootDir!\src\SDK\NET\bin\"
    call :RemoveDir "!rootDir!\src\SDK\NET\obj"

    for %%x in (!dotNetModules!) do (
        call :RemoveDir "!rootDir!\src\modules\%%x\bin\"
        call :RemoveDir "!rootDir!\src\modules\%%x\obj\"
        del "!rootDir!\src\modules\%%x\%%x-*"
    )
    for %%x in (!dotNetExternalModules!) do (
        call :RemoveDir "!externalModulesDir!\%%x\bin\"
        call :RemoveDir "!externalModulesDir!\%%x\obj\"
        del "!rootDir!\src\modules\%%x\%%x-*"
    )
    for %%x in (!dotNetDemoModules!) do (
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\bin\"
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\obj\"
        del "!rootDir!\src\demos\modules\%%x\%%x-*"
    )

    call :CleanSubDirs "!rootDir!\Installers\Windows\" "\bin\Debug\"
    call :CleanSubDirs "!rootDir!\Installers\Windows\" "\bin\Release\"
    call :CleanSubDirs "!rootDir!\Installers\Windows\" "\obj\Debug\"
    call :CleanSubDirs "!rootDir!\Installers\Windows\" "\obj\Release\"

    call :RemoveDir "!rootDir!\src\SDK\Utilities\ParseJSON\bin"
    call :RemoveDir "!rootDir!\src\SDK\Utilities\ParseJSON\obj"
    del "!rootDir!\src\SDK\Utilities\ParseJSON\ParseJSON.deps.json"
    del "!rootDir!\src\SDK\Utilities\ParseJSON\ParseJSON.dll"
    del "!rootDir!\src\SDK\Utilities\ParseJSON\ParseJSON.exe"
    del "!rootDir!\src\SDK\Utilities\ParseJSON\ParseJSON.runtimeconfig.json"
    del "!rootDir!\src\SDK\Utilities\ParseJSON\ParseJSON.xml"

    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\bin\Debug\"
    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\bin\Release\"
    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\obj\Debug\"
    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\obj\Release\"

    call :RemoveDir "!rootDir!\tests\QueueServiceTests\bin\"
    call :RemoveDir "!rootDir!\tests\QueueServiceTests\obj\"
)

if /i "%cleanInstallCurrentOS%" == "true" (

    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "Cleaning Windows Install" "White" "Blue" !lineWidth!
    call "!sdkDir!\utils.bat" WriteLine 

    REM Clean shared python venvs
    call :RemoveDir "!rootDir!\src\runtimes\bin\windows" 

    REM Clean module python venvs
    for %%x in (!pythonModules!) do (
        call :RemoveDir "!rootDir!\src\modules\%%x\bin\windows"
    )
    for %%x in (!pythonExternalModules!) do (
        call :RemoveDir "!externalModulesDir!\%%x\bin\windows"
    )
    for %%x in (!pythonDemoModules!) do (
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\bin\windows"
    )
)

if /i "%cleanUserData%" == "true" (

    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "Cleaning User data" "White" "Blue" !lineWidth!
    call "!sdkDir!\utils.bat" WriteLine 

    call :RemoveDir "!rootDir!\src\modules\FaceProcessing\datastore"
)

if /i "%cleanInstallAll%" == "true" (

    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "Cleaning install for other platforms" "White" "Blue" !lineWidth!
    call "!sdkDir!\utils.bat" WriteLine 

    REM Clean shared python installs and venvs
    call :RemoveDir "!rootDir!\src\runtimes\bin\" 

    REM Clean module python venvs
    for %%x in (!pythonModules!) do (
        call :RemoveDir "!rootDir!\src\modules\%%x\bin\"
    )
    for %%x in (!pythonExternalModules!) do (
        call :RemoveDir "!externalModulesDir!\%%x\bin\"
    )
    for %%x in (!pythonDemoModules!) do (
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\bin\"
    )
)

if /i "%cleanAssets%" == "true" (

    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "Cleaning Assets" "White" "Blue" !lineWidth!
    call "!sdkDir!\utils.bat" WriteLine 

    REM Production modules
    call :RemoveDir "!rootDir!\src\modules\BackgroundRemover\models"
    call :RemoveDir "!rootDir!\src\modules\Cartooniser\weights"
    call :RemoveDir "!rootDir!\src\modules\FaceProcessing\assets"
    call :RemoveDir "!rootDir!\src\modules\LlamaChat\models"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv5-3.1\assets"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv5-3.1\custom-models"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv5-6.2\assets"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv5-6.2\custom-models"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv8\assets"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv8\custom-models"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv5Net\assets"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv5Net\custom-models"
    call :RemoveDir "!rootDir!\src\modules\ObjectDetectionYOLOv5Net\LocalNugets"
    call :RemoveDir "!rootDir!\src\modules\OCR\paddleocr"
    call :RemoveDir "!rootDir!\src\modules\SceneClassifier\assets"
    call :RemoveDir "!rootDir!\src\modules\SoundClassifierTF\data"
    call :RemoveDir "!rootDir!\src\modules\Text2Image\assets"
    call :RemoveDir "!rootDir!\src\modules\TrainingObjectDetectionYOLOv5\assets"
    call :RemoveDir "!rootDir!\src\modules\TrainingObjectDetectionYOLOv5\datasets"
    call :RemoveDir "!rootDir!\src\modules\TrainingObjectDetectionYOLOv5\fiftyone"
    call :RemoveDir "!rootDir!\src\modules\TrainingObjectDetectionYOLOv5\training"
    call :RemoveDir "!rootDir!\src\modules\TrainingObjectDetectionYOLOv5\zoo"

    REM External modules
    call :RemoveDir "!externalModulesDir!\ALPR\paddleocr"
    call :RemoveDir "!externalModulesDir!\ALPR-RKNN\paddleocr"
    call :RemoveDir "!externalModulesDir!\ObjectDetectionCoral\assets"
    call :RemoveDir "!externalModulesDir!\ObjectDetectionCoral\edgetpu_runtime"
    call :RemoveDir "!externalModulesDir!\ObjectDetectionYoloRKNN\assets"
    call :RemoveDir "!externalModulesDir!\ObjectDetectionYoloRKNN\custom-models"

    REM Demo modules
    call :RemoveDir "!rootDir!\src\demos\modules\DotNetLongProcess\assets"
    call :RemoveDir "!rootDir!\src\demos\modules\DotNetSimple\assets"
    call :RemoveDir "!rootDir!\src\demos\modules\PythonLongProcess\assets"
    call :RemoveDir "!rootDir!\src\demos\modules\PythonSimple\assets"
)

if /i "%cleanDownloadCache%" == "true" (

    call "!sdkDir!\utils.bat" WriteLine 
    call "!sdkDir!\utils.bat" WriteLine "Cleaning Downloads" "White" "Blue" !lineWidth!
    call "!sdkDir!\utils.bat" WriteLine 

    rem delete downloads for each module
    FOR /d %%a IN ("%rootDir%\src\downloads\*") DO (
        IF /i NOT "%%~nxa"=="modules" IF /i NOT "%%~nxa"=="models" call :RemoveDir "%%a"
    )
    rem delete module packages downloads 
    FOR %%a IN ("%rootDir%\src\downloads\modules\*") DO (
        IF /i NOT "%%~nxa"=="readme.txt" call :RemoveFile "%%a"
    )
    rem delete model packages downloads 
    FOR %%a IN ("%rootDir%\src\downloads\models\*") DO (
        IF /i NOT "%%~nxa"=="models.json" call :RemoveFile "%%a"
    )
)

goto:eof

:RemoveFile
    SetLocal EnableDelayedExpansion

    set filePath=%~1

    if /i "!doDebug!" == "true" (
        call "!sdkDir!\utils.bat" WriteLine "Marked for removal: !filePath!" "!color_error!"
    ) else (
        if exist "!filePath!" (
            del "!filePath!"
            call "!sdkDir!\utils.bat" WriteLine "Removed !dirPath!" "!color_success!"
        ) else (
            call "!sdkDir!\utils.bat" WriteLine "Not Removing !filePath! (it doesn't exist)" "!color_mute!"
        )
    )


:RemoveDir
    SetLocal EnableDelayedExpansion

    set dirPath=%~1

    if /i "!doDebug!" == "true" (
        call "!sdkDir!\utils.bat" WriteLine "Marked for removal: !dirPath!" "!color_error!"
    ) else (
        if exist "!dirPath!" (
            rmdir /s /q "!dirPath!";
            call "!sdkDir!\utils.bat" WriteLine "Removed !dirPath!" "!color_success!"
        ) else (
            call "!sdkDir!\utils.bat" WriteLine "Not Removing !dirPath! (it doesn't exist)" "!color_mute!"
        )
    )

    exit /b


:CleanSubDirs
    SetLocal EnableDelayedExpansion
    
    REM Create a backspace char
    REM for /f %%a in ('"prompt $H&for %%b in (1) do rem"') do set "BS=%%a"

    set BasePath=%~1
    set DirToFind=%~2
    set ExcludeDirFragment=%~3
   
    if "!DirToFind!" == "*" set DirToFind=

    if /i "%doDebug%" == "true" (
        if "!ExcludeDirFragment!" == "" (
            call "!sdkDir!\utils.bat" WriteLine "Removing folders in !BasePath! that match !DirToFind!" "!color_info!"
        ) else (
            call "!sdkDir!\utils.bat" WriteLine "Removing folders in !BasePath! that match !DirToFind! without !ExcludeDirFragment!" "!color_info!"
        )
    )

    pushd "!BasePath!"
    if not errorlevel 0 (
        call "!sdkDir!\utils.bat" WriteLine "Can't navigate to !BasePath! (but this is probably OK)" "!color_warn!"
        exit /b
    )

    set previousRemovedDir=

    REM Loop through all subdirs recursively
    
    rem for /D /R %%i in (%DirToFind%) do ( - %i% always has %DirToFind% apended. WTF?
    for /r /d %%i in (*) do (
        set dirName=%%i

        set skip=false
        
        if "!previousRemovedDir!" neq "" (
            rem Does current dir start with previous dir?
            set endLoop=false
            for /l %%A in (0,1,1024) do (
                if "!endLoop!" == "false" (
                    set "charA=!previousRemovedDir:~%%A,1!"
                    set "charB=!dirName:~%%A,1!"

                    REM if we've hit the end of previousRemovedDir the it's a match
                    if not defined charA (
                        set skip=true
                        set endLoop=true
                    )
                    if not defined charB       set endLoop=true
                    if "!charA!" neq "!charB!" set endLoop=true
                )
            )
        )

        if "!skip!" == "false" (
            set dirMatched=false

            REM Check for match. We do this because the pattern match in the `for` command
            REM is terrible.
            if /i "!dirName:%DirToFind%=!" neq "!dirName!" set dirMatched=true

            REM Check for exclusions
            if "!ExcludeDirFragment!" neq "" (
                if "!dirName:%ExcludeDirFragment%=!" neq "!dirName!" set dirMatched=false

                rem If we wanted more of a regular expression based fragment check we could use:
                rem echo !dirName! | FindStr /B "!ExcludeDirFragment!"
                rem IF %ErrorLevel% equ 0 set dirMatched="false"
            )

            if /i "!dirMatched!" == "true" (

                set previousRemovedDir=%%i

                if /i "!doDebug!" == "true" (
                    call "!sdkDir!\utils.bat" WriteLine "Marked for removal: !dirName!" "!color_error!"
                ) else (
                    rmdir /s /q "!dirName!";
                
                    if exist "!dirName!" (
                        call "!sdkDir!\utils.bat" WriteLine "Unable to remove !dirName!"  "!color_error!"
                    ) else (
                        call "!sdkDir!\utils.bat" WriteLine "Removed !dirName!" "!color_success!"
                    )
                )
            ) else (
                if /i "!doDebug!" == "true" (                
                    call "!sdkDir!\utils.bat" WriteLine "Not deleting !dirName!" "!color_success!"
                    REM call "!sdkDir!\utils.bat" Write "Not deleting !dirName:~-40!" "!color_success!"
                    REM call "!sdkDir!\utils.bat" Write "%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%"
                )
            )
        ) else (
            if /i "!doDebug!" == "true" (
                call "!sdkDir!\utils.bat" WriteLine "Skipping !dirName!" "!color_mute!"
                REM call "!sdkDir!\utils.bat" Write "Skipping !dirName:~-40!" "!color_mute!"
                REM call "!sdkDir!\utils.bat" Write "%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%"
            )
        )

        popd
    )

    exit /b

:CleanFiles
    SetLocal EnableDelayedExpansion
    
    set BasePath=%~1
    set FileToFind=%~2
    set ExcludeFileFragment=%~3

    if /i "%doDebug%" == "true" (
        if "!ExcludeDirFragment!" == "" (
            call "!sdkDir!\utils.bat" WriteLine "Removing folders in !BasePath! that match !DirToFind!" "!color_info!"
        ) else (
            call "!sdkDir!\utils.bat" WriteLine "Removing folders in !BasePath! that match !DirToFind! without !ExcludeDirFragment!" "!color_info!"
        )
    )

    pushd "!BasePath!"  >nul 2>nul
    if not errorlevel 0 (
        call "!sdkDir!\utils.bat" WriteLine "Can't navigate to !BasePath! (but this is probably OK)" "!color_warn!"
        exit /b
    )
    
    REM Hack
    if "!FileToFind!" == "*" set FileToFind=

    REM Loop through all files in this dir
    for %%i in (*) do (

        set fileName=%%i

        set fileMatched=false

        REM Check for match.
        if /i "!fileName:%FileToFind%=!" neq "!fileName!" set fileMatched=true

        REM Check for exclusions
        if "!ExcludeFilePattern!" neq "" (
            if "!fileName:%ExcludeFilePattern%=!" neq "!fileName!" set fileMatched=false
        )

        if /i "!fileMatched!" == "true" (

            if /i "!doDebug!" == "true" (
                call "!sdkDir!\utils.bat" WriteLine "Marked for removal: !fileName!" !color_error!
            ) else (
                del /q "!fileName!";
            
                if exist "!fileName!" (
                    call "!sdkDir!\utils.bat" WriteLine "Unable to remove !fileName!"  !color_error!
                ) else (
                    call "!sdkDir!\utils.bat" WriteLine "Removed !fileName!" !color_success!
                )
            )
        ) else (
            if /i "!doDebug!" == "true" (
                call "!sdkDir!\utils.bat" WriteLine "Not deleting !fileName!" !color_success!
            )
        )
    )

    exit /b
