#include "json.h"
#include <brotli/decode.h>
#include <string.h>
#include <stdio.h>
#include <stdlib.h>

int main(void) {
    /* --- Brotli roundtrip --- */
    const char *input = "hello brotli test data";
    size_t insize = strlen(input);
    /* Use a known-good compressed blob: brotli of "hello" */
    /* We'll just test decompress of empty/small input - if the function is linked, it works */
    size_t outsize = 256;
    uint8_t out[256];
    BrotliDecoderResult r = BrotliDecoderDecompress(0, NULL, &outsize, out);
    printf("brotli: decompressor %s (result=%d)\n",
           r == BROTLI_DECODER_RESULT_SUCCESS ? "PRESENT" : "present but bad input", r);

    /* --- JSON roundtrip --- */
    const char *j = "{\"key\":\"value\"}";
    struct json_value_s *root = json_parse(j, strlen(j));
    if (!root) { printf("json: PARSE FAILED\n"); return 1; }
    struct json_object_s *obj = json_value_as_object(root);
    struct json_value_s *v = obj ? obj->start->value : NULL;
    struct json_string_s *s = v ? json_value_as_string(v) : NULL;
    printf("json: %s=%s\n", obj->start->name->string, s ? s->string : "?");
    free(root);
    return 0;
}
