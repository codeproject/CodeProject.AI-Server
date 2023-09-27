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
    set color_mute=DarkGray
    set color_info=Yellow
    set color_success=Green
    set color_warn=DarkYellow
    set color_error=Red
) else (
    set color_primary=White
    set color_mute=DarkGray
    set color_info=Yellow
    set color_success=Green
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
    for /F "tokens=1,2 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do rem"') do (
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

    if "!background!"=="" (
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
        rem echo  str = [!str!]
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
    REM  - moduleDir is the name of the current module's directory
    REM  - modulePath is the path to the module's directory
    REM  - downloadPath is the path where downloads are always stored (typically src/downloads)
    if /i "%forceOverwrite%" == "true" (
        REM Force Re-download, then force re-copy of downloads to install dir
        if exist "!downloadPath!\!moduleDir!"     rmdir /s %rmdirFlags% "!downloadPath!\!moduleDir!"
        if exist "!modulePath!\!moduleAssetsDir!" rmdir /s %rmdirFlags% "!modulePath!\!moduleAssetsDir!"
    )
    
    REM Download !storageUrl!fileToGet to downloadPath and extract into downloadPath\moduleDir
    REM Params are:     S3 storage bucket |  fileToGet   | downloadToDir  | dirToSaveTo  | message
    call :DownloadAndExtract "!storageUrl!" "!fileToGet!" "!downloadPath!\" "!moduleDir!" "!message!"
    if errorlevel 1 exit /b 1

    REM Copy contents of downloadPath\moduleDir to modulesPath\moduleDir\moduleAssetsDir
    if exist "!downloadPath!\!moduleDir!" (

        robocopy /e "!downloadPath!\!moduleDir! " "!modulePath!\!moduleAssetsDir! " /XF "*.zip" !roboCopyFlags! >NUL

        REM Delete zip file we copied to the assets dir (No longer needed)
        REM del "!modulePath!\!moduleAssetsDir!\!fileToGet!" rem >NUL 2>&1

        REM Delete all but the zip file from the downloads dir
        FOR %%I IN ("!downloadPath!\!moduleDir!\*") DO (
            IF /i "%%~xI" neq ".zip" (
                rem echo deleting %%I
                DEL "%%I" >NUL 2>&1
                rem echo cleaning "%%~nxI"
            )
        )
    ) else exit /b 1

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
        powershell -command "Start-BitsTransfer -Source '!sourceUrl!!fileToGet!' -Destination '!downloadToDir!!dirToSaveTo!\!fileToGet!'"

        if errorlevel 1 (
            call :WriteLine "An error occurred that could not be resolved." "!color_error!"
            exit /b 1
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
    
    REM REM Try tar first. If that doesn't work, fall back to powershell (slow)
    REM set tarExists=true
    REM tar -xf "!fileToGet!" >NUL 2>&1
    REM REM error 9009 means "command not found"
    REM if errorlevel 9009 set tarExists=false
    REM 
    REM REM If we don't have tar, use powershell
    REM if "!tarExists!" == "false" ( 
    REM     call :Write "(no tar - Using PowerShell)..." "!color_info!"
    REM 
    REM     REM Expand-Archive is really, really slow
    REM     REM powershell -command "Expand-Archive -Path '!fileToGet!' -DestinationPath '.' -Force"
    REM     powershell -command "Add-Type -assembly System.IO.Compression.Filesystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('!fileToGet!', '.')" 
    REM 
    REM     if errorlevel 1 (
    REM         popd
    REM         exit /b 1
    REM     )
    REM )
    REM 
    REM REM Remove the downloaded zip
    REM REM del /s /f /q "!fileToGet!" >NUL 2>&1

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
        cd
        call :WriteLine "Extracting !archiveName!" "!color_info!"
    )

    REM Try tar first. If that doesn't work, fall back to powershell (slow)
    set tarSuccessful=true
    tar -xf "!archiveName!" >NUL 2>&1

    REM mkdir pretty_name && tar xf ugly_name.tar -C pretty_name --strip-components 1
    
    REM error 9009 means "command not found"
    if errorlevel 9009 set tarSuccessful=false
    if errorlevel 1 set tarSuccessful=false

    REM If we don't have tar, use powershell
    if "!tarSuccessful!" == "false" ( 
        call :Write "Tar failed - moving to PowerShell..." "!color_info!"

        REM This fails if the tar left debris. We need to force overwrite
        rem powershell -command "Add-Type -assembly System.IO.Compression.Filesystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('!archiveName!', '.')" 

        REM Cannot seem to get the call to the ZipFileExtension method correct
        rem powershell -command "[System.IO.Compression.ZipFile]::ExtractToDirectory('!archiveName!', '.', $true)"

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
    for /f "tokens=1 delims=." %%a in ("!requestedNetVersion!") do ( set requestedNetMajorVersion=%%a )
    set requestedNetMajorVersion=!requestedNetMajorVersion: =!

    call :Write "Checking for .NET !requestedNetMajorVersion!.0 or greater..."
    FOR /F "tokens=* USEBACKQ" %%F IN (`dotnet --version`) DO ( set currentDotNetVersion=%%F )

    if "!currentDotNetVersion!" == "" (
        call :WriteLine "No .NET Found. Installing !requestedNetVersion!."
        winget install Microsoft.DotNet.SDK.%requestedNetMajorVersion%
    ) else (
        call :compareVersions "!currentDotNetVersion!" "!requestedNetMajorVersion!.0"
        if %errorlevel% == -1 (
            if /i "!offlineInstall!" == "true" (
                call :WriteLine "Offline Installation: Unable to download and install .NET." %color_error%
            ) else (
                call :WriteLine "Current version is !currentDotNetVersion!. Installing newer version." %color_warn%
                winget install Microsoft.DotNet.SDK.%requestedNetMajorVersion%
            )
        ) else (
            call :WriteLine "Current version is !currentDotNetVersion! Good to go." %color_success%
        )
    )

    exit /b

:SetupPython
    SetLocal EnableDelayedExpansion

    REM Old method
    REM set pythonVersion=%1      - this is set in script calling this method
    REM set pythonLocation=%~2    - this is set in script calling this method

    REM This new method is called by module installation scripts. IT IS UP TO 
    REM THEM TO SET pythonVersion AND pythonLocation AT THE TOP OF THEIR SCRIPT.
    REM This allows us to set once, use everywhere, with minimal copy and pasting.
    REM While this does mean it's a global variable, ease of use for the user is
    REM what's important here.
    REM
    REM But let's check. It's not that we don't trust users. It's just...

    REM pythonLocation is either "Local" or "Shared". pythonVersion can technically
    REM be anything, but we typically stick to 3.7 - 3.9. 
    if /i "!pythonVersion!" == "" (
        call :WriteLine "pythonVersion needs to be set by the caller. Exiting." "!color_error!"
        goto:eof
    )

    if /i "!pythonLocation!" == "" (
        call :WriteLine "pythonLocation needs to be set by the caller. Setting to 'Local'." "!color_warn!"
        set pythonLocation=Local
    )

    if /i "!allowSharedPythonInstallsForModules!" == "false" (
        REM if modulePath contains '/modules/' and pythonLocation != 'Local'
        if /i "!modulePath:\modules\=!" neq "!modulePath!" (
            if /i "!pythonLocation!" neq "Local" (
                call :WriteLine "Downloaded modules must have local Python install. Changing install location" "!color_warn!"
                set pythonLocation=Local
            )
        )
    )

    REM Version with ".'s removed
    set pythonName=python!pythonVersion:.=!
    set pythonInstallPath=!runtimesPath!\bin\!os!\!pythonName!

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "pythonVersion = !pythonVersion!" !color_info!
        call :WriteLine "pythonLocation = !pythonLocation!" !color_info!
        call :WriteLine "pythonName = !pythonName!" !color_info!
        call :WriteLine "pythonInstallPath = !pythonInstallPath!" !color_info!
    )

    if /i "!pythonLocation!" == "Local" (
        set virtualEnvPath=!modulePath!\bin\!os!\!pythonName!\venv

        if not exist "!modulePath!\bin"      mkdir "!modulePath!\bin"
        if not exist "!modulePath!\bin\!os!" mkdir "!modulePath!\bin\!os!"
    ) else (
        set virtualEnvPath=!pythonInstallPath!\venv
    )

    REM For debugging, or correcting, we can force redownloads. Be careful though.
    if /i "%forceOverwrite%" == "true" (
        REM Force Re-download
        call :WriteLine "Cleaning download directory to force re-download of Python" "!color_info!"
        if exist "!downloadPath!\!platform!\!pythonName!" (
            rmdir /s "%rmdirFlags% "!downloadPath!\!platform!\!pythonName!"
        )

        REM Force overwrite of python installation
        call :WriteLine "Cleaning Python directory to force re-install of Python" "!color_info!"
        call :WriteLine "This will mean any previous PIP installs wwill be lost." "!color_warn!"
        if exist "!pythonInstallPath!" rmdir /s %rmdirFlags% "!pythonInstallPath!"
    )

    if /i "%verbosity%" neq "quiet" call :WriteLine "Installing !pythonName! in !pythonInstallPath!" "!color_info!"

    REM Download whatever packages are missing 
    if exist "!pythonInstallPath!" (
        call :Write "Checking for !pythonName! download..." "!color_mute!"
        call :WriteLine "Present" "!color_success!"
    ) else (
        set baseDir=!downloadPath!\!platform!\
        if not exist "!baseDir!"             mkdir "!baseDir!"
        if not exist "!baseDir!!pythonName!" mkdir "!baseDir!!pythonName!"

        if not exist "!pythonInstallPath!" (
            
            if not exist "!runtimesPath!\bin"                   mkdir "!runtimesPath!\bin"
            if not exist "!runtimesPath!\bin\!os!"              mkdir "!runtimesPath!\bin\!os!"
            if not exist "!runtimesPath!\bin\!os!\!pythonName!" mkdir "!runtimesPath!\bin\!os!\!pythonName!"

            rem Params are:      S3 storage bucket |    fileToGet    | downloadToDir | dirToSaveTo | message
            call :DownloadAndExtract "%storageUrl%" "!pythonName!.zip" "!baseDir!"  "!pythonName!" "Downloading Python !pythonVersion! interpreter..."
            if errorlevel 1 exit /b 1

            if exist "!downloadPath!\!platform!\!pythonName!" (
                robocopy /e "!downloadPath!\!platform!\!pythonName! " "!pythonInstallPath! " /XF "!pythonName!.zip" !roboCopyFlags! >NUL
            ) else exit /b 1
        )
    )

    REM Create the virtual environments. All sorts of things can go wrong here
    REM but if you have issues, make sure you delete the venv directory before
    REM retrying.
    call :Write "Creating Virtual Environment (!pythonLocation!)..."
    if exist "!virtualEnvPath!\\pyvenv.cfg" (
        call :WriteLine "Python !pythonVersion! Already present" %color_success%
    ) else (
        if /i "%verbosity%" neq "quiet" call :WriteLine "Virtual Environment doesn't exist. Creating at !virtualEnvPath!"
        "!pythonInstallPath!\python.exe" -m venv "!virtualEnvPath!"
        call :WriteLine "Done" %color_success%
    )

    REM our DIY version of Python 'Activate' for virtual environments
    call :Write "Enabling our Virtual Environment..."
    set pythonInterpreterPath=!virtualEnvPath!\Scripts\python.exe
    call :WriteLine "Done" %color_success%

    REM Ensure Python Exists
    call :Write "Confirming we have Python !pythonVersion!..."
    rem call :WriteLine "pythonInterpreterPath = !pythonInterpreterPath!"

    "!pythonInterpreterPath!" --version | find "!pythonVersion!" >NUL
    if errorlevel 1 goto errorNoPython
    call :WriteLine "present" %color_success%

    exit /b


:InstallSinglePythonPackage

    SetLocal EnableDelayedExpansion

    set "package_name=%~1"
    set "package_desc=%~2"

    if /i "!offlineInstall!" == "true" (
        call :WriteLine "Offline Installation: Skipping download and installation of Python packages." %color_error%
        exit /b
    )

    REM IT IS UP TO THE CALLER OF THIS FUNCTION TO SET pythonVersion AND pythonLocation
    REM AT THE TOP OF THEIR SCRIPT. pythonLocation is either "Local" or "Shared".
    REM pythonVersion can technically be anything, but we typically stick to 3.7 - 3.9. 

    if /i "!pythonVersion!" == "" (
        call :WriteLine "pythonVersion needs to be set by the caller. Exiting." "!color_error!"
        goto:eof
    )

    if /i "!pythonLocation!" == "" (
        call :WriteLine "pythonLocation needs to be set by the caller. Setting to 'Local'." "!color_warn!"
        set pythonLocation=Local
    )

    if /i "!allowSharedPythonInstallsForModules!" == "false" (
        REM if modulePath contains '/modules/' and pythonLocation != 'Local'
        if /i "!modulePath:\modules\=!" neq "!modulePath!" (
            if /i "!pythonLocation!" neq "Local" (
                call :WriteLine "Downloaded modules must have local Python install. Changing install location" "!color_warn!"
                set pythonLocation=Local
            )
        )
    )

    set pythonName=python!pythonVersion:.=!

    if /i "!pythonLocation!" == "Local" (
        set virtualEnv=!modulePath!\bin\!os!\!pythonName!\venv
    ) else (
        set virtualEnv=!runtimesPath!\bin\!os!\!pythonName!\venv
    )

    REM This will be the python interpreter in the virtual env
    set venvPythonPath=!virtualEnv!\Scripts\python.exe

    set packagesPath=%virtualEnv%\Lib\site-packages

    if /i "%verbosity%" neq "quiet" (
        rem call :WriteLine package_name = !package_name! !color_info!
        call :WriteLine "package_desc = !package_desc!" !color_info!
        call :WriteLine "pythonVersion = !pythonVersion!" !color_info!
        call :WriteLine "pythonLocation = !pythonLocation!" !color_info!
        call :WriteLine "pythonName = !pythonName!" !color_info!
        call :WriteLine "venvPythonPath = !venvPythonPath!" !color_info!
        call :WriteLine "packagesPath = !packagesPath!" !color_info!
    )

    rem if /i "%verbosity%" neq "quiet" call :WriteLine "Installing !package_name!" "!color_info!"
    call :Write "  - Installing !package_desc!..."

    if /i "%verbosity%" == "quiet" (
        "!venvPythonPath!" -m pip install "!package_name!" --target "!packagesPath!" !pipFlags! >NUL 2>&1
    ) else (
        if /i "%verbosity%" == "info" (
            "!venvPythonPath!" -m pip install "!package_name!" --target "!packagesPath!" !pipFlags! >nul 
        ) else (
            "!venvPythonPath!" -m pip install "!package_name!" --target "!packagesPath!" !pipFlags!
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
            "!venvPythonPath!" -m pip show !module_name! >NUL 2>&1
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

    exit /b
    

:InstallPythonPackages

    SetLocal EnableDelayedExpansion

    if /i "!offlineInstall!" == "true" (
        call :WriteLine "Offline Installation: Skipping download and installation of Python packages." %color_error%
        exit /b
    )

    REM Old method
    REM set pythonVersion=%1      - this is set in script calling this method
    REM set pythonLocation=%~2    - this is set in script calling this method

    REM This new method is called by module installation scripts. IT IS UP TO 
    REM THEM TO SET pythonVersion AND pythonLocation AT THE TOP OF THEIR SCRIPT.
    REM This allows us to set once, use everywhere, with minimal copy and pasting.
    REM While this does mean it's a global variable, ease of use for the user is
    REM what's important here.
    REM
    REM But let's check. It's not that we don't trust users. It's just...

    REM pythonLocation is either "Local" or "Shared". pythonVersion can technically
    REM be anything, but we typically stick to 3.7 - 3.9. 
    if /i "!pythonVersion!" == "" (
        call :WriteLine "pythonVersion needs to be set by the caller. Exiting." "!color_error!"
        goto:eof
    )

    if /i "!pythonLocation!" == "" (
        call :WriteLine "pythonLocation needs to be set by the caller. Setting to 'Local'." "!color_warn!"
        set pythonLocation=Local
    )

    if /i "!allowSharedPythonInstallsForModules!" == "false" (
        REM if modulePath contains '/modules/' and pythonLocation != 'Local'
        if /i "!modulePath:\modules\=!" neq "!modulePath!" (
            if /i "!pythonLocation!" neq "Local" (
                call :WriteLine "Downloaded modules must have local Python install. Changing install location" "!color_warn!"
                set pythonLocation=Local
            )
        )
    )

    set pythonName=python!pythonVersion:.=!

    REM Folder where the requirements.txt file lives
    set requirementsDir=%~1
    if "!requirementsDir!" == "" set requirementsDir=!modulePath!

    if /i "!pythonLocation!" == "Local" (
        set virtualEnv=!modulePath!\bin\!os!\!pythonName!\venv
    ) else (
        set virtualEnv=!runtimesPath!\bin\!os!\!pythonName!\venv
    )

    REM This will be the python interpreter in the virtual env
    set pythonPath=!virtualEnv!\Scripts\python

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "pythonVersion  = !pythonVersion!" !color_info!
        call :WriteLine "pythonLocation = !pythonLocation!" !color_info!
        call :WriteLine "pythonName     = !pythonName!" !color_info!
        call :WriteLine "pythonPath     = !pythonPath!" !color_info!
        call :WriteLine "packagesPath   = !packagesPath!" !color_info!
    )
        
    REM Check for requirements.platform.[CUDA].txt first, then fall back to
    REM requirements.txt

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Searching for a suitable requirements.txts file in !requirementsDir!" "!color_info!"
    )

    REM This is getting complicated. The order of priority for the requirements file is:
    REM
    REM  requirements.os.architecture.(cuda|rocm).txt
    REM  requirements.os.(cuda|rocm).txt
    REM  requirements.cuda.txt
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

    REM TODO: Sniff the modulesettings.json file for this module and check the 
    REM       "SupportGPU": value (this requires checking all potential modulesettings
    REM       files based on OS and architecture). If it's false then don't load GPU 
    REM       stuff here.

    if /i "!enableGPU!" == "true" (
        if /i "!hasCUDA!" == "true" (

            REM We probably need to have CUDA specific requirements files
            call :GetCudaVersion
            if "!cuda_version!" neq "" echo CUDA version is !cuda_version!

            if exist "!requirementsDir!\requirements.windows.!architecture!.cuda.txt" (
                set requirementsFilename=requirements.windows.!architecture!.cuda.txt
            ) else if exist "!requirementsDir!\requirements.windows.cuda.txt" (
                set requirementsFilename=requirements.windows.cuda.txt
            ) else if exist "!requirementsDir!\requirements.cuda.txt" (
                set requirementsFilename=requirements.cuda.txt
            )
        ) 

        if /i "!hasROCm!" == "true" (
            if exist "!requirementsDir!\requirements.windows.!architecture!.rocm.txt" (
                set requirementsFilename=requirements.windows.!architecture!.rocm.txt
            ) else if exist "!requirementsDir!\requirements.windows.rocm.txt" (
                set requirementsFilename=requirements.windows.rocm.txt
            ) else if exist "!requirementsDir!\requirements.rocm.txt" (
                set requirementsFilename=requirements.rocm.txt
            )
        ) 

        if "!requirementsFilename!" == "" (
            if exist "!requirementsDir!\requirements.windows.!architecture!.gpu.txt" (
                set requirementsFilename=requirements.windows.!architecture!.gpu.txt
            ) else if exist "!requirementsDir!\requirements.windows.gpu.txt" (
                set requirementsFilename=requirements.windows.gpu.txt
            ) else if exist "!requirementsDir!\requirements.gpu.txt" (
                set requirementsFilename=requirements.gpu.txt
            )
        )
    )

    if "!requirementsFilename!" == "" (
        if exist "!requirementsDir!\requirements.windows.!architecture!.txt" (
            set requirementsFilename=requirements.windows.!architecture!.txt
        ) else if exist "!requirementsDir!\requirements.windows.txt" (
            set requirementsFilename=requirements.windows.txt
        ) else if exist "!requirementsDir!\requirements.txt" (
            set requirementsFilename=requirements.txt
        )
    )
    
    if "!requirementsFilename!" == "" (
        call :WriteLine "No suitable requirements.txt file found." "!color_warn!"
        exit /b 1
    )

    set requirementsPath=!requirementsDir!\!requirementsFilename!
    if not exist "!requirementsPath!" (
        call :WriteLine "The selected requirements file (!requirementsPath!) wasn't found." "!color_warn!"
        exit /b 1
    )

    REM =======================================================================
    REM  3a. Install PIP packages for Python analysis services

    REM For speeding up debugging
    if "!skipPipInstall!" == "true" exit /b

    REM Ensure we have pip (no internet access - ensures we have the current 
    REM python compatible version)
    call :Write "Ensuring Python package manager (pip) is installed..."
    if /i "%verbosity%" == "quiet" (
        "!pythonPath!" -m ensurepip >NUL 2>&1
    ) else (
        "!pythonPath!" -m ensurepip
    )
    call :WriteLine "Done" %color_success%

    call :Write "Ensuring Python package manager (pip) is up to date..."

    REM Upgrade to the latest pip
    if /i "%verbosity%" == "quiet" (
        "!pythonPath!" -m pip install --trusted-host pypi.python.org ^
                       --trusted-host files.pythonhosted.org ^
                       --trusted-host pypi.org --upgrade setuptools !pipFlags! >NUL 2>&1

        "!pythonPath!" -m pip install --trusted-host pypi.python.org ^
                       --trusted-host files.pythonhosted.org ^
                       --trusted-host pypi.org --upgrade pip !pipFlags! >NUL 2>&1
    ) else (
        "!pythonPath!" -m pip install --trusted-host pypi.python.org ^
                       --trusted-host files.pythonhosted.org ^
                       --trusted-host pypi.org --upgrade setuptools !pipFlags!

        "!pythonPath!" -m pip install --trusted-host pypi.python.org ^
                       --trusted-host files.pythonhosted.org ^
                       --trusted-host pypi.org --upgrade pip !pipFlags!
    )
    
    call :WriteLine "Done" %color_success%

    set packagesPath=%virtualEnv%\Lib\site-packages

    call :WriteLine "Choosing Python packages from %requirementsFilename%" !color_info!

    REM call :WriteLine "virtualEnv = %virtualEnv%" !color_warn!
    REM call :WriteLine "packagesPath = !packagesPath!" !color_warn!
    REM call :WriteLine "Installing Python packages from !requirementsPath! to !packagesPath!..." "!color_info!"
 
    if "!oneStepPIP!" == "true" (
        
        call :Write "Installing Packages into Virtual Environment..."
        REM pip install -r !requirementsPath! !pipFlags!
        if /i "%verbosity%" == "quiet" (
            "!pythonPath!" -m pip install -r "!requirementsPath!" --target "!packagesPath!" !pipFlags! >nul
        ) else (
            "!pythonPath!" -m pip install -r "!requirementsPath!" --target "!packagesPath!" !pipFlags! 
        )
        call :WriteLine "Success" %color_success%

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

                    if /i "%verbosity%" == "quiet" (
                        "!pythonPath!" -m pip install "!module!" !currentOption! --target "!packagesPath!" !pipFlags! >NUL 2>&1
                    ) else (
                        if /i "%verbosity%" == "info" (
                            "!pythonPath!" -m pip install "!module!" !currentOption! --target "!packagesPath!" !pipFlags! >nul 
                        ) else (
                            "!pythonPath!" -m pip install "!module!" !currentOption! --target "!packagesPath!" !pipFlags!
                        )
                    )

                    REM If the module's name isn't simply a URL or .whl then actually check it worked
                    if /i "%module:~0,4%" neq "http" (
                        if /i "%module:~-4%" neq ".whl" ( 

                            REM Extract the module_name from the form "module_name[(<=|==|=>|)version[,<=|=>|)version]]  # comment"
                            for /f "tokens=* delims=<==> " %%a in ("!module!") do set "module_name=%%a"

                            "!pythonPath!" -m pip show !module_name! >NUL 2>&1
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
                )

                set currentOption=
            )
        )
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

    rem echo Comparing !v1! to !v2!

    call :divideLetters v1
    call :divideLetters v2
    :loop
    call :parseNode "%v1%" n1 v1
    call :parseNode "%v2%" n2 v2
    if %n1% gtr %n2% exit /b 1
    if %n1% lss %n2% exit /b -1
    if not defined v1 if not defined v2 exit /b 0
    if not defined v1 exit /b -1
    if not defined v2 exit /b 1
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

:GetCudaVersion

    set cuda_version=

    setlocal enabledelayedexpansion
    :: Run nvcc with the --version option and capture the output
    for /f "tokens=*" %%a in ('nvcc --version 2^>^&1') do (
        set "line=%%a"

        :: Check if the line contains "release" to extract the CUDA version
        echo !line! | find /i "release" > nul
        if not errorlevel 1 (
            :: Split the line by spaces to get the version part
            for /f "tokens=5 delims=, " %%b in ("!line!") do (
                EndLocal & set cuda_version=%%b
                exit /b
            )
        )
    )

    exit /b

:GetValueFromModuleSettings jsonFile key returnValue
    rem SetLocal EnableDelayedExpansion

    rem Code thanks to ChatGPT. Radically reworked to make it...work

    set jsonFile=%~1
    set key=%~2

    rem echo jsonFile is %jsonFile%
    rem echo key is %key%

    rem Use the jq command to extract the value of the property from the JSON file.
    rem ...except jq can't handle comments in JSON.
    rem for /f "usebackq tokens=*" %%j in (`jq -r ".%key%" "%jsonFile%"`) do set "jsonValue=%%j"

    rem or use inbuilt DOS commands.
    for /f "usebackq tokens=2 delims=:," %%a in (`findstr /I /R /C:"\"!key!\"[^^{]*$" "!jsonFile!"`) do (
        set "jsonValue=%%a"
        set jsonValue=!jsonValue:"=!
        set jsonValue=!jsonValue: =!
    )

    REM return value in 3rd parameter
    rem echo jsonValue = !jsonValue!
    set "%~3=!jsonValue!"

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

:: color
goto:EOF
exit
