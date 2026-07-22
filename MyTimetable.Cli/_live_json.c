#include "json.h"
#include <string.h>
#include <stdio.h>
int main(void) {
    struct json_value_s* r = json_parse("{\"x\":\"hello\"}", 16);
    struct json_object_s* o = json_value_as_object(r);
    struct json_string_s* s = json_value_as_string(json_value_as_object(r)->start->value);
    printf("%s\n", s->string);
    free(r);
    return 0;
}
