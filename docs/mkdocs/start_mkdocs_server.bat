REM Assuming we're in the /docs/mkdocs directory

call ..\..\src\AnalysisLayer\bin\windows\python39\venv\Scripts\activate.bat
pushd CodeProject.AI
start /b "" http://127.0.0.1:8000/
mkdocs serve
popd
