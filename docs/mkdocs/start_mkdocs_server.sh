# Assuming we're in the /docs/mkdocs directory

if [[ $OSTYPE == 'darwin'* ]]; then
    bash ../../src/AnalysisLayer/bin/macos/python39/venv/bin/activate
else
    bash ../../src/AnalysisLayer/bin/linux/python39/venv/bin/activate
fi

cd senseAI

if [[ $OSTYPE == 'darwin'* ]]; then
    open http://127.0.0.1:8000/ &
    ../../../src/AnalysisLayer/bin/macos/python39/venv/bin/python3 -m mkdocs serve
else
    xdg-open http://127.0.0.1:8000/ &
    ../../../src/AnalysisLayer/bin/linux/python39/venv/bin/python3 -m mkdocs serve
fi
