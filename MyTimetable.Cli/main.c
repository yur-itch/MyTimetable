// MyTimetable.CLI — самопатчащийся single-binary auth-клиент.
// Сборка: gcc main.c -lwinhttp -o mytimetable.exe -O2

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>
#include <shellapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#define DEFAULT_HOST L"localhost"
#define DEFAULT_PORT 8080
#define BUFSIZE      65536
#define MAX_PATH_A   512

// ── Self-patching storage ─────────────────────────────────────────
#define ANCHOR_SIZE      32
#define SESSION_SIZE     64
#define SESSION_DATA_LEN (ANCHOR_SIZE + SESSION_SIZE)

#define SESSION_ANCHOR      "SELF_PATCH_MYTIMETABLE_ANCHOR__!"  // ровно 32
#define SESSION_PLACEHOLDER "SESSION_EMPTY___64_BYTES_FOR_TOKEN_HERE_________________________" // ровно 64

#define session_ptr(d)  ((d) + ANCHOR_SIZE)
#define is_placeholder(p) (memcmp((p), SESSION_PLACEHOLDER, SESSION_SIZE) == 0)

// Единый буфер 32+64 байт в .data секции
static char session_data[SESSION_DATA_LEN] =
    SESSION_ANCHOR SESSION_PLACEHOLDER;

// ── Plan persistent storage ───────────────────────────────────────
#define PLAN_ANCHOR      "PLAN_STATE_MYTIMETABLE_ANCHOR___!"  // ровно 32
#define PLAN_DATA_SIZE    4096
#define PLAN_TOTAL_LEN    (ANCHOR_SIZE + PLAN_DATA_SIZE)
#define PLAN_PLACEHOLDER  "PLAN_EMPTY_PLACEHOLDER_FOR_SERIALIZED_STATE_HERE___" // ровно 64, padding

#define plan_ptr(d)  ((d) + ANCHOR_SIZE)
#define is_plan_placeholder(p) (memcmp((p), PLAN_PLACEHOLDER, PLAN_DATA_SIZE) == 0)

static char plan_data[PLAN_TOTAL_LEN] =
    PLAN_ANCHOR PLAN_PLACEHOLDER;

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
static void self_patch_any(const char* anchor_str, const char* data, int data_size, int will_restart) {
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

    memcpy(anchor + ANCHOR_SIZE, data, data_size);

    char tmp_path[MAX_PATH_A + 8];
    snprintf(tmp_path, sizeof(tmp_path), "%s.tmp", own_path());
    FILE* ftmp = fopen(tmp_path, "wb");
    if (!ftmp) { free(binary); return; }
    fwrite(binary, 1, fsize, ftmp);
    fclose(ftmp);
    free(binary);

    printf(will_restart ? "Saved. Restarting...\n" : "Cleared.\n");

    char esc_self[MAX_PATH_A * 2] = {0};
    {
        const char* s = own_path();
        char* d = esc_self;
        while (*s) {
            if (*s == '\\') *d++ = '\\';
            *d++ = *s++;
        }
    }
    char esc_tmp[MAX_PATH_A * 2 + 8] = {0};
    snprintf(esc_tmp, sizeof(esc_tmp), "%s.tmp", esc_self);

    char cmdline[4096];
    if (will_restart) {
        snprintf(cmdline, sizeof(cmdline),
            "cmd.exe /C start /B cmd.exe /C "
            "timeout /T 1 /NOBREAK >nul & "
            "copy /Y \"%s\" \"%s\" >nul & "
            "del \"%s\" & "
            "start \"\" \"%s\"",
            esc_tmp, esc_self, esc_tmp, esc_self);
    } else {
        snprintf(cmdline, sizeof(cmdline),
            "cmd.exe /C start /B cmd.exe /C "
            "timeout /T 1 /NOBREAK >nul & "
            "copy /Y \"%s\" \"%s\" >nul & "
            "del \"%s\"",
            esc_tmp, esc_self, esc_tmp);
    }

    STARTUPINFOA si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    if (CreateProcessA(NULL, cmdline, NULL, NULL, FALSE,
                       CREATE_NO_WINDOW, NULL, NULL, &si, &pi)) {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    } else {
        system(cmdline);
    }
    exit(0);
}

static void self_patch(const char* new_session, int will_restart) {
    printf(will_restart ? "Token saved. Restarting...\n" : "Token cleared.\n");
    self_patch_any(SESSION_ANCHOR, new_session, SESSION_SIZE, will_restart);
}

