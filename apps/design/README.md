# Application entry theme

`tokens.json` owns application light-theme values and semantic aliases. Colors in
views use `color.*` roles; `palette.*` is an input layer. Web and Workstation share
these meanings while keeping their existing host controls and authority boundaries.

Run `python3 eng/build-ui-tokens.py` after editing tokens or `web.css`. Commit the
generated Web `tokens.css`/`app.css` and native `Styles/DesignTokens.axaml` together
with their sources. `--check` fails when committed outputs drift. `eng/verify.sh`
runs the builder tests and freshness check; host builds check freshness as well.
Python 3 uses only the standard library and is not an application runtime dependency.

The adapter accepts opaque RGB colors, bounded pixel dimensions, scalar numbers
and same-type aliases. Missing/cyclic aliases and unsupported values fail before
files are written. CSS dimensions map to Avalonia logical units. Native resources
include brushes, typed colors (`.Value`), dimensions and typed panel-column/thickness/radius
forms. Media queries substitute token dimensions during generation because CSS
custom properties cannot be used directly as media-query thresholds.

Font families are host adapters: Web uses its system stack; Workstation uses its
bundled Inter with Segoe UI fallback. Relative text measures, letter spacing,
viewport interpolation, icon geometry and unitless structural layout values stay
in host styles. They are not additional palettes or absolute spacing scales.

This change retains the existing light theme and product version `v0.0.1`.
[UI governance](../../docs/implementation/APPLICATION_UI_GOVERNANCE.md) owns current
components, compatibility and evidence. No business screens or theme modes are
introduced by the token library.
