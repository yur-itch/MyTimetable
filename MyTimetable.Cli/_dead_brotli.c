#include <brotli/decode.h>
static void dead(void) {
    size_t s = 0;
    BrotliDecoderDecompress(0, NULL, &s, NULL);
}
int main(void) { return 0; }
