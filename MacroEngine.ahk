#Persistent
#NoEnv
#NoTrayIcon
SendMode Input

global pipePath := "\\.\pipe\MacroUIPipe"
global pipeClient := ""

SetTimer, CheckExecute, 100
SetTimer, CheckUI, 2000

CheckUI:
Process, Exist, MacroUI.exe
if (!ErrorLevel)
{
    ExitApp
}
return

SendCommand(cmd) {
    global pipePath, pipeClient
    if (!pipeClient) {
        pipeClient := FileOpen(pipePath, "w")
    }
    if (pipeClient) {
        try {
            pipeClient.WriteLine(cmd)
            pipeClient.Read(0) ; Flush
        } catch e {
            ; Pipe might be broken, reopen next time
            pipeClient.Close()
            pipeClient := ""
        }
    }
}

CheckExecute:
IfExist, execute.txt
{
    FileRead, macroId, execute.txt
    FileDelete, execute.txt
    ExecuteMacro(Trim(macroId))
}
return

ExecuteMacro(action) {
    ; Handle the execution based on the action string sent from C#
    prefix5 := SubStr(action, 1, 5)
    prefix4 := SubStr(action, 1, 4)

    if (prefix5 == "send:") {
        cmd := Trim(SubStr(action, 6))
        SendInput, %cmd%
    } else if (prefix4 == "run:") {
        cmd := Trim(SubStr(action, 5))
        Run, %cmd%
    } else {
        ; Default fallback
        SendInput, %action%
    }
}

; We use a custom variable as a toggle for Macro Mode.
; Press PgUp and PgDn together to turn Macro Mode ON or OFF.
global MacroMode := false

~PgUp & PgDn::
    MacroMode := !MacroMode
    if (MacroMode) {
        ToolTip, Macro Mode ON
    } else {
        ToolTip, Macro Mode OFF
        SendCommand("HIDE")
    }
    SetTimer, RemoveToolTip, -2000
return

RemoveToolTip:
    ToolTip
return

$Volume_Up::
    if (MacroMode) {
        SendCommand("NEXT")
    } else {
        Send {Volume_Up}
    }
return

$Volume_Down::
    if (MacroMode) {
        SendCommand("PREV")
    } else {
        Send {Volume_Down}
    }
return

$Volume_Mute::
    if (MacroMode) {
        SendCommand("SELECT")
    } else {
        Send {Volume_Mute}
    }
return