// ── Session helper ────────────────────────────────────────────────
static void session_trimmed(char* out, size_t out_sz) {
    char tmp[SESSION_SIZE + 1] = {0};
    memcpy(tmp, session_ptr(session_data), SESSION_SIZE);
    tmp[SESSION_SIZE] = '\0';
    size_t slen = strnlen(tmp, SESSION_SIZE);
    memcpy(out, tmp, slen < out_sz - 1 ? slen : out_sz - 1);
    out[slen < out_sz - 1 ? slen : out_sz - 1] = '\0';
}

// ── HTTP (WinHTTP, no proxy) ──────────────────────────────────────
typedef struct { WCHAR host[256]; int port; } Client;
static Client client = { .host = L"localhost", .port = DEFAULT_PORT };

static char* http_request(const WCHAR* method, const WCHAR* path,
                          const char* body_utf8, int* status_out) {
    HINTERNET hSession = WinHttpOpen(L"MyTimetable.CLI/1.0",
                                     WINHTTP_ACCESS_TYPE_NO_PROXY, NULL, NULL, 0);
    if (!hSession) return NULL;
    HINTERNET hConnect = WinHttpConnect(hSession, client.host, (INTERNET_PORT)client.port, 0);
    if (!hConnect) { WinHttpCloseHandle(hSession); return NULL; }
    HINTERNET hRequest = WinHttpOpenRequest(hConnect, method, path, NULL, NULL, NULL, 0);
    if (!hRequest) { WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession); return NULL; }

    WCHAR headers[512] = L"Content-Type: application/json\r\nAccept: application/json\r\n";
    if (!is_placeholder(session_ptr(session_data))) {
        char trimmed[128] = {0};
        session_trimmed(trimmed, 128);
        WCHAR wsid[128]; mbstowcs(wsid, trimmed, 128);
        WCHAR auth[256];
        swprintf(auth, 256, L"X-Session-Id: %s\r\n", wsid);
        wcscat(headers, auth);
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

    char* buf = malloc(BUFSIZE); if (!buf) { WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession); return NULL; }
    DWORD total = 0, read = 0;
    while (WinHttpReadData(hRequest, buf + total, BUFSIZE - total - 1, &read) && read > 0) {
        total += read; if (total >= BUFSIZE - 1) break;
    }
    buf[total] = '\0';
    WinHttpCloseHandle(hRequest); WinHttpCloseHandle(hConnect); WinHttpCloseHandle(hSession);
    return buf;
}

// ── Minimal JSON ──────────────────────────────────────────────────
static const char* js_ws(const char* p) {
    while (p && *p && (unsigned char)*p <= ' ') p++;
    return p;
}
static char* js_range(const char* s, const char* e) {
    char* r = malloc(e - s + 1); memcpy(r, s, e - s); r[e - s] = 0; return r;
}
static char* js_str(const char* p, const char** end) {
    p = js_ws(p); if (!p || *p != '"') return NULL;
    p++; const char* s = p;
    while (*p && *p != '"') { if (*p == '\\') p++; p++; }
    if (*p != '"') return NULL; if (end) *end = p + 1; return js_range(s, p);
}
static char* js_val(const char* p, const char** end) {
    p = js_ws(p); if (!p || !*p) return NULL;
    if (*p == '"') return js_str(p, end);
    if (*p == '{' || *p == '[') {
        const char* s = p; int d = 1; p++;
        while (*p && d > 0) {
            if (*p == '"') { p++; while (*p && *p != '"') { if (*p == '\\') p++; p++; } }
            if (*p == '{' || *p == '[') d++; if (*p == '}' || *p == ']') d--;
            if (d > 0) p++;
        }
        if (d == 0) p++; if (end) *end = p; return js_range(s, p);
    }
    const char* s = p;
    while (*p && *p != ',' && *p != '}' && *p != ']' && (unsigned char)*p > ' ') p++;
    if (end) *end = p; return js_range(s, p);
}
static char* js_obj(const char* p, const char* key) {
    p = js_ws(p); if (!p || *p != '{') return NULL; p++;
    size_t klen = strlen(key);
    while (1) {
        p = js_ws(p); if (!p || *p == '}') return NULL; if (*p != '"') return NULL;
        p++; const char* ks = p; while (*p && *p != '"') { if (*p == '\\') p++; p++; }
        if (*p != '"') return NULL; size_t l = p - ks; p++;
        p = js_ws(p); if (*p != ':') return NULL; p++;
        if (l == klen && strncmp(ks, key, klen) == 0) return js_val(p, &p);
        const char* d = NULL; js_val(p, &d); if (!d) return NULL; p = d;
        p = js_ws(p); if (*p == ',') p++; else if (*p == '}') return NULL;
    }
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
        else if ((*s & 0xc0) == 0x80) { s++; } // continuation byte, skip
        else { n++; s++; }
    }
    return n;
}

