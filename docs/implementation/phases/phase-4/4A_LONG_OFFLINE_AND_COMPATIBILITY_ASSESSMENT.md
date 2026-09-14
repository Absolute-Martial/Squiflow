# Phase 4A — Long-Offline and Compatibility Assessment

## Required startup/reconnect assessment

When a Workstation returns after a long absence, determine whether incremental sync can safely continue before applying remote or local changes.

Relevant evidence may include:

```text
Workstation application version
local SQLite schema version
Sync protocol / OperationEnvelope versions
last successful semantic sync/cursor
rule/configuration revisions
permission snapshot revision
server change-history retention position
pending local operation count/age
```

Only evidence that materially affects compatibility/recovery needs to be carried; do not create one universal version number or giant handshake object for unused dimensions.

## Outcomes

Distinguish at least as applicable:

- incremental sync allowed;
- authentication refresh required;
- client upgrade required;
- local migration required;
- permission/config refresh required;
- resnapshot/rebase required;
- device revoked/re-enrollment required;
- unsupported/incompatible state requiring explicit recovery.

## Safety

Never silently interpret an unsupported old contract as current semantics and never delete pending local intent merely because the incremental history window expired.

## Tests

Exercise weeks/months offline, expired sessions, old app/schema/protocol, compacted change history, revoked device, changed role/rules/settings, slow network and low disk.

## Exit gate

Reconnect begins with an explicit compatibility/recovery decision rather than blind incremental sync.