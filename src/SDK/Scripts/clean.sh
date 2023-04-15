# CodeProject.AI Server and Analysis modules: Cleans debris, properly, for clean build
#
# Usage:
#   bash clean.sh  [build | install | installall | downloads | all]
#
# We assume we're in the /src directory

# import the utilities. This sets os, platform and architecture
source "$(dirname "$0")/utils.sh"

function cleanSubDirs() {
	
    local basePath=$1
    local dirPattern=$2
    local excludeDirPattern=$3

    pushd "${basePath}" >/dev/null 2>/dev/null
    if [ $? -ne 0 ]; then
        writeLine "Can't navigate to ${basePath} (but that's probably OK)" $color_warn
        # popd  >/dev/null
        return
    fi

    if [ "$doDebug" == "true" ]; then
        if [ "${excludeDirPattern}" == "" ]; then
            writeLine "Removing folders in $(pwd) that match ${dirPattern}" $color_info
        else
            writeLine "Removing folders in $(pwd) that match ${dirPattern} except ${excludeDirPattern}" $color_info
        fi
    fi

    popd >/dev/null

    # Loop through all subdirs recursively
    # echo "** In ${basePath} searching for ${dirPattern} and ignoring ${excludeDirPattern}*" 

    IFS=$'\n'; set -f

    for dirName in $(find "$basePath" -type d -name "${dirPattern}" ); do    

        dirMatched="true"
        if [ "${excludeDirPattern}" != "" ]; then
            if [[ $dirName =~ ${excludeDirPattern} ]]; then
                dirMatched="false"
            fi
        fi

        if [ "$dirMatched" == "true" ]; then

            if [ "$doDebug" == "true" ]; then
                writeLine "Marked for removal: ${dirName}" $color_error
            else
                rm -r -f -d "$dirName";
            
                if [ -d "$dirName" ]; then    
                    writeLine "Unable to remove ${dirName}"  $color_error
                else
                    writeLine "Removed ${dirName}" $color_success
                fi
            fi
        else
            if [ "$doDebug" == "true" ]; then
                writeLine "Not deleting ${dirName}" $color_success
            fi
        fi

    done

    unset IFS; set +f
}

function cleanFiles() {
	
    local basePath=$1
    local excludeFilePattern=$2

    pushd "${basePath}" >/dev/null 2>/dev/null

    if [ $? -ne 0 ]; then
        writeLine "Can't navigate to ${basePath} (but that's probably OK)" $color_warn
        # popd  >/dev/null
        return
    fi

    if [ "$doDebug" == "true" ]; then
        if [ "${excludeFilePattern}" == "" ]; then
            writeLine "Removing files in $(pwd)" $color_warn
        else
            writeLine "Removing files in $(pwd) except ${excludeFilePattern}" $color_warn
        fi
    fi

    IFS=$'\n'; set -f

    for fileName in $(find . -type f ); do    

        fileMatched="true"
        if [ "${excludeFilePattern}" != "" ]; then
            if [[ $fileName == ${excludeFilePattern} ]]; then
                fileMatched="false"
            fi
        fi

        if [ "$fileMatched" == "true" ]; then

            if [ "$doDebug" == "true" ]; then
                writeLine "Marked for removal: ${fileName}" $color_error
            else
                rm -f "$fileName";
            
                if [ -f "$fileName" ]; then    
                    writeLine "Removed ${fileName}" $color_success
                else
                    writeLine "Unable to remove ${fileName}" $color_error
                fi
            fi
        else
            if [ "$doDebug" == "true" ]; then
                writeLine "Not deleting ${fileName}" $color_success
            fi
        fi

    done

    unset IFS; set +f

    popd >/dev/null
}

clear
useColor="true"
doDebug="false"

installDir="$(pwd)"
pushd ../../.. >/dev/null
rootDir="$(pwd)"
popd >/dev/null


if [ "$1" == "" ]; then
    writeLine 'Solution Cleaner' 'White'
    writeLine
    writeLine  'clean.sh [build : install : installall : all]'
    writeLine  
    writeLine  '  build      - cleans build output (bin / obj)'
    writeLine  '  assets     - removes assets to force re-copy of downloads'
    writeLine  '  install    - removes current OS installation stuff (PIPs, downloads etc)'
    writeLine  '  installall - removes installation stuff for all platforms'
    writeLine  '  downloads  - removes downloads to force re-download'
    writeLine  '  all        - removes build and installation stuff for all platforms'
    
    if [ "$os" == "macos" ]; then
        writeLine
        writeLine  '  tools      - removes xcode GCC tools'
        writeLine  '  runtimes   - removes installed runtimes (Python only)'
    fi
    
    writeLine
    exit
fi

cleanAssets='false'
cleanDownloadCache='false'
cleanBuild='false'
cleanInstallCurrentOS='false'
cleanInstallAll='false'
cleanAll='false'
cleanTools='false'
cleanRuntimes='false'

if [ "$1" == "assets" ]; then         cleanAssets='true'; fi
if [ "$1" == "download-cache" ]; then cleanDownloadCache='true'; fi
if [ "$1" == "build" ]; then          cleanBuild='true'; fi
if [ "$1" == "install" ]; then        cleanInstallCurrentOS='true'; fi
if [ "$1" == "installall" ]; then     cleanInstallAll='true'; fi
if [ "$1" == "all" ]; then            cleanAll='true'; fi