static void fmt_slot(const char* slot_json, char* out, int sz) {
    out[0] = 0;
    char *ns=js_obj(slot_json,"number"), *def=js_obj(slot_json,"default");
    char *cust=js_obj(slot_json,"custom"), *hid=js_obj(slot_json,"hidden");
    (void)ns;
    int h = hid && strcmp(hid,"true")==0;
    if (def && strcmp(def,"null")!=0) {
        char *t=js_obj(def,"title"), *ty=js_obj(def,"lesson_type"), *rm=js_obj(def,"room");
        const char* sn = short_name(t);
        char tc = ty ? ty[0] : '?';
        if (h) snprintf(out,sz,"%s %c",sn,tc);
        else {
            snprintf(out,sz,"%s %c",sn,tc);
            if (rm && rm[0]) { size_t bl=strlen(out); snprintf(out+bl,sz-bl," [%s]",rm); }
        }
        free(t); free(ty); free(rm);
    } else if (cust && strcmp(cust,"null")!=0) {
        snprintf(out,sz,"!self");
    }
    free(ns); free(def); free(cust); free(hid);
    if (!out[0]) snprintf(out,sz,"-");
}

static char** parse_cells(const char* slots_json) {
    char** c = calloc(6, sizeof(char*));
    if (!c) return NULL;
    const char* p = js_ws(slots_json); if (*p == '[') p++;
    while (1) {
        p = js_ws(p); if (!p || *p == ']' || !*p) break; if (*p != '{') { p++; continue; }
        const char* se = NULL; char* slot = js_val(p, &se); if (!slot) break;
        char* ns = js_obj(slot,"number");
        int n = ns ? atoi(ns) : 0;
        if (n >= 1 && n <= 6) {
            if (c[n-1]) free(c[n-1]);
            c[n-1] = malloc(128); c[n-1][0] = 0;
            fmt_slot(slot, c[n-1], 128);
        }
        free(ns); free(slot);
        p = se ? se : p+1; p = js_ws(p); if (*p == ',') p++;
    }
    return c;
}

static void free_cells(char** c) {
    if (!c) return; for (int i=0;i<6;i++) free(c[i]); free(c);
}

static int copy_visible(char* dst, const char* src, int max_vis) {
    int copied = 0;
    while (*src && copied < max_vis) {
        if (*src == 0x1b) {
            src++;
            while (*src && *src != 'm') src++;
            if (*src) src++;
        } else if ((*src & 0xc0) == 0x80) {
            *dst++ = *src++; // UTF-8 continuation byte: copy but don't count
        } else {
            *dst++ = *src++;
            copied++;
        }
    }
    return copied;
}

// ── Table renderer with runtime verification ──────────────────────
static void print_checked(const char* buf, int expected, const char* tag) {
    int got = vis_len(buf);
    if (got != expected)
        fprintf(stderr, "[%s vis=%d != exp=%d]\n", tag, got, expected);
    printf("%s\n", buf);
}


static void build_bar(char* buf, int full, int* pp, int l1,int l2,int l3, int r1,int r2,int r3, int j1,int j2,int j3, int h1,int h2,int h3) {
    int pos=0, pi=1;
    for(int v=0;v<full;v++){
        if(v==0){buf[pos++]=l1;buf[pos++]=l2;buf[pos++]=l3;}
        else if(v==full-1){buf[pos++]=r1;buf[pos++]=r2;buf[pos++]=r3;}
        else if(pi<8&&v==pp[pi]){buf[pos++]=j1;buf[pos++]=j2;buf[pos++]=j3;pi++;}
        else{buf[pos++]=h1;buf[pos++]=h2;buf[pos++]=h3;}
    }
    buf[pos]=0;
}


