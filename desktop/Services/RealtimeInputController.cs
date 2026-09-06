using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PubgAutoMapper.Services;

public sealed class RealtimeInputController : IDisposable
{
    private readonly ControlBridge _bridge;
    private readonly ConcurrentQueue<InputEvent> _queue = new();
    private readonly HashSet<string> _held = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private HwndSource? _source;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private HookProc? _keyboardProc;
    private HookProc? _mouseProc;
    private POINT _lastMouse;
    private bool _haveMouse;
    private bool _active;

    public double JoystickX { get; set; } = 0.193265;
    public double JoystickY { get; set; } = 0.674267;
    public double LookX { get; set; } = 0.63;
    public double LookY { get; set; } = 0.47;
    public double JoystickRadius { get; set; } = 0.10;
    public double MouseScale { get; set; } = 2.2;
    public int ScreenWidth { get; set; } = 2400;
    public int ScreenHeight { get; set; } = 1080;

    public bool IsActive => _active;

    public RealtimeInputController(ControlBridge bridge) => _bridge = bridge;

    public void Attach(IntPtr hwnd)
    {
        if (_source is not null) return;
        _source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Unable to attach input hooks.");
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
            throw new InvalidOperationException("Windows input hooks could not be installed.");
    }

    public void Start()
    {
        if (_active) return;
        _active = true;
        _cts = new CancellationTokenSource();
        _worker = Task.Run(() => WorkerAsync(_cts.Token));
    }

    public void Stop()
    {
        _active = false;
        lock (_gate) _held.Clear();
        _cts?.Cancel();
        try { _worker?.Wait(250); } catch { }
        _worker = null;
        _cts?.Dispose();
        _cts = null;
    }

    private async Task WorkerAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (!ct.IsCancellationRequested)
        {
            while (_queue.TryDequeue(out var ev))
                await ProcessAsync(ev, ct).ConfigureAwait(false);

            string[] held;
            lock (_gate) held = _held.ToArray();
            foreach (var key in held)
                await SendMovementAsync(key, ct).ConfigureAwait(false);

            await Task.Delay(18, ct).ConfigureAwait(false);
        }
    }

    private async Task ProcessAsync(InputEvent ev, CancellationToken ct)
    {
        switch (ev.Kind)
        {
            case InputKind.KeyDown:
                if (ev.Key is "W" or "A" or "S" or "D")
                {
                    lock (_gate) _held.Add(ev.Key);
                    await SendMovementAsync(ev.Key, ct).ConfigureAwait(false);
                }
                else if (ev.Key == "Space") await TapAsync(0.925329, 0.664495, ct);
                else if (ev.Key == "C") await TapAsync(0.838946, 0.928339, ct);
                else if (ev.Key == "Z") await TapAsync(0.901903, 0.908795, ct);
                else if (ev.Key == "R") await TapAsync(0.768668, 0.934853, ct);
                else if (ev.Key == "Tab") await TapAsync(0.0893119, 0.899023, ct);
                else if (ev.Key == "M") await TapAsync(0.920937, 0.117264, ct);
                else if (ev.Key == "1") await TapAsync(0.443631, 0.915309, ct);
                else if (ev.Key == "2") await TapAsync(0.547584, 0.912052, ct);
                else if (ev.Key == "Shift") await TapAsync(0.689605, 0.781759, ct);
                break;
            case InputKind.KeyUp:
                if (ev.Key is "W" or "A" or "S" or "D") lock (_gate) _held.Remove(ev.Key);
                break;
            case InputKind.MouseLeft:
                if (ev.Down) await TapAsync(0.847731, 0.762215, ct);
                break;
            case InputKind.MouseRight:
                if (ev.Down) await TapAsync(0.923865, 0.521173, ct);
                break;
            case InputKind.MouseMove:
                if (Math.Abs(ev.Dx) + Math.Abs(ev.Dy) > 0)
                {
                    var x2 = Math.Clamp(LookX + ev.Dx * MouseScale / ScreenWidth, 0.05, 0.95);
                    var y2 = Math.Clamp(LookY + ev.Dy * MouseScale / ScreenHeight, 0.05, 0.95);
                    await _bridge.SwipeAsync(LookX * ScreenWidth, LookY * ScreenHeight, x2 * ScreenWidth, y2 * ScreenHeight, 18, ct).ConfigureAwait(false);
                }
                break;
        }
    }

    private Task SendMovementAsync(string key, CancellationToken ct)
    {
        var dx = key == "A" ? -1 : key == "D" ? 1 : 0;
        var dy = key == "W" ? -1 : key == "S" ? 1 : 0;
        var x2 = Math.Clamp(JoystickX + dx * JoystickRadius, 0.01, 0.99);
        var y2 = Math.Clamp(JoystickY + dy * JoystickRadius, 0.01, 0.99);
        return _bridge.SwipeAsync(JoystickX * ScreenWidth, JoystickY * ScreenHeight, x2 * ScreenWidth, y2 * ScreenHeight, 35, ct);
    }

    private Task TapAsync(double nx, double ny, CancellationToken ct)
        => _bridge.TapAsync(nx * ScreenWidth, ny * ScreenHeight, ct);

    private IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _active)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var down = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            var up = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;
            if (down || up)
            {
                var key = data.vkCode switch
                {
                    0x57 => "W", 0x41 => "A", 0x53 => "S", 0x44 => "D", 0x20 => "Space",
                    0x43 => "C", 0x5A => "Z", 0x52 => "R", 0x09 => "Tab", 0x4D => "M",
                    0x31 => "1", 0x32 => "2", 0xA0 or 0xA1 => "Shift", _ => null
                };
                if (key is not null) _queue.Enqueue(new InputEvent(down ? InputKind.KeyDown : InputKind.KeyUp, key, false, 0, 0));
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _active)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if (wParam == (IntPtr)WM_MOUSEMOVE)
            {
                if (_haveMouse)
                    _queue.Enqueue(new InputEvent(InputKind.MouseMove, "", false, data.pt.X - _lastMouse.X, data.pt.Y - _lastMouse.Y));
                _lastMouse = data.pt; _haveMouse = true;
            }
            else if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
                _queue.Enqueue(new InputEvent(wParam == (IntPtr)WM_LBUTTONDOWN ? InputKind.MouseLeft : InputKind.MouseRight, "", true, 0, 0));
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _source = null;
    }

    private readonly record struct InputEvent(InputKind Kind, string Key, bool Down, int Dx, int Dy);
    private enum InputKind { KeyDown, KeyUp, MouseLeft, MouseRight, MouseMove }
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
    private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204;
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
