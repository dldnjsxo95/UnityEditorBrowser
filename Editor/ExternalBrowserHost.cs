using System;
using System.Diagnostics;
using System.IO;
using EditorBrowser.Native;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EditorBrowser
{
    /// <summary>
    /// 외부 브라우저(Chrome/Edge) 프로세스를 별도로 띄우고 그 메인 윈도우를
    /// Unity 메인 HWND의 WS_CHILD 로 reparent. EditorWindow의 body 영역(스크린 픽셀)에
    /// 맞춰 매 프레임 위치/크기를 동기화한다.
    ///
    /// Caption(타이틀바·메뉴)은 두 단계로 제거:
    ///   1) chrome.exe --app=&lt;url&gt; 플래그로 브라우저 자체의 chrome(탭/주소창/메뉴) 제거
    ///   2) Win32 SetWindowLong 로 WS_CAPTION/WS_THICKFRAME 등 잔존 비트 strip + WS_CHILD 부여
    ///
    /// 본 구현은 Windows 전용. 다른 플랫폼에서는 Start()가 no-op 한다.
    /// </summary>
    internal sealed class ExternalBrowserHost : IDisposable
    {
        private const string LogPrefix = "[EditorBrowser]";

        private static readonly string UserDataDirRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EditorBrowser", "BrowserProfile");

        private Process _process;
        private IntPtr _browserHwnd;
        private IntPtr _unityHwnd;
        private bool _attached;
        private bool _visible;
        private bool _disposed;

        private int _lastX, _lastY, _lastW, _lastH;

        public bool IsAlive => _process != null && !_process.HasExited;
        public bool IsAttached => _attached && _browserHwnd != IntPtr.Zero && Win32.IsWindow(_browserHwnd);

        /// <summary>
        /// 새 URL로 네비게이트. 미실행 시 프로세스 시작, 실행 중이면 재시작.
        /// (V1: chrome --app= 모드는 in-place 네비게이트가 까다로워 restart 채택. V2에서 CDP로 개선 가능.)
        /// </summary>
        public void Navigate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            if (!IsRunningOnWindows())
            {
                Debug.LogWarning($"{LogPrefix} 외부 브라우저 임베드는 현재 Windows 전용입니다.");
                return;
            }

            if (IsAlive) DisposeProcess();
            Start(url);
        }

        /// <summary>
        /// 본 메서드는 매 프레임 호출하기에 안전. body의 절대 스크린 픽셀 RECT를 받아
        /// Unity 메인 HWND 클라이언트 좌표로 환산 후 SetWindowPos 동기화.
        /// width/height &lt;= 0 이면 hide.
        /// </summary>
        public void SyncBoundsAbsoluteScreen(int absX, int absY, int absW, int absH)
        {
            if (!IsAlive) return;
            if (!_attached && !TryAttach()) return;
            if (!Win32.IsWindow(_browserHwnd))
            {
                _attached = false;
                return;
            }

            if (absW <= 0 || absH <= 0)
            {
                Hide();
                return;
            }

            // 절대 스크린 좌표 → Unity 메인 HWND의 client 좌표
            var pt = new Win32.POINT { X = absX, Y = absY };
            if (!Win32.ScreenToClient(_unityHwnd, ref pt))
            {
                // 변환 실패는 보통 _unityHwnd가 무효해진 경우 — 재attach 트리거
                _attached = false;
                return;
            }

            // drift gate: 직전과 동일하면 SetWindowPos 호출 생략
            if (_visible && pt.X == _lastX && pt.Y == _lastY && absW == _lastW && absH == _lastH)
                return;

            _lastX = pt.X; _lastY = pt.Y; _lastW = absW; _lastH = absH;

            Win32.SetWindowPos(
                _browserHwnd, IntPtr.Zero,
                pt.X, pt.Y, absW, absH,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_ASYNCWINDOWPOS);

            if (!_visible)
            {
                Win32.ShowWindow(_browserHwnd, Win32.SW_SHOWNOACTIVATE);
                _visible = true;
            }
        }

        public void Hide()
        {
            if (!IsAlive || !_visible || _browserHwnd == IntPtr.Zero) return;
            Win32.ShowWindow(_browserHwnd, Win32.SW_HIDE);
            _visible = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DisposeProcess();
        }

        // ----- internal helpers -----

        private static bool IsRunningOnWindows()
        {
            return Application.platform == RuntimePlatform.WindowsEditor;
        }

        private void Start(string url)
        {
            var info = BrowserDetector.Detect();
            if (!info.IsAvailable)
            {
                Debug.LogError($"{LogPrefix} Chrome·Edge 둘 다 감지되지 않음. 둘 중 하나 설치 필요.");
                return;
            }

            try
            {
                Directory.CreateDirectory(UserDataDirRoot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} user-data-dir 생성 실패({ex.Message}) — 기본 프로필로 진행");
            }

            // --app=url : 탭/주소창/메뉴 없는 PWA 앱 모드 (브라우저 자체의 chrome 제거)
            // --user-data-dir : 호스트 사용자 일반 프로필과 격리
            // --no-first-run : 첫 실행 다이얼로그 억제
            // --no-default-browser-check : 기본 브라우저 변경 프롬프트 억제
            // --window-position/size : 일단 화면 어딘가에 띄움 (직후 SetWindowPos가 덮어씀)
            var args =
                $"--app={url} " +
                $"--user-data-dir=\"{UserDataDirRoot}\" " +
                "--no-first-run --no-default-browser-check --disable-popup-blocking " +
                "--window-position=0,0 --window-size=800,600";

            var psi = new ProcessStartInfo
            {
                FileName = info.ExecutablePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            try
            {
                _process = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} 브라우저 프로세스 시작 실패: {ex.Message}");
                _process = null;
                return;
            }

            _unityHwnd = Process.GetCurrentProcess().MainWindowHandle;
            _browserHwnd = IntPtr.Zero;
            _attached = false;
            _visible = false;
            _lastX = _lastY = _lastW = _lastH = int.MinValue;

            Debug.Log($"{LogPrefix} 브라우저 시작 kind={info.Kind} pid={_process?.Id} url={url}");
        }

        /// <summary>
        /// MainWindowHandle이 잡혔으면 WS_CHILD reparent + 스타일 strip 수행.
        /// 아직 안 잡혔으면 false 반환(다음 틱 재시도).
        /// </summary>
        private bool TryAttach()
        {
            if (_process == null || _process.HasExited) return false;

            try { _process.Refresh(); } catch { return false; }
            var h = _process.MainWindowHandle;
            if (h == IntPtr.Zero || !Win32.IsWindow(h)) return false;

            // 부모 결정 — Unity 메인 HWND. 이 핸들은 도메인 리로드 후에도 유지됨.
            if (_unityHwnd == IntPtr.Zero || !Win32.IsWindow(_unityHwnd))
                _unityHwnd = Process.GetCurrentProcess().MainWindowHandle;

            if (_unityHwnd == IntPtr.Zero)
            {
                Debug.LogWarning($"{LogPrefix} Unity 메인 HWND를 얻지 못함 — 다음 틱 재시도");
                return false;
            }

            // 1) 캡션·테두리 등 데코레이션 strip + WS_CHILD 부여
            var style = (uint)Win32.GetWindowLongPtr(h, Win32.GWL_STYLE).ToInt64();
            const uint Strip = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_THICKFRAME
                               | Win32.WS_SYSMENU | Win32.WS_MINIMIZEBOX | Win32.WS_MAXIMIZEBOX
                               | Win32.WS_BORDER | Win32.WS_DLGFRAME | Win32.WS_POPUP;
            style &= ~Strip;
            style |= Win32.WS_CHILD | Win32.WS_CLIPSIBLINGS | Win32.WS_CLIPCHILDREN;
            Win32.SetWindowLongPtr(h, Win32.GWL_STYLE, new IntPtr(unchecked((int)style)));

            // 2) 작업 표시줄에서 사라지도록 EX 스타일 조정 (TOOLWINDOW로 전환, APPWINDOW 제거)
            var ex = (uint)Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE).ToInt64();
            ex &= ~Win32.WS_EX_APPWINDOW;
            ex |= Win32.WS_EX_TOOLWINDOW;
            Win32.SetWindowLongPtr(h, Win32.GWL_EXSTYLE, new IntPtr(unchecked((int)ex)));

            // 3) reparent
            Win32.SetParent(h, _unityHwnd);

            // 4) 스타일 변경을 적용시키기 위한 SetWindowPos with SWP_FRAMECHANGED
            Win32.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER
                | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);

            _browserHwnd = h;
            _attached = true;

            Debug.Log($"{LogPrefix} HWND 부착 완료 hwnd=0x{h.ToInt64():X} parent=0x{_unityHwnd.ToInt64():X}");
            return true;
        }

        private void DisposeProcess()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch { /* 이미 종료된 경우 등 */ }

            try { _process?.Dispose(); } catch { }

            _process = null;
            _browserHwnd = IntPtr.Zero;
            _attached = false;
            _visible = false;
            _lastX = _lastY = _lastW = _lastH = int.MinValue;
        }
    }
}
