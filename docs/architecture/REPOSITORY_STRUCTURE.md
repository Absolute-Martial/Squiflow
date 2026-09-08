# Repository and Deployment Boundaries

**Version:** v0.0.15

```text
SquiFlow/
├── apps/
│   ├── web/          # tenant Web + tenant-owner Settings
│   ├── admin-web/    # SquiFlow platform administration
│   └── desktop/      # Windows Workstation
├── services/
│   ├── core-api/     # ASP.NET Core HTTP/composition host
│   └── worker/       # durable background execution
├── modules/          # business capability modules
├── packages/         # stable shared primitives/contracts
├── persistence/
│   ├── abstractions/
│   └── reference/    # provider proof/reference adapters
├── infrastructure/
├── contracts/
├── tests/
├── benchmarks/
├── dev/
├── tools/
├── build/
├── deploy/
├── docs/
└── evaluations/
```

Web, Admin Web, Desktop, Core API and Worker are visible runtime/build/deployment boundaries. Business modules remain modular-monolith code and do not automatically become services.

Provider-specific reference projects prove candidates; their existence does not select the provider.
