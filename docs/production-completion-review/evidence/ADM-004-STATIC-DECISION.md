# ADM-004 static topology decision and receiving evidence — 2026-10-04

**Baseline Git HEAD:** `c652db5e4ce722f3289dadddda6c0bcd5e1dcffa`  
**Disposition:** topology decision implemented; live ZITADEL Cloud qualification `EVIDENCE_PENDING`

## Accepted static decision

The current source now owns one explicit initial provider topology:

- ZITADEL Cloud, one instance/issuer per deployment environment;
- one SquiFlow-owned provider Organization for the initial projects;
- separate **Tenant Access** and **Platform Admin** ZITADEL projects;
- distinct Tenant and Platform Admin applications/audiences;
- stable SquiFlow account key `(issuer, subject)`;
- one human may belong to multiple SquiFlow tenants through local memberships without provider identity duplication;
- `ZITADEL OrganizationId` is never equal-by-contract to `SquiFlow TenantId` and is never tenant authority;
- tenant Owner/Staff authority remains SquiFlow membership + OpenFGA/domain authorization;
- platform administration uses its distinct audience and still requires the private Admin-device + platform OpenFGA boundary;
- existing-human import/link uses a dedicated non-human provider service identity with the smallest qualified **read-only** provider role; instance-owner/write credentials are not acceptable runtime scope;
- customer-specific provider Organizations, enterprise federation/custom domains, provider-side human creation/invitation and delegated provider administration remain outside the accepted baseline until separately qualified.

Canonical owner: `docs/implementation/ZITADEL_LIVE_TOPOLOGY.md`.

## Permanent static guards executed

```text
python3 -m py_compile eng/qualify-zitadel-topology.py eng/test-qualify-zitadel-topology.py
=> exit 0

python3 eng/test-qualify-zitadel-topology.py
=> ADM-004 topology static regressions: PASS

python3 eng/qualify-zitadel-topology.py \
  --inventory deploy/zitadel/adm-004-topology.example.json \
  --static-only
=> exit 0; live = NOT_RUN_STATIC_ONLY
```

The regression harness proves that the checked-in tooling rejects:

- one shared Tenant/Admin project ID;
- one shared Tenant/Admin API audience;
- insecure non-loopback HTTP callbacks;
- insecure Admin HTTP callbacks;
- owner-shaped or write-shaped verifier role declarations.

The example inventory is deliberately non-secret and placeholder-only. Static success is not live provider qualification.

## Exact live evidence still required

Authorized nonproduction ZITADEL Cloud access was not supplied to this receiving environment. Before ADM-004 can be `PRODUCTION_HONEST`, run the read-only harness against a real inventory and retain reviewed/redacted evidence for:

1. exact OIDC discovery issuer and HTTPS same-origin authorization/token/JWKS endpoints;
2. one known human subject read through ZITADEL V2 `GET /v2/users/{id}`;
3. one known service/machine subject read proving it is non-human and therefore rejected by interactive import semantics;
4. one intentionally absent subject producing safe provider non-success;
5. independently inspected provider role-assignment evidence proving the runtime verifier credential is the smallest read-only role that covers intended importable users and is not an instance-owner/write credential;
6. distinct live Tenant/Admin project/application/audience IDs and exact redirect inventory;
7. one provider human resolving one existing SquiFlow account with memberships in at least two local tenants without duplicate identity binding;
8. export/reprovision inventory plus known-subject account-link recovery evidence preserving exact issuer/subject identity.

Secrets, tokens, client secrets, private keys, recovery codes, raw user subject IDs, user profile/email/phone data and session material must not enter repository evidence; the harness emits subject SHA-256 fingerprints instead.

## Non-claims

This record does not qualify provider-side account creation, invitations, Web session persistence, Workstation native callback packaging, enterprise SSO, customer custom domains, provider organization lifecycle, provider role synchronization, account suspension/recovery or self-hosted ZITADEL.

## Requalification triggers

Use the triggers in `docs/implementation/ZITADEL_LIVE_TOPOLOGY.md`; any issuer, project/application layout, API audience, verifier authentication/role, provider user contract, tenant/provider-organization policy or recovery-procedure change invalidates the relevant evidence.

## Receiving normal-gate attempt

The supplied offline .NET SDK 10.0.401 archive passed its SHA-512 sidecar and installed successfully. `./eng/verify.sh` was then attempted on this exact working tree. It could not complete locked restore because this receiving container cannot reach `https://api.nuget.org/v3/index.json`; restore reported `NU1301` / `Resource temporarily unavailable`. No formatting/build/test result is claimed from that interrupted gate. This is an environment limitation, not live ZITADEL evidence.
