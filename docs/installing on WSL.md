# Setting up the development environment for Windows Subsystem for Linux (WSL)

1. Install WSL by opening a PowerShell terming and typing
    ```powershell
    wsl --install
    ```

2. Ensure VS Code is installed. The downloads for each platform are at https:/`/code.visualstudio.com/download`. For WSL select Ubuntu, 64 bit (under "Debian, Ubuntu" select '64 bit')

3. Install the [VS Code Remote Development extension pack](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.vscode-remote-extensionpack)

4. Install the .NET SDK:

   1. Add the signing keys and package repository
       ```sh
      wget https://packages.microsoft.com/config/ubuntu/21.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
       sudo dpkg -i packages-microsoft-prod.deb
       rm packages-microsoft-prod.deb
       ```

   2. Install the .NET SDK (this contains the compilers to build the code and .NET runtime to execute the compiled apps)
        ```sh
        sudo apt-get update; \
        sudo apt-get install -y apt-transport-https && \
        sudo apt-get update && \
        sudo apt-get install -y dotnet-sdk-6.0
        ```
        (Side note: When deploying the compiled SenseAI to Linux you won't need the full SDK; The runtime will be fine and will save a little bit of space. To install just the runtime and not the full SDK replace `aspnetcore-sdk-6.0` with `aspnetcore-runtime-6.0` in the instructions above)
        
5. Navigate to your repo and launch VS Code
    ```
    cd /mnt/c/Dev/CodeProject/CodeProject.SenseAI
    code .
    ````

6. Re-open in WSL by hitting `Ctrl+Shift P` for the command pallete, select "Remote-WSL: Reopen Folder in WSL" and hit enter.

You are now coding against the existing Windows repository, but using a remote connection to the WSL system from within VS Code. From within this current environment it's all Linux.