# Phase 6 — Independent Platform Control and Background Execution

**Status:** direction only — independent Admin/Worker runtime boundaries are `NOT_INTRODUCED`

## Direction

This phase represents a likely maturity point where a real workload may earn an independent Platform Admin control plane and/or durable Worker runtime. These boundaries are not guaranteed to arrive together and are not created for symmetry.

## Known dependencies

A real control-plane operation must need independent security/availability, or a real durable background workload must need an independent execution lifecycle.

## Current preservation constraints

- platform permission/audit concepts may exist without an Admin executable;
- transactional consequence/outbox semantics may be introduced with real commits without prebuilding Worker;
- once durable work exists, process memory cannot be its source of truth;
- new processes require real lifecycle/fault/security/resource justification.

## Activation trigger

The first real Admin or Worker workload earns the boundary. At that time, its detailed gate is written from the actual workload, provider, authority and failure model.

The retired 6A–6F decomposition is planning history, not a contract. See `docs/implementation/FUTURE_PHASE_CARRY_FORWARD.md`.