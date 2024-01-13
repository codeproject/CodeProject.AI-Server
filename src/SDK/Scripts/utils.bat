:: CodeProject.AI Server Utilities
::
:: Utilities for use with Windows Development Environment install scripts
::
:: We assume we're in the source code /Installers/Dev directory.
::

@echo off

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

    REM Param 1: Name of the file to get eg packages_for_gpu.zip
    set fileToGet=%1
    set fileToGet=!fileToGet:"=!

    REM Param 3: Name of the folder within the current module where this download
    REM          will be stored. eg assets
    set moduleAssetsDir=%2
    set moduleAssetsDir=!moduleAssetsDir:"=!

    REM Param 3: output message
    set message=%3
    set message=!message:"=!

    REM Clean up directories to force a download and re-copy if necessary. Note that:
    REM  - moduleDirName is the name of the current module's directory
    REM  - moduleDirPath is the path to the module's directory
    REM  - downloadDirPath is the path where downloads are always stored (typically src/downloads)
    if /i "%forceOverwrite%" == "true" (
        REM Force Re-download, then force re-copy of downloads to install dir
        if exist "!downloadDirPath!\!moduleDirName!" rmdir /s %rmdirFlags% "!downloadDirPath!\!moduleDirName!"
        if exist "!moduleDirPath!\!moduleAssetsDir!" rmdir /s %rmdirFlags% "!moduleDirPath!\!moduleAssetsDir!"
    )
    
    REM Download !storageUrl!fileToGet to downloadDirPath and extract into downloadDirPath\moduleDirName
    REM Params are:     S3 storage bucket |  fileToGet   | downloadToDir  | dirToSaveTo  | message
    call :DownloadAndExtract "!storageUrl!" "!fileToGet!" "!downloadDirPath!\" "!moduleDirName!" "!message!"

    REM Copy contents of downloadDirPath\moduleDirName to modulesDirPath\moduleDirName\moduleAssetsDir
    if exist "!downloadDirPath!\!moduleDirName!" (

        REM if /i "%verbosity%" neq "quiet" ( ... )

        call :Write "Copying contents of !fileToGet! to !moduleAssetsDir!..."
        robocopy /e "!downloadDirPath!\!moduleDirName! " "!moduleDirPath!\!moduleAssetsDir! " /XF "*.zip" !roboCopyFlags! >NUL
        if errorlevel 8 (
            call :WriteLine "Some files not copied" !color_warn!
        ) else if errorlevel 16 (
            call :WriteLine "Failed" !color_error!
        ) else (
            call :WriteLine "done" !color_success!
        )

        REM Delete all but the zip file from the downloads dir
        call :Write "Cleaning up..."
        if /i "%verbosity%" neq "quiet" call :WriteLine "Cleaning up extracted files"
        FOR %%I IN ("!downloadDirPath!\!moduleDirName!\*") DO (
            IF /i "%%~xI" neq ".zip" (
                REM echo deleting %%I
                DEL "%%I" >NUL 2>&1
                REM echo cleaning "%%~nxI"
            )
        )
        call :WriteLine "done" !color_success!
    ) else (
        REM if /i "%verbosity%" neq "quiet" (
            call :WriteLine "Failed to download and extract !fileToGet!" "!color_error!"
        REM )
        exit /b 1
    )

    REM NOTE: Before each "exit" call we have a benign call to :WriteLine, which
    REM       means this method should never return with an errorlevel > 0. We do
    REM       this because robocopy will return status as errorlevel, with error
    REM       level up to 7 meaning "it may have worked". See the docs at
    REM       https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy

    exit /b


