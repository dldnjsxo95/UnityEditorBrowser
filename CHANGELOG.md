# Changelog

본 패키지는 [Keep a Changelog](https://keepachangelog.com/) 규약과 [SemVer](https://semver.org/)를 따른다.

## [Unreleased]

### Added
- `BrowserWindow` EditorWindow — 툴바(뒤로/앞으로/새로고침/URL), 임베드 본문 영역, 상태바
- `UrlResolver` — Enter 입력을 URL/도메인/검색 쿼리로 분기하는 순수 함수
- `BrowserHistory` — 뒤로/앞으로 네비게이션 상태 관리
- 메뉴 항목 `Window > Editor Browser` + 단축키 Shift+Alt+W
- 기본 홈페이지 `https://www.google.com/`
- `BrowserDetector` — Chrome 우선, Edge 폴백 (알려진 설치 경로 기반 감지)
- `ExternalBrowserHost` — Chrome/Edge 별도 프로세스 spawn + WS_CHILD reparent로 Unity EditorWindow에 임베드
  - `--app=<url>` 플래그로 Chrome chrome(탭/주소창/메뉴) 제거
  - Win32 `SetWindowLong`로 WS_CAPTION·WS_THICKFRAME·WS_SYSMENU 등 잔존 데코레이션 strip
  - 매 에디터 틱 `body.worldBound` → 스크린 픽셀 → Unity 클라이언트 좌표 환산 → `SetWindowPos`
  - drift gate로 동일 RECT 시 호출 생략
  - `AssemblyReloadEvents.beforeAssemblyReload` + `EditorApplication.quitting` 안전망으로 도메인 리로드/종료 시 프로세스 정리
- `Native/Win32.cs` — user32 P/Invoke 정의 (SetParent, SetWindowLongPtr, SetWindowPos, ScreenToClient 등)

### Notes
- 브라우저 user-data-dir: `%LOCALAPPDATA%\EditorBrowser\BrowserProfile` (호스트 사용자 일반 프로필과 격리)
- Windows 전용 — 다른 플랫폼에서는 임베드가 비활성화되고 UI 쉘만 동작
- V1: URL 변경 시 process kill 후 재시작. V2에서 CDP(remote debugging) 기반 in-place navigate로 개선 예정.
