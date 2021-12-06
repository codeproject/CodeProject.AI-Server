:: CodeProject SenseAI Server download builder script. 
::
:: This script will build a directory full of everything needed to run CodeProject.SenseAI. It's a
:: large download. There's no getting around that, unfortunately. The only alternative is to create
:: a download that includes an install script that then pulls down the required pieces. Total 
:: download will still be the same, though.

:: We assume we're in the source code /install directory.

@echo off
cls
setlocal enabledelayedexpansion

set installationDir=c:\CodeProject.SenseAI.Package

:: Do all the main hardwork
call windows_install %installationDir% false

echo Building demo
cd ..\demos\dotNET\CodeProject.SenseAI.Playground
dotnet build --configuration Release --nologo --verbosity q

echo Copying demo
robocopy /e bin/Release/net5.0-windows %installationDir%\demos\.NET /NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul

cd ..\..
mkdir %installationDir%\demos\TestData
robocopy /e TestData %installationDir%\demos\TestData /NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul

mkdir %installationDir%\demos\Javascript
robocopy /e Javascript %installationDir%\demos\Javascript /NFL /NDL /NJH /NJS /nc /ns  >nul 2>nul

:: dotnet build --configuration Release --nologo --verbosity !dotnetFlags! --self-contained true  - .NET 6

echo Done
