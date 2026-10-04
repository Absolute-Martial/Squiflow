#!/usr/bin/env python3
"""Build a deterministic, hash-verified catalog-only ZIP; no source export."""
import argparse
import hashlib
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parent.parent


def package(output_dir):
    subprocess.run([sys.executable, str(ROOT / 'validate_catalog.py')], check=True, cwd=REPO)
    entries = []
    for line in (ROOT / 'TASK_HASHES.sha256').read_text().splitlines():
        digest, name = line.split('  ', 1)
        path = ROOT / name
        if not path.resolve().is_relative_to(ROOT) or path.is_symlink():
            raise ValueError('Unsafe catalog member: ' + name)
        entries.append((name, digest))
    entries.append(('TASK_HASHES.sha256', hashlib.sha256((ROOT / 'TASK_HASHES.sha256').read_bytes()).hexdigest()))
    output_dir.mkdir(parents=True, exist_ok=True)
    archive = output_dir / 'Application-development-tasks.zip'
    with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for name, digest in sorted(entries):
            data = (ROOT / name).read_bytes()
            if hashlib.sha256(data).hexdigest() != digest:
                raise ValueError('Catalog changed during packaging: ' + name)
            info = zipfile.ZipInfo('development-tasks/' + name, date_time=(2026, 10, 3, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            bundle.writestr(info, data, compresslevel=9)
    with zipfile.ZipFile(archive) as bundle:
        expected = {'development-tasks/' + name: digest for name, digest in entries}
        if set(bundle.namelist()) != set(expected) or bundle.testzip() is not None:
            raise ValueError('ZIP membership/CRC verification failed')
        for name, digest in expected.items():
            if hashlib.sha256(bundle.read(name)).hexdigest() != digest:
                raise ValueError('ZIP member SHA-256 mismatch: ' + name)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    sidecar = archive.with_suffix(archive.suffix + '.sha256')
    sidecar.write_text(digest + '  ' + archive.name + '\n')
    print(f'Packaged and verified {len(entries)} catalog files: {archive}')
    print(f'SHA-256: {digest}')
    print(f'Checksum file: {sidecar}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir', type=Path, default=REPO / 'artifacts' / 'development-tasks')
    package(parser.parse_args().output_dir.resolve())
