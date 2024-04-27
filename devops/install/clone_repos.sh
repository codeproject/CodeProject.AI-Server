# CodeProject.AI Server: Clone all repos
#
# We assume we're in the /devops/install directory

dotNetExternalModules=( "CodeProject.AI-PortraitFilter" "CodeProject.AI-SentimentAnalysis")
pythonExternalModules=( "CodeProject.AI-ALPR" "CodeProject.AI-ALPR-RKNN" "CodeProject.AI-BackgroundRemover" \
                        "CodeProject.AI-Cartoonizer" "CodeProject.AI-FaceProcessing" \
                        "CodeProject.AI-LlamaChat" "CodeProject.AI-ObjectDetectionCoral" \
                        "CodeProject.AI-ObjectDetectionYOLOv5-3.1" "CodeProject.AI-ObjectDetectionYOLOv8"  \
                        "CodeProject.AI-ObjectDetectionYoloRKNN" "CodeProject.AI-TrainingObjectDetectionYOLOv5" \
                        "CodeProject.AI-OCR" "CodeProject.AI-SceneClassifier" "CodeProject.AI-SoundClassifierTF" \
                        "CodeProject.AI-SuperResolution" "CodeProject.AI-TextSummary" "CodeProject.AI-Text2Image")


# We're assuming this script lives in /devops/build
pushd ../.. >/dev/null
rootDirPath="$(pwd)"
popd >/dev/null

externalModulesDir="${rootDirPath}/../CodeProject.AI-Modules"
if [ ! -d "${externalModulesDir}" ]; then
    pushd "${rootDirPath}/.." > /dev/null
    mkdir CodeProject.AI-Modules
    popd > /dev/null
fi

if [ -d  "${externalModulesDir}" ]; then
    pushd "${externalModulesDir}" >/dev/null

    for repoName in "${pythonExternalModules[@]}"
    do
        if [ ! -d "${repoName}" ]; then git clone "https://github.com/codeproject/${repoName}"; fi
    done

    for repoName in "${dotNetExternalModules[@]}"
    do
        if [ ! -d "${repoName}" ]; then git clone "https://github.com/codeproject/${repoName}"; fi
    done

    popd >/dev/null
fi