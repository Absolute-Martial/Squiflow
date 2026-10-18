// Rendered checks against the real Blazor host. Dependencies stay outside the runtime project.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const axeSource = fs.readFileSync(require.resolve(process.env.APPLICATION_ENTRY_AXE_PATH || 'axe-core/axe.min.js'), 'utf8');

module.exports = async function audit(page, name, output) {
    await page.evaluate(axeSource);
    const result = await page.evaluate(async () => {
        const { violations, incomplete, passes, testEngine } = await axe.run(document, {
            runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa', 'best-practice'] }
        });
        const simplify = rules => rules.map(({ id, impact, nodes }) => ({ id, impact, nodes: nodes.map(({ target, failureSummary }) => ({ target, failureSummary })) }));
        return { violations: simplify(violations), incomplete: simplify(incomplete), passedRules: passes.length, engine: testEngine };
    });
    fs.writeFileSync(path.join(output, `${name}-accessibility.json`), JSON.stringify(result, null, 2));
    assert.deepEqual(result.violations, [], `${name}: accessibility violations`);
    const layout = await page.evaluate(() => ({
        overflow: document.documentElement.scrollWidth > innerWidth,
        smallTargets: [...document.querySelectorAll('button, a')].filter(element => {
            const bounds = element.getBoundingClientRect();
            return bounds.width && bounds.height && (bounds.width < 24 || bounds.height < 24);
        }).map(element => element.textContent.trim()),
        bodyFont: parseFloat(getComputedStyle(document.body).fontSize),
        forcedColorNavigation: matchMedia('(forced-colors: active)').matches
            ? [...document.querySelectorAll('.business-navigation a[aria-current="page"]')].map(element => {
                const style = getComputedStyle(element);
                const luminance = color => color.match(/[\d.]+/g).slice(0, 3)
                    .map(value => Number(value) / 255)
                    .map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4)
                    .reduce((total, value, index) => total + value * [0.2126, 0.7152, 0.0722][index], 0);
                const values = [luminance(style.color), luminance(style.backgroundColor)].sort((first, second) => first - second);
                return { underlined: style.textDecorationLine.includes('underline'), contrast: (values[1] + 0.05) / (values[0] + 0.05) };
            })
            : [],
    }));
    assert.equal(layout.overflow, false, `${name}: horizontal overflow`);
    assert.deepEqual(layout.smallTargets, [], `${name}: undersized targets`);
    assert.ok(layout.bodyFont >= 16, `${name}: body text too small`);
    for (const current of layout.forcedColorNavigation) {
        assert.ok(current.underlined, `${name}: current navigation needs a visible cue independent of color`);
        assert.ok(current.contrast >= 4.5, `${name}: current navigation text needs 4.5:1 forced-color contrast`);
    }
    await page.screenshot({ path: path.join(output, `${name}.png`), fullPage: true });
};
