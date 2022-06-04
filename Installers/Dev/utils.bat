:: CodeProject SenseAI Server Utilities
::
:: Utilities for use with Windows Development Environment install scripts
::
:: We assume we're in the source code /Installers/Dev directory.
::

@echo off

:: %1 is the name of the method to call. Shift will shuffle the arguments that were passed one spot
:: to the left, meaning the called subroutine will get the arguments it expects in order
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

:: Outputs a line, including linefeed, to the terminal using the given foreground / background
:: colors 
::
:: string The text to output. Optional if no foreground provided. Default is just a line feed.
:: string Foreground color name. Optional if no background provided. Defaults to "White"
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

    if /i "%useColor%"=="true" (
        call :setColor %2 %3
        echo !currentColor!!str!!resetColor!
    ) else (
        echo !str!
    )
    exit /b 0

:: Outputs a line without a linefeed to the terminal using the given foreground / background colors 
::
:: string The text to output. Optional if no foreground provided. Default is just a line feed.
:: string Foreground color name. Optional if no background provided. Defaults to "White"
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


:Download
    SetLocal EnableDelayedExpansion

    REM "https://codeproject-ai.s3.ca-central-1.amazonaws.com/sense/installer/"
    set storageUrl=%1
    set storageUrl=!storageUrl:"=!

    REM "downloads/" - relative to the current directory
    set downloadToDir=%2
    set downloadToDir=!downloadToDir:"=!

    REM eg packages_for_gpu.zip
    set fileToGet=%3
    set fileToGet=!fileToGet:"=!

    REM  eg packages
    set dirToSave=%4
    set dirToSave=!dirToSave:"=!

    REm output message
    set message=%5
    set message=!message:"=!

    if "!message!" == "" set message=Downloading !fileToGet!...
    call :Write "!message!"

    if exist "!downloadToDir!!dirToSave!.zip" (
        call :Write "already exists..." "Yellow"
    ) else (

        REM Doesn't provide progress as % 
        REM powershell Invoke-WebRequest -Uri !storageUrl: =!!fileToGet! ^
        REM                              -OutFile !downloadPath!!dirToSave!.zip

        REM Be careful with the quotes so we can handle paths with spaces
        REM call :Write "Start-BitsTransfer -Source '!storageUrl!!fileToGet!' -Destination '!downloadToDir!!dirToSave!.zip' ..." "White"
        powershell -command "Start-BitsTransfer -Source '!storageUrl!!fileToGet!' -Destination '!downloadToDir!!dirToSave!.zip'"

        if errorlevel 1 (
            call :WriteLine "An error occurred that could not be resolved." "Red"
            exit /b
        )

        if not exist "!downloadToDir!!dirToSave!.zip" (
            call :WriteLine "An error occurred that could not be resolved." "Red"
            exit /b
        )
    )

    call :Write "Expanding..." "Yellow"

    REM Try tar first. If that doesn't work, fall back to pwershell (slow)
    set tarExists=true
    pushd "!downloadToDir!"
    if not exist "!dirToSave!" mkdir "!dirToSave!"
    copy "!dirToSave!.zip" "!dirToSave!" > nul 2>nul
    pushd "!dirToSave!"
    tar -xf "!dirToSave!.zip" > nul 2>nul
    if "%errorlevel%" == "9009" set tarExists=false
    del /s /f /q "!dirToSave!.zip" > nul
    popd
    popd

    if "!tarExists!" == "false" (
        powershell -command "Expand-Archive -Path '!downloadToDir!!dirToSave!.zip' -DestinationPath '!downloadToDir!' -Force"
    )

    REM del /s /f /q "!downloadToDir!!dirToSave!.zip" > nul

    call :WriteLine "Done." "Green"

    exit /b


