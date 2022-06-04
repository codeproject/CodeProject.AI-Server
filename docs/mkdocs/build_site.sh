if [[ $OSTYPE == 'darwin'* ]]; then
    bash ../../src/AnalysisLayer/bin/macos/python39/venv/bin/activate
else
    bash ../../src/AnalysisLayer/bin/linux/python39/venv/bin/activate
fi

cd senseAI

if [[ $OSTYPE == 'darwin'* ]]; then
    ../../../src/AnalysisLayer/bin/macos/python39/venv/bin/python3 -m mkdocs build
else
    ../../../src/AnalysisLayer/bin/linux/python39/venv/bin/python3 -m mkdocs build
fi
