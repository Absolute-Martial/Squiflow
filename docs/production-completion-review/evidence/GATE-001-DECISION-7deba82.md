# GATE-001 qualification decision — current backend entry baseline

**Disposition: `ACCEPTED` for its declared narrow scope, with two recorded qualifications.**

| Field | Value |
|---|---|
| Task | `docs/development-tasks/phase-00-baseline/GATE-001-qualify-the-current-backend-entry-baseline.md` |
| Baseline SHA | `7deba82d4c1a15dfc63acd660c8d99209b97f9c1` ("Ignore local tool cache directory") |
| Verified against remote `main` | `7deba82d4c1a15dfc63acd660c8d99209b97f9c1` — identical, no drift |
| Working method | Isolated clone; archive extracted separately; no work over the active checkout |
| Product version | `v0.0.1` (unchanged) |
| Decision date (UTC) | 2026-10-04 |
| Reviewer | opencode (Claude), monitoring agent |

## 1. Package and source identity

`Application-main-monitoring-handoff.zip` SHA-256 matched its sidecar
(`cd4b3657…dff7df`). After extraction, `python3 verify_package.py --restore-modes`
verified **1157 files** against baseline `7deba82`, including executable-mode
restoration. The archive supplied source, complete task files and 205 recovered
historical evidence files; it intentionally excludes Git metadata and build output.

The clone's `HEAD`, local `main` and `ls-remote origin main` all resolved to
`7deba82`. The tracked `docs/production-completion-review/BASELINE.json` still named
`18aae19`; that historical record was **preserved, not overwritten**. A fresh
manifest is retained at `BAS-001-MANIFEST-7deba82.txt`.

## 2. BAS-001 — fresh manifest

Recorded in `BAS-001-MANIFEST-7deba82.txt`: full SHA, clean tracked tree, remote
agreement, `v0.1.0`-era version markers re-read as `v0.0.1`, and live toolchain
capability (.NET SDK 10.0.401 matching `global.json`, Docker 29.8.1, writable NuGet
global-packages cache).

Catalog validated **without `--refresh`**:

```
Validated 119 tasks: metadata, exact IDs, dependency DAG, prompts, scopes,
evidence/security sections, local links, coverage routes and SHA-256 manifest.
```

No task metadata, hash manifest or dependency edge was rewritten.

## 3. Prerequisite receipts (executed sequentially, current source)

| Task | Named checks executed on `7deba82` | Result | Disposition |
|---|---|---|---|
| **ADM-001** membership + tenant lifecycle | `TenantMembershipLifecyclePostgresTests` (real PostgreSQL 17); `MembershipLifecycleBoundaryTests` (real host + OpenFGA) | 11 passed 0 failed 0 skipped; 4 passed 0 failed 0 skipped | **ACCEPTED** for narrow scope |
| **ADM-002** protected request budgets | `RequestBudgetTests` through the real ASP.NET pipeline; `PublicHealthBearerReviewTests` | 20 passed 0/0; 14 passed 0/0 | **ACCEPTED** for narrow scope |
| **OPS-022** parallel Testcontainers startup | 6 concurrent fresh `Application.IdentityAccess.Postgres.Tests` processes; container observation during run; residue check after exit | 6/6 exit 0; **114 passed 0 failed 0 skipped**; peak 33 containers; **0 residue** (0 running, 0 total) | **ACCEPTED** for focused harness scope |

ADM-001's dedicated historical receipt was absent, as LUNA-CHECK reported. It is now
replaced by current-source real-provider evidence rather than by relabelling the older
`18aae19` pass summaries. The earlier failed run
(`artifacts/verification/2026-10-03-shared-backend-integration/application-tenancy-lifecycle.log`,
17 failures) is retained unmodified as failure history and is **not** presented as passing.

## 4. ADM-003 reconciliation (both records)

The two production authentication registrations still match the recovered review hashes
**exactly**:

```
MATCH  services/admin-api/Application.AdminApi/Authentication/AdminApiAuthenticationRegistration.cs
MATCH  services/core-api/Application.CoreApi/Authentication/CoreApiAuthenticationRegistration.cs
```

