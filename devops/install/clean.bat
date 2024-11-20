:: CodeProject.AI Server and Analysis modules: Cleans debris, properly, for clean build
::
:: Usage:
::   clean [build | install | installall | downloads | all]
::
:: We assume we're in the /devops/install directory

@echo off
cls
setlocal enabledelayedexpansion

start ..\utils\stop_all.bat

set useColor=true
set doDebug=false
set lineWidth=70

REM List of modules we'll look after ===========================================

set dotNetModules=ObjectDetectionYOLOv5Net
set pythonModules=ObjectDetectionYOLOv5-6.2

set dotNetExternalModules=CodeProject.AI-PortraitFilter CodeProject.AI-SentimentAnalysis
set pythonExternalModules=CodeProject.AI-ALPR CodeProject.AI-ALPR-RKNN CodeProject.AI-BackgroundRemover ^
                          CodeProject.AI-Cartoonizer CodeProject.AI-FaceProcessing CodeProject.AI-LlamaChat ^
                          CodeProject.AI-ObjectDetectionCoral CodeProject.AI-ObjectDetectionYOLOv5-3.1 ^
                          CodeProject.AI-ObjectDetectionYOLOv8 CodeProject.AI-ObjectDetectionYoloRKNN ^
                          CodeProject.AI-TrainingObjectDetectionYOLOv5 CodeProject.AI-OCR ^
                          CodeProject.AI-SceneClassifier CodeProject.AI-SoundClassifierTF ^
                          CodeProject.AI-SuperResolution CodeProject.AI-TextSummary CodeProject.AI-Text2Image

set dotNetDemoModules=DotNetLongProcess DotNetSimple DotNetLongProcess
set pythonDemoModules=PythonLongProcess PythonSimple PythonLongProcess

REM Setup ======================================================================

pushd ..\..
set rootDir=%cd%
popd

set installScriptsDirPath=!rootDir!\devops\install
set utilsScriptsDirPath=!rootDir!\src\scripts
set utilsScript=!utilsScriptsDirPath!\utils.bat
set externalModulesDir=!rootDir!\..\CodeProject.AI-Modules

REM Output usage ===============================================================

if "%1" == "" (
    call "!utilsScript!" WriteLine "Solution Cleaner" "White"
    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "clean option"
    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine " where 'option' is one of:"
    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "  assets         - removes assets that were downloaded and moved into place"
    call "!utilsScript!" WriteLine "  build          - cleans build output (bin / obj)"
    call "!utilsScript!" WriteLine "  data           - removes user data stored by modules"
    call "!utilsScript!" WriteLine "  download-cache - removes download cache to force re-download"
    call "!utilsScript!" WriteLine "  install        - removes installation stuff, current OS (Python, PIPs, downloads etc)"
    call "!utilsScript!" WriteLine "  install-all    - removes installation stuff for all OSs"
    call "!utilsScript!" WriteLine "  libraries      - removes installed libraries (PIPs), current OS"
    call "!utilsScript!" WriteLine "  libraries-all  - removes installed libraries (PIPs), all OSs"
    call "!utilsScript!" WriteLine "  all            - removes build and installation stuff for all OSs"
    call "!utilsScript!" WriteLine 
    exit /b
)

REM Param checks ===============================================================

set cleanAssets=false
set cleanBuild=false
set cleanUserData=false
set cleanDownloadCache=false
set cleanInstallCurrentOS=false
set cleanInstallAll=false
set cleanLibraries=false
set cleanLibrariesAll=false

set cleanAll=false

if /i "%1" == "assets"         set cleanAssets=true
if /i "%1" == "build"          set cleanBuild=true
if /i "%1" == "data"           set cleanUserData=true
if /i "%1" == "download-cache" set cleanDownloadCache=true
if /i "%1" == "install"        set cleanInstallCurrentOS=true
if /i "%1" == "install-all"    set cleanInstallAll=true
if /i "%1" == "libraries"      set cleanLibraries=true
if /i "%1" == "libraries-all"  set cleanLibrariesAll=true
if /i "%1" == "all"            set cleanAll=true

REM Set directing variables ====================================================

if /i "!cleanAll!" == "true" (
    set cleanAssets=true
    set cleanBuild=true
    set cleanDownloadCache=true
    set cleanInstallAll=true
    set cleanLibrariesAll=true
    set cleanUserData=true
)

