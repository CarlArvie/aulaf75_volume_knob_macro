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

DetectHiddenWindows, On
WinSetTitle, ahk_id %A_ScriptHwnd%, , AulaMacroEngine_IPC
OnMessage(0x004A, "Receive_WM_COPYDATA") ; WM_COPYDATA = 0x004A

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

Receive_WM_COPYDATA(wParam, lParam) {
    ; Retrieves the CopyDataStruct's lpData member.
    StringAddress := NumGet(lParam + 2*A_PtrSize)
    ; Copy the string out of the structure (AHK v1.1 Unicode expects UTF-16)
    action := StrGet(StringAddress)
    ExecuteMacro(Trim(action))
    return true
}

ExecuteMacro(action) {
    ; Handle the execution based on the action string sent from C#
    prefix4 := SubStr(action, 1, 4)
    prefix5 := SubStr(action, 1, 5)
    prefix8 := SubStr(action, 1, 8)
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
    } else if (prefix4 == "sys:") {
        cmd := Trim(SubStr(action, 5))
        if (cmd == "LockComputer") {
            DllCall("user32.dll\LockWorkStation")
        } else if (cmd == "SleepComputer") {
            DllCall("PowrProf\SetSuspendState", "int", 0, "int", 0, "int", 0)
        } else if (cmd == "MuteAudio") {
            Send {Volume_Mute}
        } else if (cmd == "TurnOffDisplay") {
            SendMessage, 0x112, 0xF170, 2,, Program Manager
        } else if (cmd == "PlayPauseMedia") {
            Send {Media_Play_Pause}
        } else if (cmd == "LogOff") {
            Shutdown, 0
        }
    } else if (prefix8 == "suspend:") {
        cmd := Trim(SubStr(action, 9))
        if (cmd == "on") {
            Suspend, On
        } else if (cmd == "off") {
            Suspend, Off
        }
    } else if (prefix4 == "ahk:") {
        cmd := Trim(SubStr(action, 5))
        FileDelete, %A_ScriptDir%\temp_macro.ahk
        FileAppend, %cmd%, %A_ScriptDir%\temp_macro.ahk
        Run, "%A_AhkPath%" "%A_ScriptDir%\temp_macro.ahk"
    } else if InStr(action, "RELOAD") {
        Reload
    } else {
        ; Default fallback
        SendInput, %action%
    }
}

SendAsPaste(text) {
    FileAppend, SendAsPaste called with %text%`n, macro_debug.txt
    
    ; Resolve AHK dynamic variables (e.g. %A_YYYY%)
    Transform, text, Deref, %text%
    
    ; Check for [CURSOR] token
    cursorPos := InStr(text, "[CURSOR]")
    if (cursorPos) {
        text := StrReplace(text, "[CURSOR]", "")
    }

    ClipSave := ClipboardAll
    Clipboard := ""
    Clipboard := text
    ClipWait, 1
    Sleep, 50
    Send, ^v
    Sleep, 150
    Clipboard := ClipSave
    
    ; Position cursor if [CURSOR] token was used
    if (cursorPos) {
        charsToMoveBack := StrLen(text) - cursorPos + 1
        if (charsToMoveBack > 0) {
            Send, {Left %charsToMoveBack%}
        }
    }
}

PasteImage(imagePath) {
    FileAppend, PasteImage called with %imagePath%`n, macro_debug.txt
    ClipSave := ClipboardAll
    psCommand := "Add-Type -AssemblyName System.Windows.Forms; Add-Type -AssemblyName System.Drawing; $img = [System.Drawing.Image]::FromFile('" . imagePath . "'); [System.Windows.Forms.Clipboard]::SetImage($img);"
    RunWait, powershell.exe -NoProfile -Command "%psCommand%", , Hide
    Sleep, 100
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

BackAction:
    if (MacroMode) {
        SendCommand("BACK")
    } else {
        key := StrReplace(A_ThisHotkey, "$", "")
        Send {%key%}
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

#Include *i %A_ScriptDir%\custom_hotkeys.ahk
