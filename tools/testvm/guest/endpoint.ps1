# Runs inside the test VM: enable/disable an audio endpoint exactly like the
# Sound control panel does (IPolicyConfig::SetEndpointVisibility). Disabling the
# PnP node instead leaves the endpoint "Active" to Core Audio, which is useless
# for simulating an unplugged device.
param(
    [Parameter(Mandatory)] [string] $Name,          # substring of the endpoint name, e.g. "16ch"
    [ValidateSet('Render', 'Capture')] [string] $Flow = 'Render',
    [Parameter(Mandatory)] [bool] $Enabled
)

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

[ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPolicyConfig {
    int GetMixFormat(); int GetDeviceFormat(); int ResetDeviceFormat(); int SetDeviceFormat();
    int GetProcessingPeriod(); int SetProcessingPeriod(); int GetShareMode(); int SetShareMode();
    int GetPropertyValue(); int SetPropertyValue(); int SetDefaultEndpoint();
    [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int visible);
}
[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] class CPolicyConfigClient { }

public static class Endpoints {
    public static int SetVisible(string id, bool visible) {
        return ((IPolicyConfig)new CPolicyConfigClient()).SetEndpointVisibility(id, visible ? 1 : 0);
    }
}
'@

$root = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\$Flow"
$prefix = if ($Flow -eq 'Render') { '{0.0.0.00000000}' } else { '{0.0.1.00000000}' }
$hits = foreach ($key in Get-ChildItem $root) {
    # {a45c254e-...},2 = PKEY_Device_DeviceDesc ("CABLE In 16ch").
    $desc = (Get-ItemProperty (Join-Path $key.PSPath 'Properties'))."{a45c254e-df1c-4efd-8020-67d146a850e0},2"
    if ($desc -like "*$Name*") { [pscustomobject]@{ Id = "$prefix.$($key.PSChildName)"; Name = $desc } }
}
if (@($hits).Count -ne 1) { throw "Expected exactly one $Flow endpoint matching '$Name', found: $(@($hits).Name -join ', ')" }
$hr = [Endpoints]::SetVisible($hits.Id, $Enabled)
"{0} '{1}' -> enabled={2} (hr=0x{3:X8})" -f $Flow, $hits.Name, $Enabled, $hr
