:: Installation script ::::::::::::::::::::::::::::::::::::::::::::::::::::::::
::
::                           CodeProject.AI Demos


call "%installBasePath%\utils.bat" SetupPython 3.9
call "%installBasePath%\utils.bat" InstallPythonPackages 3.9 "%modulePath%\Python" "cv2"
