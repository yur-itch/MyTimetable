// MyTimetable.CLI — самопатчащийся single-binary auth-клиент.
// Сборка (из MyTimetable.Cli/ dir):
//   gcc main.c ../brotli_src/c/dec/*.c ../brotli_src/c/common/*.c \
//       -I ../brotli_src/c/include -lwinhttp -o mytimetable.exe -O2

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>
#include <shellapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include "json.h"
#include <brotli/decode.h>

#define DEFAULT_HOST L"localhost"
#define DEFAULT_PORT 8080
#define BUFSIZE      262144
#define MAX_PATH_A   512

// ── Self-patching storage ─────────────────────────────────────────
#define ANCHOR_SIZE      16
#define SESSION_SIZE     16
#define SESSION_DATA_LEN (ANCHOR_SIZE + SESSION_SIZE)

#define SESSION_ANCHOR      "SELF_PATCH_16_AN"
#define SESSION_PLACEHOLDER "SES_EMPTY_16_B__"                   // ровно 16

#define session_ptr(d)  ((d) + ANCHOR_SIZE)
#define is_placeholder(p) (memcmp((p), SESSION_PLACEHOLDER, SESSION_SIZE) == 0)

// Единый буфер 16+16 байт в .data секции
static char session_data[SESSION_DATA_LEN] =
    SESSION_ANCHOR SESSION_PLACEHOLDER;

// ── Plan persistent storage ───────────────────────────────────────
#define PLAN_ANCHOR "PLAN_ANCHOR_16__"
#define PLAN_DATA_SIZE  4096
#define PLAN_TOTAL_LEN  (ANCHOR_SIZE + PLAN_DATA_SIZE)
#define MAX_SUBJECTS   32
#define MAX_STRATEGIES 32
#define MAX_TITLE_LEN  64

#define plan_ptr(d)  ((d) + ANCHOR_SIZE)
#define is_plan_placeholder(p) ((p)[0] == 0)

static char plan_data[PLAN_TOTAL_LEN] = PLAN_ANCHOR;

// ── Поиск подстроки в бинарных данных ────────────────────────────
static void* mem_find(const void* haystack, size_t hlen,
                      const void* needle, size_t nlen) {
    if (nlen == 0) return (void*)haystack;
    const unsigned char* h = (const unsigned char*)haystack;
    const unsigned char* n = (const unsigned char*)needle;
    for (size_t i = 0; i + nlen <= hlen; i++)
        if (memcmp(h + i, n, nlen) == 0) return (void*)(h + i);
    return NULL;
}

// ── Свой путь ─────────────────────────────────────────────────────
static const char* own_path(void) {
    static char path[MAX_PATH_A] = {0};
    if (path[0]) return path;
    WCHAR wpath[MAX_PATH_A];
    GetModuleFileNameW(NULL, wpath, MAX_PATH_A);
    wcstombs(path, wpath, MAX_PATH_A);
    return path;
}

