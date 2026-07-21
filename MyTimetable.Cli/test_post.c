#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>
#include <stdio.h>
#include <string.h>
#pragma comment(lib, "winhttp.lib")

int main() {
    HINTERNET hS = WinHttpOpen(L"t", WINHTTP_ACCESS_TYPE_NO_PROXY, NULL, NULL, 0);
    HINTERNET hC = WinHttpConnect(hS, L"localhost", 8080, 0);
    HINTERNET hR = WinHttpOpenRequest(hC, L"POST", L"/Cli/login", NULL, NULL, NULL, 0);
    const char* body = "{\"username\":\"admin\",\"password\":\"123456Qq!\"}";
    DWORD blen = strlen(body);
    WCHAR hdrs[] = L"Content-Type: application/json\r\nAccept: application/json\r\n";
    WinHttpSendRequest(hR, hdrs, -1, (LPVOID)body, blen, blen, 0);
    WinHttpReceiveResponse(hR, NULL);
    DWORD st = 0, sz = sizeof(st);
    WinHttpQueryHeaders(hR, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER, NULL, &st, &sz, NULL);
    printf("HTTP %lu\n", st);
    char buf[256] = {0}; DWORD rd = 0;
    WinHttpReadData(hR, buf, 255, &rd);
    printf("body(%lu): '%s'\n", rd, buf);
    WinHttpCloseHandle(hR); WinHttpCloseHandle(hC); WinHttpCloseHandle(hS);
    return 0;
}
