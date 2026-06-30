using System.Runtime.InteropServices;

// Hosts any number of global hotkeys on a message-only window, each mapped to its
// own action. Bindings are registered independently so one conflict does not
// disable the rest. WM_HOTKEY is delivered on the UI thread's message pump.
sealed class HotkeyManager : IDisposable
{
    readonly Win32.WndProc _wndProc;
    readonly IntPtr _hwnd;
    readonly string _className = "audswHotkeys_" + Guid.NewGuid().ToString("N");
    readonly Dictionary<int, Action> _actions = new();
    int _nextId = 1;

    public HotkeyManager()
    {
        _wndProc = WndProc;
        var cls = new Win32.WNDCLASSW
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = Win32.GetModuleHandleW(null),
            lpszClassName = _className,
        };
        Win32.RegisterClassW(ref cls);
        _hwnd = Win32.CreateWindowExW(0, _className, "audsw", 0, 0, 0, 0, 0, Win32.HWND_MESSAGE, IntPtr.Zero, cls.hInstance, IntPtr.Zero);
    }

    // Register one binding. Returns false (without throwing) if the spec is invalid
    // or the combo is already taken, so callers can warn and continue.
    public bool Register(string spec, Action onPressed)
    {
        if (string.IsNullOrWhiteSpace(spec)) return false;
        if (!HotKey.TryParse(spec, out uint mods, out uint vk)) return false;

        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_hwnd, id, mods | NativeMethods.MOD_NOREPEAT, vk))
            return false;

        _actions[id] = onPressed;
        return true;
    }

    public void Clear()
    {
        foreach (int id in _actions.Keys)
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _actions.Clear();
    }

    IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32.WM_HOTKEY && _actions.TryGetValue((int)wParam, out var action))
            action();
        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Clear();
        if (_hwnd != IntPtr.Zero) Win32.DestroyWindow(_hwnd);
    }
}
