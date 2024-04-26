# To setup your Jetson Nano

1. **Create a boot SD card** by downloading [balena Etcher](https://etcher.balena.io/) and the [QEngineering Ubuntu 20.04 Image](https://github.com/Qengineering/Jetson-Nano-Ubuntu-20-image). We recommend this in preference to the [Official Jetson Nano image](https://developer.nvidia.com/embedded/learn/get-started-jetson-nano-devkit#write) due to the QEngineering image being a more updated distro, as well as it being chock-full of all the AI pieces you need such as Tensorflow, PyTorch, TensorRT and OpenCV.

2. **Setup your Nano** by inserting the card into the device, plugging in keyboard, mouse, monitor and power, and following the instructions. After a restart you'll find yourself on the Jetson Nano desktop

3. **Increase your swap size** by downloading the [`setSwapMemorySize.sh`](https://github.com/JetsonHacksNano/resizeSwapMemory/blob/master/setSwapMemorySize.sh) script. The script will arrive in `~/Downloads`. Open a terminal and run the following.
    ```bash
    cd ~/Downloads
    bash setSwapMemorySize.sh -g 4
    ```

    Reboot your Jetson to allow the changes to take effect.

4. **Download VSCode** using the Mozilla browser (don't download Chrome on the QEngineering image because it will mess with snapd) and head to [code.visualstudio.com/download](https://code.visualstudio.com/download). Download the arm64 debian package. The Visual Studio Code installer will be in saved to `~/Downloads`

5. To **install Visual Studio Code** open a terminal window and call
   ```bash
   cd ~/Downloads
   sudo apt install code_1.85.1-102461056_arm64.deb
   ```
   `code_1.85.1-102461056_arm64.deb` is the latest filename, but this will change. Use the name of the .deb file you downloaded from Microsoft.

6. Open VSCode, sign in, and **sync your VSCode settings** (the 'head' icon at the bottom left of the VSCode window). A browser window will pop up to allow you to authenticate: always close this after authentication to reduce memory pressure. The syncing of settings may take some time depending on how many extensions you have. The Jetson itself may become unresponsive during this due to lack of memory. Let it run - it could be half an hour to an hour.

    **Your Nano may lockup** and VSCode may crash. Multiple times. Just keep trying. Eventually you will have VS Code setup and synced.

7. **Clone the [CodeProject.AI project](https://github.com/codeproject/CodeProject.AI-Server)** from GitHub (using the Git tools in VSCode is easiest) and then open the project. Again: the GitHub auth will launch Chrome and you may run out of memory and see lockups and crashes. Persist.

8. **Setup the dev environment** on the Nano by heading to `src` in the project and running 
    ```bash
    bash setup.sh
    ````
    You will need to provide an admin password at various points in the process. The entire setup will take over an hour.

9. **Build and run** in VSCode using the debug tab. Choose 'Build all and Launch server (arm64)'. Note that this will build and run CodeProject.AI on the Jetson using the full VSCode editor, the .NET runtime, and Python, while also opening a Chrome browser. **Your Jetson Nano will be stressed to the limit**.

10. **Alternative option: SSH to the Jetson** Using SSH to connect to the Jetson Nano allows you to have a smooth and seamless editing, build and debug experience on the Jetson without placing undue memory and processing pressure on the device from VSCode. If the device locks up you can still edit, and sometimes even save changes, while the device works through its issues. 

    You can SSH from another machine into the Jetson Nano using the VSCode [Remote SSH](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-ssh) extension. Install this on your main desktop, go to the Dev Containers tab on your desktop, enter the IP address of your Jetson (use `ifconfig` on the Jetson to find your address) and then follow the prompts to login. 
    
    The 'Open Folder' menu option allows you to then open the folder containing the cloned CodeProject.AI solution, and from then on you are editing, building and debugging on the Jetson, but using the power of your desktop to take the editing and GUI load off the Nano.

    As in step 9, simply choose 'Build all and Launch server (arm64)' to build on the Jetson, and then launch the server and dashboard for the Jetson.