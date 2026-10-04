#!/usr/bin/env python3
"""Validate assignment metadata, dependency graph, local links and catalog hashes."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parent.parent
ID = re.compile(r'\b(?:BAS|ADM|COM|OPS|WEB|UIA|GATE)-\d{3}\b')
STATUSES = {'VERIFY_EXISTING', 'READY_AFTER_DEPENDENCIES', 'DECISION_REQUIRED', 'CONDITIONAL'}
MODELS = {'GPT-6 Luna (high)', 'GPT-6.1 Sol'}
PHASES = {'00-baseline', '01-admin-platform', '02-commercial-backend', '03-runtime-operations', '04-tenant-web', '05-admin-web', '06-qualification'}
FIELDS = ('Task ID', 'Phase', 'Status', 'Model', 'Dependencies', 'Release requirement')
SECTIONS = ('Outcome', 'Current basis and canonical inputs', 'Scope and exclusions', 'Decisions/prerequisites', 'Acceptance and edge cases', 'Security/static review', 'Dynamic verification and unavailable-environment handling', 'Handoff', 'Assignable prompt')


def section(text, heading):
    match = re.search(r'^## ' + re.escape(heading) + r'\n(.*?)(?=^## |\Z)', text, re.M | re.S)
    return match[1].strip() if match else ''


def read_tasks():
    tasks, errors = {}, []
    for path in sorted(ROOT.glob('phase-*/*.md')):
        body = path.read_text()
        header = body.split('\n## ', 1)[0]
        meta = dict(re.findall(r'^([^\n:]+): ([^\n]+)$', header, re.M))
        if 'Task ID' not in meta:
            continue
        task_id = meta['Task ID']
        relative = path.relative_to(ROOT).as_posix()
        for field in FIELDS:
            if not meta.get(field):
                errors.append(f'{relative}: missing {field}')
        if not ID.fullmatch(task_id) or task_id in tasks:
            errors.append(f'{relative}: invalid or duplicate task ID {task_id}')
        if not path.name.startswith(task_id):
            errors.append(f'{relative}: filename does not start with task ID')
        if meta.get('Phase') not in PHASES or path.parent.name != 'phase-' + meta.get('Phase', ''):
            errors.append(f'{relative}: invalid/mismatched phase')
        if meta.get('Status') not in STATUSES or meta.get('Model') not in MODELS:
            errors.append(f'{relative}: invalid status or model')
        if meta.get('Release requirement') not in {'REQUIRED', 'CONDITIONAL'}:
            errors.append(f'{relative}: invalid release requirement')
        dependencies = ID.findall(meta.get('Dependencies', ''))
        if task_id in dependencies or len(dependencies) != len(set(dependencies)):
            errors.append(f'{relative}: self or duplicate dependency')
        if meta.get('Dependencies') != 'none' and ', '.join(dependencies) != meta.get('Dependencies'):
            errors.append(f'{relative}: Dependencies must be exact IDs separated by comma-space')
        conditional = meta.get('Conditional dependencies', 'none')
        if conditional != 'none' and not ID.search(conditional):
            errors.append(f'{relative}: conditional prerequisites need exact task IDs and conditions')
        declared = set(dependencies + ID.findall(conditional))
        cross = meta.get('Cross-track prerequisites', 'none')
        if cross != 'none' and (not ID.search(cross) or not set(ID.findall(cross)) <= declared):
            errors.append(f'{relative}: cross-track prerequisites must be declared dependency IDs')
        for heading in SECTIONS:
            if not section(body, heading):
                errors.append(f'{relative}: missing/empty {heading}')
        prompt = re.search(r'```text\n(.*?)\n```', section(body, 'Assignable prompt'), re.S)
        if not prompt or not 8 <= len(prompt[1].strip().splitlines()) <= 15:
            errors.append(f'{relative}: prompt needs 8-15 nonempty lines')
        scope = section(body, 'Scope and exclusions')
        if not re.search(r'Allowed areas:|Write |write scope|allowed', scope, re.I):
            errors.append(f'{relative}: write scope not named')
        if re.search(r'integrator maps|orchestrator maps|map.*exact.*IDs?|semantic prerequisites', meta.get('Cross-track prerequisites', ''), re.I):
            errors.append(f'{relative}: unresolved cross-track prerequisites')
        tasks[task_id] = {'task_id': task_id, 'title': body.splitlines()[0].lstrip('# ').replace(task_id + ' — ', ''), 'phase': meta.get('Phase'), 'status': meta.get('Status'), 'model': meta.get('Model'), 'release_requirement': meta.get('Release requirement'), 'dependencies': dependencies, 'conditional_dependencies': [{'task_id': dep, 'condition': clause.strip()} for clause in conditional.split(';') for dep in ID.findall(clause)], 'write_scope': scope, 'file': relative}
    for task in tasks.values():
        for dep in task['dependencies'] + [item['task_id'] for item in task['conditional_dependencies']]:
            if dep not in tasks:
                errors.append(f'{task["task_id"]}: unknown dependency {dep}')
    visiting, done = [], set()
    def visit(task_id):
        if task_id in visiting:
            errors.append('Dependency cycle: ' + ' -> '.join(visiting + [task_id]))
            return
        if task_id in done or task_id not in tasks:
            return
        visiting.append(task_id)
        for dep in tasks[task_id]['dependencies'] + [item['task_id'] for item in tasks[task_id]['conditional_dependencies']]:
            visit(dep)
        visiting.pop()
        done.add(task_id)
    for task_id in tasks:
        visit(task_id)
    for prefix, count in {'BAS': 1, 'ADM': 34, 'COM': 32, 'OPS': 22, 'WEB': 16, 'UIA': 9, 'GATE': 5}.items():
        expected = {f'{prefix}-{n:03d}' for n in range(1, count + 1)}
        actual = {task_id for task_id in tasks if task_id.startswith(prefix + '-')}
        if expected != actual:
            errors.append(f'{prefix}: expected contiguous IDs; missing {sorted(expected - actual)}, extra {sorted(actual - expected)}')
    return tasks, errors


def closure(tasks, task_id, include_conditional=False):
    result, pending = set(), [task_id]
    while pending:
        current = pending.pop()
        if current in result or current not in tasks:
            continue
        result.add(current)
        pending.extend(tasks[current]['dependencies'])
        if include_conditional:
            pending.extend(item['task_id'] for item in tasks[current]['conditional_dependencies'])
    return result


def validate_release_routes(tasks):
    errors = []
    groups = {
        'GATE-002': {'00-baseline', '01-admin-platform', '02-commercial-backend', '03-runtime-operations'},
        'GATE-003': {'04-tenant-web'},
        'GATE-004': {'05-admin-web'},
        'GATE-005': PHASES,
    }
    for gate, phases in groups.items():
        required = {t['task_id'] for t in tasks.values() if t['phase'] in phases and t['release_requirement'] == 'REQUIRED'} - {gate}
        missing = required - closure(tasks, gate)
        if missing:
            errors.append(f'{gate}: required tasks missing from gate prerequisites: {sorted(missing)}')
    unrouted = set(tasks) - closure(tasks, 'GATE-005', include_conditional=True)
    if unrouted:
        errors.append(f'Tasks have no required/conditional release route: {sorted(unrouted)}')
    return errors


def execution_layers(tasks):
    remaining, completed, layers = set(tasks), set(), []
    while remaining:
        ready = sorted(t for t in remaining if set(tasks[t]['dependencies'] + [d['task_id'] for d in tasks[t]['conditional_dependencies']]) <= completed)
        if not ready:
            raise ValueError('Cannot generate execution layers for an invalid dependency graph')
        layers.append(ready)
        completed.update(ready)
        remaining.difference_update(ready)
    return layers


def generate(tasks):
    ordered = sorted(tasks.values(), key=lambda task: (task['phase'], task['task_id']))
    (ROOT / 'tasks.json').write_text(json.dumps({'schema_version': 1, 'planning_only': True, 'tasks': ordered}, indent=2, ensure_ascii=False) + '\n')
    rows = ['# Exact task index', '', 'Generated by `validate_catalog.py --refresh`. Status is assignment metadata, not runtime qualification. Conditional prerequisites apply only to the named selected branch.', '', '| Task | Phase | Status | Model | Release | Dependencies | Conditional prerequisites |', '|---|---|---|---|---|---|---|']
    for task in ordered:
        optional = '; '.join(dict.fromkeys(item['condition'] for item in task['conditional_dependencies'])) or 'none'
        rows.append(f'| [{task["task_id"]} — {task["title"]}]({task["file"]}) | {task["phase"]} | {task["status"]} | {task["model"]} | {task["release_requirement"]} | {", ".join(task["dependencies"]) or "none"} | {optional} |')
    rows.extend(['', 'Exact allowed write scope is retained in each complete task and in `tasks.json`; attach the task, not this row alone.', ''])
    (ROOT / 'TASK_INDEX.md').write_text('\n'.join(rows))
    for phase in sorted(PHASES):
        members = [task for task in ordered if task['phase'] == phase]
        rows = [f'# Phase {phase} task index', '', 'Generated from full task metadata. Read the complete file and accepted dependency handoffs before dispatch.', '', '| Task | Status | Model | Release | Dependencies |', '|---|---|---|---|---|']
        for task in members:
            rows.append(f'| [{task["task_id"]} — {task["title"]}]({Path(task["file"]).name}) | {task["status"]} | {task["model"]} | {task["release_requirement"]} | {", ".join(task["dependencies"]) or "none"} |')
        rows.extend(['', 'Conditional prerequisites and approved writable areas are in the full task and root tasks.json.', ''])
        (ROOT / ('phase-' + phase) / 'TASK_INDEX.md').write_text('\n'.join(rows))
    rows = ['# Dependency-ordered execution layers', '', 'Generated with every conditional prerequisite included. This is a conservative all-branches-selected ordering, not permission to dispatch all rows or a claim that optional work is required. Evaluate approved conditions, accepted handoffs and exact file ownership using ORCHESTRATOR.md.', '', '| Layer | Tasks whose prerequisites are in earlier layers |', '|---|---|']
    for index, layer in enumerate(execution_layers(tasks), 1):
        rows.append(f'| {index:02d} | ' + ', '.join(f'[{task_id}]({tasks[task_id]["file"]})' for task_id in layer) + ' |')
    (ROOT / 'EXECUTION_ORDER.md').write_text('\n'.join(rows) + '\n')
    coverage = ROOT / 'coverage.json'
    if coverage.exists():
        rows = ['# Responsibility coverage matrix', '', 'Generated from coverage.json. Required means a release obligation after its decisions are accepted; conditional needs explicit workload selection or an absent-with-reason disposition. Deferred/excluded scope is visible and is not declared implemented.', '', '| Responsibility | Disposition | Focused owner | Assignments | Applicability / non-claim |', '|---|---|---|---|---|']
        for row in json.loads(coverage.read_text())['coverage']:
            links = ', '.join(f'[{t}]({tasks[t]["file"]})' for t in row.get('tasks', []) if t in tasks) or 'none'
            rows.append(f'| {row["responsibility"]} | {row["disposition"]} | [{row["owner"]}](../../{row["owner"]}) | {links} | {row.get("deferred_reason", "Current source/decisions and the full task define the exact scope.")} |')
        (ROOT / 'COVERAGE_MATRIX.md').write_text('\n'.join(rows) + '\n')
    entries = []
    for path in sorted(ROOT.rglob('*')):
        if path.is_file() and path.name != 'TASK_HASHES.sha256' and '__pycache__' not in path.parts:
            entries.append(hashlib.sha256(path.read_bytes()).hexdigest() + '  ' + path.relative_to(ROOT).as_posix())
    (ROOT / 'TASK_HASHES.sha256').write_text('\n'.join(entries) + '\n')


def validate_links_and_coverage(tasks):
    errors = []
    for path in ROOT.rglob('*.md'):
        body = path.read_text()
        for target in re.findall(r'(?<!!)\[[^\]]+\]\(([^)]+)\)', body):
            target = target.strip('<>').split('#', 1)[0]
            if not target or re.match(r'[A-Za-z][A-Za-z0-9+.-]*:', target):
                continue
            if not (path.parent / target).exists():
                errors.append(f'{path.relative_to(ROOT)}: broken local link {target}')
        for task_id in set(ID.findall(body)):
            if task_id not in tasks:
                errors.append(f'{path.relative_to(ROOT)}: unknown task reference {task_id}')
    coverage = ROOT / 'coverage.json'
    if not coverage.exists():
        errors.append('Missing coverage.json')
    else:
        routed = set()
        for row in json.loads(coverage.read_text())['coverage']:
            if not row.get('responsibility') or not row.get('disposition') or not row.get('owner'):
                errors.append('Incomplete coverage row: ' + str(row))
            if not (REPO / row['owner']).exists():
                errors.append('Missing coverage owner: ' + row['owner'])
            if not row.get('tasks') and not row.get('deferred_reason'):
                errors.append('Unrouted coverage: ' + row['responsibility'])
            if row.get('disposition') not in {'REQUIRED', 'CONDITIONAL', 'DEFERRED', 'EXCLUDED'}:
                errors.append('Invalid coverage disposition: ' + row['responsibility'])
            for task_id in row.get('tasks', []):
                if task_id not in tasks:
                    errors.append('Unknown coverage task: ' + task_id)
                routed.add(task_id)
        if set(tasks) - routed:
            errors.append('Tasks missing responsibility coverage: ' + ', '.join(sorted(set(tasks) - routed)))
    index = ROOT / 'tasks.json'
    expected = sorted(tasks.values(), key=lambda task: (task['phase'], task['task_id']))
    if not index.exists() or json.loads(index.read_text()).get('tasks') != expected:
        errors.append('tasks.json differs from task metadata; run --refresh')
    return errors


def validate_hashes():
    errors, listed = [], set()
    manifest = ROOT / 'TASK_HASHES.sha256'
    if not manifest.exists():
        return ['Missing TASK_HASHES.sha256; run --refresh']
    for line in manifest.read_text().splitlines():
        match = re.fullmatch(r'([0-9a-f]{64})  (.+)', line)
        if not match:
            errors.append('Malformed SHA-256 manifest entry')
            continue
        digest, name = match.groups()
        path = ROOT / name
        if path.is_symlink() or not path.resolve().is_relative_to(ROOT) or name in listed:
            errors.append('Unsafe or duplicate hash path: ' + name)
            continue
        listed.add(name)
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            errors.append('Hash mismatch: ' + name)
    actual = {p.relative_to(ROOT).as_posix() for p in ROOT.rglob('*') if p.is_file() and p.name != 'TASK_HASHES.sha256' and '__pycache__' not in p.parts}
    if actual != listed:
        errors.append('Hash manifest membership differs from catalog; run --refresh')
    return errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--refresh', action='store_true', help='regenerate task index and hash manifest')
    args = parser.parse_args()
    tasks, errors = read_tasks()
    errors += validate_release_routes(tasks)
    if args.refresh and not errors:
        generate(tasks)
    errors += validate_links_and_coverage(tasks) + validate_hashes()
    if errors:
        print('\n'.join(errors))
        raise SystemExit(1)
    print(f'Validated {len(tasks)} tasks: metadata, exact IDs, dependency DAG, prompts, scopes, evidence/security sections, local links, coverage routes and SHA-256 manifest.')