if /i "!cleanInstallCurrentOS!" == "true" (
    set cleanAssets=true
    set cleanBuild=true
    set cleanLibraries=true
    set cleanUserData=true
)

if /i "!cleanInstallAll!" == "true" (
    set cleanAssets=true
    set cleanBuild=true
    set cleanLibrariesAll=true
    set cleanUserData=true
)

echo clean assets = %cleanAssets%
echo clean build = %cleanBuild%
echo clean data = %cleanUserData%
echo clean download-cache = %cleanDownloadCache%
echo clean install = %cleanInstallCurrentOS%
echo clean install-all = %cleanInstallAll%
echo clean libraries = %cleanLibraries%
echo clean libraries-all = %cleanLibrariesAll%
echo clean all = %cleanAll%


REM Start cleaning =============================================================

if /i "%cleanAssets%" == "true" (

    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning Assets" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    REM Production modules
    call :RemoveDir "!rootDir!\modules\ObjectDetectionYOLOv5-6.2\assets"
    call :RemoveDir "!rootDir!\modules\ObjectDetectionYOLOv5-6.2\custom-models"
    call :RemoveDir "!rootDir!\modules\ObjectDetectionYOLOv5Net\assets"
    call :RemoveDir "!rootDir!\modules\ObjectDetectionYOLOv5Net\custom-models"
    call :RemoveDir "!rootDir!\modules\ObjectDetectionYOLOv5Net\LocalNugets"

    REM External modules
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ALPR\paddleocr"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ALPR-RKNN\paddleocr"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-BackgroundRemover\models"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-Cartoonizer\weights"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-FaceProcessing\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-LlamaChat\models"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionYOLOv5-3.1\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionYOLOv5-3.1\custom-models"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionYOLOv8\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionYOLOv8\custom-models"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionCoral\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionCoral\edgetpu_runtime"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionYoloRKNN\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-ObjectDetectionYoloRKNN\custom-models"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-OCR\paddleocr"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-SceneClassifier\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-SoundClassifierTF\data"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-Text2Image\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-TrainingObjectDetectionYOLOv5\assets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-TrainingObjectDetectionYOLOv5\datasets"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-TrainingObjectDetectionYOLOv5\fiftyone"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-TrainingObjectDetectionYOLOv5\training"
    call :RemoveDir "!externalModulesDir!\CodeProject.AI-TrainingObjectDetectionYOLOv5\zoo"

    REM Demo modules
    call :RemoveDir "!rootDir!\src\demos\modules\DotNetLongProcess\assets"
    call :RemoveDir "!rootDir!\src\demos\modules\DotNetSimple\assets"
    call :RemoveDir "!rootDir!\src\demos\modules\PythonLongProcess\assets"
    call :RemoveDir "!rootDir!\src\demos\modules\PythonSimple\assets"
)

if /i "%cleanBuild%" == "true" (
       
    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning Build" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    call :RemoveDir "!rootDir!\src\server\bin\"
    call :RemoveDir "!rootDir!\src\server\obj"

    call :RemoveDir "!rootDir!\src\SDK\NET\bin\"
    call :RemoveDir "!rootDir!\src\SDK\NET\obj"

    for %%x in (!dotNetModules!) do (
        call :RemoveDir "!rootDir!\modules\%%x\bin\"
        call :RemoveDir "!rootDir!\modules\%%x\obj\"
        call :DelDirPattern "!rootDir!\modules\%%x\" "\\%%x-*.zip"
    )
    for %%x in (!dotNetExternalModules!) do (
        call :RemoveDir "!externalModulesDir!\%%x\bin\"
        call :RemoveDir "!externalModulesDir!\%%x\obj\"
        call :DelDirPattern "!externalModulesDir!\%%x\" "\\%%x-*.zip"
    )
    for %%x in (!dotNetDemoModules!) do (
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\bin\"
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\obj\"
        call :DelDirPattern "!rootDir!\src\demos\modules\%%x\" "\\%%x-*.zip"
    )

    call :RemoveDir "!rootDir!\utils\ParseJSON\bin"
    call :RemoveDir "!rootDir!\utils\ParseJSON\obj"
    del "!rootDir!\utils\ParseJSON\ParseJSON.deps.json"
    del "!rootDir!\utils\ParseJSON\*.dll"
    del "!rootDir!\utils\ParseJSON\ParseJSON.exe"
    del "!rootDir!\utils\ParseJSON\ParseJSON.runtimeconfig.json"
    del "!rootDir!\utils\ParseJSON\ParseJSON.xml"

    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\bin\Debug\"
    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\bin\Release\"
    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\obj\Debug\"
    call :CleanSubDirs "!rootDir!\src\demos\clients\"      "\obj\Release\"

    call :RemoveDir "!rootDir!\tests\QueueServiceTests\bin\"
    call :RemoveDir "!rootDir!\tests\QueueServiceTests\obj\"
)