static void render_schedule(int n, char** dates, char*** cells) {
    int cw[7] = {10,10,10,10,10,10,10};
    for (int d=0; d<n; d++) {
        int dl = vis_len(dates[d]); if (dl > cw[0]) cw[0] = dl;
        for (int s=0; s<6; s++) {
            int vl = cells[d][s] ? vis_len(cells[d][s]) : 0;
            if (vl > cw[s+1]) cw[s+1] = vl;
        }
    }
    int full = 22;
    for (int i=0; i<7; i++) full += cw[i];

    // pipe visual positions for bar building
    int pp[8]; pp[0] = 0;
    int acc = 0;
    for (int bi=0; bi<6; bi++) { acc += cw[bi]; pp[bi+1] = acc + 3*(bi+1); }
    pp[7] = full - 1;

    char row[8192];
    int p;

    build_bar(row, full, pp, 0xE2,0x95,0x94, 0xE2,0x95,0x97, 0xE2,0x95,0xA6, 0xE2,0x95,0x90); print_checked(row,full,"bar");

    // header row
    p = 0; row[p++] = 0xE2; row[p++] = 0x95; row[p++] = 0x91; row[p++] = 32;
    for (int i=0; i<cw[0]-4; i++) row[p++] = 32;
    row[p++] = 68; row[p++] = 97; row[p++] = 116; row[p++] = 101;
    for (int s=1; s<=6; s++) {
        row[p++] = 32; row[p++] = 0xE2; row[p++] = 0x95; row[p++] = 0x91; row[p++] = 32;
        char h[8]; int nlen = snprintf(h,8,"%d",s);
        memcpy(row+p, h, nlen); p += nlen;
        for (int i=nlen; i<cw[s]; i++) row[p++] = 32;
    }
    row[p++] = 32; row[p++] = 0xE2; row[p++] = 0x95; row[p++] = 0x91;
    row[p] = 0; print_checked(row, full, "hdr");

    build_bar(row, full, pp, 0xE2,0x95,0xA0, 0xE2,0x95,0xA3, 0xE2,0x95,0xAC, 0xE2,0x94,0x80); print_checked(row,full,"bar");

    for (int d=0; d<n; d++) {
        p = 0; row[p++] = 0xE2; row[p++] = 0x95; row[p++] = 0x91; row[p++] = 32;
        int dl = strlen(dates[d]); memcpy(row+p, dates[d], dl); p += dl;
        for (int i=dl; i<cw[0]; i++) row[p++] = 32;
        for (int s=0; s<6; s++) {
            row[p++] = 32; row[p++] = 0xE2; row[p++] = 0x95; row[p++] = 0x91; row[p++] = 32;
            int vlen = cells[d][s] ? vis_len(cells[d][s]) : 0;
            int blen = cells[d][s] ? (int)strlen(cells[d][s]) : 0;
            if (blen > 0) { memcpy(row+p, cells[d][s], blen); p += blen; }
            for (int i=vlen; i<cw[s+1]; i++) row[p++] = 32;
        }
        row[p++] = 32; row[p++] = 0xE2; row[p++] = 0x95; row[p++] = 0x91;
        row[p] = 0; print_checked(row, full, "row");
    }

    build_bar(row, full, pp, 0xE2,0x95,0x9A, 0xE2,0x95,0x9D, 0xE2,0x95,0xA9, 0xE2,0x95,0x90); print_checked(row,full,"bar");
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

static char* do_login(const char* user, const char* pass) {
    char body[256]; snprintf(body, sizeof(body), "{\"username\":\"%s\",\"password\":\"%s\"}", user, pass);
    int st = 0; char* r = http_request(L"POST", L"/api/login", body, &st);
    if (!r || st != 200) { free(r); return NULL; }
    char* sid = js_obj(r, "sessionId"); free(r); return sid;
}

// ── Commands ──────────────────────────────────────────────────────
static int cmd_login(int argc, char** argv) {
    const char* user = "admin", *pass = "admin";
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--user")==0 && i+1<argc) user = argv[++i];
        if (strcmp(argv[i],"--password")==0 && i+1<argc) pass = argv[++i];
        if (strcmp(argv[i],"--host")==0 && i+1<argc) mbstowcs(client.host, argv[++i], 256);
        if (strcmp(argv[i],"--port")==0 && i+1<argc) client.port = atoi(argv[++i]);
    }
    char* sid = do_login(user, pass);
    if (!sid) { printf("Login failed\n"); return 1; }
    printf("Logged in as '%s'\nPatching token into binary...\n", user);
    char new_64[SESSION_SIZE] = {0};
    size_t sl = strlen(sid);
    memcpy(new_64, sid, sl > SESSION_SIZE ? SESSION_SIZE : sl);
    free(sid);
    self_patch(new_64, 1);
    return 0;
}

