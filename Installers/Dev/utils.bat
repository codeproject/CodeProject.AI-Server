:: CodeProject.AI Server Utilities
::
:: Utilities for use with Windows Development Environment install scripts
::
:: We assume we're in the source code /Installers/Dev directory.
::

@echo off

set pipFlags=-q -q
if /i "%verbosity%"=="info" set pipFlags=-q
if /i "%verbosity%"=="loud" set pipFlags=

set color_primary=White
set color_mute=DarkGray
set color_info=Yellow
set color_success=Green
set color_warn=DarkYellow
set color_error=Red

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

    if "!background!"=="" background=Black

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

    exit /B 0


:: Sets the currentColor global for the given foreground/background colors
:: currentColor must be output to the terminal before outputing text in
:: order to generate a colored output.
::
:: string foreground color name. Optional if no background provided.
::        Defaults to "White"
:: string background color name.  Optional. Defaults to Black.
:setColor

    REM If you want to get a little fancy then you can also try
    REM  - %ESC%[4m - Underline
    REM  - %ESC%[7m - Inverse

    set foreground=%~1
    set background=%~2

    if "!foreground!"=="" set foreground=White
    if /i "!foreground!"=="Default" set foreground=White
    if "!background!"=="" set background=Black
    if /i "!background!"=="Default" set background=Black

    if "!ESC!"=="" call :setESC

    if /i "!foreground!"=="Contrast" (
		call :setContrastForeground !background!
		set foreground=!contrastForeground!
	)

    set currentColor=

    REM Foreground Colours
    if /i "!foreground!"=="Black"       set currentColor=!ESC![30m
    if /i "!foreground!"=="DarkRed"     set currentColor=!ESC![31m
    if /i "!foreground!"=="DarkGreen"   set currentColor=!ESC![32m
    if /i "!foreground!"=="DarkYellow"  set currentColor=!ESC![33m
    if /i "!foreground!"=="DarkBlue"    set currentColor=!ESC![34m
    if /i "!foreground!"=="DarkMagenta" set currentColor=!ESC![35m
    if /i "!foreground!"=="DarkCyan"    set currentColor=!ESC![36m
    if /i "!foreground!"=="Gray"        set currentColor=!ESC![37m
    if /i "!foreground!"=="DarkGray"    set currentColor=!ESC![90m
    if /i "!foreground!"=="Red"         set currentColor=!ESC![91m
    if /i "!foreground!"=="Green"       set currentColor=!ESC![92m
    if /i "!foreground!"=="Yellow"      set currentColor=!ESC![93m
    if /i "!foreground!"=="Blue"        set currentColor=!ESC![94m
    if /i "!foreground!"=="Magenta"     set currentColor=!ESC![95m
    if /i "!foreground!"=="Cyan"        set currentColor=!ESC![96m
    if /i "!foreground!"=="White"       set currentColor=!ESC![97m

    if "!currentColor!"=="" set currentColor=!ESC![97m
	
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

    REM eg packages_for_gpu.zip
    set fileToGet=%1
    set fileToGet=!fileToGet:"=!

    REM eg assets
    set moduleAssetsDir=%2
    set moduleAssetsDir=!moduleAssetsDir:"=!

    REM output message
    set message=%3
    set message=!message:"=!

    REM Clean up directories to force a download and re-copy if necessary
    if /i "%forceOverwrite%" == "true" (
        REM Force Re-download, then force re-copy of downloads to install dir
        if exist "!downloadPath!\!moduleDir!"     rmdir /s %rmdirFlags% "!downloadPath!\!moduleDir!"
        if exist "!modulePath!\!moduleAssetsDir!" rmdir /s %rmdirFlags% "!modulePath!\!moduleAssetsDir!"
    )
    
    REM Download !storageUrl!fileToGet to downloadPath and extract into downloadPath\moduleDir
    REM Params are:     S3 storage bucket |  fileToGet   | downloadToDir  | dirToSaveTo  | message
    call :DownloadAndExtract "!storageUrl!" "!fileToGet!" "!downloadPath!\" "!moduleDir!" "!message!"
    
    REM Copy contents of downloadPath\moduleDir to analysisLayerPath\moduleDir\moduleAssetsDir
    if exist "!downloadPath!\!moduleDir!" (

        robocopy /e "!downloadPath!\!moduleDir! " "!modulePath!\!moduleAssetsDir! " /XF "*.zip" !roboCopyFlags! > NUL

        REM Delete zip file we copied to the assets dir (No longer needed)
        REM del "!modulePath!\!moduleAssetsDir!\!fileToGet!" rem > NUL 2>nul

        REM Delete all but the zip file from the downloads dir
        FOR %%I IN ("!downloadPath!\!moduleDir!\*") DO (
            IF /i "%%~xI" neq ".zip" (
                DEL "%%I" rem > NUL 2>nul
                rem echo cleaning "%%~nxI"
            )
        )
    )

    exit /b


:DownloadAndExtract
    SetLocal EnableDelayedExpansion

    REM "https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
    set storageUrl=%1
    set storageUrl=!storageUrl:"=!

    REM File to download eg packages_for_gpu.zip
    set fileToGet=%2
    set fileToGet=!fileToGet:"=!

    REM Where to store the downloade zip. eg "downloads\" - relative to the 
    REM current directory
    set downloadToDir=%3
    set downloadToDir=!downloadToDir:"=!

    REM Whre to extract the contents eg assets
    set dirToSaveTo=%4
    set dirToSaveTo=!dirToSaveTo:"=!

    REM output message
    set message=%5
    set message=!message:"=!

    if "!message!" == "" set message=Downloading !fileToGet!...

    if /i "%verbosity%" neq "quiet" (
        call :WriteLine "Downloading !fileToGet! to !downloadToDir!\!dirToSave!" "!color_info!"
    )

    call :Write "!message!" "!color_primary!"

    rem call :WriteLine "Checking "!downloadToDir!!dirToSaveTo!\!fileToGet!" "!color_info!"
    if exist "!downloadToDir!!dirToSaveTo!\!fileToGet!" (
        call :Write "already exists..." "!color_info!"
    ) else (

        if not exist "!downloadToDir!"              mkdir "!downloadToDir!"
        if not exist "!downloadToDir!!dirToSaveTo!" mkdir "!downloadToDir!!dirToSaveTo!"

        REM Be careful with the quotes so we can handle paths with spaces
        powershell -command "Start-BitsTransfer -Source '!storageUrl!!fileToGet!' -Destination '!downloadToDir!!dirToSaveTo!\!fileToGet!'"

        if errorlevel 1 (
            call :WriteLine "An error occurred that could not be resolved." "!color_error!"
            exit /b
        )

        if not exist "!downloadToDir!!dirToSaveTo!\!fileToGet!" (
            call :WriteLine "An error occurred that could not be resolved." "!color_error!"
            exit /b
        )
    )

    call :Write "Expanding..." "!color_info!"

    pushd "!downloadToDir!!dirToSaveTo!"

    REM Try tar first. If that doesn't work, fall back to powershell (slow)
    set tarExists=true

    tar -xf "!fileToGet!" > nul 2>nul
    if "%errorlevel%" == "9009" set tarExists=false

    REM If we don't have tar, use powershell
    if "!tarExists!" == "false" ( powershell -command "Expand-Archive -Path '!fileToGet!' -Force" )

    REM Remove the downloaded zip
    REM del /s /f /q "!fileToGet!" > nul 2>nul

    popd

    call :WriteLine "Done." "!color_success!"

    exit /b


:SetupPython
    SetLocal EnableDelayedExpansion

    set pythonVersion=%1

    REM Version with ".'s removed
    set pythonName=python!pythonVersion:.=!

    set installPath=!analysisLayerPath!\bin\!platform!\!pythonName!

    REM For debugging, or correcting, we can force redownloads. Be careful though.
    if /i "%forceOverwrite%" == "true" (
        REM Force Re-download
        call :WriteLine "Cleaning download directory to force re-download of Python" "!color_info!"
        if exist "!downloadPath!\!platform!\!pythonName!" (
            rmdir /s "%rmdirFlags% "!downloadPath!\!platform!\!pythonName!"
        )

        REM Force overwrite
        call :WriteLine "Cleaning Python directory to force re-install of Python" "!color_info!"
        call :WriteLine "This will mean any previous PIP installs wwill be lost." "!color_warn!"
        if exist "!installPath!" rmdir /s %rmdirFlags% "!installPath!"
    )

    REM Download whatever packages are missing 
    rem call :WriteLine "Checking !installPath!" "!color_info!"

    if exist "!installPath!" (
        call :WriteLine "!pythonName! package already downloaded" "!color_mute!"
    ) else (
        set baseDir=!downloadPath!\!platform!\
        if not exist "!baseDir!" mkdir "!baseDir!"
        if not exist "!baseDir!\!pythonName!" mkdir "!baseDir!\!pythonName!"

        if not exist "!installPath!" (

            rem Params are:     S3 storage bucket |    fileToGet   | downloadToDir | dirToSaveTo  | message
            call :DownloadAndExtract "%storageUrl%" "!pythonName!.zip" "!baseDir!"   "!pythonName!" "Downloading Python !pythonVersion! interpreter..."
            if exist "!downloadPath!\!platform!\!pythonName!" (
                robocopy /e "!downloadPath!\!platform!\!pythonName! " "!installPath! " !roboCopyFlags! > NUL
            )
        )
    )

    REM Create the virtual environments. All sorts of things can go wrong here
    REM but if you have issues, make sure you delete the venv directory before
    REM retrying.
    call :Write "Creating Virtual Environment..."
    if exist "!installPath!\venv" (
        call :WriteLine "Python !pythonVersion! Already present" %color_success%
    ) else (
        "!installPath!\python.exe" -m venv "!installPath!\venv"
        call :WriteLine "Done" %color_success%
    )

    REM our DIY version of Python 'Activate' for virtual environments
    call :Write "Enabling our Virtual Environment..."
    pushd "!installPath!"
    set venvPath=%cd%\venv
    set pythonInterpreterPath="!venvPath!\Scripts\python"
    popd

    call :WriteLine "Done" %color_success%

    REM Ensure Python Exists
    call :Write "Confirming we have Python !pythonVersion!..."
    !pythonInterpreterPath! --version | find "!pythonVersion!" > NUL
    if errorlevel 1 goto errorNoPython
    call :WriteLine "present" %color_success%

    exit /b

:InstallPythonPackages

    SetLocal EnableDelayedExpansion

    REM Whether or not to install all python packages in one step
    REM (-r requirements.txt) or step by step. Doing this allows the PIP manager
    REM to handle incompatibilities better.
    set oneStepPIP=false

    set pythonVersion=%1
    set pythonName=python!pythonVersion:.=!

    set requirementsDir=%~2
    set testForPipExistanceName=%~3

    set virtualEnv=!analysisLayerPath!\bin\!platform!\!pythonName!\venv

    REM This will be the python interpreter in the virtual env
    set pythonPath=!virtualEnv!\Scripts\python

    set hasCUDA=false

    if "!supportCUDA!" == "true" (
        call :Write "Checking for CUDA..."
        wmic PATH Win32_VideoController get Name | find "NVIDIA" > NUL
        if errorlevel 0 (
            set hasCUDA=true
            call :WriteLine "Present" %color_success%
        ) else (
            call :WriteLine "Not found" "!color_mute!"
        )
    )

    REM Check for requirements.platform.[CUDA].txt first, then fall back to
    REM requirements.txt

    set requirementsFilename=

    if /i "!enableGPU!" == "true" (
        if /i "!hasCUDA!" == "true" (
            if exist "!requirementsDir!\requirements.windows.cuda.txt" (
                set requirementsFilename=requirements.windows.cuda.txt
            ) else if exist "!requirementsDir!\requirements.cuda.txt" (
                set requirementsFilename=requirements.cuda.txt
            )
        ) 

        if "!requirementsFilename!" == "" (
            if exist "!requirementsDir!\requirements.windows.gpu.txt" (
                set requirementsFilename=requirements.windows.gpu.txt
            ) else if exist "!requirementsDir!\requirements.gpu.txt" (
                set requirementsFilename=requirements.gpu.txt
            )
        )
    )

    if "!requirementsFilename!" == "" (
        if exist "!requirementsDir!\requirements.windows.txt" (
            set requirementsFilename=requirements.windows.txt
        ) else if exist "!requirementsDir!\requirements.txt" (
            set requirementsFilename=requirements.txt
        )
    )
    
    if "!requirementsFilename!" == "" (
        call :WriteLine "No suitable requirements.txt file found." "!color_warn!"
        exit /b
    ) else (
        set requirementsPath="!requirementsDir!\!requirementsFilename!"
    )

    if not exist "!requirementsPath!" (
        call :WriteLine "No suitable requirements.txt file found." "!color_warn!"
        exit /b
    )

    REM =======================================================================
    REM  3a. Install PIP packages for Python analysis services

    REM Ensure we have pip (no internet access - ensures we have the current 
    REM python compatible version.
    call :Write "Ensuring Python package manager (pip) is installed..."
    if /i "%verbosity%" == "quiet" (
        !pythonPath! -m ensurepip !pipFlags!  >nul 2>nul 
    ) else (
        !pythonPath! -m ensurepip !pipFlags!
    )
    call :WriteLine "Done" %color_success%

    call :Write "Ensuring Python package manager (pip) is up to date..."

    REM Upgrade to the latest pip
    if /i "%verbosity%" == "quiet" (
        !pythonPath! -m pip install --trusted-host pypi.python.org ^
                     --trusted-host files.pythonhosted.org ^
                     --trusted-host pypi.org --upgrade pip !pipFlags!  >nul 2>nul 
    ) else (
        !pythonPath! -m pip install --trusted-host pypi.python.org ^
                     --trusted-host files.pythonhosted.org ^
                     --trusted-host pypi.org --upgrade pip !pipFlags!
    )
    
    call :WriteLine "Done" %color_success%

    call :Write "Checking for required packages..."

    REM ASSUMPTION: If venv\Lib\site-packages\<test name> exists then no need 
    REM to check further

    set packagesPath="!virtualEnv!\Lib\site-packages"

    if exist "!packagesPath!\!testForPipExistanceName!" (
        call :WriteLine "present." %color_success%
        exit /b
    )
 
    call :WriteLine "Packages missing. Installing from !requirementsFilename!..." "!color_info!"
    if "!oneStepPIP!" == "true" (
        
        call :Write "Installing Packages into Virtual Environment..."
        REM pip install -r !requirementsPath! !pipFlags!
        if /i "%verbosity%" == "quiet" (
            !pythonPath! -m pip install -r !requirementsPath! --target "!packagesPath!" !pipFlags!   >nul 2>nul 
        ) else (
            !pythonPath! -m pip install -r !requirementsPath! --target "!packagesPath!" !pipFlags! 
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
                    set module=%%a
                    set description=%%b
                )

                if "!description!" == "" set description= Installing !module!

                if "!module!" NEQ "" (
                    call :Write "  -!description!..."

                    if /i "%verbosity%" == "quiet" (
                        !pythonPath! -m pip install !module! !currentOption! --target "!packagesPath!" !pipFlags! >nul 2>nul 
                    ) else (
                        !pythonPath! -m pip install !module! !currentOption! --target "!packagesPath!" !pipFlags!
                    )

                    call :WriteLine "Done" %color_success%
                )

                set currentOption=
            )
        )
    )

    exit /b


:: Jump points

:errorNoPython
call :WriteLine
call :WriteLine
call :WriteLine "-------------------------------------------------------------"
call :WriteLine "Error: Python not installed" "!color_error!"
goto:EOF
exit
