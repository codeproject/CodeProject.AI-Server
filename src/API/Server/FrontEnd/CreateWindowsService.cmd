Rem CreateWindowsService.ps1

sc.exe create "CodeProject SenseAI Server" binPath= "%CD%\CodeProject.SenseAI.Server.exe --urls http://*:5000 --environment Production --VISION-FACE=true --VISION-DETECTION=true --VISION-SCENE=true" start= auto
sc.exe description "CodeProject SenseAI Server" "A Service hosting the CodeProject SenseAI WebAPI for face detection and recognition, object detection, and scene classification."
sc.exe failure "CodeProject SenseAI Server" reset= 30 actions= restart/5000/restart/5000/restart/5000
sc.exe start "CodeProject SenseAI Server"