static int cmd_logout(void) {
    printf("Clearing baked-in token...\n");
    self_patch(SESSION_PLACEHOLDER, 0);
    return 0;
}

static int cmd_schedule(int argc, char** argv) {
    const char* range = "today";
    for (int i = 2; i < argc; i++) {
        if (strcmp(argv[i],"--host")==0 && i+1<argc) mbstowcs(client.host, argv[++i], 256);
        else if (strcmp(argv[i],"--port")==0 && i+1<argc) client.port = atoi(argv[++i]);
        else range = argv[i];
    }
    if (needs_login()) { fprintf(stderr, "No token. Run 'login' first.\n"); return 1; }

    char date_from[16]={0}, date_to[16]={0};
    if (strcmp(range,"today")==0) {
        time_t t=time(NULL); struct tm tm; localtime_s(&tm,&t);
        snprintf(date_from,16,"%04d-%02d-%02d",tm.tm_year+1900,tm.tm_mon+1,tm.tm_mday);
        strcpy(date_to,date_from);
    } else if (strcmp(range,"week")==0) {
        time_t t=time(NULL); struct tm tm; localtime_s(&tm,&t);
        int w=tm.tm_wday; if(w==0)w=7; tm.tm_mday-=(w-1); mktime(&tm);
        snprintf(date_from,16,"%04d-%02d-%02d",tm.tm_year+1900,tm.tm_mon+1,tm.tm_mday);
        tm.tm_mday+=6; mktime(&tm);
        snprintf(date_to,16,"%04d-%02d-%02d",tm.tm_year+1900,tm.tm_mon+1,tm.tm_mday);
    } else { strcpy(date_from,range); strcpy(date_to,range); }

    char sid[128]={0}; session_trimmed(sid,128);
    char path[512]; snprintf(path,512,"/api/schedule?session_id=%s&dateFrom=%s&dateTo=%s",sid,date_from,date_to);
    WCHAR wp[512]; mbstowcs(wp,path,512);
    int st=0; char* r = http_request(L"GET",wp,NULL,&st);
    if(!r){fprintf(stderr,"Connection failed\n");return 1;}
    if(st==401){
        fprintf(stderr,"Token expired. Clearing...\n");
        free(r);
        self_patch(SESSION_PLACEHOLDER,0);
        return 1;
    }
    if(st!=200){fprintf(stderr,"Error %d\n",st);free(r);return 1;}

    printf("Schedule:\n");
    char* days=js_obj(r,"days");
    if(!days){printf("No schedule data\n");free(r);return 1;}
    const char* p=js_ws(days); if(*p=='[')p++;
    int max_days=100, dc=0;
    char** dates=malloc(max_days*sizeof(char*));
    char*** cells=malloc(max_days*sizeof(char**));
    if(!dates||!cells){free(days);free(r);return 1;}
    while(1){
        p=js_ws(p); if(!p||*p==']'||!*p)break; if(*p!='{'){p++;continue;}
        const char* de=NULL; char* day=js_val(p,&de); if(!day)break;
        char* d=js_obj(day,"date"); char* s=js_obj(day,"slots");
        if(d&&s){
            dates[dc]=d; cells[dc]=parse_cells(s); free(s);
            dc++;
            if(dc>=max_days)break;
            free(day); p=de?de:p+1; p=js_ws(p); if(*p==',')p++;
            continue;
        }
        free(d);free(s);free(day); p=de?de:p+1; p=js_ws(p); if(*p==',')p++;
    }
    if(dc>0){
        render_schedule(dc,dates,cells);
        for(int i=0;i<dc;i++){free(dates[i]);free_cells(cells[i]);}
    }else{
        printf("No lessons in this period\n");
    }
    free(dates);free(cells);free(days);free(r);
    return 0;
}