// ── Self-patch ────────────────────────────────────────────────────
static void self_patch_any(const char* anchor_str, const char* data, int data_size, int will_restart, const char* restart_args) {
    FILE* f = fopen(own_path(), "rb");
    if (!f) { fprintf(stderr, "Cannot read self\n"); return; }
    fseek(f, 0, SEEK_END);
    long fsize = ftell(f);
    fseek(f, 0, SEEK_SET);
    char* binary = malloc(fsize);
    if (!binary) { fclose(f); return; }
    fread(binary, 1, fsize, f);
    fclose(f);

    char* anchor = (char*)mem_find(binary, fsize, anchor_str, ANCHOR_SIZE);
    if (!anchor) {
        fprintf(stderr, "Anchor not found - cannot self-patch\n");
        free(binary);
        return;
    }

    long payload_offset = (long)(anchor - binary) + ANCHOR_SIZE;

    // Escape path for PowerShell single quotes (double any ' inside)
    char ps_path[MAX_PATH_A * 2] = {0};
    {
        const char* s = own_path();
        char* d = ps_path;
        while (*s) {
            if (*s == '\'') { *d++ = '\''; *d++ = '\''; }
            *d++ = *s++;
        }
    }

    printf(will_restart ? "Saved. Restarting...\n" : "Cleared.\n");
    fflush(stdout);

    // Escape restart_args for PowerShell single quotes
    char restart_esc[256] = "";
    if (restart_args) {
        const char* s = restart_args;
        char* d = restart_esc;
        while (*s && d - restart_esc < 250) {
            if (*s == '\'') { *d++ = '\''; *d++ = '\''; }
            *d++ = *s++;
        }
    }

    // Base64-encode the payload
    char b64[(PLAN_DATA_SIZE + 2) / 3 * 4 + 1];
    static const char b64_table[] =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    int b64_pos = 0;
    for (int i = 0; i < data_size; i += 3) {
        int b = (unsigned char)data[i] << 16;
        if (i + 1 < data_size) b |= (unsigned char)data[i+1] << 8;
        if (i + 2 < data_size) b |= (unsigned char)data[i+2];
        b64[b64_pos++] = b64_table[(b >> 18) & 0x3F];
        b64[b64_pos++] = b64_table[(b >> 12) & 0x3F];
        b64[b64_pos++] = (i + 1 < data_size) ? b64_table[(b >> 6) & 0x3F] : '=';
        b64[b64_pos++] = (i + 2 < data_size) ? b64_table[b & 0x3F] : '=';
    }
    b64[b64_pos] = 0;

    char cmdline[8192];
    snprintf(cmdline, sizeof(cmdline),
        "powershell -NoProfile -Command \"&{"
        "sleep 1; "
        "$f=[IO.File]::Open('%s',[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None); "
        "$f.Seek(%ld,0); "
        "$b=[Convert]::FromBase64String('%s'); "
        "$f.Write($b,0,$b.Length); "
        "$f.Close(); "
        "if(%d){Start-Process '%s' -ArgumentList '%s'}}\"",
        ps_path, payload_offset, b64, will_restart, ps_path, restart_esc);

    free(binary);

    STARTUPINFOA si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    int spawned = 0;
    if (CreateProcessA(NULL, cmdline, NULL, NULL, FALSE,
                       CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        spawned = 1;
    } else if (system(cmdline) == 0) {
        spawned = 1;
    }
    if (spawned) {
        exit(0);
    }
    fprintf(stderr, "Self-patch spawn failed\n");
    return;
}

static void self_patch(const char* new_session, int will_restart, const char* restart_args) {
    printf(will_restart ? "Token saved. Restarting...\n" : "Token cleared.\n");
    self_patch_any(SESSION_ANCHOR, new_session, SESSION_SIZE, will_restart, restart_args);
}

// ── Session helper ────────────────────────────────────────────────
static void session_hex(char* out, size_t out_sz) {
    const unsigned char* p = (const unsigned char*)session_ptr(session_data);
    for (int i = 0; i < SESSION_SIZE && i*2+2 < (int)out_sz; i++)
        sprintf(out + i*2, "%02x", p[i]);
    out[out_sz - 1] = '\0';
}

static int hex_decode(const char* hex, unsigned char* out, int out_sz) {
    int len = 0;
    while (*hex && *(hex+1) && len < out_sz) {
        char hi = *hex++;
        char lo = *hex++;
        int h = (hi >= 'a') ? (hi - 'a' + 10) : (hi >= 'A') ? (hi - 'A' + 10) : (hi - '0');
        int l = (lo >= 'a') ? (lo - 'a' + 10) : (lo >= 'A') ? (lo - 'A' + 10) : (lo - '0');
        if (h < 0 || h > 15 || l < 0 || l > 15) return -1;
        out[len++] = (unsigned char)((h << 4) | l);
    }
    return *hex && *(hex+1) ? -1 : len;
}

// ── Plan save/load ────────────────────────────────────────────────
static int plan_serialize(char* buf, int bufsz,
                           char titles[][MAX_TITLE_LEN], int* counts, int n,
                           char strats[][32], int s) {
    int pos = 0;
    for (int i = 0; i < n && pos < bufsz - 2; i++) {
        pos += snprintf(buf + pos, bufsz - pos, "subjects:%s=%d\n", titles[i], counts[i]);
    }
    for (int i = 0; i < s && pos < bufsz - 2; i++) {
        pos += snprintf(buf + pos, bufsz - pos, "strategies:%s\n", strats[i]);
    }
    if (pos < bufsz) buf[pos] = 0;
    return pos;
}

static int plan_deserialize(const char* data,
                             char titles[][MAX_TITLE_LEN], int* counts, int* n,
                             char strats[][32], int* s) {
    *n = 0; *s = 0;
    if (!data || !*data) return 0;
    if (memcmp(data, "subjects:", 9) != 0 && memcmp(data, "strategies:", 11) != 0) return 0;

    char copy[PLAN_DATA_SIZE];
    strncpy(copy, data, PLAN_DATA_SIZE - 1);
    copy[PLAN_DATA_SIZE - 1] = 0;

    char* line = strtok(copy, "\n");
    while (line) {
        if (strncmp(line, "subjects:", 9) == 0) {
            const char* kv = line + 9;
            const char* eq = strchr(kv, '=');
            if (eq && *n < MAX_SUBJECTS) {
                size_t tl = eq - kv;
                if (tl >= MAX_TITLE_LEN) tl = MAX_TITLE_LEN - 1;
                memcpy(titles[*n], kv, tl);
                titles[*n][tl] = 0;
                counts[*n] = atoi(eq + 1);
                if (counts[*n] < 1) counts[*n] = 1;
                (*n)++;
            }
        } else if (strncmp(line, "strategies:", 11) == 0) {
            if (*s < MAX_STRATEGIES) {
                strncpy(strats[*s], line + 11, 31);
                strats[*s][31] = 0;
                (*s)++;
            }
        }
        line = strtok(NULL, "\n");
    }
    return 1;
}

static void plan_patch_save(char titles[][MAX_TITLE_LEN], int* counts, int n,
                             char strats[][32], int s) {
    char buf[PLAN_DATA_SIZE];
    int len = plan_serialize(buf, sizeof(buf), titles, counts, n, strats, s);
    if (len == 0 || (len == 1 && buf[0] == '\n')) {
        memset(buf, 0, PLAN_DATA_SIZE);
    } else {
        for (int i = len; i < PLAN_DATA_SIZE; i++) buf[i] = 0;
    }
    printf("Saving plan to binary...\n");
    fflush(stdout);
    self_patch_any(PLAN_ANCHOR, buf, PLAN_DATA_SIZE, 1, "plan");
}

// ── HTTP (WinHTTP, gzip auto-decompression) ───────────────────────
typedef struct { WCHAR host[256]; int port; } Client;
static Client client = { .host = L"localhost", .port = DEFAULT_PORT };

static char* http_request(const WCHAR* method, const WCHAR* path,
                          const char* body_utf8, int* status_out,
                          char content_encoding_out[32]) {
    HINTERNET hSession = WinHttpOpen(L"MyTimetable.CLI/1.0",
                                     WINHTTP_ACCESS_TYPE_NO_PROXY, NULL, NULL, 0);
    if (!hSession) return NULL;
    // Enable gzip/deflate auto-decompression
    DWORD decompress_flags = 0x03;  // GZIP | DEFLATE
    WinHttpSetOption(hSession, 118 /* WINHTTP_OPTION_DECOMPRESSION */, &decompress_flags, sizeof(decompress_flags));
    HINTERNET hConnect = WinHttpConnect(hSession, client.host, (INTERNET_PORT)client.port, 0);
    if (!hConnect) { WinHttpCloseHandle(hSession); return NULL; }
    HINTERNET hRequest = WinHttpOpenRequest(hConnect, method, path, NULL, NULL, NULL, 0);
    if (!hRequest) { WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession); return NULL; }

    WCHAR headers[512] = L"Content-Type: application/json\r\nAccept: application/json\r\n";
    if (!is_placeholder(session_ptr(session_data))) {
        char hex[64] = {0};
        session_hex(hex, 64);
        WCHAR wsid[64];
        if (mbstowcs(wsid, hex, 64) != (size_t)-1) {
            WCHAR auth[256];
            swprintf(auth, 256, L"X-Session-Id: %s\r\n", wsid);
            wcscat(headers, auth);
        }
    }

    DWORD body_len = body_utf8 ? (DWORD)strlen(body_utf8) : 0;
    BOOL ok = WinHttpSendRequest(hRequest, headers, -1,
                                 (LPVOID)(body_utf8 ? body_utf8 : ""),
                                 body_len, body_len, 0);
    if (!ok || !WinHttpReceiveResponse(hRequest, NULL))
        { WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession); return NULL; }

    DWORD status = 0, status_len = sizeof(status);
    WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                        NULL, &status, &status_len, NULL);
    if (status_out) *status_out = (int)status;

    if (content_encoding_out) content_encoding_out[0] = '\0';
    if (content_encoding_out) {
        WCHAR ce[64] = {0};
        DWORD ce_sz = sizeof(ce);
        if (WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_CONTENT_ENCODING,
                                WINHTTP_HEADER_NAME_BY_INDEX, ce, &ce_sz,
                                WINHTTP_NO_HEADER_INDEX)) {
            wcstombs(content_encoding_out, ce, 31);
            content_encoding_out[31] = '\0';
        }
    }

    static char buf[BUFSIZE];
    DWORD total = 0, read = 0;
    while (WinHttpReadData(hRequest, buf + total, BUFSIZE - total - 1, &read) && read > 0) {
        total += read; if (total >= BUFSIZE - 1) break;
    }
    buf[total] = '\0';
    WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession);

    // Brotli decompression: if server sent Content-Encoding: br,
    // WinHTTP doesn't decode it, so we do it manually.
    if (content_encoding_out && strcmp(content_encoding_out, "br") == 0 && total > 0) {
        static char decomp_buf[BUFSIZE];
        size_t decomp_sz = BUFSIZE - 1;
        BrotliDecoderResult r = BrotliDecoderDecompress(
            total, (const uint8_t*)buf, &decomp_sz, (uint8_t*)decomp_buf);
        if (r == BROTLI_DECODER_RESULT_SUCCESS) {
            decomp_buf[decomp_sz] = '\0';
            return decomp_buf;
        }
    }
    return buf;
}

