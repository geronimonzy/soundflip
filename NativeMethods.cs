using System.Runtime.InteropServices;
using System.Text;

static class NativeMethods
{
    internal const uint MOD_NOREPEAT = 0x4000;
    internal const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    internal const uint ATTACH_PARENT_PROCESS = unchecked((uint)-1);
    internal const int STD_OUTPUT_HANDLE = -11;
    internal static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr hIcon);
}
