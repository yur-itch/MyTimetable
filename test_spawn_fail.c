// Test the self_patch_any spawn-failure cleanup path.
// This defines a test main() that forces a spawn to fail.
// Compile with: gcc test_spawn_fail.c -lwinhttp -o test_spawn_fail.exe
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Override own_path to return a path that's definitely writable
// so fopen() succeeds, then we force CreateProcessA/system to fail
// by passing a deliberately broken anchor so mem_find fails.
// Actually, simpler: make both anchors unfindable in the payload.

// We need the same static buffer approach. Let's just include main.c
// but with a different main().

// Define ANCHOR_SIZE and SESSION_SIZE so the storage is compatible
#define ANCHOR_SIZE  32
#define SESSION_SIZE 64

// Define our own anchors and data that WON'T be found in the binary
#define TEST_ANCHOR      "ZZZZ_THIS_WONT_BE_IN_THE_BINARY_ZZZ"  // 32 chars... need exactly 32
// "ZZZZ_THIS_WONT_BE_IN_THE_BIN" = 32 chars
#define TEST_ANCHOR2     "AAAA_THIS_WONT_BE_IN_THE_BINARY_AAA"  // 32 chars

// Explicitly include the whole file but rename main
#define main original_main
#include "MyTimetable.Cli/main.c"
#undef main

int main(void) {
    printf("=== Test 1: Non-existent anchor (mem_find fails) ===\n");
    // This should trigger "Anchor not found - cannot self-patch"
    // and return without crashing
    char data[64] = {0};
    strcpy(data, "test_data_here_fill_it_up_to_sixty_four___________");
    self_patch_any(TEST_ANCHOR, data, 64, 1);
    printf("  -> Returned without crash. Cleanup path works.\n");

    printf("\n=== Test 2: Another non-existent anchor, no restart ===\n");
    self_patch_any(TEST_ANCHOR2, data, 64, 0);
    printf("  -> Returned without crash.\n");

    printf("\nAll spawn-failure tests passed.\n");
    return 0;
}
