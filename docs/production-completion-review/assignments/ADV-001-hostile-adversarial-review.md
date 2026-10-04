# ADV-001 — Hostile adversarial review of the current tree

Review the repository by attempting to **break** it, not by confirming it works. The objective is
to find defects an ordinary green gate cannot see: authorization bypass, existence disclosure,
injection, cross-tenant reach, race and replay defects, resource exhaustion, secret leakage, and
false evidence.

This is a review-only assignment. It introduces no feature, changes no product decision, and does
not qualify any gate.

## Non-negotiable rules

1. **Do not modify any file in the repository.** No fixes, no formatting, no test edits, no
   `.gitignore` edits. Scratch work goes in `/tmp`. If you find a defect, report it; do not repair it.
2. **Do not weaken, skip, delete or "fix" a failing test to reach green.** A failing test is
   evidence. Report it.
3. **No retry-until-green, no serialization to dodge a race, no analyzer suppression, no
   `ResourceReaper` disablement, no fake/stub provider, no test double promoted into production
   code.** If a check is flaky, that is a finding about the check.
4. **Verify the environment before claiming it is unavailable.** Docker, .NET SDK, NuGet feed and
   provider reachability must each be *probed and reported*. Do not accept "no Docker" or "no
   network" from a previous message, a document, or an archived log — check it yourself. A claim
   that a check could not run is only acceptable with the exact command and its actual output.
5. **Never manufacture a result.** Report the exact command, exit code and counts. If you did not
   run something, say so plainly.
6. **No commit, no push, no branch, no git-state change.**

## Environment expectations

- Required: .NET SDK matching `global.json` (`10.0.401`), Docker, and the locked NuGet packages.
  `./eng/verify.sh` performs locked restore, format verification, Release build and the full suite.
- Real PostgreSQL 17 and OpenFGA run as Testcontainers. Prefer **real** provider evidence over any
  mock for any property that depends on SQL, RLS, constraints, triggers or OpenFGA evaluation.
- If the environment genuinely lacks something, you may still deliver source review plus
  *unexecuted* adversarial test code, but you must label the affected claims `EVIDENCE_PENDING` and
  list the exact unrun commands. Static review never substitutes for a real boundary.

## Baseline first

Establish and report, before attacking anything:

1. `git rev-parse HEAD`, `git status --porcelain` (must be clean for your review copy), and whether
   the tree you received matches the stated baseline.
2. `./eng/verify.sh` — record exit, warnings/errors, and every suite's passed/failed/skipped totals.
   Establish the true baseline count instead of trusting any recorded total.
3. `python3 docs/development-tasks/validate_catalog.py` — must pass without `--refresh`.
4. Inventory the real attack surface: every route in both hosts, every embedded `.sql` resource,
   every migration, every authorization relation, every embedded secret-like literal.

## Attack catalogue — attempt each, record pass or fail with evidence

### A. Authorization and authority ordering

For **every** protected route in CoreApi and AdminApi, prove or break this ordering:
authentication → current-principal/credential check → current-authority (pinned-model OpenFGA)
→ handler. Then try to invert it:

- Call the handler's business/persistence path with authority denied. Does any read, count, cursor
  parse or validation happen first?
- Send a request that would return *different* status or body shape for "exists" versus
  "does not exist" versus "not permitted". **Any observable difference is an existence-disclosure
  defect.** Compare 403 vs 404 vs 400 vs empty-body-vs-body precisely.
- Enumerate every route from the real endpoint table (`EndpointDataSource`) rather than trusting the
  route list in docs or OpenAPI.

### B. Credential and header forgery

- Anonymous requests to every protected route → expect `401`, `no-store`, empty/absent body detail.
- Valid bearer with **no** authority relation → `403` with no existence signal.
- Valid bearer + valid device, but **revoked/disabled** device or principal.
- Forged privileged headers (`X-Platform-Admin`, `X-Admin-Device`, forwarding/proxy headers,
  `Host`, `X-Forwarded-*`) on public *and* protected routes. Determine from source which headers
  any authority decision actually reads, then confirm none of them can promote a caller.
- Malformed, truncated, wrong-algorithm, wrong-issuer, `alg:none`, and audience-mismatched tokens.
- Certificate/identity binding: can one tenant's or one device's credential be replayed as another's?

### C. Injection and untrusted input

- **SQL injection** against every new/changed embedded `.sql`: single quote, double quote, backslash,
  comment sequences (`--`, `/*`, `#`), semicolon, dollar-quoting (`$$`, `$tag$`), Unicode and
  NUL, newline and CRLF, and very long values. Verify each statement uses parameters and cannot
  concatenate input. Try ORDER BY / LIMIT / identifier positions specifically — those commonly
  escape parameterization.
