# Local Reference Sources

This directory keeps curated upstream source snapshots used to evaluate or implement SquiFlow mechanisms.

## Boundary

- `snapshots/` is a local research workspace. It is not product source, an implementation claim, or a package dependency.
- Production/test projects must not reference files below this directory.
- Snapshots contain selected source, tests, documentation and license files only. They contain no upstream `.git` history, object database, branches, hooks or credentials.
- Each snapshot is pinned to the immutable revision recorded in `SOURCES.md` and its local `_SQUIFLOW_SNAPSHOT.md`.
- The snapshot tree is ignored by Git because upstream source is bulky and retains its own license. The manifest and materializer are tracked.
- Keep the local snapshots until the SquiFlow project is completed or the user explicitly requests their removal.

## Selection rule

Materialize only sources admitted by a current focused owner or source review:

1. selected runtime mechanisms;
2. direct or focused package candidates;
3. bounded donors whose inspected implementation/tests are still useful.

Documentation-only references, rejected alternatives and very large unrelated platforms stay as links. Do not clone every project from the reference catalog.

License is not an admission filter for internal research. A source may be inspected regardless of its license when it has concrete architectural value. License, notice, redistribution and modification obligations are still recorded before any source is copied into product code or distributed; research admission never implies product adoption.

## Layout

```text
reference-sources/
  README.md
  SOURCES.md
  materialize.sh
  snapshots/                    # ignored, durable local workspace
    base-reference/             # complete source-only backend base used for selective adaptation
    selected-runtime/
    direct-candidates/
    poc-gated/
    donors/
```

## Materialization

Run from the repository root:

```bash
bash reference-sources/materialize.sh
```

For repository slices, the script first reuses matching shallow/source checkouts under `/tmp` from the research session. If one is absent, it fetches only the pinned revision into a temporary shallow repository, archives the allow-listed paths, and discards Git metadata. For a small explicit file list in a very large repository, it downloads the files directly from the immutable revision. Existing snapshots are never overwritten automatically.

A source upgrade is a review action: change the pinned revision and selection in `SOURCES.md` and `materialize.sh`, materialize into an empty destination, re-run the relevant source-admission review, and update the focused owner if the decision changes.

## Rider

Rider MCP is the preferred way to inspect SquiFlow files and navigate the curated snapshots. The ignored snapshot tree must not be added as solution projects or treated as repository implementation. Open individual source files when comparison is needed.