:DownloadAndExtract
    SetLocal EnableDelayedExpansion

    REM Param 1: The URL where the download can be found.
    REM eg "https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
    set sourceUrl=%1
    set sourceUrl=!sourceUrl:"=!

    REM Param 2: The file to download. eg packages_for_gpu.zip
    set fileToGet=%2
    set fileToGet=!fileToGet:"=!

    REM Param 3: Where to store the downloade zip. eg "downloads\" - relative
    REM          to the current directory
    set downloadToDir=%3
    set downloadToDir=!downloadToDir:"=!

    REM Param 4: The name of the folder within the downloads directory where 
    REM          the contents should be extracted. eg. assets 
    set dirToSaveTo=%4
    set dirToSaveTo=!dirToSaveTo:"=!

    REM Param 5: The output message
    set message=%5
    set message=!message:"=!

    if "!message!" == "" set message=Downloading !fileToGet!...

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Downloading !fileToGet! to !downloadToDir!!dirToSaveTo!" "!color_info!"
    )

    call :Write "!message!" "!color_primary!"

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Checking '!downloadToDir!!dirToSaveTo!\!fileToGet!'" "!color_info!"
    )
    
    if exist "!downloadToDir!!dirToSaveTo!\!fileToGet!" (
        call :Write "already exists..." "!color_info!"
    ) else (

        if /i "!offlineInstall!" == "true" (
            call :WriteLine "Offline Installation: Unable to download !fileToGet!." %color_error%
            exit /b 1
        )

        if not exist "!downloadToDir!"              mkdir "!downloadToDir!"
        if not exist "!downloadToDir!!dirToSaveTo!" mkdir "!downloadToDir!!dirToSaveTo!"

        REM Be careful with the quotes so we can handle paths with spaces
        powershell -command "Start-BitsTransfer -Source '!sourceUrl!!fileToGet!' -Description !fileToGet! -Destination '!downloadToDir!!dirToSaveTo!\!fileToGet!'"

        REM If these fail, it could be becuase of hanging transfers
        if errorlevel 1 (
            powershell -Command "Get-BitsTransfer | Remove-BitsTransfer"
            powershell -command "Start-BitsTransfer -Source '!sourceUrl!!fileToGet!' -Description !fileToGet! -Destination '!downloadToDir!!dirToSaveTo!\!fileToGet!'"
        )

        REM if that doesn't work, fallback to a slower safer method
        if errorlevel 1 (
            call :WriteLine "BITS transfer failed. Trying Powershell...." "!color_warn!"
            powershell -Command "Invoke-WebRequest '!sourceUrl!!fileToGet!' -OutFile '!downloadToDir!!dirToSaveTo!\!fileToGet!'"
            if errorlevel 1 (
                call :WriteLine "Download failed. Sorry." "!color_error!"
                exit /b 1
            )
        )

        if not exist "!downloadToDir!!dirToSaveTo!\!fileToGet!" (
            call :WriteLine "An error occurred that could not be resolved." "!color_error!"
            exit /b 1
        )
    )

    call :Write "Expanding..." "!color_info!"

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Heading to !downloadToDir!!dirToSaveTo!" "!color_info!"
    )

    pushd "!downloadToDir!!dirToSaveTo!"

    call :ExtractToDirectory "!fileToGet!"
    
    if errorlevel 1 (
        popd
        exit /b 1
    )

    popd

    call :WriteLine "Done." "!color_success!"

    exit /b

