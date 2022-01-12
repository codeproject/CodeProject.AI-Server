Rem DeleteWindowsService.ps1

sc.exe stop "CodeProject SenseAI Server"
sc.exe delete "CodeProject SenseAI Server"
