#include <brotli/decode.h>
#include <string.h>
int main(void) {
    size_t s = 100;
    char out[100];
    BrotliDecoderDecompress(0, NULL, &s, (uint8_t*)out);
    return 0;
}