:SetupPython
    SetLocal EnableDelayedExpansion

    set pythonVersion=%1

    REM Version with ".'s removed
    set pythonName=python!pythonVersion:.=!

    set installPath=!analysisLayerPath!\bin\!platform!\!pythonName!

    if /i "%forceOverwrite%" == "true" (
        REM Force Re-download
        if exist "!downloadPath!\!platform!\!pythonName!" (
            rmdir /s "%rmdirFlags% "!downloadPath!\!platform!\!pythonName!"
        )

        REM Force overwrite
        if exist "!installPath!" rmdir /s %rmdirFlags% "!installPath!"
    )

    REM Download whatever packages are missing 
    if exist "!installPath!" (
        call :WriteLine "!pythonName! package already downloaded" "DarkGray"
    ) else (
        set baseDir=!downloadPath!\!platform!\
        if not exist "!baseDir!" mkdir "!baseDir!"
        if not exist "!installPath!" (
            call :Download "%storageUrl%" "!baseDir!" "!pythonName!.zip" "!pythonName!" "Downloading Python !pythonVersion! interpreter..."
            if exist "!downloadPath!\!platform!\!pythonName!" (
                robocopy /e "!downloadPath!\!platform!\!pythonName! " "!installPath! " !roboCopyFlags! > NUL
            )
        )
    )

    call :Write "Creating Virtual Environment..."
    if exist "!installPath!\venv" (
        call :WriteLine "Python !pythonVersion! Already present" "Green"
    ) else (
        "!installPath!\python.exe" -m venv "!installPath!\venv"
        call :WriteLine "Done" "Green"
    )

    call :Write "Enabling our Virtual Environment..."
    pushd "!installPath!"

    set venvPath=%cd%\venv
    set pythonInterpreterPath="!venvPath!\Scripts\python"

    popd

    call :WriteLine "Done" "Green"

    rem Ensure Python Exists
    call :Write "Confirming we have Python !pythonVersion!..."
    !pythonInterpreterPath! --version | find "!pythonVersion!" > NUL
    if errorlevel 1 goto errorNoPython
    call :WriteLine "present" "Green"

    exit /b

:InstallPythonPackages

    SetLocal EnableDelayedExpansion

    REM Whether or not to install all python packages in one step (-r requirements.txt) or step by
    REM step. Doing this allows the PIP manager to handle incompatibilities better.
    set oneStepPIP=true

    set pythonVersion=%1
    set pythonName=python!pythonVersion:.=!

    set requirementsPath=%~2
    set testForPipExistanceName=%~3

    set virtualEnv=!analysisLayerPath!\bin\!platform!\!pythonName!\venv

    rem This will be the python interpreter in the virtual env
    set pythonPath=!virtualEnv!\Scripts\python

    rem ============================================================================
    rem 3a. Install PIP packages for Python analysis services

    call :Write "Installing Python package manager..."
    !pythonPath! -m pip install --trusted-host pypi.python.org ^
                 --trusted-host files.pythonhosted.org ^
                 --trusted-host pypi.org --upgrade pip !pipFlags!
    call :WriteLine "Done" "Green"

    call :Write "Checking for required packages..."

    rem ASSUMPTION: If venv\Lib\site-packages\<test name> exists then no need to check further
    if not exist "!virtualEnv!\Lib\site-packages\!testForPipExistanceName!" (

        call :WriteLine "Packages missing. Installing..." "Yellow"

        if "!oneStepPIP!" == "true" (
            
            call :Write "Installing Packages into Virtual Environment..."
            REM pip install -r !requirementsPath! !pipFlags!
            !pythonPath! -m pip install -r !requirementsPath! !pipFlags!
            call :WriteLine "Success" "Green"

        ) else (

            REM We'll do this the long way so we can see some progress

            set currentOption=
            for /f "tokens=*" %%x in (' more ^< "!requirementsPath!" ') do (
                set line=%%x

                if "!line!" == "" (
                    set currentOption=
                ) else if "!line:~0,2!" == "##" (
                    set currentOption=
                ) else if "!line:~0,8!" == "# Python" (  REM Note: It's actually #! Python in the file.
                    set currentOption=
                ) else if "!line:~0,12!" == "--find-links" (
                    set currentOption=!line!
                ) else (
           
                    REM  breakup line into module name and description
                    set module=!line!
                    for /F "tokens=1,2 delims=#" %%a in ("!line!") do (
                        set module=%%a
                        set description=%%b
                    )

                    if "!description!" == "" set description=Installing !module!

                    if "!module!" NEQ "" (
                        call :Write "  -!description!..."

                        if /i "%verbosity%" == "quiet" (
                            !pythonPath! -m pip install !module! !currentOption! !pipFlags! >nul 2>nul 
                        ) else (
                            !pythonPath! -m pip install !module! !currentOption! !pipFlags!
                        )

                        call :WriteLine "Done" "Green"
                    )

                    set currentOption=
                )
            )

        )

    ) else (
        call :WriteLine "present." "Green"
    )

    exit /b


:: Jump points

:errorNoPython
call :WriteLine
call :WriteLine
call :WriteLine "-------------------------------------------------------"
call :WriteLine "Error: Python not installed" "Red"
goto:EOF
exit
