# Publication metadata

Before publishing generated reports, run:

```shell
python scripts/Sanitize-PublishMetadata.py --write
python scripts/Sanitize-PublishMetadata.py
```

The script replaces user home paths, this checkout's absolute path, and the
current computer name with `<USER_HOME>`, `<REPO_ROOT>`, and `<MACHINE>`.
It does not detect or remove all possible credentials or personal information;
review newly added files before committing.

Historical validation reports were sanitized for publication. Their recorded
hashes and byte counts describe the original evidence and inputs, not the
sanitized copies. Those records are not a fresh validation run. Use a new run
and its original artifacts when verifying byte-level evidence integrity.

Local credential files, signing keys, and generated Python bytecode are excluded
from the new repository history. Commits use the GitHub account's noreply email.
