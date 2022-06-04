REM Assuming we're in the /docs/mkdocs directory

call ..\..\src\AnalysisLayer\bin\windows\python39\venv\Scripts\activate.bat
pushd senseAI
mkdocs build
popd