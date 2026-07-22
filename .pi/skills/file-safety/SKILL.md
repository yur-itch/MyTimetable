---
name: file-safety
description: Prevents the agent from deleting or overwriting files it did not create. Active in every session for this project.
---

# File Safety Rule

When using bash to delete or overwrite files, follow these rules:

1. **Never delete files you did not create.** If you didn't `write` it or `bash`-generate it in this session, don't touch it.
2. **Before `rm` on anything batch** (wildcards, directories), check each target: is it yours?
3. **Before `rm` on a specific file**, verify it was created by you in this session. If unsure, ask.
4. **Test artifacts go in a `_scratch/` subdirectory** — never scatter them in the project root or alongside tracked source files. Create `_scratch/` if it doesn't exist and use it. `_scratch/` is safe to bulk-delete because only you populate it.
