# Editor Browser

Unity 에디터의 일반 Tab 창(Inspector·Hierarchy와 동일하게 도킹 가능한 `EditorWindow`)에서 웹 브라우저를 사용할 수 있게 하는 도구.

## 설치

### 1. 임베디드 (개발)
본 리포지토리를 클론하여 그대로 사용. `Packages/com.lwt.editor-browser` 폴더가 자동 인식됨.

### 2. file 의존 (다른 프로젝트에서 사용)
대상 프로젝트의 `Packages/manifest.json`에 추가:

```json
{
  "dependencies": {
    "com.lwt.editor-browser": "file:../EditorBrowser/Packages/com.lwt.editor-browser"
  }
}
```

### 3. Git URL (권장)
대상 프로젝트의 `Packages/manifest.json`에 추가:

```json
{
  "dependencies": {
    "com.lwt.editor-browser": "https://github.com/<your-org-or-user>/com.lwt.editor-browser.git#v0.1.0"
  }
}
```

`#v0.1.0` 부분을 원하는 버전 태그로 변경 가능. 생략 시 항상 main 최신.

## 사용

- 메뉴: `Window > Editor Browser`
- 단축키: **Shift + Alt + W**
- 기본 홈페이지: `https://www.google.com/`
- URL 입력란에 텍스트 입력 후 Enter:
  - 유효 URL → 직접 이동
  - 도메인 형태(`example.com`) → `https://` 자동 부착
  - 그 외 → Google 검색

## 제거

`Packages/com.lwt.editor-browser` 폴더 삭제(또는 `manifest.json` 의존성 제거). 다음 잔재 없이 깨끗하게 제거됨:
- 메뉴 항목·단축키
- EditorWindow 인스턴스
- EditorPrefs / SessionState 잔재 0 (애초에 사용 안 함)

Chrome 프로필 캐시는 `%LOCALAPPDATA%\EditorBrowser\BrowserProfile\` 에 남음. 완전 정리 필요시 수동 삭제.

## 요구사항

- Unity 2022.3 이상 (Unity 6 권장)
- Windows (현재 브라우저 임베드 단계에서는 Windows 전용; UI 쉘은 크로스 플랫폼)

## 라이선스

MIT (또는 별도 명시 시 그에 따름).
