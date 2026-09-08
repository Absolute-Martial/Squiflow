# Repository and Deployment Boundaries

**Version:** v0.0.15

The repository structure describes **real responsibilities**, not a requirement to create empty projects/directories before they are needed.

```text
SquiFlow/
├── apps/
│   ├── web/          # tenant staff Web + tenant-owner Settings
│   ├── admin-web/    # SquiFlow platform administration/control plane
│   └── desktop/      # Windows Workstation + Guard/on-demand helpers
├── services/
│   ├── core-api/     # ASP.NET Core HTTP/composition host
│   └── worker/       # durable background execution
├── modules/          # business capability modules
├── packages/         # stable shared primitives/contracts
├── persistence/
│   ├── abstractions/
│   └── reference/    # provider proof/reference adapters only when actively evaluated
├── infrastructure/   # object storage, telemetry, identity/provider integrations
├── contracts/        # wire contracts only when a separate boundary is useful
├── tests/
├── benchmarks/       # only measured qualification/POCs that matter
├── dev/
├── tools/
├── build/
├── deploy/
└── docs/
```

## Runtime boundaries

Web, Admin Web, Desktop, Core API and Worker are genuine build/run/deployment boundaries.

Business modules remain modular-monolith code and do not automatically become services.

## Creation rule

Do not create a project/file merely because the architecture diagram has a box.

Create a separate project only when it provides at least one real boundary:
- dependency direction;
- independently built/deployed executable;
- provider adapter under active evaluation;
- test/benchmark isolation;
- security/fault/process isolation;
- stable contract shared across runtimes.

If `Manager → Service → Executor → Handler` only forwards calls, collapse it.

## Provider reference projects

Provider-specific projects can be created under `persistence/reference`/`infrastructure/reference` to prove a candidate.

Their existence means “we are evaluating/proving this adapter,” not “the product has selected this provider.”

## Web/control-plane separation

- `apps/web`: tenant business Web + tenant-owner Settings/Administration.
- `apps/admin-web`: SquiFlow platform control plane only.
- `apps/desktop`: local-first business client; never tenant/platform permission management or platform server controls.

## ASP.NET Core host pattern

`services/core-api` owns HTTP routing/composition, similar in role to a separate Axum host project in a Rust solution.

Domain/application logic lives in modules and should not depend on ASP.NET Core or provider-specific infrastructure.