- **Path/route traversal** in route values, cursor payloads, cursor file names, OpenAPI paths.
- **Cursor and codec tampering**: forge a cursor, truncate it, flip one byte, reuse a cursor from a
  *different resource kind*, reuse a membership cursor against a *different tenant*, supply
  `Guid.Empty`, a non-`N` format GUID, an oversized payload, base64url vs standard base64, and a
  cursor naming an identifier the caller may not read.
- **JSON**: duplicate keys, `__proto__`-style keys, deeply nested payloads (depth bomb), huge
  numbers, invalid UTF-16 surrogates, mixed content types, wrong `Content-Type`, missing
  `Content-Type`, and a second body stream.

### D. Cross-tenant and provider-boundary reach

- With a **tenant-scoped** credential or database role, attempt every platform-wide and
  tenant-owned read. Confirm which statements are reachable by which granted role by reading
  `deploy/database/*.sql` grants *and* checking for `FORCE ROW LEVEL SECURITY`.
- Attempt to read or mutate another tenant's rows by substituting identifiers.
- Verify RLS actually engages: attempt a query with a wrong/absent tenant context and confirm the
  database refuses it rather than the application filtering it.
- Confirm runtime roles hold **no** DDL, no `UPDATE`/`DELETE` outside declared scope, and no
  `BYPASSRLS`/`SUPERUSER`.

### E. Concurrency, replay and atomicity

- Same idempotency key with **identical** intent concurrently → exactly one durable effect.
- Same key with **changed** intent → must conflict, never silently re-execute.
- Response lost after commit, then retried → must not duplicate the effect.
- Concurrent commits/revisions/abandonments on one aggregate → exactly one winner, revision monotonic.
- Cancellation mid-operation → no partial durable effect, no leaked permit/admission capacity, no
  orphan container/connection/transaction.
- Run these against **real** PostgreSQL, not in-memory substitutes.

### F. Resource exhaustion and availability

- Oversized page sizes, negative/zero/absent limits, duplicate query parameters, very long cursors,
  and unbounded iteration. Look for any unbounded materialization, missing cap, or N+1 provider call.
- Admission/capacity caps: saturate them and confirm `503`/`429` with `no-store`, then confirm
  capacity is **released** after success, failure and cancellation.
- Deadlines: verify a request cannot hang past its budget, that a deadline response is safe, and
  that a caller cancellation is not silently converted into a deadline response (and vice versa).
- Dependency outage: fail PostgreSQL or OpenFGA and confirm fail-closed, safe bodies, no partial
  writes, and no disclosure of provider internals.

### G. Secret and diagnostic leakage

- Force each error path and search responses, logs and exception text for connection strings,
  tokens, certificate material, internal type/stack detail, `traceId` correlation that leaks
  internals, tenant identifiers, or provider error text.
- Confirm every protected response is `no-store` and that cache headers cannot be cached publicly.
- Grep the tree for committed secrets, `.env`, keys, and credentialed connection strings.

### H. Evidence and documentation integrity (this class has found real defects here)

Treat the repository's own records as an attack surface, because **false evidence is a defect**:

- Every file referenced as retained evidence by a receipt, owner doc or status file: does it exist?
- Every SHA-256 in `HASHES.sha256`, `BASELINE.json`, `TASK_HASHES.sha256` and receipts: recompute
  and compare. Report every mismatch.
- Every recorded test total: does it match a run you actually performed? Flag any total that no
  current run reproduces, and any claim of `PASS`/`ACCEPTED`/`PRODUCTION_HONEST` without a
  reproducible artifact.
- Every `NOT_INTRODUCED` claim: confirm nothing implementing it exists in the tree.
- Every gate state (`BLOCKED`/`PRODUCTION_HONEST`/`ACCEPTED`): is it justified by a retained
  artifact, or is it asserted? Flag contradictions between documents, and any document still
  describing a resolved issue as open.
- Verify that no commit was left red: for the last 3–5 commits, check whether the tree at that
  commit builds and passes. Report any commit whose message implies green while its own tree is red.

## Deliverable

Return a single report containing:

1. **Summary table**: attack class → attempted → passed/broken → evidence reference.
2. **Findings**, ranked critical/high/medium/low. Each: what you attempted, the exact request or
   command, observed vs expected, `file:line`, why it matters, and the minimal remediation you would
   recommend (describe it; do not implement it).
3. **Confirmed non-issues**: attacks that did not work, with the evidence that proves it. This is
   valuable — do not omit it.
4. **Commands run**: exact, with exit codes and totals.
5. **Environment**: SDK, Docker, provider reachability, and anything you could not execute.
6. **Explicit disposition**: `NO_DEFECT_FOUND` / `DEFECTS_FOUND` / `EVIDENCE_PENDING`, plus what
   would change it.
7. **Non-claims**: no remote CI, no live provider, no deployment or production qualification was
   established by this review.

## Out of scope

Do not implement fixes, refactor, add dependencies, change configuration, modify CI, or expand
product scope. Do not create future projects to "test" an absent capability. Do not evaluate whether
the product *should* exist; evaluate whether the current implementation withstands the attacks above.
