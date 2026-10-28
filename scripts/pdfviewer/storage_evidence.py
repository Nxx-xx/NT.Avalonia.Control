"""Bounded disk evidence housekeeping for the isolated storage probes."""
import gzip,hashlib,json,shutil
from pathlib import Path

def require_space(parent,minimum=512*1024*1024):
    available=shutil.disk_usage(parent).free
    if available<minimum:raise OSError(f'Reserve at least {minimum} bytes before isolated image tests; available={available}')

def compress_images(root):
    root=Path(root).resolve();records=[]
    for path in sorted(root.rglob('*.raw')):
        assert path.resolve().is_relative_to(root) and not path.is_symlink()
        digest=hashlib.file_digest(path.open('rb'),'sha256').hexdigest().upper()
        archive=path.with_suffix('.raw.gz')
        with path.open('rb') as src,gzip.open(archive,'wb',compresslevel=1) as dst:shutil.copyfileobj(src,dst,1024*1024)
        with gzip.open(archive,'rb') as src:assert hashlib.file_digest(src,'sha256').hexdigest().upper()==digest
        records.append(dict(rawPath=path.relative_to(root).as_posix(),rawSHA256=digest,rawBytes=path.stat().st_size,archive=archive.relative_to(root).as_posix(),archiveBytes=archive.stat().st_size))
        path.unlink()
    (root/'images.json').write_text(json.dumps(records,indent=2))
