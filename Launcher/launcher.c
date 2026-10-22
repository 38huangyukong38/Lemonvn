// Copyright (c) 2026 LemonVN contributors. Licensed under Apache-2.0; see LICENSE.
// Native Windows entry point only. No CRT, .NET runtime, shell or PATH probing.
// Minimal Windows x64 ABI declarations; no SDK headers or runtime library.
typedef unsigned long DWORD;
typedef unsigned short WORD;
typedef unsigned short WCHAR;
typedef int BOOL;
typedef void *HANDLE;
#define WINAPI __stdcall
#define NULL ((void *)0)
#define FALSE 0
#define INVALID_FILE_ATTRIBUTES ((DWORD)-1)
#define FILE_ATTRIBUTE_DIRECTORY 0x10
#define MB_OK 0
#define MB_ICONERROR 0x10

typedef struct {
    DWORD cb;
    WCHAR *lpReserved, *lpDesktop, *lpTitle;
    DWORD dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
    WORD wShowWindow, cbReserved2;
    void *lpReserved2;
    HANDLE hStdInput, hStdOutput, hStdError;
} STARTUPINFOW;
typedef struct { HANDLE hProcess, hThread; DWORD dwProcessId, dwThreadId; } PROCESS_INFORMATION;
_Static_assert(sizeof(DWORD) == 4 && sizeof(WCHAR) == 2 && sizeof(void *) == 8, "Windows x64 ABI required");
_Static_assert(sizeof(STARTUPINFOW) == 104 && sizeof(PROCESS_INFORMATION) == 24, "Unexpected Windows ABI layout");
__declspec(dllimport) DWORD WINAPI GetModuleFileNameW(void *, WCHAR *, DWORD);
__declspec(dllimport) int WINAPI lstrcmpiW(const WCHAR *, const WCHAR *);
__declspec(dllimport) DWORD WINAPI GetFileAttributesW(const WCHAR *);
__declspec(dllimport) BOOL WINAPI CreateProcessW(const WCHAR *, WCHAR *, void *, void *, BOOL, DWORD, void *, const WCHAR *, STARTUPINFOW *, PROCESS_INFORMATION *);
__declspec(dllimport) BOOL WINAPI CloseHandle(HANDLE);
__declspec(dllimport) __declspec(noreturn) void WINAPI ExitProcess(DWORD);
__declspec(dllimport) int WINAPI MessageBoxW(void *, const WCHAR *, const WCHAR *, unsigned int);

#define CAPACITY 32768
static WCHAR module_path[CAPACITY];
static WCHAR application_path[CAPACITY];
static WCHAR working_directory[CAPACITY];
static WCHAR command_line[CAPACITY];
static STARTUPINFOW startup;
static PROCESS_INFORMATION child;

static void Fail(void)
{
    MessageBoxW(NULL,
        L"LemonVN 无法启动。请完整解压 ZIP，保留 app 文件夹及全部内容。\n"
        L"现有收藏不会被删除。\n\n"
        L"Unable to start LemonVN. Extract the complete ZIP with its app folder. "
        L"Your local collection is unchanged.",
        L"LemonVN", MB_OK | MB_ICONERROR);
    ExitProcess(1);
}

static DWORD Append(WCHAR *destination, DWORD at, const WCHAR *value)
{
    while (*value)
    {
        if (at >= CAPACITY - 1) Fail();
        destination[at++] = *value++;
    }
    destination[at] = 0;
    return at;
}

void WINAPI LauncherEntry(void)
{
    DWORD length = GetModuleFileNameW(NULL, module_path, CAPACITY);
    if (!length || length >= CAPACITY) Fail();
    DWORD filename = length;
    while (filename && module_path[filename - 1] != L'\\') filename--;
    if (!filename) Fail();
    BOOL uninstall = lstrcmpiW(module_path + filename, L"Uninstall.exe") == 0;
    module_path[filename] = 0;

    DWORD at = Append(application_path, 0, module_path);
    Append(application_path, at, L"app\\LemonVN.exe");
    at = Append(working_directory, 0, module_path);
    Append(working_directory, at, L"app");
    DWORD attributes = GetFileAttributesW(application_path);
    if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY)) Fail();

    at = Append(command_line, 0, L"\"");
    at = Append(command_line, at, application_path);
    at = Append(command_line, at, L"\"");
    if (uninstall) Append(command_line, at, L" --uninstall");
    startup.cb = sizeof(startup);
    if (!CreateProcessW(application_path, command_line, NULL, NULL, FALSE, 0,
        NULL, working_directory, &startup, &child)) Fail();
    CloseHandle(child.hThread);
    CloseHandle(child.hProcess);
    // Do not hold an EXE open while the confirmed uninstall helper removes it.
    ExitProcess(0);
}
