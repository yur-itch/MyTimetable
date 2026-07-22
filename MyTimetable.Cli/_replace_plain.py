import re

# Restore from backup
with open('main.c.nofmtbak', 'r', encoding='utf-8') as f:
    content = f.read()

lines = content.split('\n')
new_lines = []
changes = 0
skipped = 0

for line in lines:
    # printf("plain") -> fputs("plain", stdout)
    m = re.match(r'^(\s*)printf\("((?:[^"\\]|\\.)*)"\)\s*;?\s*$', line)
    if m:
        indent, fmt = m.groups()
        if re.search(r'%[sdluoxXfFegGcpaAn]', fmt):
            skipped += 1
            new_lines.append(line)
            continue
        changes += 1
        new_lines.append(f'{indent}fputs("{fmt}", stdout);')
        continue

    # fprintf(stderr, "plain") -> fputs("plain", stderr)
    m = re.match(r'^(\s*)fprintf\(stderr,\s*"((?:[^"\\]|\\.)*)"\)\s*;?\s*$', line)
    if m:
        indent, fmt = m.groups()
        if re.search(r'%[sdluoxXfFegGcpaAn]', fmt):
            skipped += 1
            new_lines.append(line)
            continue
        changes += 1
        new_lines.append(f'{indent}fputs("{fmt}", stderr);')
        continue

    new_lines.append(line)

with open('main.c', 'w', encoding='utf-8') as f:
    f.write('\n'.join(new_lines))

print(f'Replaced: {changes}, Skipped: {skipped}')

# Show a few sample changes for verification
with open('main.c.nofmtbak', 'r', encoding='utf-8') as f:
    old_lines = f.read().split('\n')

sample = 0
for i, (o, n) in enumerate(zip(old_lines, new_lines)):
    if o != n and sample < 5:
        print(f'  Line {i+1}: {o.strip()[:60]}')
        print(f'        -> {n.strip()[:60]}')
        sample += 1
