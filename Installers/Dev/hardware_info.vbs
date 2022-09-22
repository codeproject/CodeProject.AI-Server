'On Error Resume Next

Set oWSH = CreateObject("WScript.Shell")
vbsInterpreter = "cscript.exe"

Call ForceConsole()

Function printf(txt)
    WScript.StdOut.WriteLine txt
End Function
 
Function ForceConsole()
    If InStr(LCase(WScript.FullName), vbsInterpreter) = 0 Then
        oWSH.Run vbsInterpreter & " //NoLogo " & Chr(34) & WScript.ScriptFullName & Chr(34)
        WScript.Quit
    End If
End Function 

Function NameToLabel(strInput)
  
  strInput = Trim(strInput)
  Dim strOutput: strOutput = ""
  dim prevChar:  prevChar = ""

  For i=1 To Len(strInput)
    Dim char: char = Mid(strInput,i,1)

    if i = 1 or LCase(char) = char Then         ' Start, or lowercase letter
        strOutput = strOutput & char
    Else                                        ' Uppercase letter
        if LCase(prevChar) = prevChar Then      ' Prev was lowercase
            strOutput = strOutput & " " & char
        Else                                    ' Prev was also uppercase
            if i = Len(strInput) Then
                strOutput = strOutput & char    ' End of string
            ElseIf i < Len(strInput) and UCase(Mid(strInput,i+1,1)) = Mid(strInput,i+1,1) Then
                strOutput = strOutput & char    ' Next is also upper
            Else
                strOutput = strOutput & " " & char  ' Next is lower. Add a break
            End If
        End If
    End if

    prevChar = char
  Next

  NameToLabel = strOutput
End Function

Function CIMType(typeId)
    CIMType = "Unknown"
    Select Case typeId
        case 16  CIMType="Int8"
        case 17  CIMType="UInt8"
        case 2   CIMType="Int16"
        case 18  CIMType="UInt16"
        case 3   CIMType="Int32"
        case 19  CIMType="UInt32"
        case 20  CIMType="Int64"
        case 21  CIMType="UInt64"
        case 4   CIMType="Real32"
        case 5   CIMType="Real64"
        case 11  CIMType="Bool"
        case 8   CIMType="String"
        case 101 CIMType="DateTime"
        case 102 CIMType="Reference"
        case 103 CIMType="Char16"
        case 13  CIMType="Object"
    End Select
End Function

Function RPad(StringToPad, Length, CharacterToPad)
    if Len(StringToPad) = 0 Then StringToPad=" "

    Dim padLength : padLength = 0
    If Length > Len(StringToPad) Then padLength = Length - len(StringToPad)
    RPad = StringToPad & String(padLength, CharacterToPad)
End Function

Sub Pause(strPause)
    WScript.Echo (strPause)
    Dim discard : discard = WScript.StdIn.Read(1)
End Sub

strComputer = "."

Set objWMIService = GetObject("winmgmts:\\" & strComputer & "\root\cimv2")
Set colItems = objWMIService.ExecQuery ("Select * from Win32_VideoController")

For Each objItem in colItems

    printf ""
    printf "Name: " & objItem.Name
    printf "-----------------------------------------------------------------------------------"

     if InStr(1, UCase(objItem.Name), "NVIDIA") = 1 Then

        Dim wso, exe, output
        set wso = CreateObject("Wscript.Shell")

        Dim rxp, matches, match
        Set rxp = new RegExp
        rxp.Global     = True
        rxp.Multiline  = False
        rxp.IgnoreCase = True

        ' To use just nvcc, uncomment the following
        ' set exe = wso.Exec("nvcc --version")
        ' output = exe.StdOut.ReadAll
        ' rxp.Pattern = "Cuda compilation tools, release ([\d.]*)"
        ' Set matches = rxp.Execute(output)
        ' Set match = matches(0)
        ' printf RPad("CUDA Tools Version", 35, " ") & match.SubMatches(0)

        ' Using the full nvidia-smi utility:

        set exe = wso.Exec("nvidia-smi")
        output = exe.StdOut.ReadAll

        rxp.Pattern = "NVIDIA-SMI ([\d.]+) \s+ Driver Version: ([\d.]+) \s+ CUDA Version: ([\d.]+)"
        Set matches = rxp.Execute(output)

        if matches.Count > 0 Then
            Set match = matches(0)
            printf RPad("NVIDIA-SMI Version", 35, " ") & match.SubMatches(0)
            printf RPad("NVIDIA Driver Version", 35, " ") & match.SubMatches(1)
            printf RPad("CUDA Tools Version", 35, " ") & match.SubMatches(2)
            printf ""
        End If

    End If

    For Each oProp in objItem.Properties_
        dim label: label = NameToLabel(oProp.Name)
        if label <> "Name" And label <> "System Name" Then 
        '    printf RPad(label, 35, " ")  & RPad(oProp, 20, " ") & " (" & oProp.Name & ", " & CIMType(oProp.CIMType) & ")"
            printf RPad(label, 35, " ") & oProp
        end if
    Next

Next

Pause("Press Enter to continue")
