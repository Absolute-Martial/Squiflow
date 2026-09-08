# CSV Audit and Source Cleanup

**Version:** v0.0.15

## What the CSV files were

The earlier architecture ZIP contained two generated CSV files:

1. `00_governance/08_DOCUMENT_INVENTORY.csv`
   - machine-generated file inventory;
   - path/size/word-count/hash data;
   - useful for package integrity, not architecture decisions.

2. `00_governance/52_V13_EVERY_FILE_REVIEW_LEDGER.csv`
   - machine-generated V13 review/classification ledger;
   - recorded role/currentness/flags/review action for each file at that time.

## What was wrong with treating them as current design documents

The v0.0.15 ZIP had **267 files**, but both CSVs had only **248 data rows**. They were generated before later V14/V15 additions, so they were stale by construction.

The V13 ledger also encoded V13-era classifications. Example problems found during review:
- Grafana Cloud documents were still classified `current-supporting` even after later decisions made New Relic + Aiven OpenSearch the current managed observability targets;
- the ledger's action text repeatedly said implementation must satisfy the `V13` master plan even though later architecture versions had superseded that entry point;
- later tenant-permission, custom-domain, identity and Web strategy documents were absent from the ledger;
- keyword flags such as `provider-specific-persistence-language` are mechanical signals, not semantic proof that a document is wrong/current.

Therefore the CSV could make an engineer believe a stale classification was an architecture decision.

## Current policy

Do **not** use CSV inventories/review ledgers as design sources of truth.

GitLab keeps human-readable current architecture/decision documents instead.

If inventory/hash/coverage data is useful later, generate it in CI as a build/release artifact rather than committing it as an architectural authority.

The current source precedence is:

1. `MASTER_IMPLEMENTATION_PLAN.md`;
2. `docs/decisions/CURRENT_DECISIONS.md`;
3. focused current docs under `docs/`;
4. `docs/decisions/OPEN_DECISIONS.md` for intentionally unresolved items;
5. historical/review artifacts only for traceability.

## Why Markdown instead of CSV here

The important information is semantic: *why* a classification changed and which decision is current. Markdown is easier for engineers to review in GitLab and less likely to be mistaken for a machine-authoritative truth table.