if /i "%cleanDownloadCache%" == "true" (

    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning Downloads" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    REM remove non module or model folders
    FOR /d %%a IN ("%rootDir%\downloads\modules\assets\*") DO (
        IF /i NOT "%%~nxa"=="modules" IF /i NOT "%%~nxa"=="models" call :RemoveDir "%%a"
    )

    REM clean out models files
    FOR %%a IN ("%rootDir%\downloads\models\*") DO (
        IF /i NOT "%%~nxa"=="models.json" call :RemoveFile "%%a"
    )
)

if /i "%cleanInstallCurrentOS%" == "true" (

    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning Windows Install" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    REM Clean shared python venvs
    call :RemoveDir "!rootDir!\runtimes\bin\windows" 

    REM Clean module python venvs
    for %%x in (!pythonModules!) do (
        call :RemoveDir "!rootDir!\modules\%%x\bin\windows"
    )
    for %%x in (!pythonExternalModules!) do (
        call :RemoveDir "!externalModulesDir!\%%x\bin\windows"
    )
    for %%x in (!pythonDemoModules!) do (
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\bin\windows"
    )
)

if /i "%cleanInstallAll%" == "true" (

    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning install for all platforms" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    REM Clean shared python installs and venvs
    call :RemoveDir "!rootDir!\runtimes\bin\" 

    REM Clean module python venvs
    for %%x in (!pythonModules!) do (
        call :RemoveDir "!rootDir!\modules\%%x\bin\"
    )
    for %%x in (!pythonExternalModules!) do (
        call :RemoveDir "!externalModulesDir!\%%x\bin\"
    )
    for %%x in (!pythonDemoModules!) do (
        call :RemoveDir "!rootDir!\src\demos\modules\%%x\bin\"
    )
)

if /i "%cleanLibraries%" == "true" (

    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning Libraries, current OS" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    call :DelDirPattern "!rootDir!\runtimes\bin\windows\" "\\python[0-9]*\\venv\\Lib\\site-packages$"

    REM Clean module python venvs
    for %%x in (!pythonModules!) do (
        call :DelDirPattern "!rootDir!\modules\%%x\bin\windows\" "\\python[0-9]*\\venv\\Lib\\site-packages$"
    )
    for %%x in (!pythonExternalModules!) do (
        call :DelDirPattern "!externalModulesDir!\%%x\bin\windows\" "\\python[0-9]*\\venv\\Lib\\site-packages$"
    )
    for %%x in (!pythonDemoModules!) do (
        call :DelDirPattern "!rootDir!\src\demos\modules\%%x\bin\windows\" "\\python[0-9]*\\venv\\Lib\\site-packages$"
    )
)

if /i "%cleanLibrariesAll%" == "true" (

    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning Libraries, all OSs" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    REM Clean module python venvs
    for %%x in (!pythonModules!) do ( 
        call :DelDirPattern "!rootDir!\modules\%%x\" "\\bin$"
    )
    for %%x in (!pythonExternalModules!) do (
        call :DelDirPattern "!externalModulesDir!\%%x\" "\\bin$"
    )
    for %%x in (!pythonDemoModules!) do (
        call :DelDirPattern "!rootDir!\src\demos\modules\%%x\" "\\bin$"
    )
)

if /i "%cleanUserData%" == "true" (

    call "!utilsScript!" WriteLine 
    call "!utilsScript!" WriteLine "Cleaning User data" "White" "Blue" !lineWidth!
    call "!utilsScript!" WriteLine 

    call :RemoveDir "!externalModulesDir!\CodeProject.AI-FaceProcessing\datastore"
)

goto:eof


REM Functions ==================================================================

:RemoveFile
    SetLocal EnableDelayedExpansion

    set filePath=%~1

    if /i "!doDebug!" == "true" (
        call "!utilsScript!" WriteLine "Marked for removal: !filePath!" "!color_error!"
    ) else (
        if exist "!filePath!" (
            del "!filePath!"
            call "!utilsScript!" WriteLine "Removed !dirPath!" "!color_success!"
        ) else (
            call "!utilsScript!" WriteLine "Not Removing !filePath! (it doesn't exist)" "!color_mute!"
        )
    )

    exit /b