// ── JSON helpers (sheredom/json.h wrappers) ─────────────────────
static struct json_value_s* json_get(struct json_object_s *obj, const char *key) {
    if (!obj) return NULL;
    for (struct json_object_element_s *e = obj->start; e; e = e->next)
        if (strcmp(e->name->string, key) == 0) return e->value;
    return NULL;
}
static const char* json_get_string(struct json_object_s *obj, const char *key) {
    struct json_value_s *v = json_get(obj, key);
    struct json_string_s *s = v ? json_value_as_string(v) : NULL;
    return s ? s->string : NULL;
}

// ── Display helpers ───────────────────────────────────────────────
static const char* short_name(const char* t) {
    if (!t) return "?";
    if (strstr(t,"Физическая культура")||strstr(t,"Элективные дисциплины")) return "Физра";
    if (strstr(t,"Алгебра и геометрия")) return "Алгем";
    if (strstr(t,"Математический анализ")) return "Матан";
    if (strstr(t,"Основы программирования")) return "Прога";
    if (strstr(t,"Дискретные структуры")) return "МКН2";
    if (strstr(t,"Иностранный язык")) return "Английский";
    if (strstr(t,"История России")) return "История";
    return t;
}

static int vis_len(const char* s) {
    int n = 0;
    while (*s) {
        if (*s == 0x1b) { while (*s && *s != 'm') s++; if (*s) s++; }
        else if ((*s & 0xc0) == 0x80) { s++; }
        else { n++; s++; }
    }
    return n;
}

