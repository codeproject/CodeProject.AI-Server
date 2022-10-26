# Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
#
#                            CodeProject.AI Demos


# Install python and the required dependencies. If we find imutils then asssume it's all there
setupPython 3.8
installPythonPackages 3.8 "${modulePath}/Python" "cv2"
