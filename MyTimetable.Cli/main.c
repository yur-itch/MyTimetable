// MyTimetable.CLI — самопатчащийся single-binary auth-клиент.
// Сборка (из MyTimetable.Cli/):
/*
 * gcc main.c -lwinhttp -lshell32 \
 *     -Os -s -flto -fno-ident -fno-asynchronous-unwind-tables -fno-unwind-tables \
 *     -o mytimetable.exe
 */

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>
#include <shellapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <stdint.h>
#include <errno.h>
#include <limits.h>

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
#define PLAN_DATA_SIZE  1024
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

static int parse_int_range(const char* text, int min_value, int max_value, int* out) {
    if (!text || !*text) return 0;
    errno = 0;
    char* end = NULL;
    long value = strtol(text, &end, 10);
    if (errno == ERANGE || end == text || *end != '\0' ||
        value < min_value || value > max_value)
        return 0;
    *out = (int)value;
    return 1;
}

static int mbstowcs_terminated(WCHAR* dst, size_t dst_count, const char* src) {
    if (!dst || dst_count == 0 || !src) return 0;
    size_t converted = mbstowcs(dst, src, dst_count);
    if (converted == (size_t)-1 || converted >= dst_count) {
        dst[0] = L'\0';
        return 0;
    }
    dst[converted] = L'\0';
    return 1;
}

static int wcstombs_terminated(char* dst, size_t dst_count, const WCHAR* src) {
    if (!dst || dst_count == 0 || !src) return 0;
    size_t converted = wcstombs(dst, src, dst_count);
    if (converted == (size_t)-1 || converted >= dst_count) {
        dst[0] = '\0';
        return 0;
    }
    dst[converted] = '\0';
    return 1;
}

static int validate_cli_args(int argc, char** argv, int start,
                             const char* const* options, int option_count,
                             int allow_one_positional) {
    int positional_count = 0;
    for (int i = start; i < argc; i++) {
        int known = 0;
        for (int j = 0; j < option_count; j++) {
            if (strcmp(argv[i], options[j]) == 0) {
                known = 1;
                if (i + 1 >= argc || argv[i + 1][0] == '-') {
                    fprintf(stderr, "Missing value for %s.\n", argv[i]);
                    return 0;
                }
                i++;
                break;
            }
        }
        if (known) continue;
        if (argv[i][0] == '-') {
            fprintf(stderr, "Unknown option: %s\n", argv[i]);
            return 0;
        }
        if (!allow_one_positional || positional_count++ > 0) {
            fprintf(stderr, "Unexpected argument: %s\n", argv[i]);
            return 0;
        }
    }
    return 1;
}

// ── Свой путь ─────────────────────────────────────────────────────
static const char* own_path(void) {
    static char path[MAX_PATH_A] = {0};
    if (path[0]) return path;
    WCHAR wpath[MAX_PATH_A];
    DWORD length = GetModuleFileNameW(NULL, wpath, MAX_PATH_A);
    if (length == 0 || length >= MAX_PATH_A ||
        !wcstombs_terminated(path, sizeof(path), wpath))
        path[0] = '\0';
    return path;
}

// ── Self-patch ────────────────────────────────────────────────────
static void self_patch_any(const char* anchor_data, const char* data, int data_size, int will_restart, const char* restart_args) {
    FILE* f = fopen(own_path(), "rb");
    if (!f) { fprintf(stderr, "Cannot read self\n"); return; }
    fseek(f, 0, SEEK_END);
    long fsize = ftell(f);
    fseek(f, 0, SEEK_SET);
    if (fsize <= 0) { fclose(f); return; }
    size_t binary_size = (size_t)fsize;
    char* binary = malloc(binary_size);
    if (!binary) { fclose(f); return; }
    if (fread(binary, 1, binary_size, f) != binary_size) {
        fclose(f);
        free(binary);
        return;
    }
    fclose(f);

    char* anchor = (char*)mem_find(binary, binary_size, anchor_data, ANCHOR_SIZE);
    if (!anchor) {
        fputs("Anchor not found - cannot self-patch\n", stderr);
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
        "if(%d){& '%s' '%s'}}\"",
        ps_path, payload_offset, b64, will_restart, ps_path, restart_esc);

    free(binary);

    STARTUPINFOA si = {0};
    si.cb = sizeof(si);
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
    fputs("Self-patch spawn failed\n", stderr);
    return;
}

static void self_patch(const char* new_session, int will_restart, const char* restart_args) {
    self_patch_any(session_data, new_session, SESSION_SIZE, will_restart, restart_args);
}

// ── Session helper ────────────────────────────────────────────────
static void session_hex(char* out, size_t out_sz) {
    static const char hex[] = "0123456789abcdef";
    const unsigned char* p = (const unsigned char*)session_ptr(session_data);
    size_t pos = 0;
    for (int i = 0; i < SESSION_SIZE && pos + 2 < out_sz; i++) {
        out[pos++] = hex[(p[i] >> 4) & 0x0F];
        out[pos++] = hex[p[i] & 0x0F];
    }
    out[pos] = '\0';
}

static int hex_digit_value(unsigned char c) {
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    return -1;
}