// ── URL encoding ─────────────────────────────────────────────────
static int url_enc_char(char* d, unsigned char c) {
    if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~') {
        if (d) *d = c; return 1;
    }
    if (d) { sprintf(d, "%%%02X", c); }
    return 3;
}

static int url_encode(char* dst, const char* src) {
    int t = 0;
    for (const unsigned char* s = (const unsigned char*)src; *s; s++) {
        int n = url_enc_char(dst ? dst + t : NULL, *s);
        t += n;
    }
    if (dst) dst[t] = 0;
    return t;
}

// ── Проверка сессии ──────────────────────────────────────────────
static int needs_login(void) { return is_placeholder(session_ptr(session_data)); }

static int do_login_hex(const char* user, const char* pass) {
    char body[256]; snprintf(body, sizeof(body), "{\"username\":\"%s\",\"password\":\"%s\"}", user, pass);
    int st = 0; char* r = http_request(L"POST", L"/Cli/login", body, &st, NULL);
    if (!r || st != 200) { return 0; }
    // Response body is plain 32-char hex session ID
    if (strlen(r) != SESSION_SIZE * 2) return 0;
    unsigned char raw[SESSION_SIZE];
    if (hex_decode(r, raw, SESSION_SIZE) != SESSION_SIZE) return 0;
    memcpy(session_ptr(session_data), raw, SESSION_SIZE);
    return 1;
}

// ── Commands ──────────────────────────────────────────────────────
static int cmd_login(int argc, char** argv) {
    const char* user = "admin", *pass = "admin";
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--user")==0 && i+1<argc) user = argv[++i];
        if (strcmp(argv[i],"--password")==0 && i+1<argc) pass = argv[++i];
        if (strcmp(argv[i],"--host")==0 && i+1<argc) {
            if (mbstowcs(client.host, argv[++i], 256) == (size_t)-1)
                wcscpy(client.host, DEFAULT_HOST);
        }
        if (strcmp(argv[i],"--port")==0 && i+1<argc) {
            char* endptr = NULL;
            long val = strtol(argv[++i], &endptr, 10);
            if (endptr == argv[i] || *endptr != '\0' || val < 1 || val > 65535) {
                fprintf(stderr, "Invalid port: %s. Using default %d.\n", argv[i], DEFAULT_PORT);
                client.port = DEFAULT_PORT;
            } else {
                client.port = (int)val;
            }
        }
    }
    if (!do_login_hex(user, pass)) { printf("Login failed\n"); return 1; }
    printf("Logged in as '%s'\nPatching token into binary...\n", user);
    self_patch(session_ptr(session_data), 1, "schedule");
    return 0;
}

static int cmd_logout(void) {
    printf("Clearing baked-in token...\n");
    self_patch(SESSION_PLACEHOLDER, 0, NULL);
    return 0;
}