Four reviewed paths differ — `PublicHealthBearerReviewTests.cs`,
`PublicHealthBearerTests.cs`, `ADMIN_API_REQUEST_BUDGETS.md`, `CORE_API_REQUEST_BUDGETS.md`.
The deltas come from `4dc4082` and `92230ba` (test-harness watchdog separation,
deterministic cancellation, cleanup draining, and owner-doc revision). **No production
authentication code changed.** Those deltas were covered by re-running the affected
suites on this exact source: **17 + 14 + 9 = 40 passed, 0 failed, 0 skipped**.

The tracked receipt `ADM-003-INDEPENDENT-REVIEW-RECEIPT.md` remains the acceptance
record, with its disclosed reviewer-independence caveat. The recovered ignored review
artifact lacks a named reviewer, exact HEAD and literal disposition; those omissions are
**not** attributed to the tracked receipt. Second-party confirmation of reviewer
independence remains an accountable gate-owner decision, recorded here as outstanding
rather than assumed.

## 5. Fresh normal gate (one uncontended run)

`./eng/verify.sh` at 11:15:48Z → 11:19:34Z on the frozen integrated source:

| Step | Result |
|---|---|
| `dotnet restore --locked-mode` | pass |
| `dotnet format --verify-no-changes` | pass |
| Release build | **0 warnings, 0 errors** |
| Tests | **741 passed · 0 failed · 0 skipped** · 16/16 projects |

Architecture 14 · Branding 11 · Customers 21 · IdentityAccess 12 · Invoices 25 ·
Orders 86 · AdminBootstrap 3 · PlatformAdministration 4 · IdentityAccess.Postgres 19 ·
Profiles 18 · Tenancy 29 · CoreApi 333 · AdminApi 87 · Tenancy.Postgres 19 ·
Customers.Postgres 17 · Orders.Postgres 43.

Full log: `GATE-001-VERIFY-7deba82.log`. No retry, serialization workaround,
ResourceReaper disablement or fake provider was used. Every historical failure record was
preserved.

Independent host checks, run sequentially per `docs/implementation/INDEPENDENT_HOST_BUILDS.md`:

| Script | Exit | Host regression project |
|---|---|---|
| `./eng/verify-host.sh core-api` | 0 | 333 passed, 0/0 |
| `./eng/verify-host.sh admin-api` | 0 | 87 passed, 0/0 |
| `./eng/verify-host.sh admin-bootstrap` | 0 | 3 passed, 0/0 |
| `./eng/verify-host.sh db-migrator` | 0 | 19 passed, 0/0 |

Each reported 0 warnings / 0 errors.

## 6. Decision

`GATE-001` is **ACCEPTED for its declared narrow scope** on baseline `7deba82`:
prerequisite receipts are retained with explicit dispositions, ADM-003's production
auth code is unchanged and its test/doc deltas are re-verified on this source, and one
uncontended normal parallel gate plus all four independent host checks pass with zero
failures and zero skips.

### Qualifications and non-claims

1. **Reviewer independence for ADM-003 is not settled here.** The tracked receipt's
   author was not independent of other work in this repository. If the gate requires
   organizational independence, a second-party confirmation is still required. This
   decision accepts the technical evidence, not that governance question.
2. **Local Testcontainers only.** No remote CI, live ZITADEL/OpenFGA provider,
   deployment or production claim follows.
3. **Not whole-product acceptance.** This gate covers the backend entry baseline only.
   No invoice allocation, numbering, business-date, deployment or product-wide gate is
   implied. Invoice runtime remains host-neutral-only; no durable invoice adapter or API
   is claimed.
4. **Phase 1 implementation remains gated.** Units requiring GATE-001 (ADM-006, ADM-009,
   ADM-018, ADM-025, ADM-034) are unblocked by this acceptance; every other prerequisite
   in its full task file still applies.
5. `BLOCKED = none` introduced by this qualification.

## 7. Retained evidence

`docs/production-completion-review/evidence/`: `BAS-001-MANIFEST-7deba82.txt`,
`GATE-001-VERIFY-7deba82.log`, `OPS-022-concurrent-run-{1..6}.log`, this decision, and
the pre-existing tracked ADM-003 receipt. Recovered historical evidence remains under
ignored `artifacts/` and is unmodified.