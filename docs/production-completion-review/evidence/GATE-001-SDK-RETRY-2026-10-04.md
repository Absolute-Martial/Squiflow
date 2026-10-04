# GATE-001 SDK/runtime retry receipt — 2026-10-04

## Exact source and toolchain

- Git HEAD under qualification: `18aae19d7441018bc186b902bd5b61beddd5169e`.
- Uploaded SDK archive: `Squiflow-offline-sdk(1).zip`.
- SDK archive SHA-256: `675016a1fb3df3ff3c1d62110b395e6524b295f15da962e055cc5cf63facda89`.
- Bundled SDK tarball SHA-512: `51c8b999af9e8dd9998c9edc5944e19a90788862068acd38694e098889054ce8c23d4f0c5cccfa16bf187d044562359e5ee69a9f8ad0bbe913ba90311fbce25b`; it matches the supplied checksum file.
- Installed SDK: `.NET SDK 10.0.401`.
- Installed runtimes observed: `Microsoft.NETCore.App 10.0.12` and `Microsoft.AspNetCore.App 10.0.12`.

The supplied installer verifies the SDK tarball correctly when invoked from its own directory. It currently reads the checksum sidecar relative to the caller working directory before resolving the archive from `script_dir`, so invoking the script by absolute path from another directory fails before installation. The SDK payload itself is valid and was installed without modifying repository source.

## Normal-gate attempts

The first local `./eng/verify.sh` attempt reached MSBuild but failed before restore because this sandbox injects `PLATFORM=linux/amd64`; MSBuild interpreted that as the solution platform and rejected `Debug|linux/amd64`. The repository tracks `Any CPU` solution semantics. This is an execution-environment collision, not a source/test failure.

The command was then rerun with only the host-injected `PLATFORM` variable removed while preserving the repository command and SDK version. It reached the required locked restore and repeatedly failed with `NU1301` because `https://api.nuget.org/v3/index.json` cannot be resolved/reached from this sandbox. Direct DNS queries to `1.1.1.1` and `8.8.8.8` also timed out. The local NuGet package cache is empty. The retry was stopped after the repeated source-unavailable errors had established the environment blocker; no build/test success is inferred from this attempt.

The active repository lockfiles require **123 exact package/version pairs**. The machine-readable list, including every available lockfile content hash and referring project, is [GATE-001-NUGET-MANIFEST.json](GATE-001-NUGET-MANIFEST.json). An offline cache matching these locked versions is therefore required before this container can compile the full solution.

## Docker/Testcontainers availability

No `docker`, `dockerd`, `podman`, `containerd`, `nerdctl` or `ctr` executable is available in this container, and `/var/run/docker.sock` is absent. No Docker API listener is present on the local/default gateway probes. The provider-backed PostgreSQL/OpenFGA tests therefore cannot run here even after package restore unless a working Docker-compatible daemon/socket is supplied to the execution environment.

## Gate disposition

GATE-001 remains `BLOCKED` on environment evidence, not on a newly demonstrated product defect. The SDK prerequisite is now satisfied. The remaining requirements are:

1. make the locked NuGet graph available locally (or restore it from a reachable feed) using the exact manifest above;
2. provide a working Docker/Testcontainers daemon/socket;
3. run one uncontended normal `./eng/verify.sh` on this exact source with the sandbox `PLATFORM` collision removed;
4. inspect final exit plus every suite's pass/fail/skip summary before changing GATE-001 to `PRODUCTION_HONEST`.

No provider-backed test was skipped, mocked, serialized away or reclassified as passed during this retry.