static int hex_decode(const char* hex, unsigned char* out, int out_sz) {
    int len = 0;
    while (*hex) {
        if (!hex[1] || len >= out_sz) return -1;
        int hi = hex_digit_value((unsigned char)hex[0]);
        int lo = hex_digit_value((unsigned char)hex[1]);
        if (hi < 0 || lo < 0) return -1;
        out[len++] = (unsigned char)((hi << 4) | lo);
        hex += 2;
    }
    return len;
}

// ── Plan save/load ────────────────────────────────────────────────
static int plan_serialize(char* buf, int bufsz,
                           char titles[][MAX_TITLE_LEN], int* counts, int n,
                           char strats[][32], int s) {
    int pos = 0;
    for (int i = 0; i < n; i++) {
        int written = snprintf(buf + pos, (size_t)(bufsz - pos),
                               "subjects:%s=%d\n", titles[i], counts[i]);
        if (written < 0 || written >= bufsz - pos) return -1;
        pos += written;
    }
    for (int i = 0; i < s; i++) {
        int written = snprintf(buf + pos, (size_t)(bufsz - pos),
                               "strategies:%s\n", strats[i]);
        if (written < 0 || written >= bufsz - pos) return -1;
        pos += written;
    }
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
                size_t tl = (size_t)(eq - kv);
                if (tl >= MAX_TITLE_LEN) tl = MAX_TITLE_LEN - 1;
                memcpy(titles[*n], kv, tl);
                titles[*n][tl] = 0;
                if (!parse_int_range(eq + 1, 1, INT_MAX, &counts[*n]))
                    counts[*n] = 1;
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
    if (len < 0) {
        fputs("Plan is too large to save (maximum 1024 bytes).\n", stderr);
        return;
    }
    if (len == 0 || (len == 1 && buf[0] == '\n')) {
        memset(buf, 0, PLAN_DATA_SIZE);
    } else {
        for (int i = len; i < PLAN_DATA_SIZE; i++) buf[i] = 0;
    }
    fputs("Saving plan to binary...\n", stdout);
    fflush(stdout);
    self_patch_any(plan_data, buf, PLAN_DATA_SIZE, 1, "plan");
}

// ── HTTP (WinHTTP, gzip auto-decompression) ───────────────────────
typedef struct { WCHAR host[256]; int port; } Client;
static Client client = { .host = L"localhost", .port = DEFAULT_PORT };
static size_t last_response_len = 0;
static int last_response_truncated = 0;

static char* http_request(const WCHAR* method, const WCHAR* path,
                          const char* body_utf8, int* status_out) {
    last_response_len = 0;
    last_response_truncated = 0;
    HINTERNET hSession = WinHttpOpen(L"MyTimetable.CLI/1.0",
                                     WINHTTP_ACCESS_TYPE_NO_PROXY, NULL, NULL, 0);
    if (!hSession) return NULL;
    WinHttpSetTimeouts(hSession, 5000, 5000, 15000, 30000);
    // Enable gzip/deflate auto-decompression
    DWORD decompress_flags = WINHTTP_DECOMPRESSION_FLAG_ALL;
    if (!WinHttpSetOption(hSession, WINHTTP_OPTION_DECOMPRESSION,
                          &decompress_flags, sizeof(decompress_flags))) {
        WinHttpCloseHandle(hSession);
        return NULL;
    }
    HINTERNET hConnect = WinHttpConnect(hSession, client.host, (INTERNET_PORT)client.port, 0);
    if (!hConnect) { WinHttpCloseHandle(hSession); return NULL; }
    HINTERNET hRequest = WinHttpOpenRequest(hConnect, method, path, NULL, NULL, NULL, 0);
    if (!hRequest) { WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession); return NULL; }

    WCHAR headers[512] = L"Content-Type: application/json\r\nAccept: application/json\r\n";
    if (!is_placeholder(session_ptr(session_data))) {
        char hex[64] = {0};
        session_hex(hex, 64);
        WCHAR wsid[64];
        if (mbstowcs_terminated(wsid, sizeof(wsid) / sizeof(wsid[0]), hex)) {
            WCHAR auth[256];
            swprintf(auth, 256, L"X-Session-Id: %s\r\n", wsid);
            wcscat(headers, auth);
        }
    }

    DWORD body_len = body_utf8 ? (DWORD)strlen(body_utf8) : 0;
    BOOL ok = WinHttpSendRequest(hRequest, headers, (DWORD)-1,
                                 (LPVOID)(body_utf8 ? body_utf8 : ""),
                                 body_len, body_len, 0);
    if (!ok || !WinHttpReceiveResponse(hRequest, NULL))
        { WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession); return NULL; }

    DWORD status = 0, status_len = sizeof(status);
    if (!WinHttpQueryHeaders(hRequest,
                             WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                             NULL, &status, &status_len, NULL)) {
        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
        return NULL;
    }
    if (status_out) *status_out = (int)status;

    static char buf[BUFSIZE];
    DWORD total = 0, read = 0;
    while (WinHttpReadData(hRequest, buf + total, BUFSIZE - total - 1, &read) && read > 0) {
        total += read;
        if (total >= BUFSIZE - 1) {
            last_response_truncated = 1;
            break;
        }
    }
    last_response_len = total;
    buf[total] = '\0';
    WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession);

    return buf;
}