# not covered by "all"
if [ "$1" == "tools" ]; then      cleanTools='true'; fi
if [ "$1" == "runtimes" ]; then   cleanRuntimes='true'; fi

if [ "$cleanAll" == 'true' ]; then
    cleanInstallAll='true'
    cleanBuild='true'
    cleanAssets='true'
    cleanDownloadCache='true'
fi

if [ "$cleanInstallCurrentOS" == 'true' ] || [ "$cleanInstallAll" == 'true' ]; then
    cleanAssets='true'
fi

if [ "$cleanBuild" == "true" ]; then
    
    writeLine 
    writeLine "Cleaning Build                                                      " "White" "Blue"
    writeLine 

    cleanSubDirs "${rootDir}/src"                "bin" "runtimes/bin"
    cleanSubDirs "${rootDir}/src"                "obj" "ObjectDetection"
    cleanSubDirs "${rootDir}/Installers/windows" "bin"
    cleanSubDirs "${rootDir}/Installers/windows" "obj"
    cleanSubDirs "${rootDir}/demos"              "bin"
    cleanSubDirs "${rootDir}/demos"              "obj" "Objects"
    cleanSubDirs "${rootDir}/tests"              "bin"
    cleanSubDirs "${rootDir}/tests"              "obj"
fi

if [ "$cleanInstallCurrentOS" == "true" ]; then

    writeLine 
    writeLine "Cleaning ${platform} Install                                            " "White" "Blue"
    writeLine 

    cleanSubDirs "${rootDir}/src/runtimes/bin" "${platform}"

    cleanSubDirs "${rootDir}/src/modules/FaceProcessing"  "datastore"
fi

if [ "$cleanInstallAll" == "true" ]; then

    writeLine 
    writeLine "Cleaning install for all platforms                                  " "White" "Blue"
    writeLine 

    cleanSubDirs "${rootDir}/src/runtimes" "bin"
fi

if [ "$cleanAssets" == "true" ]; then

    writeLine 
    writeLine "Cleaning assets                                                     " "White" "Blue"
    writeLine 

    cleanSubDirs "${rootDir}/src/modules/ALPR"                "paddleocr"
    cleanSubDirs "${rootDir}/src/modules/BackgroundRemover"   "models"
    cleanSubDirs "${rootDir}/src/modules/Cartooniser"         "weights"
    cleanSubDirs "${rootDir}/src/modules/FaceProcessing"      "assets"
    cleanSubDirs "${rootDir}/src/modules/ObjectDetectionNet"  "assets"
    cleanSubDirs "${rootDir}/src/modules/ObjectDetectionNet"  "custom-models"
    cleanSubDirs "${rootDir}/src/modules/ObjectDetectionNet"  "LocalNugets"
    cleanSubDirs "${rootDir}/src/modules/ObjectDetectionYolo" "assets"
    cleanSubDirs "${rootDir}/src/modules/ObjectDetectionYolo" "custom-models"
    cleanSubDirs "${rootDir}/src/modules/OCR"                 "paddleocr"
    cleanSubDirs "${rootDir}/src/modules/YOLOv5-3.1"          "assets"
    cleanSubDirs "${rootDir}/src/modules/YOLOv5-3.1"          "custom-models"
fi

if [ "$cleanDownloadCache" == "true" ]; then

    writeLine 
    writeLine "Cleaning download cache                                             " "White" "Blue"
    writeLine 

    cleanSubDirs "${rootDir}/src/downloads"  "ALPR"
    cleanSubDirs "${rootDir}/src/downloads"  "BackgroundRemover"
    cleanSubDirs "${rootDir}/src/downloads"  "Cartooniser"
    cleanSubDirs "${rootDir}/src/downloads"  "FaceProcessing"
    cleanSubDirs "${rootDir}/src/downloads"  "ObjectDetectionNet"
    cleanSubDirs "${rootDir}/src/downloads"  "ObjectDetectionTFLite"
    cleanSubDirs "${rootDir}/src/downloads"  "ObjectDetectionYolo"
    cleanSubDirs "${rootDir}/src/downloads"  "OCR"
    cleanSubDirs "${rootDir}/src/downloads"  "SceneClassifier"
    cleanSubDirs "${rootDir}/src/downloads"  "YOLOv5-3.1"
fi

if [ "$os" == "macos" ]; then

    if [ "$cleanTools" == "true" ]; then
        writeLine 
        writeLine "Removing xcode command line tools                                " "White" "Blue"
        writeLine 

        cleanSubDirs "/Library/Developer" "CommandLineTools"
    fi
    
    if [ "$cleanRuntimes" == "true" ]; then
        writeLine 
        writeLine "Removing Python 3.8 and 3.9                                      " "White" "Blue"
        writeLine 

        # Python < 3.9 needs Rosetta to run on M1 chips, so uninstall accordingly
        if [ "$platform" == "macos-arm64" ]; then
            arch -x86_64 /usr/local/bin/brew uninstall python@3.8
        else
            brew uninstall python@3.8
        fi    
        brew uninstall python@3.9

        if [ "$platform" == "macos" ]; then
            brew uninstall onnxruntime
        fi
    fi
fi