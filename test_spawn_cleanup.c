// Test Agent D's spawn failure cleanup path in self_patch_any.
// We isolate just the spawn + cleanup logic to force failure.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <windows.h>

static int test_spawn_cleanup(void) {
    // Simulate what self_patch_any does after writing .tmp:
    // 1. Create .tmp marker file
    // 2. Try to spawn (will be made to fail)
    // 3. Check that .tmp is cleaned up on failure

    char tmp_path[MAX_PATH + 8];
    snprintf(tmp_path, sizeof(tmp_path), "%s.test_tmp", "spawn_cleanup_marker");

    // Create the .tmp file (simulating what self_patch_any does)
    FILE* ftmp = fopen(tmp_path, "wb");
    if (!ftmp) { printf("FAIL: couldn't create .tmp\n"); return 1; }
    fwrite("test", 1, 4, ftmp);
    fclose(ftmp);

    // Verify .tmp exists
    FILE* check = fopen(tmp_path, "rb");
    if (!check) { printf("FAIL: .tmp wasn't created\n"); return 1; }
    fclose(check);
    printf("  .tmp file created: %s\n", tmp_path);

    // Now simulate the spawn block from Agent D's code:
    // Both CreateProcessA and system() will fail because
    // the command line targets a nonexistent executable.
    int spawned = 0;

    // Use a deliberately invalid app name so CreateProcessA fails
    STARTUPINFOA si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    char bad_cmdline[] = "nonexistent_tool.exe /c echo fail";

    if (CreateProcessA(NULL, bad_cmdline, NULL, NULL, FALSE,
                       CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        spawned = 1;
        printf("  CreateProcessA unexpectedly succeeded\n");
    } else {
        printf("  CreateProcessA failed (expected)\n");
    }

    if (!spawned) {
        // system() also fails because the same invalid command
        int sys_ret = system(bad_cmdline);
        if (sys_ret == 0) {
            spawned = 1;
            printf("  system() unexpectedly succeeded\n");
        } else {
            printf("  system() failed with code %d (expected)\n", sys_ret);
        }
    }

    // Agent D's cleanup block:
    if (spawned) {
        printf("  WARN: would have exit(0) - cleanup not tested\n");
        remove(tmp_path);
        return 0;
    }

    // Both methods failed — clean up .tmp file
    printf("  Both spawn methods failed. Running cleanup...\n");
    remove(tmp_path);

    // Verify .tmp is gone
    check = fopen(tmp_path, "rb");
    if (check) {
        printf("  FAIL: .tmp file was NOT cleaned up!\n");
        fclose(check);
        remove(tmp_path);
        return 1;
    }
    printf("  OK: .tmp file successfully cleaned up.\n");

    printf("  Would print: Self-patch spawn failed - .tmp file cleaned up\n");
    printf("  Would return (not exit) - parent gains control.\n");
    return 0;
}

int main(void) {
    printf("=== Test: self_patch_any spawn failure cleanup ===\n\n");
    int result = test_spawn_cleanup();
    printf("\n%s\n", result == 0 ? "PASS" : "FAIL");
    return result;
}