static int cmd_schedule(int argc, char** argv) {
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--host")==0 && i+1<argc) {
            if (mbstowcs(client.host, argv[++i], 256) == (size_t)-1)
                wcscpy(client.host, DEFAULT_HOST);
        }
        else if (strcmp(argv[i],"--port")==0 && i+1<argc) {
            char* endptr = NULL;
            long val = strtol(argv[++i], &endptr, 10);
            if (endptr == argv[i] || *endptr != '\0' || val < 1 || val > 65535) {
                fprintf(stderr, "Invalid port: %s. Using default %d.\n", argv[i], DEFAULT_PORT);
                client.port = DEFAULT_PORT;
            } else {
                client.port = (int)val;
            }
        }
    }
    if (needs_login()) { fprintf(stderr, "No token. Run 'login' first.\n"); return 1; }

    // GET /Cli — brotli-compressed, decompressed inside http_request
    int st = 0;
    char ce[32];
    char* raw = http_request(L"GET", L"/Cli", NULL, &st, ce);
    if (!raw) { fprintf(stderr, "Connection failed\n"); return 1; }
    if (st == 401) {
        fprintf(stderr, "Token expired. Clearing...\n");
        self_patch(SESSION_PLACEHOLDER, 0, NULL);
        return 1;
    }
    if (st != 200) { fprintf(stderr, "Error %d\n", st); return 1; }

    // Parse JSON (already decompressed by WinHTTP)
    struct json_value_s *root = json_parse(raw, strlen(raw));
    if (!root) { fprintf(stderr, "Bad JSON from server\n"); return 1; }

    struct json_object_s *root_obj = json_value_as_object(root);
    const char *data_text = json_get_string(root_obj, "data");
    struct json_value_s *st_v = json_get(root_obj, "scrollTarget");
    struct json_number_s *st_n = st_v ? json_value_as_number(st_v) : NULL;
    int scroll_target = st_n ? atoi(st_n->number) : 0;

    if (!data_text) { fprintf(stderr, "No schedule data\n"); free(root); return 1; }

    // Split rendered text into lines
    size_t data_len = strlen(data_text);
    char *data_copy = malloc(data_len + 1);
    if (!data_copy) { free(root); return 1; }
    strcpy(data_copy, data_text);

    char **lines = malloc(sizeof(char*) * 2048);
    if (!lines) { free(data_copy); free(root); return 1; }
    int line_count = 0;
    char *p = data_copy;
    while (*p && line_count < 2048) {
        while (*p == '\r' || *p == '\n') p++;
        if (!*p) break;
        lines[line_count++] = p;
        while (*p && *p != '\n' && *p != '\r') p++;
        if (*p) { *p = '\0'; p++; }
    }

    if (line_count < 4) { fprintf(stderr, "Bad schedule data (%d lines)\n", line_count); free(root); free(lines); free(data_copy); return 1; }

    int header_lines = 3;
    int body_lines = line_count - header_lines;

    // Console dimensions
    CONSOLE_SCREEN_BUFFER_INFO csbi;
    int console_height = 40;
    if (GetConsoleScreenBufferInfo(GetStdHandle(STD_OUTPUT_HANDLE), &csbi))
        console_height = csbi.srWindow.Bottom - csbi.srWindow.Top + 1;

    int scroll_region_top = header_lines + 1;  // 1-indexed
    int scroll_region_bot = console_height - 1;
    int avail = scroll_region_bot - scroll_region_top + 1;
    if (avail < 1) avail = 10;

    // Initial viewport: center scroll_target
    int start = scroll_target - avail / 2;
    if (start < 0) start = 0;
    int end = start + avail;
    if (end > body_lines) { end = body_lines; start = end - avail; if (start < 0) start = 0; }

    // Setup scroll region
    printf("\033[2J\033[0;0H");
    for (int i = 0; i < header_lines; i++)
        printf("%s\033[K\n", lines[i]);
    printf("\033[%d;%dr", scroll_region_top, scroll_region_bot);

    // Interactive loop
    char cmd_line[64];
    int running = 1;
    while (running) {
        // Render visible window — last row without \n to avoid edge scroll
        printf("\033[%d;0H", scroll_region_top);
        for (int i = start; i < end - 1; i++) {
            int line_idx = header_lines + i;
            if (i == scroll_target)
                printf("\033[7m%s\033[27m\033[K\n", lines[line_idx]);
            else
                printf("%s\033[K\n", lines[line_idx]);
        }
        // Last line: no \n to avoid scroll region bottom edge trigger
        {
            int i = end - 1;
            int line_idx = header_lines + i;
            if (i == scroll_target)
                printf("\033[7m%s\033[27m\033[K", lines[line_idx]);
            else
                printf("%s\033[K", lines[line_idx]);
        }

        // Prompt at bottom
        printf("\033[%d;0H\033[K[ u=up  d=down  q=quit ] ", console_height);
        fflush(stdout);

        if (!fgets(cmd_line, sizeof(cmd_line), stdin)) break;
        size_t llen = strlen(cmd_line);
        while (llen > 0 && (cmd_line[llen - 1] == '\n' || cmd_line[llen - 1] == '\r'))
            cmd_line[--llen] = 0;

        if (strcmp(cmd_line, "q") == 0 || strcmp(cmd_line, "quit") == 0) {
            running = 0;
        } else if (strcmp(cmd_line, "u") == 0 || strcmp(cmd_line, "up") == 0) {
            start -= (avail - 1);
            if (start < 0) start = 0;
        } else if (strcmp(cmd_line, "d") == 0 || strcmp(cmd_line, "down") == 0) {
            start += (avail - 1);
            if (start + avail > body_lines) start = body_lines - avail;
            if (start < 0) start = 0;
        }
        end = start + avail;
        if (end > body_lines) end = body_lines;
    }

    // Cleanup: reset scroll region, clear screen, move cursor home
    printf("\033[r\033[2J\033[0;0H");
    fflush(stdout);

    free(root); free(lines); free(data_copy);
    return 0;
}

