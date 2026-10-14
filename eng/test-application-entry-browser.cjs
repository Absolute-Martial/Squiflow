// Public-contract fixture only. Requires an external Playwright installation and built Web host.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { spawn } = require('node:child_process');
const playwrightModule = process.env.APPLICATION_ENTRY_PLAYWRIGHT_PATH || 'playwright';
const { chromium } = require(playwrightModule);
const playwrightVersion = require(path.join(path.dirname(require.resolve(playwrightModule)), 'package.json')).version;

const root = path.resolve(__dirname, '..');
const output = path.join(root, 'artifacts/application-entry-2026-10-13');
fs.mkdirSync(output, { recursive: true });
let readiness = 200;
let holdBootstrap = false;
let bootstrapCalls = 0;
let displayName = 'Fieldwork';
const heldResponses = new Set();
const events = [];
let web;
let browser;
const webLog = fs.openSync(path.join(output, 'browser-web-host.log'), 'w');

function brand() {
    const details = {
        displayName, shortName: displayName, legalName: 'Fieldwork Services', themeKey: 'default',
        logoUrl: '/logo.svg', faviconUrl: '/favicon.svg', supportUrl: '/support', privacyUrl: '/privacy', termsUrl: '/terms'
    };
    details.revision = crypto.createHash('sha256').update(Object.values(details).join('\n')).digest('hex').slice(0, 24);
    return details;
}

const fixture = http.createServer((request, response) => {
    if (request.url === '/api/v1/application/bootstrap') {
        bootstrapCalls++;
        response.setHeader('Content-Type', 'application/json');
        if (holdBootstrap) {
            response.flushHeaders();
            heldResponses.add(response);
            response.on('close', () => heldResponses.delete(response));
        } else {
            response.end(JSON.stringify(brand()));
        }
    } else if (request.url === '/health/ready') {
        response.writeHead(readiness).end();
    } else if (request.url === '/favicon.svg' || request.url === '/logo.svg') {
        response.setHeader('Content-Type', 'image/svg+xml');
        response.end('<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32"><rect width="32" height="32" rx="8" fill="#20352e"/></svg>');
    } else {
        response.writeHead(404).end();
    }
});

async function until(check, timeout = 20000) {
    const deadline = Date.now() + timeout;
    while (Date.now() < deadline) {
        if (await check()) return;
        await new Promise(resolve => setTimeout(resolve, 50));
    }
    throw new Error('The expected runtime signal did not arrive within the test budget.');
}

async function stopWeb() {
    if (!web || web.exitCode !== null) return;
    const exited = new Promise(resolve => web.once('exit', resolve));
    web.kill('SIGTERM');
    await Promise.race([exited, new Promise(resolve => setTimeout(resolve, 5000))]);
    if (web.exitCode === null) {
        web.kill('SIGKILL');
        await exited;
    }
}

async function run() {
    await new Promise(resolve => fixture.listen(0, '127.0.0.1', resolve));
    const fixtureUrl = `http://127.0.0.1:${fixture.address().port}`;
    const portProbe = http.createServer();
    await new Promise(resolve => portProbe.listen(0, '127.0.0.1', resolve));
    const webUrl = `http://127.0.0.1:${portProbe.address().port}`;
    await new Promise(resolve => portProbe.close(resolve));
    async function startWeb() {
        web = spawn('dotnet', [path.join(root, 'apps/web/Application.Web/bin/Release/net10.0/Application.Web.dll'), '--urls', webUrl], {
            cwd: path.join(root, 'apps/web/Application.Web'),
            env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', ApplicationEntry__CoreApiBaseUrl: fixtureUrl },
            stdio: ['ignore', webLog, webLog]
        });
        await until(async () => {
            assert.equal(web.exitCode, null, 'Web host terminated during startup.');
            try { return (await fetch(`${webUrl}/health/live`, { signal: AbortSignal.timeout(500) })).ok; }
            catch { return false; }
        });
    }
    await startWeb();
    browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
    async function heading(text, timeout = 12000) {
        await page.getByRole('heading', { name: text, exact: true }).waitFor({ state: 'visible', timeout });
    }

    await page.goto(webUrl);
    await until(() => bootstrapCalls >= 2);
    await heading('Service available');
    assert.equal(await page.title(), 'Fieldwork');
    await page.screenshot({ path: path.join(output, 'web-desktop.png'), fullPage: true });
    events.push('Desktop branded entry and interactive circuit passed.');
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
    await page.screenshot({ path: path.join(output, 'web-mobile.png'), fullPage: true });
    events.push('390px mobile layout has no horizontal overflow.');
    await page.setViewportSize({ width: 1440, height: 960 });

    readiness = 503;
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await heading('Service is getting ready');
    await page.screenshot({ path: path.join(output, 'web-not-ready.png'), fullPage: true });
    events.push('Readiness outage did not show available.');

    holdBootstrap = true;
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await heading('Checking connection…');
    assert.equal(await page.getByRole('button', { name: 'Check again', exact: true }).isDisabled(), true);
    await page.getByRole('button', { name: 'Cancel check', exact: true }).click();
    await heading('Check canceled');
    assert.equal(await page.title(), 'Application connection');
    events.push('Busy action and cancellation cleared prior identity.');
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await heading('Connection timed out', 14000);
    events.push('Whole-check deadline is an explicit timeout.');
    holdBootstrap = false;
    for (const response of heldResponses) response.destroy();

    readiness = 200;
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await heading('Service available');
    displayName = '<img src=x onerror=alert(1)>';
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await heading('Service available');
    assert.equal(await page.locator('img[src="x"]').count(), 0);
    assert.equal(await page.title(), displayName);
    events.push('Hostile branding rendered as inert text.');
    displayName = 'Fieldwork';

    await stopWeb();
    await page.locator('#components-reconnect-modal').waitFor({ state: 'visible', timeout: 12000 });
    await page.screenshot({ path: path.join(output, 'web-disconnected.png'), fullPage: true });
    await startWeb();
    await page.reload();
    await heading('Service available');
    assert.equal(await page.title(), 'Fieldwork');
    events.push('Actual host stop/restart shows reconnect and reload rechecks current identity.');
    assert.deepEqual(errors.filter(error => !error.includes('WebSocket') && !error.includes('ERR_CONNECTION_REFUSED') &&
        !error.includes('Failed to fetch') && !error.includes('disconnected with error') && !error.includes('Failed to start the transport') &&
        !error.includes('Failed to complete negotiation')), []);
    fs.writeFileSync(path.join(output, 'browser-results.json'), JSON.stringify({ playwright: playwrightVersion, browser: browser.version(), events, errors }, null, 2));
    console.log(events.join('\n'));
}

run().catch(error => { console.error(error); process.exitCode = 1; }).finally(async () => {
    if (browser) await browser.close();
    await stopWeb();
    for (const response of heldResponses) response.destroy();
    fixture.closeAllConnections();
    await new Promise(resolve => fixture.close(resolve));
    fs.closeSync(webLog);
});
