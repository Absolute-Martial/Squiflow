# Product Identity and Versioning

**Status:** Current focused owner

**Product version:** `v0.1.0`, locked

## 1. Development codename boundary

`SquiFlow` is the repository's internal development codename. It is not an assumed product, tenant, deployment, company or legal name.

The codename may remain in the repository hosting name, explanatory repository documentation and historical decision, phase and research records. Active compiled/build identities use the neutral `Application.*` family until an actual product identity is selected. This includes solution/project filenames, project directories, C# namespaces, assembly names, test identities and synthetic local resource names.

`Application.*` is an implementation placeholder, not a public product brand. Public and deployment-facing behavior obtains its identity from validated configuration.

The codename must not be used as:

- a default or fallback display, short, legal or publisher name;
- user-facing API problem detail, endpoint description, email, report, export or UI text;
- a public telemetry meter/instrument namespace;
- a new environment variable, connection-string key, external client identifier or provider resource name;
- a new wire, persistence or interoperability discriminator;
- a tenant-specific brand or an authorization boundary.

Existing database schema names and migration identifiers remain capability contracts because changing them would alter persistence compatibility. CLR namespaces embedded only in EF model snapshots may move with the neutral source identity when model/migration agreement remains proven.

## 2. Public identity source

Deployment-wide public identity comes from the validated `BrandProfile` returned by `GET /api/v1/application/bootstrap`.

The checked-in CoreApi configuration contains blank branding values. Startup fails when a deployment does not supply a complete valid brand. There is no codename fallback.

The configured brand includes:

- display, short and legal names;
- an allow-listed theme key;
- logo and favicon locations;
- support, privacy and terms locations;
- a deterministic revision for cache invalidation.

Tenant-specific branding remains a future published Tenant Application Profile concern. It must never be inferred from a hostname or untrusted tenant candidate before tenant authority is established.

## 3. Product-version lock

The product version is exactly `v0.1.0`. `VERSION`, `CURRENT_VERSION.txt` and .NET assembly/file/informational metadata must agree.

No later product version is assigned merely because another capability, phase, commit, migration or internal build is added. Git commit IDs and deployment provenance identify individual builds while the product version remains `v0.1.0`.

The following are independent compatibility identifiers and do not change the product version:

- `/api/v1` and later API contract-family versions;
- database migration identifiers;
- persisted schema and message versions;
- Tenant Application Profile revisions;
- capability implementation revisions;
- Git commit IDs and deployment IDs.

## 4. Gate for leaving v0.1.0

Changing the product version requires an explicit reviewed decision showing that the complete accepted product is capable of real production operation. At minimum:

- required Web and Workstation journeys operate end to end for the accepted product scope;
- identity, tenant authorization, tenant data isolation and administrative trust boundaries are implemented and qualified;
- central and local durability, synchronization, background work and recovery cover their accepted production responsibilities;
- installation, upgrade, migration, backup, restore, monitoring and support paths are proven on the intended deployment profile;
- required security, concurrency, failure, capacity, compatibility and observability evidence exists;
- all introduced material responsibilities are `PRODUCTION_HONEST` and `BLOCKED = none`;
- the release is usable for real business operation without relying on prototype-only shortcuts.

The version change must update the lock in `Directory.Build.props`, both root version marker files, current owner documents and its permanent regression guard in one reviewed change.

## 5. Permanent guards

- `Directory.Build.props` fixes package, assembly, file and informational versions and fails the build when overridden inconsistently.
- Branding construction rejects missing or unsafe public identity values.
- CoreApi's checked-in branding values are blank so a deployment cannot accidentally publish the codename.
- CoreApi tests prove the bootstrap response contains only the configured brand.
- CoreApi tests prove active project, solution and compiled assembly identities do not contain the development codename.
- Runtime-facing codename scans are part of review whenever source identity, configuration, API messages, telemetry or public presentation changes.
