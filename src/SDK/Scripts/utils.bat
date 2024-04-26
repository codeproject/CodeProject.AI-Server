:: CodeProject.AI Server Utilities
::
:: Utilities for use with Windows Development Environment install scripts
::
:: We assume we're in the source code /Installers/Dev directory.
::

@echo off

REM ☑ ✓ ✔ 🗸   ✅ 🗹
REM ☒ ✗ ✘ 🗴 🗶 ❌ ⮽ 🗵 🗷

set pipFlags=-q -q -q
if /i "%verbosity%"=="info" set pipFlags=-q -q -q
if /i "%verbosity%"=="loud" set pipFlags=

set rmdirFlags=/q
if /i "%verbosity%"=="info" set rmdirFlags=/q
if /i "%verbosity%"=="loud" set rmdirFlags=

set roboCopyFlags=/NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul
if /i "%verbosity%"=="info" set roboCopyFlags=/NFL /NDL /NJH
if /i "%verbosity%"=="loud" set roboCopyFlags=

set darkMode=false
REM A little pointless as this won't tell us the terminal background colour.
REM For /F "EOL=H Tokens=3" %%G In ('%SystemRoot%\System32\reg.exe Query "HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize" /V "SystemUsesLightTheme" 2^>NUL' ) Do ( 
REM     If %%G Equ 0 ( set darkMode=true )
REM )

if "%darkMode%" == "true" (
    set color_primary=White
    set color_mute=Gray
    set color_info=DarkMagenta
    set color_success=Green
    set color_warn=DarkYellow
    set color_error=Red
) else (
    set color_primary=Black
    set color_mute=Gray
    set color_info=Magenta
    set color_success=DarkGreen
    set color_warn=DarkYellow
    set color_error=Red
)

REM For VSCode, the terminal depends on the color theme installed, so who knows?
if "%TERM_PROGRAM%" == "vscode" set color_primary=Default

:: %1 is the name of the method to call. Shift will shuffle the arguments that 
:: were passed one spot to the left, meaning the called subroutine will get the
:: arguments it expects in order
shift & goto :%~1


:: sub-routines

:: Sets up the ESC string for use later in this script
:setESC
    for /F "tokens=1,2 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do REM"') do (
      set ESC=%%b
      exit /B 0
    )
    exit /B 0


:: Sets the name of a color that will providing a contrasting foreground
:: color for the given background color.
::
:: string background color name. 
:: on return, contrastForeground will be set
:setContrastForeground

    set background=%~1

    REM Darkmode doesn't actually do anything for the windows terminal or the VS Code terminal.
    REM We've been unable to find a simple CMD solution for determining the background of the
    REM current terminal. We could force white on black using the 'color' command but that strips
    REM ALL colour, even after the fact, so that's kinda pointless for us.

    if "!background!" == "" (
        REM if "%darkMode%" == "true" ( background=Black ) else ( background=White )
        background=Black
    )

    if "%darkMode%" == "true" (
        if /i "!background!"=="Black"       set contrastForeground=White
        if /i "!background!"=="DarkRed"     set contrastForeground=White
        if /i "!background!"=="DarkGreen"   set contrastForeground=White
        if /i "!background!"=="DarkYellow"  set contrastForeground=White
        if /i "!background!"=="DarkBlue"    set contrastForeground=White
        if /i "!background!"=="DarkMagenta" set contrastForeground=White
        if /i "!background!"=="DarkCyan"    set contrastForeground=White
        if /i "!background!"=="Gray"        set contrastForeground=Black
        if /i "!background!"=="DarkGray"    set contrastForeground=White
        if /i "!background!"=="Red"         set contrastForeground=White
        if /i "!background!"=="Green"       set contrastForeground=White
        if /i "!background!"=="Yellow"      set contrastForeground=Black
        if /i "!background!"=="Blue"        set contrastForeground=White
        if /i "!background!"=="Magenta"     set contrastForeground=White
        if /i "!background!"=="Cyan"        set contrastForeground=Black
        if /i "!background!"=="White"       set contrastForeground=Black
    ) else (
        if /i "!background!"=="Black"       set contrastForeground=White
        if /i "!background!"=="DarkRed"     set contrastForeground=White
        if /i "!background!"=="DarkGreen"   set contrastForeground=White
        if /i "!background!"=="DarkYellow"  set contrastForeground=White
        if /i "!background!"=="DarkBlue"    set contrastForeground=White
        if /i "!background!"=="DarkMagenta" set contrastForeground=White
        if /i "!background!"=="DarkCyan"    set contrastForeground=White
        if /i "!background!"=="Gray"        set contrastForeground=Black
        if /i "!background!"=="DarkGray"    set contrastForeground=White
        if /i "!background!"=="Red"         set contrastForeground=White
        if /i "!background!"=="Green"       set contrastForeground=White
        if /i "!background!"=="Yellow"      set contrastForeground=Black
        if /i "!background!"=="Blue"        set contrastForeground=White
        if /i "!background!"=="Magenta"     set contrastForeground=White
        if /i "!background!"=="Cyan"        set contrastForeground=Black
        if /i "!background!"=="White"       set contrastForeground=Black
    )

    exit /B 0