// ── Binary read helpers ──────────────────────────────────────────
static int read_u16(const unsigned char* p, int* off) {
    int v = p[*off] | (p[*off + 1] << 8);
    *off += 2;
    return v;
}
static int read_i32(const unsigned char* p, int* off) {
    int v = p[*off] | (p[*off + 1] << 8) | (p[*off + 2] << 16) | (p[*off + 3] << 24);
    *off += 4;
    return v;
}
static int read_block(char (*dest)[32], int* count,
                      const unsigned char* p, size_t data_len, int* off) {
    if (*off < 0 || (size_t)*off + 2 > data_len) return 0;
    int item_count = read_u16(p, off);
    if (item_count > 32) return 0;
    *count = item_count;
    for (int i = 0; i < item_count; i++) {
        if ((size_t)*off + 2 > data_len) return 0;
        int len = read_u16(p, off);
        if ((size_t)len > data_len - (size_t)*off) return 0;
        int copied = len < 31 ? len : 31;
        memcpy(dest[i], p + *off, (size_t)copied);
        dest[i][copied] = 0;
        *off += len;
    }
    return 1;
}

// ── URL encoding ─────────────────────────────────────────────────
static int url_enc_char(char* d, unsigned char c) {
    if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~') {
        if (d) *d = (char)c;
        return 1;
    }
    if (d) {
        static const char hex[] = "0123456789ABCDEF";
        d[0] = '%';
        d[1] = hex[(c >> 4) & 0x0F];
        d[2] = hex[c & 0x0F];
    }
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

static size_t json_escaped_length(const char* src) {
    size_t len = 0;
    for (const unsigned char* p = (const unsigned char*)src; *p; p++) {
        size_t extra = 1;
        switch (*p) {
            case '\"': case '\\': case '\b': case '\f':
            case '\n': case '\r': case '\t':
                extra = 2; break;
            default:
                if (*p < 0x20) extra = 6;
                break;
        }
        if (len > SIZE_MAX - extra) return SIZE_MAX;
        len += extra;
    }
    return len;
}

static void json_escape(char* dst, const char* src) {
    static const char hex[] = "0123456789ABCDEF";
    char* out = dst;
    for (const unsigned char* p = (const unsigned char*)src; *p; p++) {
        switch (*p) {
            case '\"': *out++ = '\\'; *out++ = '\"'; break;
            case '\\': *out++ = '\\'; *out++ = '\\'; break;
            case '\b': *out++ = '\\'; *out++ = 'b'; break;
            case '\f': *out++ = '\\'; *out++ = 'f'; break;
            case '\n': *out++ = '\\'; *out++ = 'n'; break;
            case '\r': *out++ = '\\'; *out++ = 'r'; break;
            case '\t': *out++ = '\\'; *out++ = 't'; break;
            default:
                if (*p < 0x20) {
                    *out++ = '\\'; *out++ = 'u';
                    *out++ = '0'; *out++ = '0';
                    *out++ = hex[*p >> 4]; *out++ = hex[*p & 0x0F];
                } else {
                    *out++ = (char)*p;
                }
                break;
        }
    }
    *out = 0;
}

static int size_add(size_t* total, size_t value) {
    if (value > SIZE_MAX - *total) return 0;
    *total += value;
    return 1;
}

static char* make_auth_body(const char* user, const char* pass) {
    static const char prefix[] = "{\"username\":\"";
    static const char separator[] = "\",\"password\":\"";
    static const char suffix[] = "\"}";
    size_t user_len = json_escaped_length(user);
    size_t pass_len = json_escaped_length(pass);
    size_t total = 0;

    if (user_len == SIZE_MAX || pass_len == SIZE_MAX ||
        !size_add(&total, sizeof(prefix) - 1) ||
        !size_add(&total, user_len) ||
        !size_add(&total, sizeof(separator) - 1) ||
        !size_add(&total, pass_len) ||
        !size_add(&total, sizeof(suffix)))
        return NULL;

    char* body = malloc(total);
    if (!body) return NULL;
    char* out = body;
    memcpy(out, prefix, sizeof(prefix) - 1); out += sizeof(prefix) - 1;
    json_escape(out, user); out += user_len;
    memcpy(out, separator, sizeof(separator) - 1); out += sizeof(separator) - 1;
    json_escape(out, pass); out += pass_len;
    memcpy(out, suffix, sizeof(suffix));
    return body;
}

static int do_login_hex(const char* user, const char* pass) {
    char* body = make_auth_body(user, pass);
    if (!body) return 0;
    int st = 0; char* r = http_request(L"POST", L"/Cli/login", body, &st);
    free(body);
    if (!r || last_response_truncated || st != 200) { return 0; }
    // Response body is plain 32-char hex session ID
    if (strlen(r) != SESSION_SIZE * 2) return 0;
    unsigned char raw[SESSION_SIZE];
    if (hex_decode(r, raw, SESSION_SIZE) != SESSION_SIZE) return 0;
    memcpy(session_ptr(session_data), raw, SESSION_SIZE);
    return 1;
}

