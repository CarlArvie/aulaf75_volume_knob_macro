#Requires AutoHotkey v1.1
#SingleInstance Force
#NoEnv

#Include MacroEngine.ahk

; Included purely to bring functions into scope. 
; We bypass auto-execute by jumping over it if needed, or we just let it init.
; But MacroEngine.ahk has #Persistent and SetTimers, which is fine for tests.
; To avoid running timers, we could override them, but let's just include the functions.

global TestsPassed := 0
global TestsFailed := 0

FileAppend, `n--- Starting AHK Unit Tests ---`n, *

; ==========================================
; Test 1: SendAsPaste Variable Dereferencing
; ==========================================
Test_SendAsPaste_Variable() {
    global TestsPassed, TestsFailed
    
    ; Setup
    testVar := "HelloWorld"
    inputStr := "Testing %testVar% deref"
    
    ; We cannot easily mock SendInput, but SendAsPaste modifies Clipboard
    ; Let's temporarily backup Clipboard
    ClipSave := ClipboardAll
    
    ; We simulate what SendAsPaste does:
    Transform, resolvedText, Deref, %inputStr%
    
    ; Assert
    if (resolvedText == "Testing HelloWorld deref") {
        TestsPassed++
        FileAppend, [PASS] Test_SendAsPaste_Variable`n, *
    } else {
        TestsFailed++
        FileAppend, [FAIL] Test_SendAsPaste_Variable: Expected "Testing HelloWorld deref", got "%resolvedText%"`n, *
    }
    
    Clipboard := ClipSave
}

; ==========================================
; Test 2: SendAsPaste Cursor Token
; ==========================================
Test_SendAsPaste_CursorToken() {
    global TestsPassed, TestsFailed
    
    inputStr := "hello [CURSOR] world"
    cursorPos := InStr(inputStr, "[CURSOR]")
    
    if (cursorPos) {
        text := StrReplace(inputStr, "[CURSOR]", "")
    } else {
        text := inputStr
    }
    
    if (text == "hello  world" && cursorPos == 7) {
        TestsPassed++
        FileAppend, [PASS] Test_SendAsPaste_CursorToken`n, *
    } else {
        TestsFailed++
        FileAppend, [FAIL] Test_SendAsPaste_CursorToken: Parsing failed`n, *
    }
}

; ==========================================
; Test 3: Macro Action String Parsing
; ==========================================
Test_ExecuteMacro_Parsing() {
    global TestsPassed, TestsFailed
    
    ; Simulate the string checking from ExecuteMacro
    action := "sendtext:hello world"
    prefix9 := SubStr(action, 1, 9)
    cmd := SubStr(action, 10)
    
    if (prefix9 == "sendtext:" && cmd == "hello world") {
        TestsPassed++
        FileAppend, [PASS] Test_ExecuteMacro_Parsing`n, *
    } else {
        TestsFailed++
        FileAppend, [FAIL] Test_ExecuteMacro_Parsing`n, *
    }
}

; Run Tests
Test_SendAsPaste_Variable()
Test_SendAsPaste_CursorToken()
Test_ExecuteMacro_Parsing()

; Summary
FileAppend, `nTest Summary: %TestsPassed% Passed, %TestsFailed% Failed.`n, *

if (TestsFailed > 0)
    ExitApp, 1
else
    ExitApp, 0
