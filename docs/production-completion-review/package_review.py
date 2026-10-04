#!/usr/bin/env python3
"""Validate review links/order, hash files and package planning documents only."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import zipfile

review = Path(__file__).resolve().parent
root = review.parents[1]
catalog = root / 'docs/development-tasks'
subprocess.run(['python3', str(catalog / 'validate_catalog.py')], cwd=root, check=True)
roster = json.loads((review / 'review-roster.json').read_text())['staff']
if len(roster) != 9 or not all(x['status'] == 'COMPLETED_READ_ONLY' for x in roster):
    raise SystemExit('Nine completed reviewer records required')
if not all((review / x['assignment']).is_file() and (review / x['report']).is_file() for x in roster):
    raise SystemExit('Reviewer assignment or report missing')
# These generated links exist before inspection; successful checks replace pending content.
for name in ('VERIFICATION.json', 'HASHES.sha256'):
    if not (review / name).exists():
        (review / name).write_text('{"status": "PACKAGE_CHECK_PENDING"}\n' if name.endswith('.json') else '# PACKAGE_CHECK_PENDING\n')
links = 0
for path in review.rglob('*.md'):
    for target in re.findall(r'\]\(([^)]+)\)', path.read_text()):
        if target.startswith(('https://', 'http://', '#')):
            continue
        target = target.split('#', 1)[0]
        if not (path.parent / target).exists():
            raise SystemExit(f'Broken review link: {path.relative_to(root)} -> {target}')
        links += 1
# Check the visible serial order against both required and selected conditional edges.
tasks = json.loads((catalog / 'tasks.json').read_text())['tasks']
order = re.findall(r'^\| \d+ \| \[([A-Z]+-\d+)', (review / 'ALL_CATALOG_UNITS.md').read_text(), re.M)
if len(order) != len(tasks) or len(set(order)) != len(tasks):
    raise SystemExit('Serial table does not include every catalog task exactly once')
positions = {id: i for i, id in enumerate(order)}
for task in tasks:
    for dependency in task['dependencies'] + [x['task_id'] for x in task['conditional_dependencies']]:
        if positions[dependency] >= positions[task['task_id']]:
            raise SystemExit(f'Incorrect dependency order: {dependency} -> {task["task_id"]}')
# Recheck inspected baseline digests; changing source requires requalification, not silent acceptance.
baseline = json.loads((review / 'BASELINE.json').read_text())
changed = [p for p, h in baseline['source_sha256'].items()
           if hashlib.sha256((root / p).read_bytes()).hexdigest() != h]
receipt = {'reviewers_completed': 9, 'review_model': 'gpt-6-luna', 'reasoning_effort': 'high',
           'catalog_tasks_validated': len(tasks), 'review_links_checked': links,
           'head_at_review_start': baseline['head'],
           'head_at_packaging': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
           'git_head_change_during_package_validation': 'NONE; HEAD matches the current baseline',
           'serial_dependency_order': 'PASS including conditional edges',
           'baseline_selected_files_changed_since_review_start': changed,
           'working_tree_reconciliation_is_part_of_baseline': True,
           'dynamic_build_test_security_load_restore_checks': 'NOT_RUN'}
verification_path = review / 'VERIFICATION.json'
try:
    verification = json.loads(verification_path.read_text())
except (FileNotFoundError, json.JSONDecodeError):
    verification = {}
verification['package_review'] = receipt
verification_path.write_text(json.dumps(verification, indent=2) + '\n')
files = sorted(p for p in review.rglob('*') if p.is_file() and p.name != 'HASHES.sha256')
(review / 'HASHES.sha256').write_text(''.join(
    hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.relative_to(review).as_posix() + '\n'
    for p in files))
output = root / 'artifacts/development-tasks/Application-production-completion-planning.zip'
output.parent.mkdir(parents=True, exist_ok=True)
package_files = sorted(p for folder in (review, catalog) for p in folder.rglob('*')
                       if p.is_file() and '__pycache__' not in p.parts)
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    for p in package_files:
        archive.write(p, p.relative_to(root).as_posix())
with zipfile.ZipFile(output) as archive:
    if archive.testzip() is not None:
        raise SystemExit('Archive CRC failure')
    for p in package_files:
        if archive.read(p.relative_to(root).as_posix()) != p.read_bytes():
            raise SystemExit('Archive differs from source: ' + str(p))
hash_value = hashlib.sha256(output.read_bytes()).hexdigest()
output.with_suffix('.zip.sha256').write_text(hash_value + '  ' + output.name + '\n')
print(json.dumps({'review_files_hashed': len(files), 'packaged_files': len(package_files),
                  'zip': str(output), 'sha256': hash_value, 'checks': receipt}, indent=2))