static int do_register_hex(const char* user, const char* pass, char* err_buf, int err_sz) {
    char* body = make_auth_body(user, pass);
    if (!body) {
        snprintf(err_buf, (size_t)err_sz, "Request is too large");
        return 0;
    }
    int st = 0; char* r = http_request(L"POST", L"/Cli/register", body, &st);
    free(body);
    if (!r) { snprintf(err_buf, (size_t)err_sz, "Connection failed"); return 0; }
    if (last_response_truncated) {
        snprintf(err_buf, (size_t)err_sz, "Server response is too large");
        return 0;
    }
    if (st != 200) {
        snprintf(err_buf, (size_t)err_sz, "Server error %d: %s", st, r);
        return 0;
    }
    // Response body is plain 32-char hex session ID
    if (strlen(r) != SESSION_SIZE * 2) {
        snprintf(err_buf, (size_t)err_sz, "Bad token from server");
        return 0;
    }
    unsigned char raw[SESSION_SIZE];
    if (hex_decode(r, raw, SESSION_SIZE) != SESSION_SIZE) {
        snprintf(err_buf, (size_t)err_sz, "Invalid token format");
        return 0;
    }
    memcpy(session_ptr(session_data), raw, SESSION_SIZE);
    return 1;
}

// ── Commands ──────────────────────────────────────────────────────
static int cmd_login(int argc, char** argv) {
    const char* user = "admin", *pass = "123456Qq!";
    static const char* const options[] = { "--user", "--password", "--host", "--port" };
    if (!validate_cli_args(argc, argv, 2, options, 4, 0)) return 1;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--user")==0 && i+1<argc) user = argv[++i];
        if (strcmp(argv[i],"--password")==0 && i+1<argc) pass = argv[++i];
        if (strcmp(argv[i],"--host")==0 && i+1<argc) {
            if (!mbstowcs_terminated(client.host, sizeof(client.host) / sizeof(client.host[0]), argv[++i]))
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

static int cmd_register(int argc, char** argv) {
    const char* user = NULL, *pass = NULL;
    static const char* const options[] = { "--user", "--password", "--host", "--port" };
    if (!validate_cli_args(argc, argv, 2, options, 4, 0)) return 1;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--user")==0 && i+1<argc) user = argv[++i];
        if (strcmp(argv[i],"--password")==0 && i+1<argc) pass = argv[++i];
        if (strcmp(argv[i],"--host")==0 && i+1<argc) {
            if (!mbstowcs_terminated(client.host, sizeof(client.host) / sizeof(client.host[0]), argv[++i]))
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
    if (!user || !pass) {
        fputs("Usage: mytimetable register --user <username> --password <password>\n", stderr);
        return 1;
    }
    char err[512] = {0};
    if (!do_register_hex(user, pass, err, sizeof(err))) {
        printf("Registration failed: %s\n", err);
        return 1;
    }
    printf("Registered as '%s'\nPatching token into binary...\n", user);
    self_patch(session_ptr(session_data), 1, "schedule");
    return 0;
}

static int cmd_logout(int argc, char** argv) {
    static const char* const options[] = { "--host", "--port" };
    if (!validate_cli_args(argc, argv, 2, options, 2, 0)) return 1;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i], "--host") == 0 && i + 1 < argc) {
            if (!mbstowcs_terminated(client.host, sizeof(client.host) / sizeof(client.host[0]), argv[++i]))
                wcscpy(client.host, DEFAULT_HOST);
        } else if (strcmp(argv[i], "--port") == 0 && i + 1 < argc) {
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
    if (!needs_login()) {
        fputs("Logging out from server...\n", stdout);
        int st = 0;
        char* response = http_request(L"POST", L"/Cli/Logout", NULL, &st);
        if (!response || last_response_truncated || st != 200)
            fprintf(stderr, "Warning: server logout failed (HTTP %d).\n", st);
    }
    fputs("Clearing baked-in token...\n", stdout);
    self_patch(SESSION_PLACEHOLDER, 0, NULL);
    return 0;
}

// ── Shared table REPL (used by schedule and planner submit) ───────
// Takes a raw table string (from JSON "data" field) and scroll_target.
// Owns nothing — caller manages the passed string.
static void run_table_repl(const char* data_text, int scroll_target) {
    if (!data_text || !*data_text) return;

    size_t data_len = strlen(data_text);
    char *data_copy = malloc(data_len + 1);
    if (!data_copy) return;
    strcpy(data_copy, data_text);

    char **lines = malloc(sizeof(char*) * 2048);
    if (!lines) { free(data_copy); return; }
    int line_count = 0;
    char *p = data_copy;
    while (*p && line_count < 2048) {
        while (*p == '\r' || *p == '\n') p++;
        if (!*p) break;
        lines[line_count++] = p;
        while (*p && *p != '\n' && *p != '\r') p++;
        if (*p) { *p = '\0'; p++; }
    }

    if (line_count < 4) { free(lines); free(data_copy); return; }

    int header_lines = 3;
    int body_lines = line_count - header_lines;

    CONSOLE_SCREEN_BUFFER_INFO csbi;
    int console_height = 40;
    if (GetConsoleScreenBufferInfo(GetStdHandle(STD_OUTPUT_HANDLE), &csbi))
        console_height = csbi.srWindow.Bottom - csbi.srWindow.Top + 1;

    int scroll_region_top = header_lines + 1;
    int scroll_region_bot = console_height - 1;
    int avail = scroll_region_bot - scroll_region_top + 1;
    if (avail < 1) avail = 10;

    int start = scroll_target - avail / 2;
    if (start < 0) start = 0;
    int end = start + avail;
    if (end > body_lines) { end = body_lines; start = end - avail; if (start < 0) start = 0; }

    fputs("\033[2J\033[0;0H", stdout);
    for (int i = 0; i < header_lines; i++)
        printf("%s\033[K\n", lines[i]);
    printf("\033[%d;%dr", scroll_region_top, scroll_region_bot);

    char cmd_line[64];
    int running = 1;
    while (running) {
        printf("\033[%d;0H", scroll_region_top);
        for (int i = start; i < end - 1; i++) {
            int line_idx = header_lines + i;
            if (i == scroll_target)
                printf("\033[7m%s\033[27m\033[K\n", lines[line_idx]);
            else
                printf("%s\033[K\n", lines[line_idx]);
        }
        {
            int i = end - 1;
            int line_idx = header_lines + i;
            if (i == scroll_target)
                printf("\033[7m%s\033[27m\033[K", lines[line_idx]);
            else
                printf("%s\033[K", lines[line_idx]);
        }

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

    fputs("\033[r\033[2J\033[0;0H", stdout);
    fflush(stdout);

    free(lines); free(data_copy);
}

static int cmd_schedule(int argc, char** argv) {
    static const char* const options[] = { "--host", "--port" };
    if (!validate_cli_args(argc, argv, 2, options, 2, 0)) return 1;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--host")==0 && i+1<argc) {
            if (!mbstowcs_terminated(client.host, sizeof(client.host) / sizeof(client.host[0]), argv[++i]))
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

    // GET /Cli — gzip-compressed, decompressed by WinHTTP
    int st = 0;
    char* raw = http_request(L"GET", L"/Cli", NULL, &st);
    if (!raw) { fprintf(stderr, "Connection failed\n"); return 1; }
    if (last_response_truncated) {
        fprintf(stderr, "Server response is too large\n");
        return 1;
    }
    if (st == 401) {
        fputs("Token expired. Clearing...\n", stderr);
        self_patch(SESSION_PLACEHOLDER, 0, NULL);
        return 1;
    }
    if (st != 200) { fprintf(stderr, "Error %d\n", st); return 1; }

    // Parse binary: [4B scrollTarget LE][2B data_len LE][UTF-8 data]
    {
        const unsigned char* p = (const unsigned char*)raw;
        int off = 0;
        int scroll_target = read_i32(p, &off);
        int data_len = read_u16(p, &off);
        if (off + data_len < BUFSIZE) {
            char saved = raw[off + data_len];
            raw[off + data_len] = 0;
            run_table_repl(raw + off, scroll_target);
            raw[off + data_len] = saved;
        }
    }
    return 0;
}

// ── Interactive planner ───────────────────────────────────────────
// Fetched from server at startup — never hardcoded.
static char prebuilt_names[32][32];
static int  prebuilt_count = 0;
static char picker_names[32][32];
static int  picker_count = 0;
static char slotter_names[32][32];
static int  slotter_count = 0;
static int  strategies_loaded = 0;

static int is_prebuilt(const char* name) {
    for (int i = 0; i < prebuilt_count; i++)
        if (strcmp(prebuilt_names[i], name) == 0) return 1;
    return 0;
}
static int is_picker(const char* name) {
    for (int i = 0; i < picker_count; i++)
        if (strcmp(picker_names[i], name) == 0) return 1;
    return 0;
}
static int is_slotter(const char* name) {
    for (int i = 0; i < slotter_count; i++)
        if (strcmp(slotter_names[i], name) == 0) return 1;
    return 0;
}

// Parse a stored strategy string. "gap" = prebuilt, "picker:slotter" = composed.
// Returns 1 if prebuilt (stores in *name), 2 if composed (stores in *p / *sl),
// 0 if invalid.
static int parse_strategy_spec(const char* raw, char* name, char* p, char* sl) {
    const char* colon = strchr(raw, ':');
    if (!colon) {
        strncpy(name, raw, 31); name[31] = 0;
        return is_prebuilt(name) ? 1 : 0;
    }
    size_t plen = (size_t)(colon - raw);
    if (plen > 31) plen = 31;
    memcpy(p, raw, plen); p[plen] = 0;
    strncpy(sl, colon + 1, 31); sl[31] = 0;
    return (is_picker(p) && is_slotter(sl)) ? 2 : 0;
}

static int fetch_strategies(void) {
    if (strategies_loaded) return 1;
    int st = 0;
    char* raw = http_request(L"GET", L"/Planner/Strategies", NULL, &st);
    if (!raw || last_response_truncated || st != 200) return 0;

    // Parse binary: 3 blocks [2B count][2B len][chars]...
    {
        const unsigned char* p = (const unsigned char*)raw;
        int off = 0;
        if (!read_block(prebuilt_names, &prebuilt_count, p, last_response_len, &off) ||
            !read_block(picker_names, &picker_count, p, last_response_len, &off) ||
            !read_block(slotter_names, &slotter_count, p, last_response_len, &off)) {
            prebuilt_count = 0;
            picker_count = 0;
            slotter_count = 0;
            return 0;
        }
    }
    strategies_loaded = 1;
    return 1;
}

static void plan_show_status(char titles[][MAX_TITLE_LEN], int* counts, int n,
                              char strats[][32], int s) {
    fputs("  [", stdout);
    if (n == 0) printf("no subjects");
    for (int i = 0; i < n; i++) {
        if (i > 0) printf(", ");
        printf("%s x %d", titles[i], counts[i]);
    }
    fputs(" | ", stdout);
    if (s == 0) printf("no strategies");
    for (int i = 0; i < s; i++) {
        if (i > 0) printf(" > ");
        printf("%s", strats[i]);
    }
    fputs("]\n", stdout);
}

static int cmd_plan(int argc, char** argv) {
    static const char* const options[] = { "--host", "--port" };
    if (!validate_cli_args(argc, argv, 2, options, 2, 1)) return 1;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i], "--host") == 0 && i + 1 < argc) {
            if (!mbstowcs_terminated(client.host, sizeof(client.host) / sizeof(client.host[0]), argv[++i]))
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

    // Fetch available strategies from server — fail if unreachable.
    if (!fetch_strategies()) {
        fputs("Cannot fetch strategy list from server. Is it running?\n", stderr);
        return 1;
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
                fputs("Saved plan state:\n", stdout);
                plan_show_status(t, c, n, st, s);
                return 0;
            }
            if (strcmp(sub, "clear") == 0 || strcmp(sub, "reset") == 0) {
                fputs("Clearing saved plan state...\n", stdout);
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
                fputs("Loaded saved plan from binary.\n", stdout);
        }
    }

    fputs("Planner interactive. Type 'help' for commands, 'quit' to exit.\n", stdout);
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
            fputs("  Cleared.\n", stdout);
            plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "help") == 0 || strcmp(args[0], "h") == 0) {
            fputs("Commands:\n", stdout);
            fputs("  add <title> <count>    - add subject to queue\n", stdout);
            fputs("  rm <title>             - remove subject\n", stdout);
            fputs("  subjects               - list subjects\n", stdout);
            fputs("  push <name>            - add prebuilt strategy\n", stdout);
            fputs("  push <picker> <slotter> - add composed strategy\n", stdout);
            fputs("  pop [N]                - remove strategy (last, or by position)\n", stdout);
            fputs("  mv <from> <to>         - move strategy (1-indexed)\n", stdout);
            fputs("  strategies             - list available strategies\n", stdout);
            fputs("  submit                 - send plan to server\n", stdout);
            fputs("  save                   - persist plan in binary (restarts)\n", stdout);
            fputs("  clear                  - reset all subjects and strategies\n", stdout);
            fputs("  help  / h              - this help\n", stdout);
            fputs("  quit  / q              - exit\n", stdout);
            fputs("Prebuilt: ", stdout);
            for (int i = 0; i < prebuilt_count; i++) {
                if (i > 0) printf(", ");
                printf("%s", prebuilt_names[i]);
            }
            fputs("\nPickers:  ", stdout);
            for (int i = 0; i < picker_count; i++) {
                if (i > 0) printf(", ");
                printf("%s", picker_names[i]);
            }
            fputs("\nSlotters: ", stdout);
            for (int i = 0; i < slotter_count; i++) {
                if (i > 0) printf(", ");
                printf("%s", slotter_names[i]);
            }
            fputs("\n", stdout);
        }
        else if (strcmp(args[0], "subjects") == 0) {
            if (n == 0) { printf("(empty)\n"); }
            for (int i = 0; i < n; i++)
                printf("  %s x %d\n", titles[i], counts[i]);
        }
        else if (strcmp(args[0], "add") == 0) {
            if (ac < 3) { printf("Usage: add <title> <count>\n"); continue; }
            int cnt = 0;
            if (!parse_int_range(args[ac - 1], 1, INT_MAX, &cnt)) {
                printf("Count must be an integer >= 1\n");
                continue;
            }
            if (n >= MAX_SUBJECTS) { printf("Max %d subjects\n", MAX_SUBJECTS); continue; }
            char title_buf[MAX_TITLE_LEN] = {0};
            for (int ai = 1; ai < ac - 1; ai++) {
                if (ai > 1) strncat(title_buf, " ", MAX_TITLE_LEN - strlen(title_buf) - 1);
                strncat(title_buf, args[ai], MAX_TITLE_LEN - strlen(title_buf) - 1);
            }
            for (int i = 0; i < n; i++) {
                if (strcmp(titles[i], title_buf) == 0) {
                    if (counts[i] > INT_MAX - cnt) {
                        printf("Count is too large\n");
                        goto add_done;
                    }
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
            for (int i = 0; i < s; i++) {
                char name[32] = {0}, p[32] = {0}, sl[32] = {0};
                int kind = parse_strategy_spec(strats[i], name, p, sl);
                if (kind == 1) printf("  %d. prebuilt %s\n", i + 1, name);
                else if (kind == 2) printf("  %d. composed %s:%s\n", i + 1, p, sl);
                else printf("  %d. (invalid) %s\n", i + 1, strats[i]);
            }
        }
        else if (strcmp(args[0], "push") == 0) {
            if (ac == 2) {
                // Prebuilt: push <name>
                const char* sn = args[1];
                if (!is_prebuilt(sn)) {
                    printf("Unknown prebuilt: %s\n", sn);
                    fputs("Prebuilt: ", stdout);
                    for (int i = 0; i < prebuilt_count; i++) {
                        if (i > 0) printf(", ");
                        printf("%s", prebuilt_names[i]);
                    }
                    fputs("\n  Or: push <picker> <slotter> for composed\n", stdout);
                    continue;
                }
                if (s >= MAX_STRATEGIES) { printf("Max %d strategies\n", MAX_STRATEGIES); continue; }
                strcpy(strats[s], sn);
                s++;
                printf("  Pushed prebuilt: %s (pos %d)\n", sn, s);
            } else if (ac >= 3) {
                // Composed: push <picker> <slotter>
                const char* pn = args[1];
                const char* sl = args[2];
                if (!is_picker(pn)) {
                    printf("Unknown picker: %s\nPickers: ", pn);
                    for (int i = 0; i < picker_count; i++) {
                        if (i > 0) printf(", ");
                        printf("%s", picker_names[i]);
                    }
                    fputs("\n", stdout);
                    continue;
                }
                if (!is_slotter(sl)) {
                    printf("Unknown slotter: %s\nSlotters: ", sl);
                    for (int i = 0; i < slotter_count; i++) {
                        if (i > 0) printf(", ");
                        printf("%s", slotter_names[i]);
                    }
                    fputs("\n", stdout);
                    continue;
                }
                if (s >= MAX_STRATEGIES) { printf("Max %d strategies\n", MAX_STRATEGIES); continue; }
                snprintf(strats[s], 32, "%s:%s", pn, sl);
                s++;
                printf("  Pushed composed: %s:%s (pos %d)\n", pn, sl, s);
            } else {
                fputs("Usage: push <name>  OR  push <picker> <slotter>\n", stdout);
                continue;
            }
            plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "pop") == 0) {
            if (s == 0) { printf("(empty)\n"); continue; }
            int idx = s - 1; // default: remove last
            if (ac >= 2) {
                int position = 0;
                if (!parse_int_range(args[1], 1, s, &position)) {
                    printf("Position must be 1-%d\n", s);
                    continue;
                }
                idx = position - 1;
                if (idx < 0 || idx >= s) {
                    printf("Position must be 1-%d\n", s);
                    continue;
                }
            }
            printf("  Popped [%d]: %s\n", idx + 1, strats[idx]);
            for (int i = idx; i < s - 1; i++)
                strcpy(strats[i], strats[i + 1]);
            s--;
            plan_show_status(titles, counts, n, strats, s);
        }
        else if (strcmp(args[0], "mv") == 0) {
            if (ac < 3) { printf("Usage: mv <from> <to>\n"); continue; }
            int from_position = 0;
            int to_position = 0;
            if (!parse_int_range(args[1], 1, s, &from_position) ||
                !parse_int_range(args[2], 1, s, &to_position)) {
                printf("Positions must be 1-%d\n", s);
                continue;
            }
            int from = from_position - 1;
            int to = to_position - 1;
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

            // Build query string: /Planner?titles[Encoded]=N&...&fromCli=true
            char qs[4096];
            int pos = 0;
            pos += snprintf(qs + pos, sizeof(qs) - (size_t)pos, "/Planner?");
            for (int i = 0; i < n && pos < (int)sizeof(qs) - 512; i++) {
                if (i > 0) pos += snprintf(qs + pos, sizeof(qs) - (size_t)pos, "&");
                pos += snprintf(qs + pos, sizeof(qs) - (size_t)pos, "titles[");
                pos += url_encode(qs + pos, titles[i]);
                pos += snprintf(qs + pos, sizeof(qs) - (size_t)pos, "]=%d", counts[i]);
            }
            pos += snprintf(qs + pos, sizeof(qs) - (size_t)pos, "&fromCli=true");

            // Build JSON body with StrategySpec array
            char body[4096];
            int bpos = 0;
            bpos += snprintf(body + bpos, sizeof(body) - (size_t)bpos, "[");
            for (int i = 0; i < s; i++) {
                if (i > 0) bpos += snprintf(body + bpos, sizeof(body) - (size_t)bpos, ",");
                char name[32] = {0}, p[32] = {0}, sl[32] = {0};
                int kind = parse_strategy_spec(strats[i], name, p, sl);
                if (kind == 1) {
                    bpos += snprintf(body + bpos, sizeof(body) - (size_t)bpos,
                        "{\"$type\":\"prebuilt\",\"name\":\"%s\"}", name);
                } else if (kind == 2) {
                    bpos += snprintf(body + bpos, sizeof(body) - (size_t)bpos,
                        "{\"$type\":\"composed\",\"picker\":\"%s\",\"slotter\":\"%s\"}", p, sl);
                } else {
                    // Shouldn't happen — push validates, but skip gracefully
                    continue;
                }
            }
            bpos += snprintf(body + bpos, sizeof(body) - (size_t)bpos, "]");

            if (pos >= (int)sizeof(qs) - 512 || bpos >= (int)sizeof(body) - 512) {
                fputs("  Payload too large. Reduce subjects or strategies.\n", stdout);
                continue;
            }

            WCHAR wpath[4096];
            if (!mbstowcs_terminated(wpath, sizeof(wpath) / sizeof(wpath[0]), qs)) {
                fputs("  Query contains invalid or too-long characters.\n", stdout);
                continue;
            }

            fputs("  Sending plan...\n", stdout);
            int st = 0;
            char* resp = http_request(L"PATCH", wpath, body, &st);

            if (!resp) {
                fputs("  Connection failed.\n", stdout);
                continue;
            }
            if (last_response_truncated) {
                fputs("  Server response is too large.\n", stdout);
                continue;
            }
            if (st == 401) {
                fputs("  Token expired. Type 'login' to re-authenticate, then retry.\n", stdout);
                self_patch(SESSION_PLACEHOLDER, 0, NULL);
                return 1;
            }
            if (st != 200) {
                printf("  Error %d: %s\n", st, resp);
                continue;
            }

            // Parse binary: [4B scrollTarget LE][2B data_len LE][UTF-8 data]
            {
                const unsigned char* p = (const unsigned char*)resp;
                int off = 0;
                int starget = read_i32(p, &off);
                int data_len = read_u16(p, &off);
                if (off + data_len < BUFSIZE) {
                    char saved = resp[off + data_len];
                    resp[off + data_len] = 0;
                    run_table_repl(resp + off, starget);
                    resp[off + data_len] = saved;
                }
            }

            plan_show_status(titles, counts, n, strats, s);
        }
        else {
            printf("Unknown: %s. Type 'help'.\n", args[0]);
        }
    }
    return 0;
}

