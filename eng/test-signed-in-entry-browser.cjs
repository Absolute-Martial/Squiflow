// Local HTTPS protocol fixture. This proves browser/framework behavior, not live ZITADEL qualification.
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const https = require('node:https');
const http = require('node:http');
const os = require('node:os');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');
const { chromium } = require(process.env.APPLICATION_ENTRY_PLAYWRIGHT_PATH || 'playwright');
const audit = require('./test-entry-ui.cjs');
const root = path.resolve(__dirname, '..');
const output = process.env.APPLICATION_UI_ARTIFACTS || path.join(root, 'artifacts/signed-in-entry-browser');
fs.mkdirSync(output, { recursive: true });
const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'application-login-browser-'));
const cert = path.join(temporary, 'certificate.pem');
const key = path.join(temporary, 'private-key.pem');
const pfx = path.join(temporary, 'certificate.pfx');
execFileSync('openssl', ['req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', key, '-out', cert, '-days', '1', '-subj', '/CN=Application local test', '-addext', 'subjectAltName=IP:127.0.0.1'], { stdio: 'ignore' });
execFileSync('openssl', ['pkcs12', '-export', '-out', pfx, '-inkey', key, '-in', cert, '-passout', 'pass:local-test-only'], { stdio: 'ignore' });
const trust = path.join(temporary, 'trusted-certificates.pem');
fs.writeFileSync(trust, Buffer.concat([fs.readFileSync('/etc/ssl/certs/ca-certificates.crt'), fs.readFileSync(cert)]));
const signing = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const publicJwk = { ...signing.publicKey.export({ format: 'jwk' }), kid: 'local-signing', use: 'sig', alg: 'RS256' };
const accessToken = 'synthetic-browser-actor-token-never-render';
const clientSecret = 'synthetic-browser-client-secret-never-render';
const clientId = 'browser-entry-client';
const apiScope = 'urn:zitadel:iam:org:project:id:browser-fixture:aud';
const accountId = crypto.randomUUID();
const first = { tenantId: crypto.randomUUID(), displayName: 'North workspace' };
const second = { tenantId: crypto.randomUUID(), displayName: '<script>South workspace</script>' };
let memberships = [first, second];
let denyWorkspace = false;
let accountEnabled = true;
let readiness = 200;
let longNames = false;
let releaseReadiness;
let holdReadiness = false;
let fixtureOrigin;
let webOrigin;
let web;
let browser;
let pkceChecks = 0;
const codes = new Map();
const events = [];
const fixturePaths = [];
const browserResponses = [];
const log = fs.openSync(path.join(output, 'signed-in-web-host.log'), 'w');
function json(response, value, status = 200) {
    response.writeHead(status, { 'Content-Type': status === 403 ? 'application/problem+json' : 'application/json', 'Cache-Control': 'no-store' });
    response.end(JSON.stringify(value));
}
function brand() {
    const value = { displayName: longNames ? 'Fieldwork ' + 'North '.repeat(18).trim() : 'Fieldwork', shortName: longNames ? 'Fieldwork ' + 'North '.repeat(18).trim() : 'Fieldwork', legalName: 'Fieldwork Services', themeKey: 'default', logoUrl: '/logo.svg', faviconUrl: '/favicon.svg', supportUrl: '/support', privacyUrl: '/privacy', termsUrl: '/terms' };
    value.revision = crypto.createHash('sha256').update(Object.values(value).join('\n')).digest('hex').slice(0, 24);
    return value;
}
const fixture = https.createServer({ key: fs.readFileSync(key), cert: fs.readFileSync(cert) }, async (request, response) => {
    const uri = new URL(request.url, fixtureOrigin);
    fixturePaths.push(uri.pathname);
    if (uri.pathname === '/.well-known/openid-configuration') return json(response, {
        issuer: fixtureOrigin, authorization_endpoint: fixtureOrigin + '/authorize', token_endpoint: fixtureOrigin + '/token', jwks_uri: fixtureOrigin + '/keys',
        response_types_supported: ['code'], response_modes_supported: ['query'], grant_types_supported: ['authorization_code'],
        token_endpoint_auth_methods_supported: ['client_secret_post'], id_token_signing_alg_values_supported: ['RS256'], code_challenge_methods_supported: ['S256']
    });
    if (uri.pathname === '/keys') return json(response, { keys: [publicJwk] });
    if (uri.pathname === '/authorize') {
        assert.equal(uri.searchParams.get('client_id'), clientId);
        assert.equal(uri.searchParams.get('code_challenge_method'), 'S256');
        assert.equal(uri.searchParams.get('redirect_uri'), webOrigin + '/signin-oidc');
        assert.equal(uri.searchParams.get('scope'), 'openid ' + apiScope);
        assert.ok(uri.searchParams.get('nonce'));
        const code = crypto.randomUUID();
        codes.set(code, { nonce: uri.searchParams.get('nonce'), challenge: uri.searchParams.get('code_challenge') });
        const callback = new URL(webOrigin + '/signin-oidc');
        callback.searchParams.set('code', code); callback.searchParams.set('state', uri.searchParams.get('state'));
        response.writeHead(302, { Location: callback.href, 'Cache-Control': 'no-store' }); return response.end();
    }
    if (uri.pathname === '/token') {
        let body = '';
        for await (const chunk of request) { body += chunk; assert.ok(body.length <= 16384); }
        const form = new URLSearchParams(body); const transaction = codes.get(form.get('code')); codes.delete(form.get('code'));
        assert.ok(transaction); assert.equal(form.get('client_secret'), clientSecret);
        assert.equal(crypto.createHash('sha256').update(form.get('code_verifier')).digest('base64url'), transaction.challenge); pkceChecks++;
        const now = Math.floor(Date.now() / 1000);
        const unsigned = Buffer.from(JSON.stringify({ alg: 'RS256', kid: publicJwk.kid, typ: 'JWT' })).toString('base64url') + '.' +
            Buffer.from(JSON.stringify({ iss: fixtureOrigin, aud: clientId, sub: 'synthetic-browser-human', nonce: transaction.nonce, iat: now, exp: now + 600 })).toString('base64url');
        const token = unsigned + '.' + crypto.sign('RSA-SHA256', Buffer.from(unsigned), signing.privateKey).toString('base64url');
        return json(response, { access_token: accessToken, token_type: 'Bearer', expires_in: 600, id_token: token });
    }
    if (uri.pathname === '/api/v1/application/bootstrap') return json(response, brand());
    if (uri.pathname === '/health/ready') {
        if (holdReadiness) await new Promise(resolve => { releaseReadiness = resolve; });
        response.writeHead(readiness); return response.end();
    }
    if (uri.pathname === '/favicon.svg' || uri.pathname === '/logo.svg') { response.writeHead(200, { 'Content-Type': 'image/svg+xml' }); return response.end('<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32"><rect width="32" height="32" rx="8" fill="#20352e"/></svg>'); }
    if (uri.pathname.startsWith('/api/')) {
        if (request.headers.authorization !== 'Bearer ' + accessToken) { response.writeHead(401); return response.end(); }
        if (!accountEnabled) return json(response, { code: 'account_disabled' }, 403);
        if (uri.pathname === '/api/v1/account') return json(response, { accountId });
        if (uri.pathname === '/api/v1/account/tenants/page') return json(response, { items: memberships, nextTenantId: null });
        const match = uri.pathname.match(/^\/api\/v1\/tenants\/([^/]+)\/workspace$/);
        if (match) {
            const tenant = memberships.find(value => value.tenantId === match[1]);
            if (!tenant || denyWorkspace) return json(response, { code: 'tenant_access_denied' }, 403);
            return json(response, { tenantId: tenant.tenantId, displayName: tenant === first ? 'North workspace' : 'South workspace' });
        }
    }
    response.writeHead(404); response.end();
});
async function until(check, timeout = 20000) {
    const end = Date.now() + timeout;
    while (Date.now() < end) { if (await check()) return; await new Promise(resolve => setTimeout(resolve, 50)); }
    throw new Error('Expected runtime signal did not arrive.');
}
async function stopWeb() {
    if (!web || web.exitCode !== null) return;
    const exited = new Promise(resolve => web.once('exit', resolve)); web.kill('SIGTERM');
    await Promise.race([exited, new Promise(resolve => setTimeout(resolve, 5000))]);
    if (web.exitCode === null) { web.kill('SIGKILL'); await exited; }
}
async function startWeb() {
    web = spawn('dotnet', [path.join(root, 'apps/web/Application.Web/bin/Release/net10.0/Application.Web.dll'), '--urls', webOrigin], {
        cwd: path.join(root, 'apps/web/Application.Web'), stdio: ['ignore', log, log],
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development', SSL_CERT_FILE: trust,
            Kestrel__Certificates__Default__Path: pfx, Kestrel__Certificates__Default__Password: 'local-test-only',
            ApplicationEntry__CoreApiBaseUrl: fixtureOrigin, Authentication__Enabled: 'true', Authentication__Authority: fixtureOrigin,
            Authentication__PublicOrigin: webOrigin, Authentication__ClientId: clientId, Authentication__ClientSecret: clientSecret, Authentication__ApiScope: apiScope }
    });
    await until(() => new Promise(resolve => {
        const request = https.get(webOrigin + '/health/live', { ca: fs.readFileSync(cert) }, response => { response.resume(); resolve(response.statusCode === 200); });
        request.on('error', () => resolve(false));
    }));
}
async function run() {
    await new Promise(resolve => fixture.listen(0, '127.0.0.1', resolve)); fixtureOrigin = `https://127.0.0.1:${fixture.address().port}`;
    const probe = http.createServer(); await new Promise(resolve => probe.listen(0, '127.0.0.1', resolve));
    webOrigin = `https://127.0.0.1:${probe.address().port}`; await new Promise(resolve => probe.close(resolve));
    await startWeb(); browser = await chromium.launch({ headless: true, executablePath: process.env.APPLICATION_ENTRY_BROWSER_EXECUTABLE || undefined });
    const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 1000 } });
    const page = await context.newPage();
    page.on('response', response => browserResponses.push({ path: new URL(response.url()).pathname, status: response.status() }));
    let deliberateRestart = false;
    const expectedRestartErrors = [];
    const pageErrors = []; page.on('pageerror', error => {
        if (deliberateRestart && error.message === 'WebSocket closed with status code: 1006 (no reason given).') expectedRestartErrors.push(error.message);
        else pageErrors.push(error.message);
    });
    await page.goto(webOrigin); await page.getByRole('heading', { name: 'Service available' }).waitFor();
    await audit(page, 'entry-desktop', output);
    await page.keyboard.press('Tab');
    assert.equal(await page.locator('.skip-link').evaluate(element => element === document.activeElement), true);
    await page.keyboard.press('Enter');
    assert.equal(await page.evaluate(() => location.hash), '#main');
    assert.equal(await page.evaluate(() => document.activeElement.id), 'main');
    for (const width of [320, 390]) {
        await page.setViewportSize({ width, height: 844 });
        await audit(page, `entry-${width}`, output);
    }
    await page.setViewportSize({ width: 1440, height: 1000 });
    holdReadiness = true;
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await page.getByRole('button', { name: 'Checking connection…', exact: true }).waitFor();
    await until(() => Boolean(releaseReadiness));
    await audit(page, 'entry-loading', output);
    await page.getByRole('button', { name: 'Cancel check', exact: true }).click();
    holdReadiness = false; releaseReadiness(); releaseReadiness = undefined;
    await page.getByRole('heading', { name: 'Check canceled' }).waitFor();
    await audit(page, 'entry-canceled', output);
    readiness = 503;
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await page.getByRole('heading', { name: 'Service is getting ready' }).waitFor();
    await audit(page, 'entry-not-ready', output);
    readiness = 500;
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await page.getByRole('heading', { name: 'Unable to connect' }).waitFor();
    await audit(page, 'entry-unavailable', output);
    readiness = 200; longNames = true;
    await page.getByRole('button', { name: 'Check again', exact: true }).click();
    await page.getByRole('heading', { name: 'Service available' }).waitFor();
    await page.setViewportSize({ width: 320, height: 844 });
    await audit(page, 'entry-long-name', output);
    longNames = false;
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto(webOrigin + '/not-found');
    await page.getByRole('heading', { name: 'This page could not be found.' }).waitFor();
    await audit(page, 'not-found', output);
    await page.goto(webOrigin); await page.getByRole('heading', { name: 'Service available' }).waitFor();
    const forged = await context.request.post(webOrigin + '/auth/login', { form: {} }); assert.equal(forged.status(), 400);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    await page.waitForURL(webOrigin + '/workspace'); await page.getByRole('heading', { name: 'Choose where to work.' }).waitFor();
    assert.ok(pkceChecks > 0); assert.equal(await page.locator('input[type=radio]:checked').count(), 0);
    await audit(page, 'picker-unselected', output);
    assert.ok(await page.getByRole('button', { name: 'Enter workspace', exact: true }).isDisabled());
    assert.ok(!(await page.content()).includes(accessToken)); assert.ok(!(await page.content()).includes(clientSecret));
    const session = (await context.cookies()).find(cookie => cookie.name === '__Host-Application.Session');
    assert.ok(session?.httpOnly && session.secure); assert.ok(!session.value.includes(accessToken));
    await page.screenshot({ path: path.join(output, 'tenant-picker-desktop.png'), fullPage: true });
    events.push('Real HTTPS OIDC/PKCE, signed token validation, antiforgery and opaque session cookie passed.');
    await page.getByLabel('North workspace', { exact: true }).focus();
    await page.keyboard.press('Space');
    await audit(page, 'picker-selected', output);
    await page.getByRole('button', { name: 'Enter workspace', exact: true }).click();
    await page.getByRole('heading', { name: 'North workspace', exact: true }).waitFor();
    await audit(page, 'workspace', output);
    await page.screenshot({ path: path.join(output, 'workspace-desktop.png'), fullPage: true });
    await page.getByRole('button', { name: 'Switch workspace', exact: true }).click(); await page.getByRole('heading', { name: 'Choose where to work.' }).waitFor();
    denyWorkspace = true;
    await page.getByLabel('<script>South workspace</script>', { exact: true }).check(); await page.getByRole('button', { name: 'Enter workspace', exact: true }).click();
    await page.getByText('You cannot enter this workspace. Choose another tenant or contact support.', { exact: true }).waitFor();
    await audit(page, 'picker-denied', output);
    assert.equal(await page.getByRole('heading', { name: 'South workspace', exact: true }).count(), 0);
    denyWorkspace = false; memberships = [];
    await page.reload(); await page.getByText('No workspaces are available on this page.', { exact: true }).waitFor();
    await audit(page, 'picker-empty', output);
    events.push('Explicit selection, current workspace authority, switching, denial and empty membership states passed.');
    memberships = [first, second]; await page.reload(); await page.getByRole('heading', { name: 'Choose where to work.' }).waitFor();
    await page.setViewportSize({ width: 390, height: 844 });
    await audit(page, 'picker-mobile', output);
    await page.setViewportSize({ width: 320, height: 700 });
    await page.emulateMedia({ reducedMotion: 'reduce', forcedColors: 'active' });
    await audit(page, 'picker-320-reflow-forced-colors', output);
    await page.emulateMedia({ forcedColors: 'none' });
    await page.setViewportSize({ width: 390, height: 844 });
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
    await page.screenshot({ path: path.join(output, 'tenant-picker-mobile.png'), fullPage: true });
    await page.getByRole('button', { name: 'Sign out', exact: true }).click(); await page.getByRole('heading', { name: 'You are signed out.' }).waitFor();
    await audit(page, 'signed-out', output);
    await page.goto(webOrigin + '/workspace'); assert.equal(new URL(page.url()).pathname, '/');
    await page.getByRole('button', { name: 'Sign in', exact: true }).click(); await page.waitForURL(webOrigin + '/workspace');
    accountEnabled = false; await page.reload(); await page.getByText('This identity cannot access an active application account. Contact support.', { exact: true }).waitFor();
    await audit(page, 'disabled-account', output);
    accountEnabled = true;
    deliberateRestart = true;
    await stopWeb(); await startWeb(); await page.goto(webOrigin + '/workspace'); assert.equal(new URL(page.url()).pathname, '/');
    deliberateRestart = false;
    events.push('Mobile layout, local logout, disabled account and restart-sign-out passed.');
    assert.deepEqual(pageErrors, []);
    fs.writeFileSync(path.join(output, 'signed-in-browser-results.json'), JSON.stringify({ browser: await browser.version(), events, expectedRestartErrors, liveZitadel: 'NOT_RUN_CONTROLLED_LOCAL_ISSUER', windowsNative: 'NOT_RUN' }, null, 2));
    console.log(events.join('\n'));
}
run().catch(error => { console.error(error); console.error(JSON.stringify({ fixturePaths, browserResponses })); process.exitCode = 1; }).finally(async () => {
    holdReadiness = false; releaseReadiness?.();
    if (browser) await browser.close(); await stopWeb(); fixture.closeAllConnections(); await new Promise(resolve => fixture.close(resolve));
    fs.closeSync(log); fs.rmSync(temporary, { recursive: true, force: true });
});
