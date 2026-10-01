#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <commctrl.h>
#include <winhttp.h>
#include <shlobj.h>
#include <shellapi.h>
#include <string>
#include <vector>
#include <thread>
#include <stdio.h>
#include <wchar.h>

#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "winhttp.lib")
#pragma comment(linker,"\\\"/manifestdependency:type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\\\"")

const wchar_t* PACKAGE_URL = L"https://github.com/MediaForge2446/Down--Track/releases/latest/download/DownTrack-FullPackage.zip";

HWND g_hWnd = NULL;
HWND g_hProgress = NULL;
HWND g_hStatus = NULL;
HWND g_hBtnAction = NULL;
HWND g_hBtnLang = NULL;
HFONT g_hFontTitle = NULL;
HFONT g_hFontBody = NULL;

bool g_isHebrew = true;
bool g_isInstalledAlready = false;

std::wstring GetInstallDir() {
    wchar_t path[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_LOCAL_APPDATA, NULL, 0, path))) {
        return std::wstring(path) + L"\\DownTrack";
    }
    return L"";
}

bool CheckIfInstalled() {
    std::wstring exePath = GetInstallDir() + L"\\DownTrack.exe";
    DWORD attr = GetFileAttributesW(exePath.c_str());
    return (attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY));
}

void CreateShortcut(const std::wstring& targetPath, const std::wstring& shortcutPath, const std::wstring& iconPath) {
    CoInitialize(NULL);
    IShellLinkW* psl = NULL;
    if (SUCCEEDED(CoCreateInstance(CLSID_ShellLink, NULL, CLSCTX_INPROC_SERVER, IID_IShellLinkW, (LPVOID*)&psl))) {
        psl->SetPath(targetPath.c_str());
        psl->SetWorkingDirectory(GetInstallDir().c_str());
        psl->SetIconLocation(iconPath.c_str(), 0);

        IPersistFile* ppf = NULL;
        if (SUCCEEDED(psl->QueryInterface(IID_IPersistFile, (LPVOID*)&ppf))) {
            ppf->Save(shortcutPath.c_str(), TRUE);
            ppf->Release();
        }
        psl->Release();
    }
    CoUninitialize();
}

bool DownloadPackage(const std::wstring& targetZip, HWND hProgress, HWND hStatus) {
    URL_COMPONENTS urlComp = { sizeof(urlComp) };
    wchar_t host[256] = {0};
    wchar_t urlPath[1024] = {0};
    urlComp.lpszHostName = host;
    urlComp.dwHostNameLength = 256;
    urlComp.lpszUrlPath = urlPath;
    urlComp.dwUrlPathLength = 1024;

    if (!WinHttpCrackUrl(PACKAGE_URL, 0, 0, &urlComp)) return false;

    HINTERNET hSession = WinHttpOpen(L"DownTrack-WebSetup/1.0", WINHTTP_ACCESS_TYPE_DEFAULT_PROXY, NULL, NULL, 0);
    if (!hSession) return false;

    HINTERNET hConnect = WinHttpConnect(hSession, host, INTERNET_DEFAULT_HTTPS_PORT, 0);
    if (!hConnect) { WinHttpCloseHandle(hSession); return false; }

    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"GET", urlPath, NULL, NULL, NULL, WINHTTP_FLAG_SECURE);
    if (!hRequest) { WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession); return false; }

    DWORD redirectOption = WINHTTP_OPTION_REDIRECT_POLICY_ALWAYS;
    WinHttpSetOption(hRequest, WINHTTP_OPTION_REDIRECT_POLICY, &redirectOption, sizeof(redirectOption));

    if (!WinHttpSendRequest(hRequest, NULL, 0, NULL, 0, 0, 0) || !WinHttpReceiveResponse(hRequest, NULL)) {
        WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession);
        return false;
    }

    DWORD contentLength = 0;
    DWORD sizeLength = sizeof(contentLength);
    WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CONTENT_LENGTH | WINHTTP_QUERY_FLAG_NUMBER, NULL, &contentLength, &sizeLength, NULL);

    HANDLE hFile = CreateFileW(targetZip.c_str(), GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE) {
        WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession);
        return false;
    }

    std::vector<BYTE> buffer(64 * 1024);
    DWORD bytesRead = 0;
    DWORD totalDownloaded = 0;

    while (WinHttpReadData(hRequest, buffer.data(), (DWORD)buffer.size(), &bytesRead) && bytesRead > 0) {
        DWORD bytesWritten = 0;
        WriteFile(hFile, buffer.data(), bytesRead, &bytesWritten, NULL);
        totalDownloaded += bytesRead;

        if (contentLength > 0) {
            int percent = (int)(((double)totalDownloaded / contentLength) * 100);
            SendMessage(hProgress, PBM_SETPOS, percent, 0);

            wchar_t statusText[128];
            if (g_isHebrew) {
                swprintf_s(statusText, L"מוריד קבצים... %d%%", percent);
            } else {
                swprintf_s(statusText, L"Downloading DownTrack... %d%%", percent);
            }
            SetWindowTextW(hStatus, statusText);
        }
    }

    CloseHandle(hFile);
    WinHttpCloseHandle(hRequest);
    WinHttpCloseHandle(hConnect);
    WinHttpCloseHandle(hSession);
    return (totalDownloaded > 1000000);
}