static int cmd_conflicts(int argc, char** argv) {
    static const char* const options[] = { "--host", "--port" };
    if (!validate_cli_args(argc, argv, 2, options, 2, 0)) return 1;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--host")==0 && i+1<argc) {
            if (!mbstowcs_terminated(client.host, sizeof(client.host) / sizeof(client.host[0]), argv[++i]))
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

    // GET /Planner/Conflicts?fromCli=true
    int st = 0;
    char* raw = http_request(L"GET", L"/Planner/Conflicts?fromCli=true", NULL, &st);
    if (!raw) { fprintf(stderr, "Connection failed\n"); return 1; }
    if (last_response_truncated) {
        fprintf(stderr, "Server response is too large\n");
        return 1;
    }
    if (st == 401) {
        fputs("Token expired. Clearing...\n", stderr);
        self_patch(SESSION_PLACEHOLDER, 0, NULL);
        return 1;
    }
    if (st != 200) { fprintf(stderr, "Error %d: %s\n", st, raw); return 1; }

    // Parse binary: [2B count][10B date][1B number]...
    {
        const unsigned char* p = (const unsigned char*)raw;
        int off = 0;
        int count = read_u16(p, &off);
        for (int i = 0; i < count; i++) {
            char date[11] = {0};
            memcpy(date, p + off, 10);
            off += 10;
            int num = p[off++];
            printf("  %s  pair %d\n", date, num);
        }
        if (count == 0) fputs("No conflicts found.\n", stdout);
        else printf("%d conflict(s) total. Run 'resolve-conflicts' to fix.\n", count);
    }
    return 0;
}