// ── Interactive planner ───────────────────────────────────────────
static const char* ALL_STRATEGIES[] = {
    "gap", "emptyseed", "leading", "trailing",
    "roundrobin", "fairshare", "largest", "smallest",
    "random", "weighted",
    NULL
};

static int strategy_index(const char* name) {
    for (int i = 0; ALL_STRATEGIES[i]; i++)
        if (strcmp(ALL_STRATEGIES[i], name) == 0) return i;
    return -1;
}

static void plan_show_status(char titles[][MAX_TITLE_LEN], int* counts, int n,
                              char strats[][32], int s) {
    printf("  [");
    if (n == 0) printf("no subjects");
    for (int i = 0; i < n; i++) {
        if (i > 0) printf(", ");
        printf("%s x %d", titles[i], counts[i]);
    }
    printf(" | ");
    if (s == 0) printf("no strategies");
    for (int i = 0; i < s; i++) {
        if (i > 0) printf(" > ");
        printf("%s", strats[i]);
    }
    printf("]\n");
}

static int cmd_plan(int argc, char** argv) {
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i], "--host") == 0 && i + 1 < argc) {
            if (mbstowcs(client.host, argv[++i], 256) == (size_t)-1)
                wcscpy(client.host, DEFAULT_HOST);
        }
        else if (strcmp(argv[i], "--port") == 0 && i + 1 < argc) {
            char* endptr = NULL;
            long val = strtol(argv[++i], &endptr, 10);
            if (endptr == argv[i] || *endptr != '\0' || val < 1 || val > 65535) {
                fprintf(stderr, "Invalid port: %s. Using default %d.\n", argv[i], DEFAULT_PORT);
                client.port = DEFAULT_PORT;
            } else {
                client.port = (int)val;
            }
        }
    }

    // Check for subcommands
    {
        const char* sub = NULL;
        for (int i = 2; i < argc; i++) {
            if (strcmp(argv[i], "--host") == 0) { i++; continue; }
            if (strcmp(argv[i], "--port") == 0) { i++; continue; }
            sub = argv[i];
            break;
        }

        if (sub) {
            if (strcmp(sub, "status") == 0 || strcmp(sub, "show") == 0) {
                char t[MAX_SUBJECTS][MAX_TITLE_LEN];
                int  c[MAX_SUBJECTS];
                int  n = 0;
                char st[MAX_STRATEGIES][32];
                int  s = 0;
                const char* pp = plan_ptr(plan_data);
                if (pp[0] && (pp[0] == 's' || pp[0] == 'S'))
                    plan_deserialize(pp, t, c, &n, st, &s);
                printf("Saved plan state:\n");
                plan_show_status(t, c, n, st, s);
                return 0;
            }
            if (strcmp(sub, "clear") == 0 || strcmp(sub, "reset") == 0) {
                printf("Clearing saved plan state...\n");
                char dummy_titles[1][MAX_TITLE_LEN];
                int  dummy_counts[1];
                char dummy_strats[1][32];
                plan_patch_save(dummy_titles, dummy_counts, 0, dummy_strats, 0);
                // never reached — self_patch_any calls exit(0)
            }
        }
    }

    // State
    char titles[MAX_SUBJECTS][MAX_TITLE_LEN];
    int  counts[MAX_SUBJECTS];
    int  n = 0;
    char strats[MAX_STRATEGIES][32];
    int  s = 0;

    // Load saved state from binary
    {
        const char* pp = plan_ptr(plan_data);
        if (pp[0] && (pp[0] == 's' || pp[0] == 'S')) {
            if (plan_deserialize(pp, titles, counts, &n, strats, &s))
                printf("Loaded saved plan from binary.\n");
        }
    }

    printf("Planner interactive. Type 'help' for commands, 'quit' to exit.\n");
    plan_show_status(titles, counts, n, strats, s);

    char line[512];
    while (1) {
        printf("plan> "); fflush(stdout);
        if (!fgets(line, sizeof(line), stdin)) { printf("\n"); break; }
        size_t llen = strlen(line);
        while (llen > 0 && (line[llen - 1] == '\n' || line[llen - 1] == '\r')) line[--llen] = 0;

        char* cmd = line;
        while (*cmd == ' ') cmd++;
        if (!*cmd) continue;

        // Normalize tabs to spaces
        for (char* p = cmd; *p; p++) if (*p == '\t') *p = ' ';

        // Parse args
        char* args[16];
        int ac = 0;
        char* tok = strtok(cmd, " ");
        while (tok && ac < 16) { args[ac++] = tok; tok = strtok(NULL, " "); }

        if (strcmp(args[0], "quit") == 0 || strcmp(args[0], "q") == 0) {
            break;
        }
        else if (strcmp(args[0], "save") == 0) {
            plan_patch_save(titles, counts, n, strats, s);
            // never reached — self_patch_any calls exit(0)
        }
        else if (strcmp(args[0], "clear") == 0) {
            n = 0; s = 0;
            printf("  Cleared.\n");
            plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "help") == 0 || strcmp(args[0], "h") == 0) {
            printf("Commands:\n");
            printf("  add <title> <count>   - add subject to queue\n");
            printf("  rm <title>            - remove subject\n");
            printf("  subjects              - list subjects\n");
            printf("  push <strategy>       - add strategy on top\n");
            printf("  pop                   - remove top strategy\n");
            printf("  mv <from> <to>        - move strategy (1-indexed)\n");
            printf("  strategies            - list strategies\n");
            printf("  submit                - send plan to server\n");
            printf("  save                  - persist plan in binary (restarts)\n");
            printf("  clear                 - reset all subjects and strategies\n");
            printf("  help  / h             - this help\n");
            printf("  quit  / q             - exit\n");
            printf("Strategies: ");
            for (int i = 0; ALL_STRATEGIES[i]; i++) {
                if (i > 0) printf(", ");
                printf("%s", ALL_STRATEGIES[i]);
            }
            printf("\n");
        }
        else if (strcmp(args[0], "subjects") == 0) {
            if (n == 0) { printf("(empty)\n"); }
            for (int i = 0; i < n; i++)
                printf("  %s x %d\n", titles[i], counts[i]);
        }
        else if (strcmp(args[0], "add") == 0) {
            if (ac < 3) { printf("Usage: add <title> <count>\n"); continue; }
            int cnt = atoi(args[ac - 1]);
            if (cnt < 1) { printf("Count must be >= 1\n"); continue; }
            if (n >= MAX_SUBJECTS) { printf("Max %d subjects\n", MAX_SUBJECTS); continue; }
            char title_buf[MAX_TITLE_LEN] = {0};
            for (int ai = 1; ai < ac - 1; ai++) {
                if (ai > 1) strncat(title_buf, " ", MAX_TITLE_LEN - strlen(title_buf) - 1);
                strncat(title_buf, args[ai], MAX_TITLE_LEN - strlen(title_buf) - 1);
            }
            for (int i = 0; i < n; i++) {
                if (strcmp(titles[i], title_buf) == 0) {
                    counts[i] += cnt;
                    printf("  Updated: %s -> x %d\n", title_buf, counts[i]);
                    goto add_done;
                }
            }
            snprintf(titles[n], MAX_TITLE_LEN, "%s", title_buf);
            counts[n] = cnt;
            n++;
            printf("  Added: %s x %d\n", title_buf, cnt);
            add_done: plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "rm") == 0) {
            if (ac < 2) { printf("Usage: rm <title>\n"); continue; }
            char title_buf[MAX_TITLE_LEN] = {0};
            for (int ai = 1; ai < ac; ai++) {
                if (ai > 1) strncat(title_buf, " ", MAX_TITLE_LEN - strlen(title_buf) - 1);
                strncat(title_buf, args[ai], MAX_TITLE_LEN - strlen(title_buf) - 1);
            }
            int found = 0;
            for (int i = 0; i < n; i++) {
                if (strcmp(titles[i], title_buf) == 0) {
                    for (int j = i; j < n - 1; j++) {
                        strcpy(titles[j], titles[j + 1]);
                        counts[j] = counts[j + 1];
                    }
                    n--;
                    printf("  Removed: %s\n", title_buf);
                    found = 1;
                    plan_show_status(titles, counts, n, strats, s);
                    break;
                }
            }
            if (!found) printf("  Not found: %s\n", title_buf);
        }
        else if (strcmp(args[0], "strategies") == 0) {
            if (s == 0) { printf("(empty)\n"); }
            for (int i = 0; i < s; i++)
                printf("  %d. %s\n", i + 1, strats[i]);
        }
        else if (strcmp(args[0], "push") == 0) {
            if (ac < 2) { printf("Usage: push <strategy>\n"); continue; }
            const char* sn = args[1];
            if (strategy_index(sn) < 0) {
                printf("Unknown strategy: %s\n", sn);
                printf("Valid: ");
                for (int i = 0; ALL_STRATEGIES[i]; i++) {
                    if (i > 0) printf(", ");
                    printf("%s", ALL_STRATEGIES[i]);
                }
                printf("\n");
                continue;
            }
            if (s >= MAX_STRATEGIES) { printf("Max %d strategies\n", MAX_STRATEGIES); continue; }
            strcpy(strats[s], sn);
            s++;
            printf("  Pushed: %s (pos %d)\n", sn, s);
            plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "pop") == 0) {
            if (s == 0) { printf("(empty)\n"); continue; }
            s--;
            printf("  Popped: %s\n", strats[s]);
            plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "mv") == 0) {
            if (ac < 3) { printf("Usage: mv <from> <to>\n"); continue; }
            int from = atoi(args[1]) - 1;
            int to   = atoi(args[2]) - 1;
            if (from < 0 || from >= s || to < 0 || to >= s) {
                printf("Positions must be 1-%d\n", s);
                continue;
            }
            char tmp[32]; strcpy(tmp, strats[from]);
            if (from < to) {
                for (int i = from; i < to; i++) strcpy(strats[i], strats[i + 1]);
            } else {
                for (int i = from; i > to; i--) strcpy(strats[i], strats[i - 1]);
            }
            strcpy(strats[to], tmp);
            printf("  Moved %s from %d to %d\n", tmp, from + 1, to + 1);
            plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "submit") == 0) {
            if (n == 0) { printf("No subjects. Add some first.\n"); continue; }
            if (s == 0) { printf("No strategies. Push at least one.\n"); continue; }

            // Build query string: /Planner?titles[Encoded]=N&...&strategies[0]=S&...
            char qs[4096];
            int pos = 0;
            pos += snprintf(qs + pos, sizeof(qs) - pos, "/Planner?");
            for (int i = 0; i < n && pos < (int)sizeof(qs) - 512; i++) {
                if (i > 0) pos += snprintf(qs + pos, sizeof(qs) - pos, "&");
                pos += snprintf(qs + pos, sizeof(qs) - pos, "titles[");
                pos += url_encode(qs + pos, titles[i]);
                pos += snprintf(qs + pos, sizeof(qs) - pos, "]=%d", counts[i]);
            }
            for (int i = 0; i < s && pos < (int)sizeof(qs) - 512; i++) {
                pos += snprintf(qs + pos, sizeof(qs) - pos, "&strategies[%d]=", i);
                pos += url_encode(qs + pos, strats[i]);
            }

            if (pos >= (int)sizeof(qs) - 512) {
                printf("  Payload too large. Reduce subjects or strategies.\n");
                continue;
            }

            WCHAR wpath[4096];
            mbstowcs(wpath, qs, 4096);

            printf("  Sending plan...\n");
            int st = 0;
            char* resp = http_request(L"PATCH", wpath, NULL, &st, NULL);

            if (!resp) {
                printf("  Connection failed.\n");
                continue;
            }
            if (st == 401) {
                printf("  Token expired. Type 'login' to re-authenticate, then retry.\n");
                self_patch(SESSION_PLACEHOLDER, 0, NULL);
                return 1;
            }
            if (st != 200) {
                printf("  Error %d: %s\n", st, resp);
                continue;
            }

            struct json_value_s *jroot = json_parse(resp, strlen(resp));
            int failure = 0, success = 0;
            if (jroot) {
                struct json_object_s *jobj = json_value_as_object(jroot);
                struct json_value_s *succ_v = json_get(jobj, "success");
                struct json_value_s *fail_v = json_get(jobj, "failure");
                struct json_number_s *succ_n = succ_v ? json_value_as_number(succ_v) : NULL;
                struct json_number_s *fail_n = fail_v ? json_value_as_number(fail_v) : NULL;
                success = succ_n ? atoi(succ_n->number) : 0;
                failure = fail_n ? atoi(fail_n->number) : 0;
                free(jroot);
            }

            printf("  Result: placed %d lessons, failed %d\n", success, failure);
        }
        else {
            printf("Unknown: %s. Type 'help'.\n", args[0]);
        }
    }
    return 0;
}