void RunInstallation() {
    std::wstring installDir = GetInstallDir();
    CreateDirectoryW(installDir.c_str(), NULL);
    std::wstring zipPath = installDir + L"\\package.zip";

    SetWindowTextW(g_hStatus, g_isHebrew ? L"מתחבר לשרת ההורדות..." : L"Connecting to download server...");

    if (!DownloadPackage(zipPath, g_hProgress, g_hStatus)) {
        SetWindowTextW(g_hStatus, g_isHebrew ? L"שגיאה בהורדת הקבצים. בדוק חיבור לאינטרנט." : L"Download failed. Check your connection.");
        EnableWindow(g_hBtnAction, TRUE);
        SetWindowTextW(g_hBtnAction, g_isHebrew ? L"נסה שוב" : L"Retry");
        return;
    }

    SetWindowTextW(g_hStatus, g_isHebrew ? L"מתקין ומחלץ קבצים..." : L"Extracting files...");
    SendMessage(g_hProgress, PBM_SETPOS, 85, 0);

    std::wstring cmd = L"tar.exe -xf \"" + zipPath + L"\" -C \"" + installDir + L"\"";
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi = { 0 };
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;

    if (CreateProcessW(NULL, &cmd[0], NULL, NULL, FALSE, CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
        WaitForSingleObject(pi.hProcess, INFINITE);
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    }
    DeleteFileW(zipPath.c_str());

    SendMessage(g_hProgress, PBM_SETPOS, 95, 0);
    SetWindowTextW(g_hStatus, g_isHebrew ? L"יוצר קיצורי דרך..." : L"Creating shortcuts...");

    std::wstring exePath = installDir + L"\\DownTrack.exe";
    std::wstring iconPath = exePath;

    wchar_t desktopPath[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_DESKTOPDIRECTORY, NULL, 0, desktopPath))) {
        CreateShortcut(exePath, std::wstring(desktopPath) + L"\\DownTrack.lnk", iconPath);
    }

    wchar_t programsPath[MAX_PATH];
    if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_PROGRAMS, NULL, 0, programsPath))) {
        CreateShortcut(exePath, std::wstring(programsPath) + L"\\DownTrack.lnk", iconPath);
    }

    SendMessage(g_hProgress, PBM_SETPOS, 100, 0);
    SetWindowTextW(g_hStatus, g_isHebrew ? L"ההתקנה הושלמה בהצלחה!" : L"Installation complete!");

    EnableWindow(g_hBtnAction, TRUE);
    SetWindowTextW(g_hBtnAction, g_isHebrew ? L"הפעל את DownTrack" : L"Launch DownTrack");
}

void LaunchApp() {
    std::wstring exePath = GetInstallDir() + L"\\DownTrack.exe";
    ShellExecuteW(NULL, L"open", exePath.c_str(), NULL, GetInstallDir().c_str(), SW_SHOWNORMAL);
    PostQuitMessage(0);
}

void UpdateTexts() {
    SetWindowTextW(g_hBtnLang, g_isHebrew ? L"English" : L"עברית");
    if (g_isInstalledAlready) {
        SetWindowTextW(g_hStatus, g_isHebrew ? L"DownTrack כבר מותקן ומעודכן במחשב זה." : L"DownTrack is already installed and up to date.");
        SetWindowTextW(g_hBtnAction, g_isHebrew ? L"הפעל את DownTrack" : L"Launch DownTrack");
    } else {
        SetWindowTextW(g_hBtnAction, g_isHebrew ? L"התקן עכשיו" : L"Install Now");
    }
}

LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_CREATE: {
        g_hFontTitle = CreateFontW(24, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI Variable Display");
        g_hFontBody = CreateFontW(16, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");

        g_hBtnLang = CreateWindowW(L"BUTTON", L"English", WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON | WS_TABSTOP, 370, 15, 80, 26, hWnd, (HMENU)101, NULL, NULL);
        SendMessage(g_hBtnLang, WM_SETFONT, (WPARAM)g_hFontBody, TRUE);

        g_hStatus = CreateWindowW(L"STATIC", L"מכין את DownTrack...", WS_CHILD | WS_VISIBLE | SS_CENTER, 30, 160, 420, 30, hWnd, NULL, NULL, NULL);
        SendMessage(g_hStatus, WM_SETFONT, (WPARAM)g_hFontBody, TRUE);

        g_hProgress = CreateWindowW(PROGRESS_CLASSW, NULL, WS_CHILD | WS_VISIBLE | PBS_SMOOTH, 40, 200, 400, 8, hWnd, NULL, NULL, NULL);
        SendMessage(g_hProgress, PBM_SETRANGE, 0, MAKELPARAM(0, 100));

        g_hBtnAction = CreateWindowW(L"BUTTON", L"התקן", WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON, 140, 240, 200, 42, hWnd, (HMENU)102, NULL, NULL);
        SendMessage(g_hBtnAction, WM_SETFONT, (WPARAM)g_hFontBody, TRUE);

        g_isInstalledAlready = CheckIfInstalled();
        if (g_isInstalledAlready) {
            SendMessage(g_hProgress, PBM_SETPOS, 100, 0);
            UpdateTexts();
        } else {
            EnableWindow(g_hBtnAction, FALSE);
            std::thread(RunInstallation).detach();
        }
        return 0;
    }
    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);
        RECT rc;
        GetClientRect(hWnd, &rc);

        HBRUSH hBg = CreateSolidBrush(RGB(250, 250, 252));
        FillRect(hdc, &rc, hBg);
        DeleteObject(hBg);

        HBRUSH hPurple = CreateSolidBrush(RGB(124, 58, 237));
        SelectObject(hdc, hPurple);
        SelectObject(hdc, GetStockObject(NULL_PEN));
        RoundRect(hdc, 210, 40, 270, 100, 20, 20);
        DeleteObject(hPurple);

        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, RGB(255, 255, 255));
        SelectObject(hdc, g_hFontTitle);
        RECT rcLogo = { 210, 40, 270, 100 };
        DrawTextW(hdc, L"D", 1, &rcLogo, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

        SetTextColor(hdc, RGB(24, 24, 27));
        RECT rcTitle = { 0, 115, 480, 145 };
        DrawTextW(hdc, L"DownTrack", -1, &rcTitle, DT_CENTER | DT_SINGLELINE);

        EndPaint(hWnd, &ps);
        return 0;
    }
    case WM_COMMAND: {
        int id = LOWORD(wParam);
        if (id == 101) {
            g_isHebrew = !g_isHebrew;
            UpdateTexts();
        } else if (id == 102) {
            if (g_isInstalledAlready) {
                LaunchApp();
            } else {
                EnableWindow(g_hBtnAction, FALSE);
                std::thread(RunInstallation).detach();
            }
        }
        return 0;
    }
    case WM_DESTROY:
        if (g_hFontTitle) DeleteObject(g_hFontTitle);
        if (g_hFontBody) DeleteObject(g_hFontBody);
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hWnd, msg, wParam, lParam);
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow) {
    INITCOMMONCONTROLSEX icex = { sizeof(icex), ICC_PROGRESS_CLASS };
    InitCommonControlsEx(&icex);

    WNDCLASSEXW wc = { sizeof(wc) };
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = L"DownTrackWebSetup";
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hIcon = LoadIconW(hInstance, MAKEINTRESOURCEW(1));
    RegisterClassExW(&wc);

    int w = 480, h = 340;
    int x = (GetSystemMetrics(SM_CXSCREEN) - w) / 2;
    int y = (GetSystemMetrics(SM_CYSCREEN) - h) / 2;

    HWND hWnd = CreateWindowExW(WS_EX_APPWINDOW, wc.lpszClassName, L"DownTrack Installer",
        WS_POPUP | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_VISIBLE,
        x, y, w, h, NULL, NULL, hInstance, NULL);

    g_hWnd = hWnd;
    ShowWindow(hWnd, nCmdShow);
    UpdateWindow(hWnd);

    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    return (int)msg.wParam;
}