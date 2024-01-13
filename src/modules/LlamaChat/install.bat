:: Installation script :::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           LlamaChat
::
:: This script is only called using ..\..\src\setup.bat
::
:: For help with install scripts, notes on variables and methods available, tips,
:: and explanations, see /src/modules/install_script_help.md

@if "%1" NEQ "install" (
    echo This script is only called from ..\..\src\setup.bat
    @pause
    @goto:eof
)

REM set moduleInstalled=false
REM for /f "usebackq delims=" %%G in (`"!venvPythonCmdPath!" -m pip show llama-cpp-python 2^>NUL`) do (
REM     REM look for the name of the module in this line of output.
REM     if "%%G" neq "" (
REM         set line=%%G
REM         set line=!line:^>=!
REM         set line=!line:^<=!
REM         echo !line! | find /I "Name:" >NUL
REM         if !errorlevel! == 0 (
REM             echo !line! | find /I "llama_cpp" >NUL
REM             if !errorlevel! == 0 set moduleInstalled=true
REM         )
REM     )
REM )
REM 
REM if /i "!moduleInstalled!" == "false" (
REM     REM powershell -command "$env:CMAKE_GENERATOR = ""MinGW Makefiles"""
REM     REM powershell -command "$env:CMAKE_ARGS = ""-DLLAMA_OPENBLAS=on -DCMAKE_C_COMPILER=C:/w64devkit/bin/gcc.exe -DCMAKE_CXX_COMPILER=C:/w64devkit/bin/g++.exe"""
REM     REM call "!sdkScriptsDirPath!\utils.bat" InstallPythonPackagesByName "llama-cpp-python" "Simple Python bindings for the llama.cpp library" 
REM 
REM     REM See https://jllllll.github.io/llama-cpp-python-cuBLAS-wheels
REM     if "!cuda_version!" == "11.7" (
REM         set python_llama_cpp_wheel=https://github.com/abetlen/llama-cpp-python/releases/download/v0.2.11/llama_cpp_python-0.2.11-cp39-cp39-win_amd64.whl
REM     ) else (
REM         set python_llama_cpp_wheel=https://github.com/abetlen/llama-cpp-python/releases/download/v0.2.11/llama_cpp_python-0.2.11-cp39-cp39-win_amd64.whl
REM     )
REM     call "!sdkScriptsDirPath!\utils.bat" InstallPythonPackagesByName !python_llama_cpp_wheel! "Simple Python bindings for the llama.cpp library"
REM ) else (
REM     call "!sdkScriptsDirPath!\utils.bat" WriteLine "python-llama-cpp already installed." "!color_success!"
REM )

set fileToGet=codellama-7b.Q4_K_M.gguf

if not exist "!moduleDirPath!/models/!fileToGet!" (
    set sourceUrl=https://huggingface.co/TheBloke/CodeLlama-7B-GGUF/resolve/main/
    set destination=!downloadDirPath!\!moduleDirName!\!fileToGet!

    if not exist !downloadDirPath!\!moduleDirName! mkdir !downloadDirPath!\!moduleDirName!
    if not exist "!moduleDirPath!/models" mkdir "!moduleDirPath!/models"

    powershell -command "Start-BitsTransfer -Source '!sourceUrl!!fileToGet!' -Destination '!destination!'"
    if errorlevel 1 (
        powershell -Command "Get-BitsTransfer | Remove-BitsTransfer"
        powershell -command "Start-BitsTransfer -Source '!sourceUrl!!fileToGet!' -Destination '!destination!'"
    )
    if errorlevel 1 (
        powershell -Command "Invoke-WebRequest '!sourceUrl!!fileToGet!' -OutFile '!destination!'"
        if errorlevel 1 (
            call "!sdkScriptsDirPath!\utils.bat" WriteLine "Download failed. Sorry." "!color_error!"
            set moduleInstallErrors=Unable to download !fileToGet!
        )
    )

    if exist "!destination!" move "!destination!" "!moduleDirPath!/models/"
) else (
    call "!sdkScriptsDirPath!\utils.bat" WriteLine "!fileToGet! already downloaded." "!color_success!"
)

REM set moduleInstallErrors=