:ExtractToDirectory
    SetLocal EnableDelayedExpansion

    REM Param 1: The archive to expand. eg packages_for_gpu.zip
    set archiveName=%1
    set archiveName=!archiveName:"=!

    REM Param 2: Delete the archive after expansion? only 'true' means true.
    set deleteAfter=%2
    set deleteAfter=!deleteAfter:"=!

    set filenameWithoutExtension=%~n1

    if /i "%verbosity%" neq "quiet" (
        REM cd
        call :WriteLine "Extracting !archiveName!" "!color_info!"
    )

    REM Try tar first. If that doesn't work, fall back to powershell (slow)
    set tarSuccessful=true
    tar -xf "!archiveName!" >NUL 2>&1
   
    REM error 9009 means "command not found"
    if errorlevel 9009 set tarSuccessful=false
    if errorlevel 1 set tarSuccessful=false

    REM If we don't have tar, use powershell
    if "!tarSuccessful!" == "false" ( 
        call :Write "Tar failed - moving to PowerShell..." "!color_info!"

        REM This fails if the tar left debris. We need to force overwrite
        REM powershell -command "Add-Type -assembly System.IO.Compression.Filesystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('!archiveName!', '.')" 

        REM Cannot seem to get the call to the ZipFileExtension method correct
        REM powershell -command "[System.IO.Compression.ZipFile]::ExtractToDirectory('!archiveName!', '.', $true)"

        REM Expand-Archive is really, really slow, but it's our only hope here
        powershell -command "Expand-Archive -Path '!archiveName!' -DestinationPath '.' -Force"

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

    call :Write "Checking for .NET !requestedNetMajorVersion!.0 or greater..."

    set currentDotNetVersion=None
    set comparison=-1

    if /i "!requestedType!" == "SDK" (

        REM dotnet --version gives the SDK version, not the runtime version.
        FOR /F "tokens=* USEBACKQ" %%F IN (`dotnet --version`) DO ( set currentDotNetVersion=%%F )

    ) else (
        
        REM Let's test the runtimes only, since that's all we need
        REM example output from 'dotnet --list-runtimes'
        REM Microsoft.AspNetCore.App 7.0.11 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
        REM Microsoft.NETCore.App 3.1.32 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]

        for /f "tokens=*" %%x in ('dotnet --list-runtimes') do (
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
        call :WriteLine  "All good. .NET is !currentDotNetVersion!, requested was !requestedNetVersion!" !color_success!
    ) else if !comparison! LSS 0 (
        call :WriteLine  "Upgrading: .NET is !currentDotNetVersion!, requested was !requestedNetVersion!" !color_warn!
    ) else (
        call :WriteLine  "All good. .NET is !currentDotNetVersion!, requested was !requestedNetVersion!" !color_success!
    )

    if !comparison! LSS 0 (
        if /i "!offlineInstall!" == "true" (
            call :WriteLine "Offline Installation: Unable to download and install .NET." %color_error%
        ) else (
            call :WriteLine "Current version is !currentDotNetVersion!. Installing newer version." %color_warn%
            if /i "!requestedType!" == "SDK" (
                winget install Microsoft.DotNet.SDK.%requestedNetMajorVersion%
            ) else (
                winget install Microsoft.DotNet.AspNetCore.%requestedNetMajorVersion%
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

            REM Params are:      S3 storage bucket |    fileToGet    | downloadToDir | dirToSaveTo | message
            call :DownloadAndExtract "%storageUrl%" "!pythonName!.zip" "!pythonDownloadDir!"  "!pythonName!" "Downloading Python !pythonVersion! interpreter..."

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
    call :Write "Creating Virtual Environment (!pythonLocation!)..."
    if exist "!venvPythonCmdPath!" (
        call :WriteLine "Virtual Environment already present" %color_success%
    ) else (
        if /i "%verbosity%" neq "quiet" call :WriteLine "Virtual Environment doesn't exist. Creating at !virtualEnvDirPath!"
        "!basePythonCmdPath!" -m venv "!virtualEnvDirPath!"
        call :WriteLine "Done" %color_success%
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
    REM  requirements.device.txt                            (device = "raspberrypi", "orangepi" or "jetson" )
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
    REM  - installLocation   - can be "Shared" or "Local"
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
                for /f "tokens=1 delims=>=<~, " %%G in ("!package_name!") do set "module_name=%%G"
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
                    for /f "tokens=1,2 delims=>==< " %%a in ("!package_name!") do (
                        set "module_name=%%a"
                        REM set "module_version=%%b"
                        set "module_name=!module_name:==!"
                    )

                    "!venvPythonCmdPath!" -m pip show !module_name! >NUL 2>&1
                    if errorlevel 0 (
                        call :Write "(✔️ checked) " !color_info!
                    ) else (
                        call :Write "(failed check) " !color_error!
                    )
                ) else (
                    call :Write "(not checked) " !color_mute!
                )
            ) else (
                call :Write "(not checked) " !color_mute!
            )
        
            call :WriteLine "Done" %color_success%

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
    call :WriteLine "Done" %color_success%

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
    
    call :WriteLine "Done" %color_success%


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
                            for /f "tokens=1 delims=>=<~, " %%G in ("!module!") do set "module_name=%%G"
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
                            if errorlevel 0 (
                                call :Write "(✔️ checked) " !color_info!
                            ) else (
                                call :Write "(failed check) " !color_error!
                            )
                        ) else (
                            call :Write "(not checked) " !color_mute!
                        )
                        
                        call :WriteLine "Done" %color_success%
                    ) else (
                        call :WriteLine "Already installed" %color_success%
                    )
                )

                set currentOption=
            )
        )
    )

    exit /b


:GetCudaVersion

    REM setlocal enabledelayedexpansion

    :: Run nvcc with the --version option and capture the output
    for /f "tokens=*" %%a in ('nvcc --version 2^>^&1') do (
        set "line=%%a"

        REM echo GetCudaVersion line: !line!

        :: Check if the line contains "release" to extract the CUDA version
        echo !line! | find /i "release" > nul
        if not errorlevel 1 (
            :: Split the line by spaces to get the version part
            for /f "tokens=5 delims=, " %%b in ("!line!") do (
                set "cuda_version=%%b"

                REM echo GetCudaVersion version: !cuda_version!

                for /f "tokens=1,2 delims=." %%a in ("!cuda_version!") do (
                    set "cuda_major_version=%%a"
                    exit /b
                )
            )
        )
    )

    REM pass back values as in params 1 and 2
    REM set "%~1=!cuda_version!"
    REM set "%~2=!cuda_major_version!"

    exit /b