:: Sets the currentColor global for the given foreground/background colors
:: currentColor must be output to the terminal before outputing text in
:: order to generate a colored output.
::
:: string foreground color name. Optional if no background provided.
::        Defaults to "White"
:: string background color name.  Optional. Defaults to Black.
:: string intense. Optional. If "true" then the insensity is turned up
:setColor

    REM If you want to get a little fancy then you can also try
    REM  - %ESC%[4m - Underline
    REM  - %ESC%[7m - Inverse

    set foreground=%~1
    set background=%~2
    set intense=%~3

    REM if "!foreground!"=="" set foreground=White
    REM if /i "!foreground!"=="Default" set foreground=White
    REM if "!background!"=="" set background=Black
    REM if /i "!background!"=="Default" set background=Black

    if "!foreground!"=="" set foreground=Default
    if "!background!"=="" set background=Default

    if "!ESC!"=="" call :setESC

    if /i "!foreground!"=="Contrast" (
        call :setContrastForeground !background!
        set foreground=!contrastForeground!
    )

    REM Colour effect: <ESC>[(0|1)<code>m, where 0 = not intense / reset, 1 = intense    
    REM See this most excellent answer: https://stackoverflow.com/a/33206814
    set currentColor=!ESC![
    if /i "$intense"=="true" (
        set currentColor=!currentColor!1;
    ) else (
        set currentColor=!currentColor!0;
    )

    REM Foreground Colours
    if /i "!foreground!"=="Default"     set currentColor=!currentColor!39m

    if /i "!foreground!"=="Black"       set currentColor=!currentColor!30m
    if /i "!foreground!"=="DarkRed"     set currentColor=!currentColor!31m
    if /i "!foreground!"=="DarkGreen"   set currentColor=!currentColor!32m
    if /i "!foreground!"=="DarkYellow"  set currentColor=!currentColor!33m
    if /i "!foreground!"=="DarkBlue"    set currentColor=!currentColor!34m
    if /i "!foreground!"=="DarkMagenta" set currentColor=!currentColor!35m
    if /i "!foreground!"=="DarkCyan"    set currentColor=!currentColor!36m
    if /i "!foreground!"=="Gray"        set currentColor=!currentColor!37m

    if /i "!foreground!"=="DarkGray"    set currentColor=!currentColor!90m
    if /i "!foreground!"=="Red"         set currentColor=!currentColor!91m
    if /i "!foreground!"=="Green"       set currentColor=!currentColor!92m
    if /i "!foreground!"=="Yellow"      set currentColor=!currentColor!93m
    if /i "!foreground!"=="Blue"        set currentColor=!currentColor!94m
    if /i "!foreground!"=="Magenta"     set currentColor=!currentColor!95m
    if /i "!foreground!"=="Cyan"        set currentColor=!currentColor!96m
    if /i "!foreground!"=="White"       set currentColor=!currentColor!97m

    if "!currentColor!"=="" set currentColor=!currentColor!97m
    
    if /i "!background!"=="Default"     set currentColor=!currentColor!!ESC![49m

    if /i "!background!"=="Black"       set currentColor=!currentColor!!ESC![40m
    if /i "!background!"=="DarkRed"     set currentColor=!currentColor!!ESC![41m
    if /i "!background!"=="DarkGreen"   set currentColor=!currentColor!!ESC![42m
    if /i "!background!"=="DarkYellow"  set currentColor=!currentColor!!ESC![43m
    if /i "!background!"=="DarkBlue"    set currentColor=!currentColor!!ESC![44m
    if /i "!background!"=="DarkMagenta" set currentColor=!currentColor!!ESC![45m
    if /i "!background!"=="DarkCyan"    set currentColor=!currentColor!!ESC![46m
    if /i "!background!"=="Gray"        set currentColor=!currentColor!!ESC![47m
    if /i "!background!"=="DarkGray"    set currentColor=!currentColor!!ESC![100m
    if /i "!background!"=="Red"         set currentColor=!currentColor!!ESC![101m
    if /i "!background!"=="Green"       set currentColor=!currentColor!!ESC![102m
    if /i "!background!"=="Yellow"      set currentColor=!currentColor!!ESC![103m
    if /i "!background!"=="Blue"        set currentColor=!currentColor!!ESC![104m
    if /i "!background!"=="Magenta"     set currentColor=!currentColor!!ESC![105m
    if /i "!background!"=="Cyan"        set currentColor=!currentColor!!ESC![106m
    if /i "!background!"=="White"       set currentColor=!currentColor!!ESC![107m

    exit /B 0

:: Outputs a line, including linefeed, to the terminal using the given 
:: foreground / background colors 
::
:: string The text to output. Optional if no foreground provided. Default is 
::        just a line feed.
:: string Foreground color name. Optional if no background provided. Defaults 
::        to "White"
:: string Background color name. Optional. Defaults to "Black"
:: int    Line width. If non-blank the line will be padded right with spaces
:WriteLine
    SetLocal EnableDelayedExpansion
    
    if "!ESC!"=="" call :setESC    
    set resetColor=!ESC![0m

    set str=%~1

    if "!str!"=="" (
        echo:
        exit /b 0
    )
    if "!str: =!"=="" (
        echo:
        exit /b 0
    )

    set width=%~4
    if "!width!" neq "" (
        set spaces=                                                                           *End of line*
        set str=!str!!spaces!
        set str=!str:~0,%width%!
        REM echo  str = [!str!]
    )

    if /i "!useColor!"=="true" (
        call :setColor %2 %3
        echo !currentColor!!str!!resetColor!
    ) else (
        echo !str!
    )
    exit /b 0

:: Outputs a line without a linefeed to the terminal using the given foreground
:: / background colors 
::
:: string The text to output. Optional if no foreground provided. Default is 
::        just a line feed.
:: string Foreground color name. Optional if no background provided. Defaults 
::        to "White"
:: string Background color name. Optional. Defaults to "Black"
:Write
    SetLocal EnableDelayedExpansion
    
    if "!ESC!"=="" call :setESC
    set resetColor=!ESC![0m

    set str=%~1

    if "!str!"=="" exit /b 0
    if "!str: =!"=="" exit /b 0

    if /i "%useColor%"=="true" (
        call :setColor %2 %3
        <NUL set /p =!currentColor!!str!!resetColor!
    ) else (
        <NUL set /p =!str!
    )
    exit /b 0

:GetFromServer
    SetLocal EnableDelayedExpansion

    REM This method downloads a zip file from our S3 storage, stores in the downloads
    REM folder within a subfolder specific to the current module, then expands the
    REM zip and copies the contents over to module itself. The zip that was downloaded
    REM will be saved in order to cache downloads

    REM Param 1: Name of the folder in which to look for this file on S3 eg "models/"
    set folder=%1
    set folder=!folder:"=!

    REM Param 2: Name of the file to get eg packages_for_gpu.zip
    set fileToGet=%2
    set fileToGet=!fileToGet:"=!

    REM Param 3: Name of the folder within the current module where this download
    REM          will be stored. eg assets
    set moduleAssetsDirName=%3
    set moduleAssetsDirName=!moduleAssetsDirName:"=!

    REM Param 4: output message
    set message=%4
    set message=!message:"=!

    REM Clean up directories to force a download and re-copy if necessary. Note that:
    REM  - moduleDirName is the name of the current module's directory
    REM  - moduleDirPath is the path to the module's directory
    REM  - downloadDirPath is the path where downloads are stored (typically src/downloads)
    if /i "%forceOverwrite%" == "true" (
        REM Force Re-download, then force re-copy of downloads to install dir
        if exist "!downloadDirPath!\!modulesDir!\!moduleDirName!\!fileToGet!" (
            del /s %rmdirFlags% "!downloadDirPath!\!modulesDir!\!moduleDirName!\!fileToGet!"
        )
        if exist "!moduleDirPath!\!moduleAssetsDirName!" rmdir /s %rmdirFlags% "!moduleDirPath!\!moduleAssetsDirName!"
    )
    
    REM Download !storageUrl!fileToGet to downloadDirPath and extract into downloadDirPath\modules\moduleDirName\ModuleAssetDir

    REM Params are: S3 storage bucket | fileToGet     | zip lives in...      | zip expanded to moduleDir/... | message
    REM eg                   "S3_bucket/folder"  "rembg-models.zip" \downloads\myModuleDir"          "assets"            "Downloading models..."
    call :DownloadAndExtract "!storageUrl!!folder!" "!fileToGet!" "!downloadDirPath!\!modulesDir!\!moduleDirName!" "!moduleAssetsDirName!" "!message!"

    REM Copy downloadDirPath\modules\moduleDirName\moduleAssetsDirName folder to modulesDirPath\moduleDirName\
    if exist "!downloadDirPath!\!modulesDir!\!moduleDirName!\!moduleAssetsDirName!" (

        REM if /i "%verbosity%" neq "quiet" ( ... )

        call :Write "Copying contents of !fileToGet! to !moduleAssetsDirName!..."

        REM move "!downloadDirPath!\!modulesDir!\!moduleDirName!\!moduleAssetsDirName!" !moduleDirPath!
        REM if errorlevel 1 (
        REM     call :WriteLine "Failed" !color_error!
        REM ) else (
        REM     call :WriteLine "done" !color_success!
        REM )

        robocopy /E "!downloadDirPath!\!modulesDir!\!moduleDirName!\!moduleAssetsDirName! " ^
                    "!moduleDirPath!\!moduleAssetsDirName! " !roboCopyFlags! /MOVE >NUL
        if errorlevel 16 (
            call :WriteLine "Failed" !color_error!
        else if errorlevel 8 (
            call :WriteLine "Some files not copied" !color_warn!
        ) else (
            call :WriteLine "done" !color_success!
        )

        call :Write ""
        REM NOTE: Before each "exit" call we have a benign call to :WriteLine, or 
        REM       any other CMD which stops this method returning an errorlevel > 0. 
        REM       We do this because robocopy will return status as errorlevel, 
        REM       with error level up to 7 meaning "it may have worked". See
        REM       https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy

    ) else (
        REM if /i "%verbosity%" neq "quiet" (
            call :WriteLine "Failed to download and extract !fileToGet!" "!color_error!"
        REM )
        exit /b 1
    )

    exit /b


:DownloadAndExtract
    SetLocal EnableDelayedExpansion

    REM Param 1: The URL where the download can be found.
    REM eg "https://codeproject-ai.s3.ca-central-1.amazonaws.com/server/models/"
    set storageUrl=%1
    set storageUrl=!storageUrl:"=!

    REM Param 2: The file to download. eg packages_for_gpu.zip
    set fileToGet=%2
    set fileToGet=!fileToGet:"=!

    REM Param 3: Where to store the download zip. eg "downloads\moduleId" 
    set downloadToDir=%3
    set downloadToDir=!downloadToDir:"=!

    REM Param 4: The name of the folder within the downloads directory where 
    REM          the contents should be extracted. eg. assets 
    set dirToExtract=%4
    set dirToExtract=!dirToExtract:"=!

    REM Param 5: The output message
    set message=%5
    set message=!message:"=!

    if "!message!" == "" set message=Downloading !fileToGet!...

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Downloading !fileToGet! to !downloadToDir!\!dirToExtract!" "!color_info!"
    )

    call :Write "!message!" "!color_primary!"

    set extension=!fileToGet:~-3!
    if /i "!extension!" NEQ ".gz" (
        set extension=!fileToGet:~-4!
        if /i "!extension!" NEQ ".zip" (
            call :WriteLine "Unknown and unsupported file type for file !fileToGet!" "!color_error!"
            exit /b    REM no point in carrying on
        )
    )

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Checking '!downloadToDir!\!fileToGet!'" "!color_info!"
    )
    
    if exist "!downloadToDir!\!fileToGet!" (
        call :Write "already exists..." "!color_info!"
    ) else (

        if /i "!offlineInstall!" == "true" (
            call :WriteLine "Offline Installation: Unable to download !fileToGet!." %color_error%
            exit /b 1
        )

        if not exist "!downloadToDir!" mkdir "!downloadToDir!"

        REM Be careful with the quotes so we can handle paths with spaces
        powershell -command "Start-BitsTransfer -Source '!storageUrl!!fileToGet!' -Description !fileToGet! -Destination '!downloadToDir!\!fileToGet!'"

        REM If these fail, it could be becuase of hanging transfers
        if errorlevel 1 (
            powershell -Command "Get-BitsTransfer | Remove-BitsTransfer"
            powershell -command "Start-BitsTransfer -Source '!storageUrl!!fileToGet!' -Description !fileToGet! -Destination '!downloadToDir!\!fileToGet!'"
        )

        REM if that doesn't work, fallback to a slower safer method
        if errorlevel 1 (
            call :WriteLine "BITS transfer failed. Trying Powershell...." "!color_warn!"
            powershell -Command "Invoke-WebRequest '!storageUrl!!fileToGet!' -OutFile '!downloadToDir!\!fileToGet!'"
            if errorlevel 1 (
                call :WriteLine "Download failed. Sorry." "!color_error!"
                exit /b 1
            )
        )

        if not exist "!downloadToDir!\!fileToGet!" (
            call :WriteLine "An error occurred that could not be resolved." "!color_error!"
            exit /b 1
        )
    )

    call :Write "Expanding..." "!color_info!"

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Heading to !downloadToDir!" "!color_info!"
    )

    pushd "!downloadToDir!"
    if not exist "!downloadToDir!\!dirToExtract!" mkdir "!downloadToDir!\!dirToExtract!"

    call :ExtractToDirectory "!fileToGet!" "!dirToExtract!"

    if errorlevel 1 (
        popd
        exit /b 1
    )

    popd

    call :WriteLine "done." "!color_success!"

    exit /b


:ExtractToDirectory
    SetLocal EnableDelayedExpansion

    REM Param 1: The archive to expand. eg packages_for_gpu.zip
    set archiveName=%1
    set archiveName=!archiveName:"=!

    REM Param 2: The name of the folder within the downloads directory where 
    REM          the contents should be extracted. eg. assets 
    set dirToExtract=%2
    set dirToExtract=!dirToExtract:"=!

    REM Param 3: Delete the archive after expansion? only 'true' means true.
    set deleteAfter=%3
    set deleteAfter=!deleteAfter:"=!

    if /i "%verbosity%" neq "quiet" call :WriteLine "Extracting !archiveName!" "!color_info!"

    REM Try tar first. If that doesn't work, fall back to powershell (slow)
    set tarSuccessful=true
    tar -xf "!archiveName!" --directory "!dirToExtract!" 
   
    REM error 9009 means "command not found"
    if errorlevel 9009 set tarSuccessful=false
    if errorlevel 1 set tarSuccessful=false

    if "!tarSuccessful!" == "false" ( 

        REM If we don't have tar, use powershell
        call :Write "Tar failed - moving to PowerShell..." "!color_info!"

        REM This fails if the tar left debris. We need to force overwrite
        REM powershell -command "Add-Type -assembly System.IO.Compression.Filesystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('!archiveName!', '.')" 

        REM Expand-Archive is really, really slow, but it's a solid backup
        powershell -command "Expand-Archive -Path '!archiveName!' -DestinationPath '!dirToExtract!' -Force"

        if errorlevel 1 exit /b 1
    )

    REM Remove the archive 
    if "!deleteAfter!" == "true" (
        if /i "%verbosity%" neq "quiet" call :WriteLine "Deleting !archiveName!" "!color_info!"
        del /s /f /q "!archiveName!" >NUL 2>&1
    )
 
    exit /b


:SetupDotNet
    SetLocal EnableDelayedExpansion

    REM only major versions accepted to this method
    set requestedNetVersion=%1
    set requestedType=%2

    for /f "tokens=1 delims=." %%a in ("!requestedNetVersion!") do ( set requestedNetMajorVersion=%%a )
    set requestedNetMajorVersion=!requestedNetMajorVersion: =!

    call :Write "Checking for .NET !requestedNetMajorVersion!.0..."

    set currentDotNetVersion=0
    set comparison=-1

    if /i "!requestedType!" == "SDK" (

        call :Write "Checking SDKs..."

        REM dotnet --version gives the SDK version, not the runtime version.
        dotnet --version >NUL 2>NUL
        if "%errorlevel%" == "0" (
            FOR /F "tokens=* USEBACKQ" %%F IN (`dotnet --version 2^>NUL`) DO ( set currentDotNetVersion=%%F )

            call :compareVersions "!currentDotNetVersion!" "!requestedNetMajorVersion!.0.0"
            set comparison=!compareResult!
            
            rem echo comparing current !currentDotNetVersion! to request !requestedNetMajorVersion!.0.0
            rem echo current_comparison = !comparison!
        )
    ) else (
        
        call :Write "Checking runtimes..."

        REM Let's test the runtimes only, since that's all we need
        REM example output from 'dotnet --list-runtimes'
        REM Microsoft.AspNetCore.App 7.0.11 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
        REM Microsoft.NETCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

        for /f "tokens=*" %%x in ('dotnet --list-runtimes 2^>NUL') do (
            set line=%%x

            if "!line:~0,21!" == "Microsoft.NETCore.App" (

                for /F "tokens=2 delims= " %%a in ("!line!") do set "dotnet_version=%%a"

                call :compareVersions "!dotnet_version!" "!requestedNetMajorVersion!.0.0"
                REM set current_comparison=%errorlevel%
                set current_comparison=!compareResult!

                REM echo current_comparison=!current_comparison!
                REM echo dotnet_version=!dotnet_version!

                if !current_comparison! GTR !comparison! (
                    set comparison=!current_comparison!
                    set currentDotNetVersion=!dotnet_version!
                )
            )
        )
    )

    if !comparison! EQU 0 (
        call :WriteLine  "All good. .NET is !currentDotNetVersion!" !color_success!
    ) else if !comparison! LSS 0 (
        call :WriteLine  "Upgrading: .NET is !currentDotNetVersion!" !color_warn!
    ) else (
        call :WriteLine  "All good. .NET is !currentDotNetVersion!" !color_success!
    )

    if !comparison! LSS 0 (
        if /i "!offlineInstall!" == "true" (
            call :WriteLine "Offline Installation: Unable to download and install .NET." %color_error%
        ) else (
            call :WriteLine "Current version is !currentDotNetVersion!. Installing newer version." %color_warn%
            if /i "!architecture!" == "arm64" (
                if /i "!requestedType!" == "SDK" (
                    powershell -NoProfile -ExecutionPolicy unrestricted -File "!sdkScriptsDirPath!\dotnet-install.ps1" --Version !requestedNetVersion!
                ) else (
                    powershell -NoProfile -ExecutionPolicy unrestricted  -File "!sdkScriptsDirPath!\dotnet-install.ps1" --Runtime aspnetcore --Version !requestedNetVersion!
                )
                if "%DOTNET_ROOT%" == "" (
                    SET "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
                    SETX "DOTNET_ROOT" "%LOCALAPPDATA%\Microsoft\dotnet"
                    SET "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
                    SETX "PATH" "%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
                )
            ) else (
                if /i "!requestedType!" == "SDK" (
                    winget install Microsoft.DotNet.SDK.!requestedNetMajorVersion!
                ) else (
                    winget install Microsoft.DotNet.AspNetCore.!requestedNetMajorVersion!
                )
            )
        )
    )

    exit /b

:SetupPython
    SetLocal EnableDelayedExpansion

    REM A number of global variables are assumed here
    REM  - pythonVersion     - version in X.Y format
    REM  - pythonName        - eg python37 or python311
    REM  - venvPythonCmdPath - the path to the python interpreter for this venv
    REM  - virtualEnvDirPath - the path to the virtual environment for this module

    if /i "!offlineInstall!" == "true" (
        call :WriteLine "Offline Installation: Skipping download and installation of Python." %color_error%
        exit /b
    )

    REM We need to ensure the requested version Python is installed somewhere so
    REM we can use it to create virtual environments. In Windows we install Python
    REM in the /runtimes folder. In Linux/macOS we install Python directly into
    REM the standard system folders (so in linux/macOS the pythonRuntimeInstallPath
    REM doesn't exist)

    REM The path to the folder containing the base python installation
    set pythonRuntimeInstallPath=!runtimesDirPath!\bin\!os!\!pythonName!

    REM For debugging, or correcting, we can force redownloads. Be careful though.
    if /i "%forceOverwrite%" == "true" (

        REM Force Re-download
        call :WriteLine "Cleaning download directory to force re-download of Python" "!color_info!"
        if exist "!downloadDirPath!\!platform!\!pythonName!" (
            rmdir /s "%rmdirFlags% "!downloadDirPath!\!platform!\!pythonName!"
        )

        REM Force overwrite of python installation
        call :WriteLine "Cleaning Python directory to force re-install of Python" "!color_info!"
        call :WriteLine "This will mean any previous PIP installs wwill be lost." "!color_warn!"
        if exist "!pythonRuntimeInstallPath!" rmdir /s %rmdirFlags% "!pythonRuntimeInstallPath!"
    )

    if /i "%verbosity%" neq "quiet" call :WriteLine "Installing !pythonName! in !pythonRuntimeInstallPath!" "!color_info!"

    REM basePythonCmdPath is the path to the "base" python interpreter which will
    REM then be used to create virtual environments. We will test if the given 
    REM version of python is installed on the system, and if not we'll install it.
    
    REM TODO: We need to check if this version of python exists on the current system
    REM       and if so, we set basePythonCmdPath to that. ("where python3" gives us the default) 
    set basePythonCmdPath=!pythonRuntimeInstallPath!\python.exe

    if exist "!basePythonCmdPath!" (
        call :WriteLine "Python !pythonVersion! is already installed" "!color_success!"
    ) else (
        set pythonDownloadDir=!downloadDirPath!\!platform!\
        if not exist "!pythonDownloadDir!"             mkdir "!pythonDownloadDir!"
        if not exist "!pythonDownloadDir!!pythonName!" mkdir "!pythonDownloadDir!!pythonName!"

        if not exist "!pythonRuntimeInstallPath!" (

            REM if not exist "!pythonRuntimeInstallPath!"       mkdir "!pythonRuntimeInstallPath!"
            if not exist "!runtimesDirPath!\bin"                   mkdir "!runtimesDirPath!\bin"
            if not exist "!runtimesDirPath!\bin\!os!"              mkdir "!runtimesDirPath!\bin\!os!"
            if not exist "!runtimesDirPath!\bin\!os!\!pythonName!" mkdir "!runtimesDirPath!\bin\!os!\!pythonName!"

            REM Params are:      S3 storage bucket |    fileToGet    | downloadToDir | dirToExtract | message
            call :DownloadAndExtract "!storageUrl!runtimes/" "!pythonName!.zip" "!pythonDownloadDir!"  "!pythonName!" "Downloading Python !pythonVersion! interpreter..."

            if exist "!downloadDirPath!\!platform!\!pythonName!" (
                robocopy /e "!downloadDirPath!\!platform!\!pythonName! " "!pythonRuntimeInstallPath! " /XF "!pythonName!.zip" !roboCopyFlags! >NUL
            ) else (
                REM if /i "%verbosity%" neq "quiet" (
                    call :WriteLine "Failed to download and extract !pythonName!.zip" "!color_error!"
                REM )
                exit /b 1
            ) 
        )
    )

    REM Create the virtual environments. All sorts of things can go wrong here
    REM but if you have issues, make sure you delete the venv directory before
    REM retrying.
    call :Write "Creating Virtual Environment (!runtimeLocation!)..."
    if exist "!venvPythonCmdPath!" (
        call :WriteLine "Virtual Environment already present" %color_success%
    ) else (
        if /i "%verbosity%" neq "quiet" call :WriteLine "Virtual Environment doesn't exist. Creating at !virtualEnvDirPath!"
        "!basePythonCmdPath!" -m venv "!virtualEnvDirPath!"
        call :WriteLine "done" %color_success%
    )

    REM Ensure Python in the venv Exists
    call :Write "Confirming we have Python !pythonVersion! in our virtual environment..."
    "!venvPythonCmdPath!" --version | find "!pythonVersion!" >NUL
    if errorlevel 1 goto errorNoPython
    call :WriteLine "present" %color_success%

    exit /b



:GetRequirementsFile

    SetLocal EnableDelayedExpansion

    set searchDir=%~1

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Searching for a suitable requirements.txts file in !searchDir!" "!color_info!"
    )

    REM This is getting complicated. The order of priority for the requirements file is:
    REM
    REM  requirements.device.txt                            (device = "raspberrypi", "orangepi", "radxarock" or "jetson" )
    REM  requirements.os.architecture.cudaMajor_Minor.txt   (eg cuda12_0)
    REM  requirements.os.architecture.cudaMajor.txt         (eg cuda12)
    REM  requirements.os.architecture.(cuda|rocm).txt
    REM  requirements.os.cudaMajor_Minor.txt
    REM  requirements.os.cudaMajor.txt
    REM  requirements.os.(cuda|rocm).txt
    REM  requirements.cudaMajor_Minor.txt
    REM  requirements.cudaMajor.txt
    REM  requirements.(cuda|rocm).txt
    REM  requirements.os.architecture.gpu.txt
    REM  requirements.os.gpu.txt
    REM  requirements.gpu.txt
    REM  requirements.os.architecture.txt
    REM  requirements.os.txt
    REM  requirements.txt
    REM
    REM The logic here is that we go from most specific to least specific. The only
    REM real tricky bit is the subtlety around .cuda vs .gpu. CUDA / ROCm are specific
    REM types of card. We may not be able to support that, but may be able to support
    REM other cards generically via OpenVINO or DirectML. So CUDA or ROCm first,
    REM then GPU, then CPU. With a query at each step for OS and architecture.

    set requirementsFilename=

    REM This isn't actually used. Just here as a reminder it's possible, but also
    REM that the linux/macOS installers will use it
    REM set device_specifier=...
    REM if exist "!requirementsDir!\requirements.!device_specifier!.txt" (
    REM     set requirementsFilename=requirements.!device_specifier!.txt
    REM )

    if "!requirementsFilename!" == "" (

        REM Unless installGPU is false, we are installing CUDA equipped packages 
        REM even if EnableGPU = false in the modulesettings files. This allows
        REM  you to toggle CUDA support at runtime, rather than install time.
        if /i "!installGPU!" == "true" (
            if /i "!hasCUDA!" == "true" (

                if "!cuda_version!" neq "" (
                    set cuda_major_minor=!cuda_version:.=_!

                    if /i "%verbosity%" neq "quiet" (
                        call :WriteLine "CUDA version is !cuda_version! (!cuda_major_minor! / !cuda_major_version!)" "!color_info!"
                    )

                    if exist "!searchDir!\requirements.windows.!architecture!.cuda!cuda_major_minor!.txt" (
                        set requirementsFilename=requirements.windows.!architecture!.cuda!cuda_major_minor!.txt
                    ) else if exist "!searchDir!\requirements.windows.!architecture!.cuda!cuda_major_version!.txt" (
                        set requirementsFilename=requirements.windows.!architecture!.cuda!cuda_major_version!.txt
                    ) else if exist "!searchDir!\requirements.windows.!architecture!.cuda.txt" (
                        set requirementsFilename=requirements.windows.!architecture!.cuda.txt
                    ) else if exist "!searchDir!\requirements.windows.cuda!cuda_major_minor!.txt" (
                        set requirementsFilename=requirements.windows.cuda!cuda_major_minor!.txt
                    ) else if exist "!searchDir!\requirements.windows.cuda!cuda_major_version!.txt" (
                        set requirementsFilename=requirements.windows.cuda!cuda_major_version!.txt
                    ) else if exist "!searchDir!\requirements.windows.cuda.txt" (
                        set requirementsFilename=requirements.windows.cuda.txt
                    ) else if exist "!searchDir!\requirements.cuda!cuda_major_minor!.txt" (
                        set requirementsFilename=requirements.cuda!cuda_major_minor!.txt
                    ) else if exist "!searchDir!\requirements.cuda!cuda_major_version!.txt" (
                        set requirementsFilename=requirements.cuda!cuda_major_version!.txt
                    ) else if exist "!searchDir!\requirements.cuda.txt" (
                        set requirementsFilename=requirements.cuda.txt
                    )
                )

                if "!requirementsFilename!" == "" (
                    if exist "!searchDir!\requirements.windows.!architecture!.cuda.txt" (
                        set requirementsFilename=requirements.windows.!architecture!.cuda.txt
                    ) else if exist "!searchDir!\requirements.windows.cuda.txt" (
                        set requirementsFilename=requirements.windows.cuda.txt
                    ) else if exist "!searchDir!\requirements.cuda.txt" (
                        set requirementsFilename=requirements.cuda.txt
                    )
                )
            ) 

            if /i "!hasROCm!" == "true" (
                if exist "!searchDir!\requirements.windows.!architecture!.rocm.txt" (
                    set requirementsFilename=requirements.windows.!architecture!.rocm.txt
                ) else if exist "!searchDir!\requirements.windows.rocm.txt" (
                    set requirementsFilename=requirements.windows.rocm.txt
                ) else if exist "!searchDir!\requirements.rocm.txt" (
                    set requirementsFilename=requirements.rocm.txt
                )
            ) 

            if "!requirementsFilename!" == "" (
                if exist "!searchDir!\requirements.windows.!architecture!.gpu.txt" (
                    set requirementsFilename=requirements.windows.!architecture!.gpu.txt
                ) else if exist "!searchDir!\requirements.windows.gpu.txt" (
                    set requirementsFilename=requirements.windows.gpu.txt
                ) else if exist "!searchDir!\requirements.gpu.txt" (
                    set requirementsFilename=requirements.gpu.txt
                )
            )
        )
    )

    if "!requirementsFilename!" == "" (
        if exist "!searchDir!\requirements.windows.!architecture!.txt" (
            set requirementsFilename=requirements.windows.!architecture!.txt
        ) else if exist "!searchDir!\requirements.windows.txt" (
            set requirementsFilename=requirements.windows.txt
        ) else if exist "!searchDir!\requirements.txt" (
            set requirementsFilename=requirements.txt
        )
    )

    call :WriteLine "Python packages specified by !requirementsFilename!" "!color_info!"
    REM return value in 2nd parameter
    REM EndLocal & set "%~2=!requirementsFilename!"
    EndLocal & set "requirementsFilename=%requirementsFilename%"
    
    exit /b


:InstallPythonPackagesByName

    REM A number of global variables are assumed here
    REM  - pythonVersion     - version in X.Y format
    REM  - installLocation   - can be "Shared", "Local" or "System"
    REM  - pythonName        - eg python37 or python311
    REM  - venvPythonCmdPath - the path to the python interpreter for this venv
    REM  - virtualEnvDirPath - the path to the virtual environment for this module
    REM  - packagesDirPath   - site-packages location for this module

    SetLocal EnableDelayedExpansion

    REM List of packages to install separate by spaces
    set "packages=%~1"
    REM Description to use when describing the packages. Can be null.
    set "packages_desc=%~2" 
    REM Options to specify for the pip command (eg --index-url ...). Can be null.
    set "pip_options=%~3" 

    if /i "!offlineInstall!" == "true" (
        call :WriteLine "Offline Installation: Skipping download and installation of Python packages." %color_error%
        exit /b
    )

    REM For speeding up debugging
    if "!skipPipInstall!" == "true" exit /b

    if not exist "!venvPythonCmdPath!" (
        call :WriteLine "Virtual Environment was not created successfully." "!color_error!"
        exit /b 1
    )

    if /i "%verbosity%" neq "quiet" call :WriteLine "Installing all items in !packages!" "!color_info!"
    For /F "tokens=*" %%A IN ("!packages!") DO (

        set "package_name=%%A"
        
        REM If the module specifier isn't a URL or .whl then extract the module's name
        set module_name=
        if /i "%package_name:~0,4%" neq "http" (
            if /i "%package_name:~-4%" neq ".whl" ( 
                REM Extract the module_name from the form "module_name[(<=|==|=>|)version[,<=|=>|)version]]  # comment"
                REM Note we're also handling the form module_name[variant] (eg diffusers[torch])
                for /f "tokens=1 delims=[]>=<~, " %%G in ("!package_name!") do set "module_name=%%G"
            )
        )

        set package_desc=!packages_desc!
        if /i "!package_desc!" == "" set package_desc=!module_name!
        if /i "!package_desc!" == "" set package_desc=!package_name!

        if /i "%verbosity%" neq "quiet" call :WriteLine "Installing !package_name!" "!color_info!"
        call :Write "Installing !package_desc!..."

        REM Check if the module name is already installed
        set moduleInstalled=false
        if "!module_name!" neq "" (

            REM Run the pip show command, ditching error output. We just want stdout, and
            REM we'll check each line looking for the name of the module we're checking
            for /f "usebackq delims=" %%G in (`"!venvPythonCmdPath!" -m pip show !module_name! 2^>NUL`) do (
                REM look for the name of the module in this line of output.
                if "%%G" neq "" (
                    set line=%%G
                    set line=!line:^>=!
                    set line=!line:^<=!
                    echo !line! | find /I "Name:" >NUL
                    if !errorlevel! == 0 (
                        echo !line! | find /I "!module_name!" >NUL
                        if !errorlevel! == 0 set moduleInstalled=true
                    )
                )
            )
        )

        if /i "!moduleInstalled!" == "false" (

            REM echo [DEBUG] '!venvPythonCmdPath!' -m pip install !pipFlags! '!package_name!' --target '!packagesDirPath!' !pip_options! 
            if /i "%verbosity%" == "quiet" (
                "!venvPythonCmdPath!" -m pip install !pipFlags! "!package_name!" --target "!packagesDirPath!" !pip_options! >NUL 2>&1
            ) else (
                if /i "%verbosity%" == "info" (
                    "!venvPythonCmdPath!" -m pip install !pipFlags! "!package_name!" --target "!packagesDirPath!" !pip_options! >nul 
                ) else (
                    "!venvPythonCmdPath!" -m pip install !pipFlags! "!package_name!" --target "!packagesDirPath!" !pip_options! 
                )
            )

            REM If the module's name isn't simply a URL or .whl then actually check it worked
            if /i "%package_name:~0,4%" neq "http" (
                if /i "%package_name:~-4%" neq ".whl" ( 

                    REM Remove module endings starting with "==" using findstr and loop
                    for /f "tokens=1,2 delims=[]>==< " %%a in ("!package_name!") do (
                        set "module_name=%%a"
                        REM set "module_version=%%b"
                        set "module_name=!module_name:==!"
                    )
                    "!venvPythonCmdPath!" -m pip show !module_name! >NUL 2>&1
                    if !errorlevel! == 0 (
                        rem ✔ ✅ 🗹
                        call :Write "(✔ checked) " !color_success!
                    ) else (
                        call :Write "(❌ failed check) " !color_error!
                    )
                ) else (
                    call :Write "(not checked) " !color_warn!
                )
            ) else (
                call :Write "(not checked) " !color_warn!
            )
        
            call :WriteLine "done" %color_success%

        ) else (
            call :WriteLine "Already installed" %color_success%
        )
    )
    
    exit /b
    

:InstallRequiredPythonPackages

    REM A number of global variables are assumed here
    REM  - pythonVersion     - version in X.Y format
    REM  - pythonName        - eg python37 or python311
    REM  - venvPythonCmdPath - the path to the python interpreter for this venv
    REM  - virtualEnvDirPath - the path to the virtual environment for this module
    REM  - packagesDirPath   - site-packages location for this module

    SetLocal EnableDelayedExpansion

    if /i "!offlineInstall!" == "true" (
        call :WriteLine "Offline Installation: Skipping download and installation of Python packages." %color_error%
        exit /b
    )

    REM For speeding up debugging
    if "!skipPipInstall!" == "true" exit /b

    if not exist "!venvPythonCmdPath!" (
        call :WriteLine "Virtual Environment was not created successfully." "!color_error!"
        exit /b 1
    )


    REM =======================================================================
    REM  Install pre-requisites

    call :Write "Ensuring Python package manager (pip) is installed..."
    if /i "%verbosity%" == "quiet" (
        "!venvPythonCmdPath!" -m ensurepip >NUL 2>&1
    ) else (
        "!venvPythonCmdPath!" -m ensurepip
    )
    call :WriteLine "done" %color_success%

    call :Write "Ensuring Python package manager (pip) is up to date..."

    REM Upgrade to the latest pip
    if /i "%verbosity%" == "quiet" (
        "!venvPythonCmdPath!" -m pip install --trusted-host pypi.python.org ^
                              --trusted-host files.pythonhosted.org ^
                              --trusted-host pypi.org --upgrade setuptools !pipFlags! >NUL 2>&1

        "!venvPythonCmdPath!" -m pip install --trusted-host pypi.python.org ^
                              --trusted-host files.pythonhosted.org ^
                              --trusted-host pypi.org --upgrade pip !pipFlags! >NUL 2>&1
    ) else (
        "!venvPythonCmdPath!" -m pip install --trusted-host pypi.python.org ^
                              --trusted-host files.pythonhosted.org ^
                              --trusted-host pypi.org --upgrade setuptools !pipFlags!

        "!venvPythonCmdPath!" -m pip install --trusted-host pypi.python.org ^
                              --trusted-host files.pythonhosted.org ^
                              --trusted-host pypi.org --upgrade pip !pipFlags!
    )
    
    call :WriteLine "done" %color_success%


    REM ========================================================================
    REM Install PIP packages

    REM Getting the correct requirements file ----------------------------------

    REM if called with a parameter, use the supplied path to search for the
    REM requirements file. Otherwise use the module path.
    set requirementsSearchPath=!moduleDirPath!
    if /i "%~1" neq "" set requirementsSearchPath=%~1

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Requirements Search Path is !requirementsSearchPath!" "!color_info!"
    )
    
    call :GetRequirementsFile "!requirementsSearchPath!"

    if "!requirementsFilename!" == "" (
        call :WriteLine "No suitable requirements.txt file found." "!color_warn!"
        exit /b 1
    )

    set requirementsPath=!requirementsSearchPath!\!requirementsFilename!
    if not exist "!requirementsPath!" (
        call :WriteLine "The selected requirements file (!requirementsPath!) wasn't found." "!color_warn!"
        exit /b 1
    )

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Choosing Python packages from !requirementsPath!" "!color_info!"
    )

    if /i "!oneStepPIP!" == "true" (

        call :Write "Installing Packages into Virtual Environment..."
        REM pip install -r !requirementsPath! !pipFlags!
        if /i "%verbosity%" == "quiet" (
            "!venvPythonCmdPath!" -m pip install -r "!requirementsPath!" --target "!packagesDirPath!" !pipFlags! >nul
        ) else (
            "!venvPythonCmdPath!" -m pip install -r "!requirementsPath!" --target "!packagesDirPath!" !pipFlags! 
        )
        call :WriteLine "Success" "!color_success!"

    ) else (

        REM We'll do this the long way so we can see some progress

        set currentOption=
        for /f "tokens=*" %%x in (' more ^< "!requirementsPath!" ') do (
            set line=%%x

            if "!line!" == "" (
                set currentOption=
            ) else if "!line:~0,1!" == "#" (       REM Ignore comments
                set currentOption=
            ) else if "!line:~0,1!" == "-" (       REM For --index options etc
                set currentOption=!currentOption! !line!
            ) else (
        
                REM breakup line into module name and description
                set module=!line!
                for /F "tokens=1,2 delims=#" %%a in ("!line!") do (
                    set "module=%%a"
                    set description=%%b
                )

                if "!description!" == "" set description= Installing !module!

                if "!module!" NEQ "" (
                    call :Write "  -!description!..."

                    REM If the module specifier isn't a URL or .whl then extract the module's name
                    set module_name=
                    if /i "%module:~0,4%" neq "http" (
                        if /i "%module:~-4%" neq ".whl" ( 
                            REM Extract the module_name from the form "module_name[(<=|==|=>|)version[,<=|=>|)version]]  # comment"
                            REM Note we're also handling the form module_name[variant] (eg diffusers[torch])
                            for /f "tokens=1 delims=[]>=<~, " %%G in ("!module!") do set "module_name=%%G"
                        )
                    )

                    REM Check if the module name is already installed
                    set moduleInstalled=false
                    if "!module_name!" neq "" (

                        REM Run the pip show command, ditching error output. We just want stdout, and
                        REM we'll check each line looking for the name of the module we're checking
                        for /f "usebackq delims=" %%G in (`"!venvPythonCmdPath!" -m pip show !module_name! 2^>NUL`) do (
                            REM look for the name of the module in this line of output.
                            if "%%G" neq "" (
                                set line=%%G
                                set line=!line:^>=!
                                set line=!line:^<=!

                                echo !line! | find /I "Name:" >NUL
                                if !errorlevel! == 0 (
                                    echo !line! | find /I "!module_name!" >NUL
                                    if !errorlevel! == 0 set moduleInstalled=true
                                )
                            )
                        )
                    )

                    if /i "!moduleInstalled!" == "false" (
                        if /i "%verbosity%" == "quiet" (
                            "!venvPythonCmdPath!" -m pip install "!module!" !currentOption! --target "!packagesDirPath!" !pipFlags! >NUL 2>&1
                        ) else (
                            if /i "%verbosity%" == "info" (
                                "!venvPythonCmdPath!" -m pip install "!module!" !currentOption! --target "!packagesDirPath!" !pipFlags! >nul 
                            ) else (
                                "!venvPythonCmdPath!" -m pip install "!module!" !currentOption! --target "!packagesDirPath!" !pipFlags!
                            )
                        )

                        REM If the module's name isn't simply a URL or .whl then actually check it worked
                        if "!module_name!" neq "" (

                            "!venvPythonCmdPath!" -m pip show !module_name! >NUL 2>&1
                            if !errorlevel! == 0 (
                                call :Write "(✅ checked) " !color_success!
                            ) else (
                                call :Write "(❌ failed check) " !color_error!
                            )
                        ) else (
                            call :Write "(not checked) " !color_warn!
                        )
                        
                        call :WriteLine "done" %color_success%
                    ) else (
                        call :WriteLine "Already installed" %color_success%
                    )
                )

                set currentOption=
            )
        )
    )

    exit /b


:DownloadModels
    REM Downloads the models listed in a module's modulesettings file and marked as needing to be installed
    REM ASSUMPTION: moduleId and moduleDirPath are set

    SetLocal EnableDelayedExpansion

    call :Write "Scanning modulesettings for downloadable models..."

    set foundModels=false

    for /L %%i in (0,1,100) Do (

        call :GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "InstallOptions.DownloadableModels[%%i].Name"
        if /i "!moduleSettingsFileValue!" == "" (
            if /i "!foundModels!" == "false" call :WriteLine "No models specified" "!color_mute!"
            exit /b
        )

        set modelName=!moduleSettingsFileValue!
        call :GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "InstallOptions.DownloadableModels[%%i].PreInstall"
        if /i "!moduleSettingsFileValue!" == "true" (

            if /i "!foundModels!" == "false" call :WriteLine "Processing model list"
            set foundModels=true

            call :GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "InstallOptions.DownloadableModels[%%i].Filename"
            set modelFileName=!moduleSettingsFileValue!
            call :GetValueFromModuleSettingsFile "!moduleDirPath!", "!moduleId!", "InstallOptions.DownloadableModels[%%i].Folder"
            set modelFolderName=!moduleSettingsFileValue!

            call "%sdkScriptsDirPath%\utils.bat" GetFromServer "models/" "!modelFileName!" "!modelFolderName!" "Downloading !modelName!..."
        )
    )

    if /i "!foundModels!" == "false" call :WriteLine "No models specified" "!color_mute!"

    exit /b


:GetCudaVersion

    rem setlocal enabledelayedexpansion

    :: Use nvcc to find the CUDA version
    where nvcc >nul 2>&1
    if !errorlevel! == 0 (
        REM Get the line containing "release x.y"
        for /f "tokens=*" %%i in ('nvcc --version ^| findstr /C:"release"') do set cudaLine=%%i
        REM Get the 5th token in the line when split by , and spaces
        for /f "tokens=5 delims=, " %%a in ("!cudaLine!") do set cuda_version=%%a
    ) else (
        REM Backup attempt: Use nvidia-smi to find the CUDA version
        where nvidia-smi >nul 2>&1
        if !errorlevel! == 0 (
            REM Get the line containing "CUDA Version x.y"
            for /f "tokens=*" %%i in ('nvidia-smi ^| findstr /C:"CUDA Version"') do set cudaLine=%%i
            REM Get the 9th token in the line when split by spaces
            for /f "tokens=9 delims= " %%a in ("!cudaLine!") do set cuda_version=%%a
        ) else (
            REM echo Unable to find nvcc or nvidia-smi
        )
    )

    REM echo cudaLine = !cudaLine!
    REM echo GetCudaVersion version: !cuda_version!

    if "!cuda_version!" neq "" (
        for /f "tokens=1,2 delims=." %%a in ("!cuda_version!") do (
            set "cuda_major_version=%%a"
            exit /b
        )
    )

    REM pass back values as in params 1 and 2
    REM set "%~1=!cuda_version!"
    REM set "%~2=!cuda_major_version!"

    exit /b

:GetcuDNNVersion

    REM Typically C:\Program Files\NVIDIA\CUDNN\v8.5\bin
    for %%G in ("%PATH:;=", "%") do (
        set "pathPart=%%~G"
        REM echo !pathPart!
        echo !pathPart! | findstr /i /r /c:"NVIDIA\\cuDNN\\v[0-9]*.[0.9]" > nul
        if !errorlevel! == 0 (
            REM @echo !pathPart!
            set "prevPart="
            for %%a in ("!pathPart:\=", "!") do (
                set "part=%%~a"
                REM echo !part!
                if /i "!prevPart!" == "cuDNN" (
                    if /i "!part:~0,1!" == "v" (
                        set "cuDNN_version=!part:~1!"
                        REM @echo cuDNN version = !cuDNN_version!
                        exit /b
                    ) else (
                        set prevPart=!part!
                    )
                ) else (
                    set prevPart=!part!
                )
           )
        )
    )

    exit /b

:GetValueFromModuleSettingsFile moduleDirPath moduleId property returnValue

    SetLocal EnableDelayedExpansion

    set moduleDirPath=%~1
    set moduleId=%~2
    set property=%~3

    if "!debug_json_parse!" == "true" (
        if /i "%verbosity%" neq "quiet" (
            call :WriteLine "Searching for '!moduleId!.!property!' in a suitable modulesettings.json file in !moduleDirPath!" "!color_info!"
        )
    )

    REM Module settings files are loaded in this order. Each file will overwrite (but not delete)
    REM settings of the previous file. Becuase of this, we're going to search the files in REVERSE
    REM order until we find the first value based on the most specific to least specific file.
    REM   modulesettings.json
    REM   modulesettings.development.json 
    REM   modulesettings.os.json
    REM   modulesettings.os.development.json
    REM   modulesettings.os.architecture.json
    REM   modulesettings.os.architecture.development.json
    REM   (not supported) modulesettings.docker.json
    REM   (not supported) modulesettings.docker.development.json
    REM   (not needed yet) modulesettings.device.json (device = raspberrypi, orangepi, radxarock, jetson)
    
    set moduleSettingValue=
    
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirPath!\modulesettings.windows.!architecture!.development.json", "!moduleId!", "!property!"
        if /i "!verbosity!" neq "quiet" if "!moduleSettingValue!" NEQ "" (
            call :WriteLine "Used modulesettings.windows.!architecture!.development.json to get value !moduleSettingValue!" "!color_info!"
        )
    )
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirPath!\modulesettings.windows.!architecture!.json", "!moduleId!", "!property!"
        if /i "!verbosity!" neq "quiet" if "!moduleSettingValue!" NEQ "" (
            call :WriteLine "Used modulesettings.windows.!architecture!.json to get value !moduleSettingValue!" "!color_info!"
        )
    )
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirPath!\modulesettings.windows.development.json", "!moduleId!", "!property!"
        if /i "!verbosity!" neq "quiet" if "!moduleSettingValue!" NEQ "" (
            call :WriteLine "Used modulesettings.windows.development.json to get value !moduleSettingValue!" "!color_info!"
        )
    )
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirPath!\modulesettings.windows.json", "!moduleId!", "!property!"
        if /i "!verbosity!" neq "quiet" if "!moduleSettingValue!" NEQ "" (
            call :WriteLine "Used modulesettings.windows.json to get value !moduleSettingValue!" "!color_info!"
        )
    )
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirPath!\modulesettings.development.json", "!moduleId!", "!property!"
        if /i "!verbosity!" neq "quiet" if "!moduleSettingValue!" NEQ "" (
            call :WriteLine "Used modulesettings.development.json to get value !moduleSettingValue!" "!color_info!"
        )
    )
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirPath!\modulesettings.json", "!moduleId!", "!property!"
        if /i "!verbosity!" neq "quiet" if "!moduleSettingValue!" NEQ "" (
            call :WriteLine "Used modulesettings.json to get value !moduleSettingValue!" "!color_info!"
        )
    )

    if "!debug_json_parse!" == "true" (
        if "!moduleSettingValue!" == "" (
            call :WriteLine "Cannot find !moduleId!.!property! in any modulesettings in !moduleDirPath!" "!color_info!"
        ) else (
            call :WriteLine !moduleId!.!property! is !moduleSettingValue! in any modulesettings in !moduleDirPath!" "!color_info!"
        )
    )

    REM return value in 4th parameter
    REM EndLocal & set "%~4=!moduleSettingValue!"
    EndLocal & set "moduleSettingsFileValue=%moduleSettingValue%"

    exit /b


REM Gets a value from the modulesettings.json file (any JSON file, really) based
REM purely on the name of the propery. THIS METHOD DOES NOT TAKE INTO ACCOUNT THE
REM DEPTH OF A PROPERTY. If the property is at the root level or 10 levels down,
REM it's all the same. The extraction is done purely by grep/sed, so is very niaive. 
:GetValueFromModuleSettings  jsonFilePath moduleId property returnValue
    set "moduleSettingValue="
    SetLocal EnableDelayedExpansion

    set jsonFilePath=%~1
    set moduleId=%~2
    set property=%~3

    if "!debug_json_parse!" == "true" (
        if /i "!verbosity!" neq "quiet" (
            call :WriteLine "Searching for '!moduleId!.!property!' in '%jsonFilePath%'" "!color_info!"
        )
    )

    if not exist "!jsonFilePath!" (
        if "!debug_json_parse!" == "true" if /i "!verbosity!" neq "quiet" (
            call :WriteLine "Can't find '%jsonFilePath%'" "!color_info!"
        )
        EndLocal & set "moduleSettingValue="
        exit /b
    )

    REM RANT: Douglas Crockford decided that people were abusing the comment 
    REM syntax in JSON and so he removed it. Devs immediately added hack work-
    REM arounds which nullified his 'fix' and instead has left us with a crippled
    REM data format that has wasted countless developer hours. Explaining one's
    REM work so the next person can maintain it is critical. J in JSON stands for
    REM Javascript. The Industry needs to stop honouring a pointless short-sighted
    REM decision and standardise Javascript-style comments in JSON. 

    REM Options are 'jq' for the jq utility, 'parsejson' for our .NET utility, or
    REM 'sed', which is what one would use if they have given up all hope for 
    REM humanity. jq is solid but doesn't do comments. See above. ParseJSON does
    REM some comments but not all, so not helpful enough for the overhead. 

    if /i "!useJq!" == "true" (
        set parse_mode=jq
        REM escape '-'s
        if "!property:-=!" neq "!property!" set property="[""!property!""]"
        if "!moduleId:-=!" neq "!moduleId!" set moduleId="[""!moduleId!""]"
        set "key=.Modules.!moduleId:.=\.!.!property!"
    ) else (
        set parse_mode=parsejson
        set "key=$.Modules.!moduleId:.=\.!.!property!"
    )
  
    if /i "!parse_mode!" == "jq" (

        REM We have a problem with '!'. Because we're using delayed expansion, the ! gets stripped
        REM by echo and so we lose them from the file. Replacing "!" in a string inside a method
        REM using delayed expansion is a little too painful so we get around it by using powershell
        REM to do the heavy lifting. It's tediously slow, but works.
        
        REM Handy if you want to see the interim output of just big files
        REM if exist temp_settings1.json (
        REM     for %%I in ("temp_settings1.json") do set filesize=%%~zF
        REM     if !filesize! GTR 1000 goto:eof
        REM )

        REM Step 1. Encode "!"
        powershell -Command "(Get-Content '!jsonFilePath!') -replace '^!','^^^!' | Out-File -FilePath 'temp_settings1.json' -Force -Encoding utf8"

        REM Step 2. Strip comments
        call :StripJSONComments temp_settings1.json temp_settings2.json
        del temp_settings1.json

        REM And extract the property
        set jsonValue=
        for /f "usebackq delims=" %%j in (` type temp_settings2.json ^| "!sdkPath!\Utilities\jq-windows-amd64.exe" -r %key% `) do (
            set "jsonValue=!jsonValue!%%j"
        )
        del temp_settings2.json

        REM and thanks for this, jq...
        if /i "!jsonValue!" == "null" set "jsonValue="

    ) else if /i "!parse_mode!" == "parsejson" (     

        REM Handling quotes and spaces inside a FOR loop is a PITA. Use this trick to get to the
        REM directory containing the JSON file so we can skip quotes on !jsonFilePath!
        pushd "!jsonFilePath!\.."

		REM Extract the filename.ext from the json file since it's now in the current dir. This
        REM allows us to parse the JSON file without quotes. ASSUMING jsonFile DOESN'T HAVE SPACES
		for %%A in ("!jsonFilePath!") do set "jsonFileName=%%~nxA"
        
        REM Run the ParseJSON command on jsonFilePath, and collect ALL lines of output (eg arrays) 
        REM into the jsonValue var. Note the quotes around ParseJSON, but not around key or jsonFileName
        set "jsonValue="
        for /f "usebackq tokens=*" %%i in (` "%sdkPath%\Utilities\ParseJSON\ParseJSON.exe" !key! !jsonFileName! `) do (
            set jsonValue=!jsonValue!%%i
        )

        REM Go back from whence we came
        popd

    ) else (

        REM or use inbuilt DOS commands. This will not allow JSON path searching so is very limited
        set jsonValue=
        for /f "usebackq tokens=2 delims=:," %%a in (`findstr /I /R /C:"\"!key!\"[^^{]*$" "!jsonFilePath!"`) do (
            set "jsonValue=%%a"
            set jsonValue=!jsonValue:"=!
            set "jsonValue=!jsonValue: =!"
        )
    )

    if "!debug_json_parse!" == "true" (
        if "!jsonValue!" == "" (
            call :WriteLine "Cannot find !key! in !jsonFilePath!" "!color_info!"
        ) else (
            call :WriteLine "** !key! is !jsonValue! in !jsonFilePath!" "!color_info!"
        )
    )

    REM return value in 3rd parameter
    REM EndLocal & set %~3=!jsonValue!
    REM Or not...
    EndLocal & set "moduleSettingValue=%jsonValue%"

    exit /b

REM Gets the moduleID from a modulesettings.json file.  See above function for commentss
:GetModuleIdFromModuleSettingsFile  jsonFilePath returnValue
    set "moduleSettingValue="
    SetLocal EnableDelayedExpansion

    set jsonFilePath=%~1

    if not exist "!jsonFilePath!" (
        echo Cannot find file "!jsonFilePath!"
        EndLocal & set "moduleSettingValue="
        exit /b
    )

    if /i "!useJq!" == "true" (
        set parse_mode=jq
        set key=.Modules | keys[0]
    ) else (
        set parse_mode=parsejson
        set key=$.Modules.#keys[0]
    )
   
    if /i "!parse_mode!" == "jq" (

        REM Step 1. Encode "!"
        powershell -Command "(Get-Content '!jsonFilePath!') -replace '^!','^^^!' | Out-File -FilePath 'temp_settings1.json' -Force -Encoding utf8"

        REM Step 2. Strip comments
        call :StripJSONComments temp_settings1.json temp_settings2.json
        del temp_settings1.json

        REM And extract the property
        set jsonValue=
        for /f "usebackq delims=" %%j in (` type temp_settings2.json ^| "!sdkPath!\Utilities\jq-windows-amd64.exe" -r %key% `) do (
            set "jsonValue=!jsonValue!%%j"
        )
        del temp_settings2.json

        REM and thanks for this, jq...
        if /i "!jsonValue!" == "null" set "jsonValue="

    ) else if /i "!parse_mode!" == "parsejson" (     

        REM Handling quotes and spaces inside a FOR loop is a PITA. Use this trick to get to the
        REM directory containing the JSON file so we can skip quotes on !jsonFilePath!
        pushd "!jsonFilePath!\.."

		REM Extract the filename.ext from the json file since it's now in the current dir. This
        REM allows us to parse the JSON file without quotes. ASSUMING jsonFile DOESN'T HAVE SPACES
		for %%A in ("!jsonFilePath!") do set "jsonFileName=%%~nxA"
        
        REM Run the ParseJSON command on jsonFilePath, and collect ALL lines of output (eg arrays) 
        REM into the jsonValue var. Note the quotes around ParseJSON, but not around key or jsonFileName

        set "jsonValue="
        for /f "usebackq tokens=*" %%i in (` "%sdkPath%\Utilities\ParseJSON\ParseJSON.exe" !key! !jsonFileName! `) do (
            set jsonValue=!jsonValue!%%i
        )

        REM Go back from whence we came
        popd

    ) else (

        REM or use inbuilt DOS commands. This will not allow JSON path searching so is very limited
        set jsonValue=
        for /f "usebackq tokens=2 delims=:," %%a in (`findstr /I /R /C:"\"!key!\"[^^{]*$" "!jsonFilePath!"`) do (
            set "jsonValue=%%a"
            set jsonValue=!jsonValue:"=!
            set "jsonValue=!jsonValue: =!"
        )
    )

    if "!debug_json_parse!" == "true" (
        if "!jsonValue!" == "" (
            call :WriteLine "Cannot find !key! in !jsonFilePath!" "!color_info!"
        ) else (
            call :WriteLine "** !key! is !jsonValue! in !jsonFilePath!" "!color_info!"
        )
    )

    REM return value in 3rd parameter
    REM EndLocal & set %~3=!jsonValue!
    REM Or not...
    EndLocal & set "moduleSettingValue=%jsonValue%"

    exit /b


REM Strips single line comments from a file and stores the cleaned contents in a new file
:StripJSONComments
    setlocal enabledelayedexpansion

    set inputFilePath=%~1
    set cleanFilePath=%~2

    set debug=false

    :: Temporary file
    if exist "%cleanFilePath%" del "%cleanFilePath%"

    :: Process each line in the file
    set "insideMultilineComment=false"
    set "justExitedMultilineComment=false"
    for /f "delims=" %%a in ('type "%inputFilePath%"') do (

        set "line=%%a"
        set "newLine="

        REM Optimisation: We only need to process a line if it contains a //, /*, or */. To be
        REM even smarter, we only care about */ if we're in an open /* comment, but that split test
        REM will probably be a net negative. Pity this fails miserably
        REM echo !line! | findstr /R /C:"/\*" /C:"\*/" /C:"//" > nul
        if !errorlevel! == 0 (

            :: Process each character in the line
            set insideQuotes=false
            set cancelLoop=false

            REM MUCH better way of doing this is to go:
            REM 
            REM set "charPair= "
            REM :not_at_eol
            REM if "!charPair!" NEQ "" (
            REM     set "charPair=!line:~%%i,2!"
            REM     if "!charPair!"=="" goto :not_at_eol
            REM     ...
            REM     goto :not_at_eol
            REM )

            for /l %%i in (0,1,500) do (
            
                if "!cancelLoop!"=="false" (
                    set "charPair=!line:~%%i,2!"

                    REM Hit end of line?
                    if "!charPair!"=="" set cancelLoop=true
                )

                if "!cancelLoop!"=="false" (

                    REM if "!debug!" == "true" echo CHAR = !charPair!

                    REM checking if a string contains a " is really, really tricky
                    set firstChar=!charPair:~0,1!
                    set "checkVar=!firstChar:"=!"
                    if "!checkVar!" NEQ "!firstChar!" ( REM This means firstChar was a "
                        if "!insideQuotes!"=="false" (
                            REM if "!debug!" == "true" echo ENTER quotes
                            set "insideQuotes=true"
                        ) else (
                            REM  if "!debug!" == "true" echo EXIT quotes
                            set "insideQuotes=false"
                        )
                    )

                    REM If we find "//" and we're not inside quotes, stop processing the line
                    if "!charPair!"=="//" if "!insideQuotes!"=="false" set cancelLoop=true

                )
                
                REM Still going?
                if "!cancelLoop!"=="false" (
                
                    REM If we find "/*" and we're not inside quotes, we'll stop outputting
                    if "!charPair!"=="/*" if "!insideQuotes!"=="false" (
                        REM if "!debug!"=="true" echo ENTER multiline comment
                        set insideMultilineComment=true
                    )

                    REM Don't output stuff inside a comment, and don't output the trailing "/"
                    if "!insideMultilineComment!"=="false" if "!justExitedMultilineComment!"=="false" (
                        set "newLine=!newLine!!charPair:~0,1!"
                    )
                    set justExitedMultilineComment=false

                    if "!charPair!"=="*/" if "!insideMultilineComment!"=="true" (
                        set insideMultilineComment=false
                        set justExitedMultilineComment=true
                        REM if "!debug!" == "true" echo EXIT multiline comment
                    )

                )
            )
        )

        REM Only output changed lines during debug
        if "!debug!" == "true" if "!line!" NEQ "!newLine!" (
            echo Input:  "!line!"
            echo Output: "!newLine!"
        )

        REM Note the ":" to prevent ECHO is off messages on blank lines
        echo:!newLine! >> "%cleanFilePath%"
    )

    exit /b


REM Call this, then test: if "%online%" == "true" echo 'online'
:checkForInternet
    set online=false
    Ping 8.8.8.8 -n 1 -w >NUL 2>&1
    if errorlevel 1 set online=true
    exit /b


REM Thanks to https://stackoverflow.com/a/15809139/1128209
:compareVersions  version1  version2
::
:: Compares two version numbers and returns the result in the ERRORLEVEL
::
:: Returns 1 if version1 > version2
::         0 if version1 = version2
::        -1 if version1 < version2
::
:: The nodes must be delimited by . or , or -
::
:: Nodes are normally strictly numeric, without a 0 prefix. A letter suffix
:: is treated as a separate node
::
    setlocal enableDelayedExpansion

    set "v1=%~1"
    set "v2=%~2"

    REM echo Comparing !v1! to !v2!

    call :divideLetters v1
    call :divideLetters v2

    :loop
    call :parseNode "%v1%" n1 v1
    call :parseNode "%v2%" n2 v2

    REM echo Comparing !n1! to !n2!

    if %n1% gtr %n2% (
        REM echo %n1% greater than %n2%
        EndLocal & set compareResult=1
        exit /b 1
    )
    if %n1% lss %n2% (
        REM echo %n1% less than %n2%
        EndLocal & set compareResult=-1
        exit /b -1
    )
    if not defined v1 if not defined v2 (
        REM echo Neither value defined
        EndLocal & set compareResult=0
        exit /b 0
    )
    if not defined v1 (
        REM echo First value not defined
        EndLocal & set compareResult=-1
        exit /b -1
    )
    if not defined v2 (
        REM echo Second value not defined
        EndLocal & set compareResult=1
        exit /b 1
    )
    goto :loop

:parseNode  version  nodeVar  remainderVar
    for /f "tokens=1* delims=.,-" %%A in ("%~1") do (
        set "%~2=%%A"
        set "%~3=%%B"
    )
    exit /b

:divideLetters  versionVar
    for %%C in (a b c d e f g h i j k l m n o p q r s t u v w x y z) do set "%~1=!%~1:%%C=.%%C!"
    exit /b

:timeSince startTime duration

    setlocal enableDelayedExpansion

    set "startTime=%~1"

    REM Clean up
    set "startTime=!startTime: =0!"
    set "endTime=%time: =0%"

    set startTime=!startTime:,=.!
	set endTime=!endTime:,=.!

    if "!startTime!" == "" set startTime=0
    if "!endTime!" == ""   set endTime=0

    rem Convert times to integers for easier calculations

    REM First we need to remove leading 0's else CMD thinks they are octal
    REM eg "08" -> 1"08" % 100 -> 108 % 100 = 8
    for /F "tokens=1-4 delims=:.," %%a in ("!startTime!") do (
       set /A "start=((((1%%a %% 100)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100"
    )
    for /F "tokens=1-4 delims=:.," %%a in ("!endTime!") do ( 
       REM If build went past midnight, add 24hrs to end time to correct time wrapping
       IF !endTime! GTR !startTime! set /A "end=((((1%%a %% 100)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
       IF !endTime! LSS !startTime! set /A "end=(((((1%%a %% 100)+24)*60)+1%%b %% 100)*60+1%%c %% 100)*100+1%%d %% 100" 
    )

    REM echo !startTime!
    REM echo !endTime!

    rem Calculate the elapsed time 
    set /A elapsed=end-start

    rem Convert back to hr, min, sec
    set /A hh=elapsed/(60*60*100), rest=elapsed%%(60*60*100), mm=rest/(60*100), rest%%=60*100, ss=rest/100, cc=rest%%100
    if %hh% lss 10 set hh=0%hh%
    if %mm% lss 10 set mm=0%mm%
    if %ss% lss 10 set ss=0%ss%
    if %cc% lss 10 set cc=0%cc%

    REM echo !elapsed! !hh! !mm! !ss! !cc!

    REM Set global value
    EndLocal & set timeDuration=%hh%:%mm%:%ss%.%cc%
    REM Set return param value
    set "%~2=%timeDuration%"

    rem echo !timeDuration!

    exit /b

:: Jump points

:SetupScriptHelp
    call :Write "To install, run "
    call :Write "from this directory" "!color_error!"
    call :Write ":"

    if /i "%executionEnvironment%" == "Development" (
        call :WriteLine "..\..\Installers\Live\setup.bat" "!color_info!"
    ) else (
        call :WriteLine "..\..\..\Installers\Live\setup.bat" "!color_info!"
    )
    call :WriteLine "Ensure you run that command from the folder containing this script" "!color_warn!"
    pause
    exit /b


:errorNoPython
call :WriteLine
call :WriteLine
call :WriteLine "-------------------------------------------------------------"
call :WriteLine "Error: Python not installed" "!color_error!"
