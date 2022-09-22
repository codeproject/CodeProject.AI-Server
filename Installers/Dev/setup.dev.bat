:: CodeProject.AI Server 
::
:: Windows Development Environment install script
::
:: We assume we're in the source code /Installers/Dev directory.
::
:: To toggle GPU and CUDA support use
::
::    setup.dev.bat enableGPU:true supportCUDA:true

@echo off
cls
setlocal enabledelayedexpansion

:: verbosity can be: quiet | info | loud
set verbosity=quiet

:: If files are already present, then don't overwrite if this is false
set forceOverwrite=false

:: Show output in wild, crazy colours
set useColor=true

:: Platform can define where things are located
set platform=windows

:: should we use GPU enabled libraries?
set enableGPU=true

:: are we ready to support CUDA enabled GPUs?
set supportCUDA=true

:: Basic locations

:: The location of the solution root directory relative to this script
set rootPath=../..

:: CodeProject.AI Server specific :::::::::::::::::::::::::::::::::::::::::::::

:: The name of the dir holding the frontend API server
set APIDirName=API


:: Shared :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

:: The location of large packages that need to be downloaded
:: a. From AWS
set storageUrl=https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/dev/
:: b. Use a local directory rather than from online. Handy for debugging.
rem set storageUrl=C:\Dev\CodeProject\CodeProject.AI\install\cached_downloads\

:: The name of the source directory
set srcDir=src

:: The name of the dir, within the current directory, where install assets will
:: be downloaded
set downloadDir=downloads

:: The name of the dir holding the backend analysis services
set analysisLayerDir=AnalysisLayer

:: Absolute paths :::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: The absolute path to the root directory of CodeProject.AI
set installBasePath=%cd%
cd %rootPath%
set absoluteRootDir=%cd%
cd %installBasePath%


:: The location of directories relative to the root of the solution directory
set analysisLayerPath=%absoluteRootDir%\%srcDir%\%analysisLayerDir%
set downloadPath=%absoluteRootDir%\Installers\%downloadDir%


:: Loop through the params and sniff
set argCount=0
for %%x in (%*) do (
    set arg_curr=%%~x
    if not "!arg_curr:enableGPU=!"   == "!arg_curr!" if /i "!arg_curr:~-4!" == "true" set enableGPU=true
    if not "!arg_curr:supportCUDA=!" == "!arg_curr!" if /i "!arg_curr:~-4!" == "true" set supportCUDA=true
)


if /i "%useColor%" == "true" call utils.bat setESC

:: Set Flags

set rmdirFlags=/q
set roboCopyFlags=/NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul

if /i "%verbosity%"=="info" (
    set rmdirFlags=/q
    set roboCopyFlags=/NFL /NDL /NJH
)

if /i "%verbosity%"=="loud" (
    set rmdirFlags=
    set roboCopyFlags=
)

set spaces=                                                                           *End of line*

call utils.bat WriteLine "          Setting up CodeProject.AI Development Environment             " "DarkYellow" 
call utils.bat WriteLine "                                                                        " "DarkGreen" 
call utils.bat WriteLine "========================================================================" "DarkGreen" 
call utils.bat WriteLine "                                                                        " "DarkGreen" 
call utils.bat WriteLine "                   CodeProject.AI Installer                             " "DarkGreen" 
call utils.bat WriteLine "                                                                        " "DarkGreen"
call utils.bat WriteLine "========================================================================" "DarkGreen" 
call utils.bat WriteLine "                                                                        " "DarkGreen"


call utils.bat WriteLine "                                                                        " "DarkGreen"
call utils.bat Write "GPU Supported: "
if "!enableGPU!" == "true" (call utils.bat WriteLine "True" !color_success!) else (call utils.bat WriteLine "False" !color_warn!)
call utils.bat Write "CUDA Supported: "
if "!supportCUDA!" == "true" (call utils.bat WriteLine "True" !color_success!) else (call utils.bat WriteLine "False" !color_warn!)

:: ============================================================================
:: 1. Ensure directories are created and download required assets

set announcement=General CodeProject.AI setup !spaces!
set announcement=!announcement:~0,70!

call utils.bat WriteLine
call utils.bat WriteLine "!announcement!" "White" "Blue"
call utils.bat WriteLine

:: Create some directories
call utils.bat Write "Creating Directories..."

:: For downloading assets
if not exist "%downloadPath%\" mkdir "%downloadPath%"
call utils.bat WriteLine "Done" "Green"

:: Walk through the modules directory and call the setup script in each dir
:: TODO: This should be just a simple for /d %%D in ("%analysisLayerPath%") do (
for /f "delims=" %%D in ('dir /a:d /b "%analysisLayerPath%"') do (
    set moduleDir=%%~nxD
    set modulePath=!analysisLayerPath!\!moduleDir!

    if /i "!moduleDir!" NEQ "bin" (
        if exist "!modulePath!\install.dev.bat" (

            REM Pad right to 60 chars
            set announcement=Processing !moduleDir! !spaces!
            set announcement=!announcement:~0,70!

            call utils.bat WriteLine
            call utils.bat WriteLine "!announcement!" "White" "Blue"
            call utils.bat WriteLine

            call "!modulePath!\install.dev.bat"
        )
    )
)

call utils.bat WriteLine
call utils.bat WriteLine "Modules installed" "Green"


:: ============================================================================
:: and we're done.

set announcement=                Development Environment setup complete !spaces!
set announcement=!announcement:~0,70!

call utils.bat WriteLine
call utils.bat WriteLine "!announcement!" "White" "DarkGreen"
call utils.bat WriteLine