:DelDirPattern
    SetLocal EnableDelayedExpansion

    set rootFolder=%~1
    set pathPattern=%~2

    if /i "!doDebug!" == "true" (
        call "!utilsScript!" WriteLine "Marked for removal: !rootFolder!!pathPattern!" "!color_error!"
    ) else (

        for /f "delims=" %%i in ('dir /s /b /ad "!rootFolder!" 2^>NUL ^| findstr /r /c:"!pathPattern!"') do (
            rmdir /s /q "%%i"
            call "!utilsScript!" WriteLine "Removed folder %%i" "!color_success!"
        )

        :: Find all files matching the pattern
        for /f "delims=" %%i in ('dir /s /b /a-d "!rootFolder!" 2^>NUL ^| findstr /r /c:"!pathPattern!"') do (
            del /f /q "%%i"
            call "!utilsScript!" WriteLine "Removed file %%i" "!color_success!"
        )

        REM call "!utilsScript!" WriteLine "Removed !rootFolder!!pathPattern!" "!color_success!"
    )

    exit /b


:RemoveDir
    SetLocal EnableDelayedExpansion

    set dirPath=%~1

    if /i "!doDebug!" == "true" (
        call "!utilsScript!" WriteLine "Marked for removal: !dirPath!" "!color_error!"
    ) else (
        if exist "!dirPath!" (
            rmdir /s /q "!dirPath!";
            call "!utilsScript!" WriteLine "Removed !dirPath!" "!color_success!"
        ) else (
            call "!utilsScript!" WriteLine "Not Removing !dirPath! (it doesn't exist)" "!color_mute!"
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
            call "!utilsScript!" WriteLine "Removing folders in !BasePath! that match !DirToFind!" "!color_info!"
        ) else (
            call "!utilsScript!" WriteLine "Removing folders in !BasePath! that match !DirToFind! without !ExcludeDirFragment!" "!color_info!"
        )
    )

    pushd "!BasePath!"
    if not errorlevel 0 (
        call "!utilsScript!" WriteLine "Can't navigate to !BasePath! (but this is probably OK)" "!color_warn!"
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
                    call "!utilsScript!" WriteLine "Marked for removal: !dirName!" "!color_error!"
                ) else (
                    rmdir /s /q "!dirName!";
                
                    if exist "!dirName!" (
                        call "!utilsScript!" WriteLine "Unable to remove !dirName!"  "!color_error!"
                    ) else (
                        call "!utilsScript!" WriteLine "Removed !dirName!" "!color_success!"
                    )
                )
            ) else (
                if /i "!doDebug!" == "true" (                
                    call "!utilsScript!" WriteLine "Not deleting !dirName!" "!color_success!"
                    REM call "!utilsScript!" Write "Not deleting !dirName:~-40!" "!color_success!"
                    REM call "!utilsScript!" Write "%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%"
                )
            )
        ) else (
            if /i "!doDebug!" == "true" (
                call "!utilsScript!" WriteLine "Skipping !dirName!" "!color_mute!"
                REM call "!utilsScript!" Write "Skipping !dirName:~-40!" "!color_mute!"
                REM call "!utilsScript!" Write "%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%%BS%"
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
            call "!utilsScript!" WriteLine "Removing folders in !BasePath! that match !DirToFind!" "!color_info!"
        ) else (
            call "!utilsScript!" WriteLine "Removing folders in !BasePath! that match !DirToFind! without !ExcludeDirFragment!" "!color_info!"
        )
    )

    pushd "!BasePath!"  >nul 2>nul
    if not errorlevel 0 (
        call "!utilsScript!" WriteLine "Can't navigate to !BasePath! (but this is probably OK)" "!color_warn!"
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
                call "!utilsScript!" WriteLine "Marked for removal: !fileName!" !color_error!
            ) else (
                del /q "!fileName!";
            
                if exist "!fileName!" (
                    call "!utilsScript!" WriteLine "Unable to remove !fileName!"  !color_error!
                ) else (
                    call "!utilsScript!" WriteLine "Removed !fileName!" !color_success!
                )
            )
        ) else (
            if /i "!doDebug!" == "true" (
                call "!utilsScript!" WriteLine "Not deleting !fileName!" !color_success!
            )
        )
    )

    exit /b
