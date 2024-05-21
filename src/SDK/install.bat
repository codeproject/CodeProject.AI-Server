:: Setup script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                    CodeProject.AI SDK Setup
::
:: This script is called from the SDK directory using: 
::
::    ..\setup.bat
::
:: The setup.bat file will find this install.bat file and execute it.
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\setup.bat
    @pause
    @goto:eof
)


REM .NET -----------------------------------------------------------------------

REM Setup .NET for the server, for the .NET Utilities, and any .NET modules. Not
REM all machines will necessarily have the version of .NET we need, so for Dev
REM we ensure .NET is up to scratch, and for production, .NET is installed as 
REM part of the Windows installer. The SetupDotNet function will check for .NET 
REM and do nothing if it finds a suitable version already installed.
if /i "!executionEnvironment!" == "Development" (
    call "%sdkScriptsDirPath%\utils.bat" SetupDotNet 7.0.405 SDK
)


REM CUDA -----------------------------------------------------------------------

REM Ensure cuDNN is installed. Disabled for now pending full testing
REM if /i "%hasCUDA%"=="true" if /i "%cuda_version%" == "" (
REM    call ../install_CUDnn.bat
REM )


REM Utilities ------------------------------------------------------------------

if /i "!useJq!" == "false" (
    dotnet build "!sdkPath!\Utilities\ParseJSON" /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary -c Release >NUL
    if exist "!sdkPath!\Utilities\ParseJSON\bin\Release\net7.0\" (
        pushd "!sdkPath!\Utilities\ParseJSON\bin\Release\net7.0\"
        move * ..\..\..\ >NUL
        popd
    )
)

REM TODO: Check .NET installed correctly
REM set moduleInstallErrors=...
