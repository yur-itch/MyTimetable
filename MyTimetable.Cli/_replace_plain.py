import re

with open('main.c', 'r', encoding='utf-8') as f:
    content = f.read()

original_lines = content.split('\n')
changes = 0
skipped = 0

# We'll work line by line to be safe
new_lines = []
for line in original_lines:
    stripped = line.strip()
    
    # printf("plain string") -> fputs("plain string", stdout)
    # Must have exactly: printf("...") with NO format specifiers and NO comma args
    m = re.match(r'^(\s*)printf\("((?:[^"\\]|\\.)*)"\)\s*;?\s*$', line)
    if m:
        indent, fmt = m.groups()
        if re.search(r'%[sdluoxXfFegGcpaAn]', fmt):
            skipped += 1
            new_lines.append(line)
        else:
            changes += 1
            new_lines.append(f'{indent}fputs("{fmt}\\n", stdout);')
            continue
    
    # fprintf(stderr, "plain string") -> fputs("plain string", stderr)
    m = re.match(r'^(\s*)fprintf\(stderr,\s*"((?:[^"\\]|\\.)*)"\)\s*;?\s*$', line)
    if m:
        indent, fmt = m.groups()
        if re.search(r'%[sdluoxXfFegGcpaAn]', fmt):
            skipped += 1
            new_lines.append(line)
        else:
            changes += 1
            new_lines.append(f'{indent}fputs("{fmt}\\n", stderr);')
            continue
    
    new_lines.append(line)

with open('main.c', 'w', encoding='utf-8') as f:
    f.write('\n'.join(new_lines))

print(f'Replaced: {changes}, Skipped (format): {skipped}')
