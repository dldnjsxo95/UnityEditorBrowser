using System;
using System.Runtime.InteropServices;

namespace EditorBrowser.Native
{
    /// <summary>
    /// Windows P/Invoke definitions used by <see cref="ExternalBrowserHost"/>.
    /// Editor-only and Windows-only at the call sites.
    /// </summary>
    internal static class Win32
    {
        // ----- GetWindowLong / SetWindowLong indices -----
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int GWLP_HWNDPARENT = -8;

        // ----- Window styles (WS_*) -----
        public const uint WS_OVERLAPPED   = 0x00000000;
        public const uint WS_POPUP        = 0x80000000;
        public const uint WS_CHILD        = 0x40000000;
        public const uint WS_VISIBLE      = 0x10000000;
        public const uint WS_CLIPSIBLINGS = 0x04000000;
        public const uint WS_CLIPCHILDREN = 0x02000000;
        public const uint WS_CAPTION      = 0x00C00000;
        public const uint WS_BORDER       = 0x00800000;
        public const uint WS_DLGFRAME     = 0x00400000;
        public const uint WS_SYSMENU      = 0x00080000;
        public const uint WS_THICKFRAME   = 0x00040000;
        public const uint WS_MINIMIZEBOX  = 0x00020000;
        public const uint WS_MAXIMIZEBOX  = 0x00010000;

        // ----- Extended styles (WS_EX_*) -----
        public const uint WS_EX_TOOLWINDOW = 0x00000080;
        public const uint WS_EX_APPWINDOW  = 0x00040000;
        public const uint WS_EX_NOACTIVATE = 0x08000000;

        // ----- SetWindowPos flags -----
        public const uint SWP_NOSIZE         = 0x0001;
        public const uint SWP_NOMOVE         = 0x0002;
        public const uint SWP_NOZORDER       = 0x0004;
        public const uint SWP_NOACTIVATE     = 0x0010;
        public const uint SWP_FRAMECHANGED   = 0x0020;
        public const uint SWP_SHOWWINDOW     = 0x0040;
        public const uint SWP_HIDEWINDOW     = 0x0080;
        public const uint SWP_NOOWNERZORDER  = 0x0200;
        public const uint SWP_ASYNCWINDOWPOS = 0x4000;

        // ----- ShowWindow commands -----
        public const int SW_HIDE           = 0;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int SW_SHOW           = 5;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        // GetWindowLong is 32-bit; GetWindowLongPtr is the 64-bit-safe variant.
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP   = 0x0004;
        public const uint MOUSEEVENTF_MOVE     = 0x0001;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

        // ----- Z-order constants -----
        public static readonly IntPtr HWND_TOP       = new IntPtr(0);
        public static readonly IntPtr HWND_BOTTOM    = new IntPtr(1);
        public static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        // ----- RedrawWindow flags -----
        public const uint RDW_INVALIDATE  = 0x0001;
        public const uint RDW_ERASE       = 0x0004;
        public const uint RDW_FRAME       = 0x0400;
        public const uint RDW_ALLCHILDREN = 0x0080;
        public const uint RDW_UPDATENOW   = 0x0100;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        // ----- Window messages -----
        public const uint WM_SIZE  = 0x0005;
        public const uint WM_PAINT = 0x000F;
        public const uint SIZE_RESTORED = 0;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // ----- CreateProcess (kernel32) — used to spawn Chrome detached from
        //       Unity's Job Object so paint/lifetime limits don't apply.
        public const uint DETACHED_PROCESS         = 0x00000008;
        public const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;
        public const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
        public const uint CREATE_NO_WINDOW         = 0x08000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize;
            public int dwXCountChars, dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr h);

        // ----- Region (GDI) — used for Chrome PWA fake-titlebar cut-out -----
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// SetWindowRgn — set the window's visible region. Ownership of hRgn
        /// transfers to the OS on success.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

        // ----- WinEventHook (user32) — fires from a native callback even
        //       while Unity's main thread is blocked inside the OS drag
        //       modal loop, so we can keep Chrome synced during a drag.
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint EVENT_SYSTEM_MOVESIZESTART  = 0x000A;
        public const uint EVENT_SYSTEM_MOVESIZEEND    = 0x000B;
        public const uint EVENT_SYSTEM_FOREGROUND     = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT       = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS     = 0x0002;

        // idObject = 0 is OBJID_WINDOW (the window itself). Negative values
        // address inner controls (OBJID_CLIENT = -4, OBJID_VSCROLL = -5, etc.)
        // which are noise for tracking window position/size.
        public const int OBJID_WINDOW = 0;

        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }
}
