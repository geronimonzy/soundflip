# Runs inside the test VM: prints active endpoints and which one holds the
# Console (default) and Communications roles, for render and capture.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator {
    int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice {
    int Activate(ref Guid iid, int ctx, IntPtr p, out IntPtr o);
    int OpenPropertyStore(int access, out IntPtr store);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
}
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

public static class AudioRoles {
    // role: 0 console, 2 communications. flow: 0 render, 1 capture.
    public static string DefaultId(int flow, int role) {
        var e = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice d;
        string id;
        if (e.GetDefaultAudioEndpoint(flow, role, out d) != 0 || d == null) return "(none)";
        return d.GetId(out id) == 0 ? id : "(none)";
    }
}
'@

$endpoints = Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue
function NameOf($id) {
    if ($id -eq '(none)') { return $id }
    $match = $endpoints | Where-Object { $_.InstanceId -like "*$($id.ToUpper())*" -or $_.InstanceId -like "*$id*" }
    if ($match) { $match[0].FriendlyName } else { $id }
}

"Endpoints:"
$endpoints | ForEach-Object { "  [{0}] {1}" -f $_.Status, $_.FriendlyName }
foreach ($flow in 0, 1) {
    $label = @('Output', 'Input')[$flow]
    "{0} default:        {1}" -f $label, (NameOf ([AudioRoles]::DefaultId($flow, 0)))
    "{0} communications: {1}" -f $label, (NameOf ([AudioRoles]::DefaultId($flow, 2)))
}
