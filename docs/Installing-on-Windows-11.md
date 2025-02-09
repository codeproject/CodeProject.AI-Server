**Installing on Windows 11
**

**With Nvidia GPU**  
Download and install the graphics driver for your card  
https://www.nvidia.com/en-gb/drivers/  
  
**Download all the bits you need**  
D-1. Download Microsoft .Net [here](https://dotnet.microsoft.com/en-us/download) (Current compatible version .Net 9)  
D-2. Download the latest CodeProject AI Server version [here](https://codeproject.github.io/)  
D-3. Download models that CodeProject AI uses to detect motion, face processing etc.  
  
   - Goto the github repository, click the green **Code** button then **Download ZIP**  
   - YOLOv8 (Newer GPUs) [here](https://github.com/codeproject/CodeProject.AI-ObjectDetectionYOLOv8) https://github.com/codeproject/CodeProject.AI-ObjectDetectionYOLOv8  
   - Face Processing [here](https://github.com/codeproject/CodeProject.AI-FaceProcessing) https://github.com/codeproject/CodeProject.AI-FaceProcessing  
  
**Extract the files**  
-Right click, extract all on the following-  
E-1. Extract the CodeProject AI Server from step D-2 (CodeProject.AI-Server_*version*_win_x64.zip)  
E-2. Extract the modules from step D-3 (CodeProject.AI-ObjectDetectionYOLOv8-main.zip, CodeProject.AI-FaceProcessing-main.zip)  
  
**Install**  
I-1. Install Microsoft .Net (dotnet-sdk-*version*-win-x64.exe) from step D-1  
I-2. Right click and Run As Administrator the CodeProject AI Server Installer from extracted folder in step E-1 (CodeProject.AI-Server_*version*_win_x64.exe)  
     **Important: Untick all modules as these will timeout and you will likely need to uninstall and start again**  
       
I-3. Once the server has installed. Copy the extracted modules to C:\Program Files\CodeProject\AI\modules,   
     - CodeProject.AI-ObjectDetectionYOLOv8-main &  
     - CodeProject.AI-FaceProcessing-main  
     Now going to C:\Program Files\CodeProject\AI\modules\CodeProject.AI-ObjectDetectionYOLOv8-main folder, you should see many files. If you see another folder called odeProject.AI-ObjectDetectionYOLOv8-main then you need to copy this up a level.  
       
I-4. Open an elevated command prompt (click start, type cmd, right click, Run as administrator) and navigate into the module directory (_cd C:\Program Files\CodeProject\AI\modules\CodeProject.AI-ObjectDetectionYOLOv8-main_)  
I-5. Now enter "..\..\setup" to install the module and wait for it to finish.  
I-6. Now install any other modules, e.g. in the same elevated command prompt window, enter "_cd C:\Program Files\CodeProject\AI\modules\CodeProject.AI-FaceProcessing-main_" then again run _..\..\setup_ and wait for it to complete.  
I-7. Restart your server, then load up the CodeProject AI Server Dashboard from the start menu.  
I-8. If modules havnt already started, click the play button next to each.  
I-9. If it detects a GPU it will automatically switch from CPU to GPU (CUDA)  
  
Creddit: u/Stevosworld - https://www.reddit.com/r/BlueIris/comments/1gfi96k/heres_a_10_step_program_to_fresh_install/