static int cmd_proxy(void) {
    printf("Starting proxy...\n");
    system("start cmd /k \"dotnet run --project MyTimetable.Proxy --port 9155\"");
    wcsncpy(client.host, L"localhost", 256);
    client.port = 9155;
    printf("  Proxy on localhost:9155\n  Run 'login' to bake a token.\n");
    return 0;
}

static void help(void) {
    printf("MyTimetable CLI - self-patching single-binary auth\n\n"
           "Usage:\n"
           "  mytimetable login [--user <u>] [--password <p>] [--host <h>] [--port <p>]\n"
           "  mytimetable logout\n"
           "  mytimetable schedule                 full-year schedule, scrollable\n"
           "  mytimetable plan                      interactive planner\n"
           "  mytimetable plan status|show          show saved plan state\n"
           "  mytimetable plan clear|reset           clear saved plan state\n"
           "  mytimetable proxy\n"
           "  mytimetable help\n\n"
           "Token stored INSIDE the .exe file. No config files.\n"
           "Run 'logout' to erase it.\n");
}

// ── Entry ─────────────────────────────────────────────────────────
int main(int argc, char** argv) {
    HANDLE hOut = GetStdHandle(STD_OUTPUT_HANDLE);
    DWORD mode = 0; GetConsoleMode(hOut, &mode);
    SetConsoleMode(hOut, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);

    if (argc < 2) { help(); return 0; }
    const char* cmd = argv[1];
    if (strcmp(cmd,"login")==0) return cmd_login(argc,argv);
    if (strcmp(cmd,"logout")==0) return cmd_logout();
    if (strcmp(cmd,"schedule")==0) return cmd_schedule(argc,argv);
    if (strcmp(cmd,"plan")==0) return cmd_plan(argc,argv);
    if (strcmp(cmd,"proxy")==0) return cmd_proxy();
    if (strcmp(cmd,"help")==0) { help(); return 0; }
    fprintf(stderr,"Unknown: %s\n",cmd); help();
    return 1;
}
