:: CodeProject.AI Server and Analysis modules startup script for Windows
::
:: Usage:
::   start.bat
::
:: We assume we're in the /src directory

@echo off
cls

set separate_analysis=false

:: Move into the working directory
if not "%1" == "" cd "%1"

echo Starting API Server
set ASPNETCORE_ENVIRONMENT=Development

:: Start server so it can load up the environment variables
if "%separate_analysis%" == "true" (

	pushd .\API\Server\Frontend\bin\Debug\net6.0\
	START "CodeProject.AI Server" CodeProject.AI.Server.exe --LaunchAnalysisServices=false
	popd

	REM Start analysis services

	pushd .\AnalysisLayer
	REM NOTE: Environment variables aren't being passed into this script
	START "CodeProject.AI Analysis" /i /B "start-analysis.bat" --embedded
	popd

) else (
	cd .\API\Server\Frontend\bin\Debug\net6.0\
	CodeProject.AI.Server.exe
)

:: Wait forever. We need these processes to stay alive
pause 
