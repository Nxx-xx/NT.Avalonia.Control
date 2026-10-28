# P4-03 save fault and recovery probe

`scripts/pdfviewer/verify_save_faults.py` owns the child processes it starts and kills
only those PIDs at internal production save barriers. The public save / recovery APIs
do the real work; PDF data and environment variables cannot enable the fault hooks.

```powershell
dotnet restore benchmarks/PdfViewer.P403/PdfViewer.P403.csproj --locked-mode
dotnet build benchmarks/PdfViewer.P403/PdfViewer.P403.csproj -c Release --no-restore
python scripts/pdfviewer/verify_save_faults.py --app benchmarks/PdfViewer.P403/bin/Release/net10.0/PdfViewer.P403.dll --output artifacts/pdfviewer-save-faults/windows
```

On Linux use `python3` and a local filesystem that enforces Unix permissions; run as
a regular user so the readonly-parent failure remains meaningful. Each output path
must be fresh. The JSON preserves process commands, PIDs, exit codes, file hashes and
successful / refused recovery results. The dedicated native CI workflow includes it.

Injected disk-full / flush / mount errors test failure handling, not actual storage
exhaustion or network disconnects. Forced process termination does not certify power
failure durability. Independent pypdf / Poppler validation is recorded separately.