:GetValueFromModuleSettingsFile moduleDirName moduleId property returnValue

    SetLocal EnableDelayedExpansion

    set moduleDirName=%~1
    set moduleId=%~2
    set property=%~3

    REM escape '-'s
    if "!property:-=!" neq "!property!" set property="[""!property!""]"
    if "!moduleId:-=!" neq "!moduleId!" set moduleId="[""!moduleId!""]"

    if /i "!useJq!" == "true" (
        set key=.Modules.!moduleId!.!property!
    ) else (
        set key=$.Modules.!moduleId!.!property!
    )

    REM if /i "%verbosity%" neq "quiet" (
    REM     call :WriteLine "Searching for '!key!' in a suitable modulesettings.json file in !moduleDirName!" "!color_info!"
    REM )

    REM The order in which modulesettings files are added is
    REM WE DO NOT SUPPORT DOCKER IN Windows, plus we are ONLY searching non-development files here
    REM   modulesettings.json
    REM   (not searched) modulesettings.development.json 
    REM   modulesettings.os.json
    REM   (not searched) modulesettings.os.development.json
    REM   modulesettings.os.architecture.json
    REM   (not searched) modulesettings.os.architecture.development.json
    REM   (not supported) modulesettings.docker.json
    REM   (not supported) modulesettings.docker.development.json
    REM   (not needed yet) modulesettings.device.json (device = raspberrypi, orangepi, jetson)
    REM So we need to check each modulesettings file in reverse order until we find a value for 'key'
    
    call :GetValueFromModuleSettings "!moduleDirName!\modulesettings.windows.!architecture!.json", "!key!"
    REM echo Check 1: moduleSettingValue = !moduleSettingValue!
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirName!\modulesettings.windows.json", "!key!"
        REM echo Check 2: moduleSettingValue = !moduleSettingValue!
    )
    if "!moduleSettingValue!" == "" (
        call :GetValueFromModuleSettings "!moduleDirName!\modulesettings.json", "!key!"
        REM echo Check 3: moduleSettingValue = !moduleSettingValue!
    )

    REM if "!moduleSettingValue!" == "" (
    REM     call :WriteLine "Cannot find !key! in modulesettings in !moduleDirName!" "!color_info!"
    REM ) else (
    REM     call :WriteLine "!key! is !moduleSettingValue! in modulesettings in !moduleDirName!" "!color_info!"
    REM )

    REM return value in 4th parameter
    REM EndLocal & set "%~4=!moduleSettingValue!"
    EndLocal & set "moduleSettingsFileValue=%moduleSettingValue%"

    exit /b


