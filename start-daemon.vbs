' Launches the audsw daemon with no window at all (style 0).
' Put a shortcut to this file in shell:startup to run it at login.
Dim sh, here
Set sh = CreateObject("WScript.Shell")
here = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
sh.CurrentDirectory = here
sh.Run """" & here & "audsw.exe"" daemon", 0, False