// ── Interactive planner ───────────────────────────────────────────
#define MAX_SUBJECTS 32
#define MAX_STRATEGIES 32
#define MAX_TITLE_LEN 64

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
    // Subjects line
    printf("  [");
    if (n == 0) printf("no subjects");
    for (int i = 0; i < n; i++) {
        if (i > 0) printf(", ");
        printf("%s x %d", titles[i], counts[i]);
    }
    // Strategies line
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
        if (strcmp(argv[i], "--host") == 0 && i + 1 < argc) mbstowcs(client.host, argv[++i], 256);
        else if (strcmp(argv[i], "--port") == 0 && i + 1 < argc) client.port = atoi(argv[++i]);
    }

    // State
    char titles[MAX_SUBJECTS][MAX_TITLE_LEN];
    int  counts[MAX_SUBJECTS];
    int  n = 0;
    char strats[MAX_STRATEGIES][32];
    int  s = 0;

    printf("Planner interactive. Type 'help' for commands, 'quit' to exit.\n");
    plan_show_status(titles, counts, n, strats, s);

    char line[512];
    while (1) {
        printf("plan> "); fflush(stdout);
        if (!fgets(line, sizeof(line), stdin)) { printf("\n"); break; }
        // strip trailing newline
        size_t llen = strlen(line);
        while (llen > 0 && (line[llen - 1] == '\n' || line[llen - 1] == '\r')) line[--llen] = 0;

        char* cmd = line;
        while (*cmd == ' ') cmd++;
        if (!*cmd) continue;

        // Normalize tabs to spaces (strtok doesn't handle tabs)
        for (char* p = cmd; *p; p++) if (*p == '\t') *p = ' ';

        // Parse args
        char* args[16];
        int ac = 0;
        char* tok = strtok(cmd, " ");
        while (tok && ac < 16) { args[ac++] = tok; tok = strtok(NULL, " "); }

        if (strcmp(args[0], "quit") == 0 || strcmp(args[0], "q") == 0) {
            break;
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
            // Title = all args except first (add) and last (count), rejoined by space
            char title_buf[MAX_TITLE_LEN] = {0};
            for (int ai = 1; ai < ac - 1; ai++) {
                if (ai > 1) strncat(title_buf, " ", MAX_TITLE_LEN - strlen(title_buf) - 1);
                strncat(title_buf, args[ai], MAX_TITLE_LEN - strlen(title_buf) - 1);
            }
            // Check for duplicate
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
            // Title = all args after 'rm' rejoined by space
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

            // Build query string — estimate max size
            // Each Cyrillic char becomes %XX (3x), brackets add 2, = and & add ~2
            // Worst case: ~64 bytes per subject, ~20 per strategy. 32*64 + 32*20 = ~2700
            // Use 8192 to be safe
            char qs[8192];
            int qsz = sizeof(qs);
            int qpos = 0;

            // Session ID
            if (!needs_login()) {
                char sid[128] = {0};
                session_trimmed(sid, 128);
                qpos += snprintf(qs + qpos, qsz - qpos, "session_id=%s&", sid);
            }

            // Strategies
            for (int i = 0; i < s && qpos < qsz - 256; i++) {
                char enc[128];
                url_encode(enc, strats[i]);
                qpos += snprintf(qs + qpos, qsz - qpos, "strategies=%s&", enc);
            }

            // Titles
            for (int i = 0; i < n && qpos < qsz - 256; i++) {
                char enc_title[256];
                url_encode(enc_title, titles[i]);
                qpos += snprintf(qs + qpos, qsz - qpos, "titles[%s]=%d&", enc_title, counts[i]);
            }

            if (qpos >= qsz - 256) {
                printf("  Query too long. Reduce subjects or strategies.\n");
                continue;
            }
            if (qpos > 0) qs[qpos - 1] = 0; // remove trailing &

            printf("  Sending plan...\n");

            // Rebuild path as narrow string then widen
            char path_utf8[8192 + 32];
            snprintf(path_utf8, sizeof(path_utf8), "/App/Plan?%s", qs);

            WCHAR wpath[8192];
            mbstowcs(wpath, path_utf8, 8192);

            int st = 0;
            char* resp = http_request(L"PATCH", wpath, NULL, &st);

            if (!resp) {
                printf("  Connection failed.\n");
                continue;
            }
            if (st == 401) {
                printf("  Token expired. Type 'login' to re-authenticate, then retry.\n");
                free(resp);
                self_patch(SESSION_PLACEHOLDER, 0);
                return 1;
            }
            if (st != 200) {
                printf("  Error %d: %s\n", st, resp);
                free(resp);
                continue;
            }

            // Parse response
            char* success = js_obj(resp, "success");
            char* failure_str = js_obj(resp, "failure");
            int failure = failure_str ? atoi(failure_str) : 0;
            int placed = 0;
            for (int i = 0; i < n; i++) placed += counts[i];
            placed -= failure;

            printf("  Result: placed %d lessons, failed %d\n", placed, failure);
            free(success);
            free(failure_str);
            free(resp);
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
           "  mytimetable schedule [today|week|<date>]\n"
           "  mytimetable plan\n"
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
