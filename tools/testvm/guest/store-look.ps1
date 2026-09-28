# Runs in tester's session: dark mode (apps + taskbar) and a solid black desktop,
# the look used for the store screenshots.
Add-Type -Namespace Native -Name Look -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(int action, int param, string value, int flags);
[DllImport("user32.dll")] public static extern bool SetSysColors(int count, int[] elements, int[] colors);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam, string lParam, int flags, int timeout, out IntPtr result);
'@
$personalize = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
Set-ItemProperty $personalize -Name AppsUseLightTheme -Value 0 -Type DWord
Set-ItemProperty $personalize -Name SystemUsesLightTheme -Value 0 -Type DWord

Set-ItemProperty 'HKCU:\Control Panel\Colors' -Name Background -Value '0 0 0'
Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name WallPaper -Value ''
New-Item -Force 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers' | Out-Null
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers' -Name BackgroundType -Value 1 -Type DWord
[Native.Look]::SetSysColors(1, @(1), @(0)) | Out-Null
[Native.Look]::SystemParametersInfo(0x0014, 0, '', 0x03) | Out-Null

# Tell the shell and apps the theme changed.
$r = [IntPtr]::Zero
[Native.Look]::SendMessageTimeout([IntPtr]0xffff, 0x001A, [IntPtr]::Zero, 'ImmersiveColorSet', 2, 5000, [ref]$r) | Out-Null
Set-Content C:\sf\look.done 1
