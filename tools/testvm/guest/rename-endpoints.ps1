# Runs inside the test VM as an elevated administrator (PowerShell Direct is).
# Gives the VB-Audio endpoints realistic names for store screenshots, then
# restarts the audio services so Core Audio reloads them.
#
# The MMDevices "Properties" keys are writable only by the audio service's own
# account (not even SYSTEM), so this takes ownership of those keys and grants
# Administrators write access first. Test VM only.
param([string] $Map = 'C:\sf\rename-map.json')

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Privilege {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct TokenPrivileges { public int Count; public long Luid; public int Attributes; }
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, int access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool LookupPrivilegeValue(string system, string name, out long luid);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges state, int length, IntPtr previous, IntPtr returnLength);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    public static void Enable(string name) {
        IntPtr token; long luid;
        OpenProcessToken(GetCurrentProcess(), 0x28 /* ADJUST_PRIVILEGES | QUERY */, out token);
        LookupPrivilegeValue(null, name, out luid);
        var state = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 /* ENABLED */ };
        AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero);
    }
}
'@
[Privilege]::Enable('SeTakeOwnershipPrivilege')
[Privilege]::Enable('SeRestorePrivilege')

$admins = New-Object System.Security.Principal.SecurityIdentifier 'S-1-5-32-544'
function Grant-AdminWrite([string] $subKey) {
    $rw = [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($subKey, $rw, [System.Security.AccessControl.RegistryRights]::TakeOwnership)
    $acl = $key.GetAccessControl([System.Security.AccessControl.AccessControlSections]::None)
    $acl.SetOwner($admins); $key.SetAccessControl($acl); $key.Close()
    $key = [Microsoft.Win32.Registry]::LocalMachine.OpenSubKey($subKey, $rw, [System.Security.AccessControl.RegistryRights]::ChangePermissions)
    $acl = $key.GetAccessControl()
    $acl.AddAccessRule((New-Object System.Security.AccessControl.RegistryAccessRule $admins, 'FullControl', 'Allow'))
    $key.SetAccessControl($acl); $key.Close()
}

# { "Render": { "CABLE Input": ["Speakers", "Realtek(R) Audio"], ... }, "Capture": { ... } }
$renames = Get-Content $Map -Raw | ConvertFrom-Json
$desc = '{a45c254e-df1c-4efd-8020-67d146a850e0},2'          # PKEY_Device_DeviceDesc   ("Speakers")
$iface = '{b3f8fa53-0004-438e-9003-51a46e139bfc},6'         # PKEY_DeviceInterface_FriendlyName ("Realtek(R) Audio")
# Don't write PKEY_Device_FriendlyName (,14): it isn't stored here, Windows derives
# it from the two above, and a stored copy makes Core Audio report "Unknown".
$root = 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio'

foreach ($flow in 'Render', 'Capture') {
    $wanted = $renames.$flow
    if (-not $wanted) { continue }
    foreach ($key in Get-ChildItem "HKLM:\$root\$flow") {
        $props = Join-Path $key.PSPath 'Properties'
        $current = (Get-ItemProperty $props -ErrorAction SilentlyContinue).$desc
        if (-not $current) { continue }
        $target = $wanted.PSObject.Properties | Where-Object Name -eq $current | Select-Object -First 1
        if (-not $target) { continue }
        Grant-AdminWrite "$root\$flow\$($key.PSChildName)\Properties"
        $name, $device = $target.Value
        Set-ItemProperty $props -Name $desc -Value $name
        Set-ItemProperty $props -Name $iface -Value $device
        "$flow '$current' -> '$name ($device)'"
    }
}
# The part in brackets ("Speakers (Realtek(R) Audio)") comes live from the audio
# device's own FriendlyName, shared by all its endpoints, not from the key above.
if ($renames.DeviceName) {
    foreach ($dev in Get-PnpDevice -Class MEDIA | Where-Object FriendlyName -like 'VB-Audio*') {
        $sub = "SYSTEM\CurrentControlSet\Enum\$($dev.InstanceId)"
        Grant-AdminWrite $sub
        Set-ItemProperty "HKLM:\$sub" -Name FriendlyName -Value $renames.DeviceName
        "Device '$($dev.FriendlyName)' -> '$($renames.DeviceName)'"
    }
}
Restart-Service AudioEndpointBuilder -Force
Start-Service Audiosrv
