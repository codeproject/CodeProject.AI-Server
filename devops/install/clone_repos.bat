:: CodeProject.AI Server: Clone all repos
::
:: We assume we're in the /devops/install directory

@echo off
cls
setlocal enabledelayedexpansion

set dotNetExternalModules=CodeProject.AI-PortraitFilter CodeProject.AI-SentimentAnalysis
set pythonExternalModules=CodeProject.AI-ALPR CodeProject.AI-ALPR-RKNN CodeProject.AI-BackgroundRemover ^
                          CodeProject.AI-Cartooniser CodeProject.AI-FaceProcessing CodeProject.AI-LlamaChat ^
                          CodeProject.AI-ObjectDetectionCoral CodeProject.AI-ObjectDetectionYOLOv5-3.1 ^
                          CodeProject.AI-ObjectDetectionYOLOv8 CodeProject.AI-ObjectDetectionYoloRKNN ^
                          CodeProject.AI-TrainingObjectDetectionYOLOv5 CodeProject.AI-OCR ^
                          CodeProject.AI-SceneClassifier CodeProject.AI-SoundClassifierTF ^
                          CodeProject.AI-SuperResolution CodeProject.AI-TextSummary CodeProject.AI-Text2Image


pushd ..\..
set rootDir=%cd%
cd src\SDK\Scripts
set sdkDir=%cd%
popd

set externalModulesDir=!rootDir!\..\CodeProject.AI-Modules

if not exist !externalModulesDir! (
    pushd !rootDir!\..
    mkdir CodeProject.AI-Modules
    popd
)

if exist !externalModulesDir! (
    pushd !externalModulesDir!

    for %%x in (!pythonExternalModules!) do (
        if not exist %%x git clone https://github.com/codeproject/%%x
    )
    for %%x in (!dotNetExternalModules!) do (
        if not exist %%x git clone https://github.com/codeproject/%%x
    )

    popd
)