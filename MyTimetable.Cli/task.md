### 1. Расширенные флаги компиляции и линковки

Собрать проект с агрессивной зачисткой мертвых секций и выравниванием PE-заголовков:

```bash
gcc main.c ../brotli_src/c/dec/*.c ../brotli_src/c/common/*.c \
    -I ../brotli_src/c/include \
    -Os -s -flto \
    -ffunction-sections -fdata-sections \
    -Wl,--gc-sections \
    -Wl,--file-alignment=0x200 -Wl,--section-alignment=0x200 \
    -fno-ident -fno-asynchronous-unwind-tables -fno-unwind-tables \
    -lwinhttp -lshell32 -o mytimetable.exe

```

---

### 2. Избавление от CRT-форматирования (`sprintf`/`snprintf`)

Вырезать тяжелый парсинг спецификаторов формата CRT из горячих точек:

1. **`session_hex`**: замерить размер после замены `sprintf` на битовые сдвиги и таблицу:
```c
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

```


2. **`url_enc_char`**: замерить размер после замены `sprintf(d, "%%%02X", c)` на прямую подстановку байт через `hex[]`.
3. **Остальные `snprintf**` (в `cmd_login`, `cmd_register`, `submit`): переписать сборку простых JSON/Query строк на `memcpy` / `strcpy` / быстрый ручной `itoa`.

---

### 3. Уменьшение `.data` секции

Поскольку `plan_data` инициализирован символами `PLAN_ANCHOR`, компилятор выделяет весь массив физически внутри `.exe`:

* Изменить `PLAN_DATA_SIZE` с `4096` на `1024` или `2048` (в зависимости от реального объема текстового плана).
* Каждое уменьшение на 1 КБ вычитает ровно 1 КБ из итогового бинарника.

---

### 4. Проверка инвариантов (Smoke Test)

После каждой удачной итерации проверить:

1. Запуск `./mytimetable.exe help` проходит без ошибок.
2. Якори `SELF_PATCH_16_AN` и `PLAN_ANCHOR_16__` физически присутствуют в бинарнике и успешно находятся через `mem_find()`.
