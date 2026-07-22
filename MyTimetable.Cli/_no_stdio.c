#include <windows.h>
void mainCRTStartup(void) {
    HANDLE h = GetStdHandle(STD_OUTPUT_HANDLE);
    const char* msg = "hello\n";
    DWORD w;
    WriteFile(h, msg, 6, &w, NULL);
    ExitProcess(0);
}