static int cmd_resolve_conflicts(int argc, char** argv) {
    static const char* const options[] = { "--host", "--port" };
    if (!validate_cli_args(argc, argv, 2, options, 2, 0)) return 1;
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--host")==0 && i+1<argc) {
            if (!mbstowcs_terminated(client.host, sizeof(client.host) / sizeof(client.host[0]), argv[++i]))
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

    // PATCH /Planner/ResolveConflicts?fromCli=true
    int st = 0;
    char* raw = http_request(L"PATCH", L"/Planner/ResolveConflicts?fromCli=true", NULL, &st);
    if (!raw) { fprintf(stderr, "Connection failed\n"); return 1; }
    if (last_response_truncated) {
        fprintf(stderr, "Server response is too large\n");
        return 1;
    }
    if (st == 401) {
        fputs("Token expired. Clearing...\n", stderr);
        self_patch(SESSION_PLACEHOLDER, 0, NULL);
        return 1;
    }
    if (st != 200) { fprintf(stderr, "Error %d: %s\n", st, raw); return 1; }

    fputs("Conflicts resolved.\n", stdout);
    // Parse binary: [4B scrollTarget LE][2B data_len LE][UTF-8 data]
    {
        const unsigned char* p = (const unsigned char*)raw;
        int off = 0;
        int scroll_target = read_i32(p, &off);
        int data_len = read_u16(p, &off);
        if (off + data_len < BUFSIZE) {
            char saved = raw[off + data_len];
            raw[off + data_len] = 0;
            run_table_repl(raw + off, scroll_target);
            raw[off + data_len] = saved;
        }
    }
    return 0;
}

