---
name: ux-ui-design-doctrine
description: "Apply shared UX/UI guidance for screen/component design, tokens, accessibility and interface copy. Use for UI implementation or review, including Blazor and Avalonia; preserve the existing project stack and authority boundaries."
---

# UX/UI guidance for Codex

Use this doctrine for screen/component implementation, UI review, accessibility,
tokens, theming and interface copy. It is adapted from the user-selected
[UX/UI Agent Skills](https://github.com/plugin87/ux-ui-agent-skills) kit. The
revision and migration mapping are in [SOURCE.json](SOURCE.json); retain [LICENSE](LICENSE).

## Scope and precedence

Follow the user's request and the consuming repository's instructions, current
framework, brand, business boundaries and version policy. The kit supplies design
guidance, not authorization to replace a stack, add product capabilities, change
business authority or publish. Adapt examples to the actual host. Prefer existing
accessible controls and shared components; introduce a primitive or token only
when the current work needs it. Design variants and states that apply, rather than
creating an entire component catalog for one change.

## Resolve resources

All bundled library paths resolve from [references/kit](references/kit). For shell
commands set `UX_UI_KIT` explicitly to that directory's absolute path, derived from
this skill's actual location. Codex does not supply `CLAUDE_SKILL_DIR` or a
replacement skill-directory environment variable. Confirm the requested file
exists before reading or running it. Keep the reference kit unchanged; generated
tokens, code and evidence belong in the application or its task artifacts.

Load only the references relevant to the task:

| Work | Detailed rule | Supporting material |
|---|---|---|
| Tokens, palette, theme consistency | [Tokens and color](references/kit/rules/tokens-and-color.md) | `tokens/`, `design-systems/` |
| Type, spacing and hierarchy | [Typography and spacing](references/kit/rules/typography-and-spacing.md) | `taste/design-taste.md` |
| Components, layout and states | [Components](references/kit/rules/components.md) | `components/` |
| Keyboard, names, contrast and motion | [Accessibility](references/kit/rules/accessibility.md) | `accessibility/` |
| Host implementation | [Frameworks](references/kit/rules/frameworks.md) | `frameworks/adapter-protocol.md` and the actual host adapter |
| Copy, brand and motion | [Brand and operations](references/kit/rules/brand-and-operations.md) | `content/`, `taste/` |
| Review, research and handoff | [Review and research](references/kit/rules/review-and-research.md) | `workflows/` |

For this repository read [the Blazor/Avalonia adapter](references/application-hosts.md).
It maps the library to the implemented hosts and current authority boundaries.

## Design decisions that matter

Prioritize task completion, accessibility, consistency, visual hierarchy and
maintainability in that order. Establish the audience, current journey, primary
action and visual direction before markup. Reuse existing brand/theme decisions;
state material assumptions and ask only for information that changes the work.

Give each screen a clear focal point and a useful ending. Use semantic tokens by
intent: destructive actions, normal primary actions, neutral secondary actions and
status must remain distinguishable. Keep a shared theme per host, with explicit
semantic mappings across hosts; do not copy a separate palette into every page.
Use real icons or clear text rather than decorative emoji.

Handle default, hover, focus, pressed, disabled, loading, error and selected states
where applicable. Keep loading distinct from unavailable. Empty/error states
explain what happened and offer an available next step. Feedback must reflect the
actual result; a successful animation is not a successful business command.

Use semantic controls and native accessibility behavior before adding ARIA or
custom replacements. Keyboard navigation, visible focus, labels, status
announcements and adequate contrast are implementation requirements. The kit's
HTML select preferences do not ban an accessible native ComboBox or justify
replacing a working control without evidence. Support reflow, scaling, long text
and reduced motion. A new theme or dark mode needs a requested/accepted scope.

## Verification and honest claims

Run the application's relevant checks against the actual changed files or runtime.
Render and inspect screenshots, exercise controls and check observable results.
Measure contrast/state/keyboard/layout claims; code inspection or a fixture with
different controls does not prove them. For native UI use native tests and target
device evidence; an HTML gate cannot qualify Avalonia or Windows.

The bundled scripts are optional tools for compatible, explicit targets. Read a
script's supported inputs first. Most browser gates accept local HTML files or
harnesses; do not assume they accept an application URL. Require a real browser
when interpreting a browser gate: `DS_REQUIRE_BROWSER=1` makes absence fail rather
than becoming a skipped exit-zero run. Playwright/browser dependencies are not
installed by this guidance package. A skipped or unrun check is not a pass.

`scripts/accuracy_report.mjs` is the upstream kit's self-test and is deliberately
not bundled. It evaluates its examples/evals, not this application. Do not report
the upstream gate total as an application quality score. Token-library validation
proves library inputs only. Aesthetic review is judgment, not a percentage.

## Codex workflows

Use `$ux-ui-design-screen` for a requested screen, `$ux-ui-design-code` for host
implementation, `$ux-ui-design-component` for a component spec,
`$ux-ui-a11y-audit` for accessibility and `$ux-ui-ux-writing` for UI copy. The other
installed workflows retain focused descriptions for selection.

The eleven upstream explicit-only workflows retain that policy in
`agents/openai.yaml`; ordinary workflows remain eligible for implicit selection.
Claude `context: fork`, tool allowlists and background fields are not Codex
configuration and are not copied. `$ux-ui-critique` requests an evidence-based
review; only call it independent when a real separate authorized reviewer ran.
Claude write/stop hooks have no automatic equivalent in this package. Use explicit
application checks; do not claim a hook is installed or enforcing anything.
