# ADM-004 ZITADEL deployment inventory and qualification

This directory contains **non-secret** deployment material for the accepted ADM-004 ZITADEL Cloud topology.

- `adm-004-topology.example.json` is a template only. Copy it outside source control for a real environment inventory or render an equivalent deployment artifact from your secret/configuration system.
- `eng/qualify-zitadel-topology.py` is read-only. It performs no user, project, application, organization, role or credential mutation.
- Bearer tokens and known test subject IDs are supplied through environment variables. The script never prints the bearer value or user profile fields.

## Static inventory check

```bash
python3 eng/qualify-zitadel-topology.py \
  --inventory deploy/zitadel/adm-004-topology.example.json \
  --static-only
```

Static-only mode permits the checked-in placeholders and proves the inventory shape, distinct Tenant/Admin project/audience contract and redirect constraints. It is **not** live provider qualification.

## Live nonproduction qualification

Prepare a real non-secret inventory with no placeholders and SHA-256 references to separately retained/redacted scope and recovery evidence, then supply:

```text
ZITADEL_API_TOKEN       dedicated narrow read-only verifier credential
ZITADEL_HUMAN_SUBJECT   known safe human test identity
ZITADEL_MACHINE_SUBJECT known safe service/machine identity
ZITADEL_UNKNOWN_SUBJECT optional intentionally absent subject probe
```

Run:

```bash
python3 eng/qualify-zitadel-topology.py --inventory /secure/path/nonprod-zitadel-inventory.json \
  > adm-004-live-qualification.json
```

The live run verifies OIDC discovery, exact issuer, HTTPS endpoints, a known human, a known non-human identity and unknown-subject non-success. It also rejects identical Tenant/Admin project IDs or audiences and rejects an owner/write-shaped declared verifier role.

The script cannot prove provider least privilege from a bearer token alone. The inventory therefore requires `identityVerifier.roleAssignmentEvidenceSha256`, a SHA-256 reference to independently retained **redacted** provider role-assignment evidence. The reviewer must inspect that evidence and verify that the runtime service identity has only the smallest provider read role that permits the accepted user lookup. Instance-owner credentials are not accepted runtime evidence.

Likewise, `recoveryEvidence.*Sha256` refers to separately retained redacted configuration/export and known-subject account-link recovery evidence. Do not copy tokens, client secrets, private keys, recovery codes, profile/email/phone data or session material into this repository.

## Required deployment configuration mapping

For each environment:

```text
CoreApi Authentication:Authority  = inventory.issuer
CoreApi Authentication:Audience   = inventory.tenantAccess.apiAudience
AdminApi Authentication:Authority = inventory.issuer
AdminApi Authentication:Audience  = inventory.platformAdmin.apiAudience
AdminApi IdentityProvisioning:Zitadel:ApiUrl = same issuer origin
AdminApi IdentityProvisioning:Zitadel:ApiToken = secret runtime verifier token
```

Tenant and Platform Admin audiences must be different. Production and nonproduction must not share an issuer/application credential set.

See `docs/implementation/ZITADEL_LIVE_TOPOLOGY.md` for the authoritative topology, exclusions, recovery contract and requalification triggers.
