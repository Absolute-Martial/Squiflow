# Phase 4E — Integrated Phase-4 Gate

Phase 4 is complete enough when:

- a months-old supported Workstation follows an explicit compatibility path;
- unsupported protocol/schema states fail explicitly;
- at least one real aggregate conflict is resolved without generic last-write-wins;
- resnapshot preserves pending local intent and attachments/evidence;
- rebase has deterministic/reviewable outcomes;
- permission/device/config/limit authority is refreshed before protected admission;
- low disk/slow network/interrupted resnapshot are recoverable;
- the Workstation explains recovery states in business/operator terms.

Passing Phase 4 means the long-offline/conflict/recovery foundation is reusable. It does not freeze retention windows or conflict policies; those remain capability/profile-specific and may evolve through the compatibility framework.