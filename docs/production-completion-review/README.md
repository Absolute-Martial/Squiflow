# Departmental production-completion review

Product version: **v0.0.1**. This is a planning/review package, not a production qualification or permission for a delegate to start broad implementation. Root orchestrated nine bounded GPT-6 Luna (high) staff roles across business, development/integration and security/operations. No production source was changed by these reviewers.

Start with [sequential milestones](SEQUENCE.md), then [all catalog units in a valid serial dependency order](ALL_CATALOG_UNITS.md). The existing [development catalog](../development-tasks/README.md) retains full implementation prompts, scopes and dependencies; these are assignments, not a count of missing features. Its [current truth owner](../../README.IMPLEMENTATION.md) and focused accepted decisions remain authoritative.

| File | Use |
|---|---|
| [DEPARTMENTS.md](DEPARTMENTS.md) | Three departments, three separate review roles each, reusable assignment prompts |
| [SEQUENCE.md](SEQUENCE.md) | Understand the order, independent tracks, first units to dispatch and each exit outcome |
| [ALL_CATALOG_UNITS.md](ALL_CATALOG_UNITS.md) | Select one of the existing units with prerequisites and model route; never assign the entire table |
| [RECONCILIATION.md](RECONCILIATION.md) | Verified irregularities, rejected/qualified hypotheses and remaining deltas |
| [END_TO_END_CHECKS.md](END_TO_END_CHECKS.md) | Real operation flows and hostile/interrupted cases that must connect |
| [PRODUCTION_TARGET.md](PRODUCTION_TARGET.md) | Distinguish backend/browser delivery from complete-product production acceptance |
| [HAR-001](assignments/HAR-001.md) | Single bounded cancellation-test cleanup repair assignment under ADM-002 |
| [FUTURE_BOUNDARIES.md](FUTURE_BOUNDARIES.md) / [FUT-001](assignments/FUT-001.md) | Missing later-product task chain and one bounded planning assignment to earn it |
| [BASELINE.json](BASELINE.json) | Exact HEAD plus dirty source hashes; old passes are not current evidence |
| [review-roster.json](review-roster.json) | Actual reviewer model, role and report status |
| [VERIFICATION.json](VERIFICATION.json) | Package checks actually performed; dynamic tests remain explicitly unrun |
| [HASHES.sha256](HASHES.sha256) | Integrity of the review package files |

## How to use one assignment

Attach its full existing task file, shared AGENT_RULES/HANDOFF instructions and your exact code ZIP with SHA-256. Record one outcome, permitted files, accepted prerequisites and exclusions. Ask the author for implementation, focused permanent tests, static security review, exact attempted/unrun dynamic checks and source-only ZIP/hash. A second independent reviewer checks hostile cases and the actual target property. Environments without SDK/Docker/live providers can deliver source and tests, but cannot close their dynamic evidence. The receiving environment runs those checks before acceptance.

The nine review prompts here are deliberately read-only and Luna-sized. They cannot settle money, authorization, device transport or durability decisions. Existing stronger-model implementation/review routes remain unchanged; a model's agreement never replaces owner acceptance or actual runtime evidence.

Source may evolve during a review. Baseline hashes are a snapshot, not a Git commit of dirty files. Reconcile again before each implementation handoff. No commit or push is included. Details of what was actually checked are in VERIFICATION.json.

The downloadable archive contains this review folder and the existing assignment catalog; it does not contain application source or Git history. Supply the separately identified code baseline to each implementing agent.
