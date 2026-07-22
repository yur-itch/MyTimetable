#include "json.h"
#include <string.h>
static void dead(void) {
    struct json_value_s* r = json_parse("{}", 2);
    (void)json_value_as_object(r);
    free(r);
}
int main(void) { return 0; }
