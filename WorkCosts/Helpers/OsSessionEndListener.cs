using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace WorkCosts.Helpers;

internal sealed class OsSessionEndListener : IDisposable
{
    private const int GwlpWndProc = -4;
    private const uint WmQueryEndSession = 0x0011;
    private const uint WmEndSession = 0x0016;

    private readonly WndProc _wndProc;
    private readonly IntPtr _hwnd;
    private readonly IntPtr _previousWndProc;
    private bool _disposed;

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public bool IsSessionEnding { get; private set; }

    public OsSessionEndListener(Window window)
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _wndProc = HandleMessage;
        _previousWndProc = SetWindowProcedure(_hwnd, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hwnd != IntPtr.Zero && _previousWndProc != IntPtr.Zero)
        {
            SetWindowProcedure(_hwnd, _previousWndProc);
        }
    }

    private IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg is WmQueryEndSession or WmEndSession)
        {
            IsSessionEnding = true;
        }

        return CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam);
    }

    private static IntPtr SetWindowProcedure(IntPtr hWnd, IntPtr newProc) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, GwlpWndProc, newProc)
            : new IntPtr(SetWindowLong32(hWnd, GwlpWndProc, newProc.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
