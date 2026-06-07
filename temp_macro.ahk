Run, notepad.exe

; Wait up to 3 seconds for Notepad to open and become the active window
WinWaitActive, ahk_exe notepad.exe,, 3 

if ErrorLevel
{
    MsgBox, Notepad took too long to open.
    return
}

Sleep, 200 ; Wait 200 milliseconds just to be safe
SendInput, Hello! This is a multi-step macro testing the new Raw AHK Script feature.
Sleep, 500 ; Wait half a second
SendInput, {Enter}{Enter}It works perfectly!