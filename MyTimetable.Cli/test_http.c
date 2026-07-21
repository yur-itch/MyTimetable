#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>
#include <stdio.h>
#pragma comment(lib, "winhttp.lib")

int main() {
    HINTERNET hSession = WinHttpOpen(L"MyTest", WINHTTP_ACCESS_TYPE_NO_PROXY, NULL, NULL, 0);
    printf("hSession: %p\n", hSession);
    if (!hSession) { printf("WinHttpOpen error: %lu\n", GetLastError()); return 1; }
    HINTERNET hConnect = WinHttpConnect(hSession, L"localhost", 8080, 0);
    printf("hConnect: %p\n", hConnect);
    if (!hConnect) { printf("WinHttpConnect error: %lu\n", GetLastError()); WinHttpCloseHandle(hSession); return 1; }
    HINTERNET hRequest = WinHttpOpenRequest(hConnect, L"GET", L"/Cli", NULL, NULL, NULL, 0);
    printf("hRequest: %p\n", hRequest);
    if (!hRequest) { printf("error: %lu\n", GetLastError()); return 1; }
    BOOL ok = WinHttpSendRequest(hRequest, NULL, 0, NULL, 0, 0, 0);
    printf("SendRequest: %d (err=%lu)\n", ok, GetLastError());
    ok = WinHttpReceiveResponse(hRequest, NULL);
    printf("ReceiveResponse: %d (err=%lu)\n", ok, GetLastError());
    DWORD status = 0, sz = sizeof(status);
    WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER, NULL, &status, &sz, NULL);
    printf("HTTP status: %lu\n", status);
    WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession);
    return 0;
}