REM Gets a value from the modulesettings.json file (any JSON file, really) based
REM purely on the name of the propery. THIS METHOD DOES NOT TAKE INTO ACCOUNT THE
REM DEPTH OF A PROPERTY. If the property is at the root level or 10 levels down,
REM it's all the same. The extraction is done purely by grep/sed, so is very niaive. 
:GetValueFromModuleSettings  jsonFile key returnValue
    SetLocal EnableDelayedExpansion

    set jsonFile=%~1
    set key=%~2

    REM if /i "!verbosity!" neq "quiet" (
    REM     call :WriteLine "Searching for '%key%' in '%jsonFile%'" "!color_info!"
    REM )

    if not exist "!jsonFile!" (
        EndLocal & set moduleSettingValue=
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
    ) else (
        set parse_mode=parsejson
    )

    REM echo jq location is !sdkPath!\Utilities\jq-windows-amd64
    REM echo json location is "%jsonFile%"
    REM echo key is %key%

    if /i "!parse_mode!" == "jq" (

        REM We have a problem with '!'. Because we're using delayed expansion, the ! really get in
        REM the way. The most straightforward way to get around the escaping and working around it
        REM is to replace the ! with something odd, do the extracting of values, then move the !
        REM back where it should be. It's not pretty. Oh no it's not. But it works. Mostly.
        
        REM Remove comments (// and /* */) and replace '!' with '#@@#'

        REM Loading -Raw (ie load all at once, include newlines) and processing /* and // at once
        REM will removes line breaks, and cause issues. Instead we'll do it in two parts.
        REM for /f "usebackq delims=" %%i in (` powershell -Command " (Get-Content '!jsonFile!' -Raw) -replace '//[^^""]*$^|/\*[\s\S]*?\*/','' -replace '^!','#@@#' " `) do (
        
        REM PROBLEM: We need to use temp files due to the size of the data
        REM Load line by line (no -Raw) and remove // comments, process '!'
        REM set filtered=
        REM for /f "usebackq delims=" %%i in (` powershell -Command " (Get-Content '!jsonFile!') -replace '//[^^""]*$^','' -replace '^!','#@@#' " `) do (
        REM     set "filtered=!filtered!%%i"
        REM )

        powershell -Command "(Get-Content '!jsonFile!') -replace '//[^^""]*$^','' -replace '^!','#@@#' | Out-File -FilePath 'temp_settings1.json' -Force -Encoding utf8"

        REM PROBLEM: We need to use temp files due to the size of the data
        REM Load in one fell swoop (use -Raw), including newlines, so we can do the /*...*/ thing
        REM for /f "usebackq delims=" %%i in (` powershell -Command " (Get-Content 'temp_settings1.json' -Raw) -replace '/\*[\s\S]*?\*/','' " `) do (
        REM    set "filtered=%%i"
        REM )
        REM Extract the property
        REM for /f "usebackq delims=" %%j in (` echo !filtered! ^| "!sdkPath!\Utilities\jq-windows-amd64.exe" -r %key% `) do (
        REM    set "jsonValue=!jsonValue!%%j"
        REM )        
        
        powershell -Command " (Get-Content 'temp_settings1.json' -Raw) -replace '/\*[\s\S]*?\*/','' | Out-File -FilePath 'temp_settings2.json' -Force -Encoding utf8"

        REM extract the property
        set jsonValue=
        for /f "usebackq delims=" %%j in (` type temp_settings2.json ^| "!sdkPath!\Utilities\jq-windows-amd64.exe" -r %key% `) do (
            set "jsonValue=!jsonValue!%%j"
        )

        REM clean up
        del temp_settings1.json
        del temp_settings2.json

        REM and thanks for this...
        if /i "!jsonValue!" == "null" (
            set "jsonValue="
        ) else (

            REM We have our property. It may include '#@@#', which everyone knows means '!'. Translate.

            set "correctedValue="
            for /L %%i in (0, 1, 2000) do (
                if defined jsonValue (
                    set "char=!jsonValue:~0,1!"
                    set "phrase=!jsonValue:~0,4!"
                    if "!phrase!" == "#@@#" (
                        set "correctedValue=!correctedValue!^!"
                        set "jsonValue=!jsonValue:~4!"
                    ) else (
                        set "correctedValue=!correctedValue!!char!"
                        set "jsonValue=!jsonValue:~1!"
                    )
                )
            )
            REM echo correctedValue = !correctedValue!
            set "jsonValue=!correctedValue!"
        )

        REM echo jsonFile  = !jsonFile!
        REM echo key       = !key!
        REM echo jsonValue = !jsonValue!

    ) else if /i "!parse_mode!" == "parsejson" (     

        REM Strip comments
        set filtered=
        for /f "usebackq delims=" %%i in (` powershell -Command " (Get-Content '!jsonFile!') -replace '//[^^""]*$^|/\*.*?\*/','' " `) do (
            set "filtered=!filtered!%%i"
        )

        REM extract the property
        set jsonValue=
        for /f "usebackq delims=" %%j in (` echo !filtered! ^| "!sdkPath!\Utilities\ParseJSON\ParseJSON" %key% `) do (
            set "jsonValue=!jsonValue!%%j"
        )

    ) else (

        REM or use inbuilt DOS commands.
        set jsonValue=
        for /f "usebackq tokens=2 delims=:," %%a in (`findstr /I /R /C:"\"!key!\"[^^{]*$" "!jsonFile!"`) do (
            set "jsonValue=%%a"
            set jsonValue=!jsonValue:"=!
            set "jsonValue=!jsonValue: =!"
        )
    )

    REM if "!jsonValue!" == "" (
    REM     call :WriteLine "Cannot find !key! in !jsonFile!" "!color_info!"
    REM ) else (
    REM     call :WriteLine "** !key! is !jsonValue! in !jsonFile!" "!color_info!"
    REM )

    REM return value in 3rd parameter
    REM EndLocal & set %~3=!jsonValue!
    REM Or not...
    EndLocal & set "moduleSettingValue=%jsonValue%"

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
