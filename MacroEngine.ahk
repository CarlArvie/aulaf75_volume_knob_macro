#Requires AutoHotkey v1.1
#Persistent
#SingleInstance Force
#NoEnv
#NoTrayIcon
SetWorkingDir %A_ScriptDir%
FileAppend, MacroEngine started at %A_Now%`n, macro_debug.txt
SendMode Input

global pipePath := "\\.\pipe\MacroUIPipe"
global pipeClient := ""

SetTimer, CheckExecute, 100
SetTimer, CheckUI, 2000

IniRead, SelectHotkey, %A_ScriptDir%\settings.ini, Hotkeys, Select, Volume_Mute
IniRead, BackHotkey, %A_ScriptDir%\settings.ini, Hotkeys, Back, RButton

if (SelectHotkey)
    Hotkey, $%SelectHotkey%, SelectAction, On
if (BackHotkey)
    Hotkey, $%BackHotkey%, BackAction, On

CheckUI:
Process, Exist, MacroUI.exe
if (!ErrorLevel)
{
    FileAppend, CheckUI failed to find MacroUI.exe. ErrorLevel: %ErrorLevel%`n, macro_debug.txt
    ; ExitApp
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
    prefix9 := SubStr(action, 1, 9)

    if (prefix5 == "send:") {
        cmd := Trim(SubStr(action, 6))
        SendInput, %cmd%
    } else if (prefix9 == "sendtext:") {
        cmd := SubStr(action, 10)
        SendInput {Text}%cmd%
    } else if (prefix4 == "run:") {
        cmd := Trim(SubStr(action, 5))
        Run, %cmd%
    } else if InStr(action, "RELOAD") {
        Reload
    } else {
        ; Default fallback
        SendInput, %action%
    }
}

SendAsPaste(text) {
    FileAppend, SendAsPaste called with %text%`n, macro_debug.txt
    ClipSave := ClipboardAll
    Clipboard := ""
    Clipboard := text
    ClipWait, 1
    Sleep, 50
    Send, ^v
    Sleep, 150
    Clipboard := ClipSave
}

#Include %A_ScriptDir%\hotstrings.ahk 

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

SelectAction:
    if (MacroMode) {
        SendCommand("SELECT")
    } else {
        key := StrReplace(A_ThisHotkey, "$", "")
        Send {%key%}
    }
return

BackAction:
    if (MacroMode) {
        SendCommand("BACK")
    } else {
        key := StrReplace(A_ThisHotkey, "$", "")
        Send {%key%}
    }
return
