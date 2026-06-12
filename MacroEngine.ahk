#Requires AutoHotkey v2.0
#SingleInstance Force
#NoTrayIcon
SetWorkingDir(A_ScriptDir)
FileAppend("MacroEngine started at " A_Now "`n", "macro_debug.txt")

global pipePath := "\\.\pipe\MacroUIPipe"
global pipeClient := ""

DetectHiddenWindows(true)
WinSetTitle("AulaMacroEngine_IPC", "ahk_id " A_ScriptHwnd)
OnMessage(0x004A, Receive_WM_COPYDATA) ; WM_COPYDATA = 0x004A

SetTimer(CheckUI, 2000)

SelectHotkey := IniRead(A_ScriptDir "\settings.ini", "Hotkeys", "Select", "Volume_Mute")
BackHotkey := IniRead(A_ScriptDir "\settings.ini", "Hotkeys", "Back", "RButton")

if (SelectHotkey)
    Hotkey("$" SelectHotkey, SelectAction, "On")
if (BackHotkey)
    Hotkey("$" BackHotkey, BackAction, "On")

CheckUI() {
    if !ProcessExist("MacroUI.exe") {
        FileAppend("CheckUI failed to find MacroUI.exe.`n", "macro_debug.txt")
        ExitApp()
    }
}

SendCommand(cmd) {
    global pipePath, pipeClient
    if (!pipeClient) {
        try {
            pipeClient := FileOpen(pipePath, "w")
        } catch {
            pipeClient := ""
        }
    }
    if (pipeClient) {
        try {
            pipeClient.WriteLine(cmd)
            pipeClient.Read(0) ; Flush
        } catch {
            ; Pipe might be broken, reopen next time
            pipeClient.Close()
            pipeClient := ""
        }
    }
}

Receive_WM_COPYDATA(wParam, lParam, msg, hwnd) {
    ; Retrieves the CopyDataStruct's lpData member.
    StringAddress := NumGet(lParam, 2 * A_PtrSize, "Ptr")
    ; Copy the string out of the structure (AHK v2 Unicode expects UTF-16)
    action := StrGet(StringAddress, "UTF-16")
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
        Send(cmd)
    } else if (prefix9 == "sendtext:") {
        cmd := SubStr(action, 10)
        SendText(cmd)
    } else if (prefix4 == "run:") {
        cmd := Trim(SubStr(action, 5))
        Run(cmd)
    } else if (prefix4 == "sys:") {
        cmd := Trim(SubStr(action, 5))
        if (cmd == "LockComputer") {
            DllCall("user32.dll\LockWorkStation")
        } else if (cmd == "SleepComputer") {
            DllCall("PowrProf\SetSuspendState", "int", 0, "int", 0, "int", 0)
        } else if (cmd == "MuteAudio") {
            Send("{Volume_Mute}")
        } else if (cmd == "TurnOffDisplay") {
            SendMessage(0x112, 0xF170, 2,, "Program Manager")
        } else if (cmd == "PlayPauseMedia") {
            Send("{Media_Play_Pause}")
        } else if (cmd == "LogOff") {
            Shutdown(0)
        }
    } else if (prefix8 == "suspend:") {
        cmd := Trim(SubStr(action, 9))
        if (cmd == "on") {
            Suspend(true)
        } else if (cmd == "off") {
            Suspend(false)
        }
    } else if (prefix4 == "ahk:") {
        cmd := Trim(SubStr(action, 5))
        try FileDelete(A_ScriptDir "\temp_macro.ahk")
        FileAppend(cmd, A_ScriptDir "\temp_macro.ahk")
        Run('"' A_AhkPath '" "' A_ScriptDir '\temp_macro.ahk"')
    } else if InStr(action, "RELOAD") {
        Reload()
    } else {
        ; Default fallback
        Send(action)
    }
}

SendAsPaste(text) {
    FileAppend("SendAsPaste called with " text "`n", "macro_debug.txt")
    
    ; Check for [CURSOR] token
    cursorPos := InStr(text, "[CURSOR]")
    if (cursorPos) {
        text := StrReplace(text, "[CURSOR]", "")
    }

    ClipSave := ClipboardAll()
    A_Clipboard := ""
    A_Clipboard := text
    if !ClipWait(1)
        return
    Sleep(50)
    Send("^v")
    Sleep(150)
    A_Clipboard := ClipSave
    
    ; Position cursor if [CURSOR] token was used
    if (cursorPos) {
        charsToMoveBack := StrLen(text) - cursorPos + 1
        if (charsToMoveBack > 0) {
            Send("{Left " charsToMoveBack "}")
        }
    }
}

PasteImage(imagePath) {
    FileAppend("PasteImage called with " imagePath "`n", "macro_debug.txt")
    ClipSave := ClipboardAll()
    psCommand := "Add-Type -AssemblyName System.Windows.Forms; Add-Type -AssemblyName System.Drawing; $img = [System.Drawing.Image]::FromFile('" imagePath "'); [System.Windows.Forms.Clipboard]::SetImage($img);"
    RunWait("powershell.exe -NoProfile -Command `"" psCommand "`"", , "Hide")
    Sleep(100)
    Send("^v")
    Sleep(150)
    A_Clipboard := ClipSave
}

#Include "*i %A_ScriptDir%\hotstrings.ahk"

; We use a custom variable as a toggle for Macro Mode.
; Press PgUp and PgDn together to turn Macro Mode ON or OFF.
global MacroMode := IniRead(A_Temp "\AulaMacroState.ini", "State", "MacroMode", 1)

~PgUp & PgDn:: {
    global MacroMode
    MacroMode := !MacroMode
    IniWrite(MacroMode, A_Temp "\AulaMacroState.ini", "State", "MacroMode")
    if (MacroMode) {
        ToolTip("Macro Mode ON")
    } else {
        ToolTip("Macro Mode OFF")
        SendCommand("HIDE")
    }
    SetTimer(RemoveToolTip, -2000)
}

RemoveToolTip() {
    ToolTip()
}

$Volume_Up:: {
    global MacroMode
    if (MacroMode) {
        SendCommand("NEXT")
    } else {
        Send("{Volume_Up}")
    }
}

$Volume_Down:: {
    global MacroMode
    if (MacroMode) {
        SendCommand("PREV")
    } else {
        Send("{Volume_Down}")
    }
}

BackAction(ThisHotkey) {
    global MacroMode
    if (MacroMode) {
        SendCommand("BACK")
    } else {
        key := StrReplace(ThisHotkey, "$", "")
        Send("{" key "}")
    }
}

SelectAction(ThisHotkey) {
    global MacroMode
    if (MacroMode) {
        SendCommand("SELECT")
    } else {
        key := StrReplace(ThisHotkey, "$", "")
        Send("{" key "}")
    }
}

#Include "*i %A_ScriptDir%\custom_hotkeys.ahk"