static void help(void) {
    printf("MyTimetable CLI - self-patching single-binary auth\n\n"
           "Usage:\n"
           "  mytimetable login [--user <u>] [--password <p>] [--host <h>] [--port <p>]\n"
           "  mytimetable register [--user <u>] [--password <p>] [--host <h>] [--port <p>]\n"
           "  mytimetable logout [--host <h>] [--port <p>]\n"
           "  mytimetable schedule                 full-year schedule, scrollable\n"
           "  mytimetable plan                      interactive planner\n"
           "  mytimetable plan status|show          show saved plan state\n"
           "  mytimetable plan clear|reset           clear saved plan state\n"
           "  mytimetable conflicts                list conflicting lessons\n"
           "  mytimetable resolve-conflicts          resolve all conflicts\n"
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
    if (strcmp(cmd,"register")==0) return cmd_register(argc,argv);
    if (strcmp(cmd,"logout")==0) return cmd_logout(argc,argv);
    if (strcmp(cmd,"schedule")==0) return cmd_schedule(argc,argv);
    if (strcmp(cmd,"plan")==0) return cmd_plan(argc,argv);
    if (strcmp(cmd,"conflicts")==0) return cmd_conflicts(argc,argv);
    if (strcmp(cmd,"resolve-conflicts")==0) return cmd_resolve_conflicts(argc,argv);
    if (strcmp(cmd,"help")==0) { help(); return 0; }
    fprintf(stderr,"Unknown: %s\n",cmd); help();
    return 1;
}
