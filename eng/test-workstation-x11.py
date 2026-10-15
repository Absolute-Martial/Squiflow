#!/usr/bin/env python3
"""Linux/X11 public-entry process guard; needs Xvfb, python-xlib, ImageMagick and a built host."""
import hashlib
import json
import os
from pathlib import Path
import select
import subprocess
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from Xlib import X, XK, display, protocol

root = Path(__file__).resolve().parent.parent
output = Path(os.environ.get('APPLICATION_UI_ARTIFACTS', root / 'artifacts/workstation-x11'))
output.mkdir(parents=True, exist_ok=True)
page_color = int(json.loads((root / 'apps/design/tokens.json').read_text())['palette']['paper']['$value'][1:], 16)
brand = dict(displayName='Fieldwork', shortName='Fieldwork', legalName='Fieldwork Services',
             themeKey='default', logoUrl='/logo.svg', faviconUrl='/favicon.svg', supportUrl='/support',
             privacyUrl='/privacy', termsUrl='/terms')
brand['revision'] = hashlib.sha256('\n'.join(brand.values()).encode()).hexdigest()[:24]
calls = []

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        calls.append(self.path)
        if self.path == '/api/v1/application/bootstrap':
            body = json.dumps(brand).encode()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        elif self.path == '/health/ready':
            self.send_response(200)
            self.end_headers()
        else:
            self.send_response(404)
            self.end_headers()
    def log_message(self, *_):
        pass

fixture = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
threading.Thread(target=fixture.serve_forever, daemon=True).start()
log = open(output / 'native-host.log', 'w')
read_fd, write_fd = os.pipe()
xserver = subprocess.Popen(['Xvfb', '-displayfd', str(write_fd), '-screen', '0', '1800x1200x24', '-nolisten', 'tcp'],
                           pass_fds=[write_fd], stdout=log, stderr=log)
os.close(write_fd)
app = None
connection = None
try:
    assert select.select([read_fd], [], [], 10)[0], 'Xvfb did not signal readiness'
    number = os.read(read_fd, 32).decode().strip()
    connection = display.Display(':' + number)
    xroot = connection.screen().root
    xroot.change_attributes(event_mask=X.SubstructureNotifyMask)
    environment = dict(os.environ, DISPLAY=':' + number,
                       APPLICATION_CORE_API_URL='http://127.0.0.1:' + str(fixture.server_port))
    for setting in ['APPLICATION_OIDC_AUTHORITY', 'APPLICATION_OIDC_CLIENT_ID', 'APPLICATION_OIDC_API_SCOPE']:
        environment.pop(setting, None)
    app = subprocess.Popen(['dotnet', str(root / 'apps/desktop/workstation/Application.Workstation/bin/Release/net10.0/Application.Workstation.dll')],
                           env=environment, cwd=root, stdout=log, stderr=log)
    def wait_window(title):
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            assert app.poll() is None, 'Native host terminated during launch/check'
            for window in xroot.query_tree().children:
                if window.get_wm_name() == title and window.get_attributes().map_state == X.IsViewable:
                    window.change_attributes(event_mask=X.PropertyChangeMask)
                    return window
            select.select([connection.fileno()], [], [], 0.05)
            while connection.pending_events():
                connection.next_event()
        raise AssertionError('Native window did not show the expected title: ' + title)
    window = wait_window('Application connection')
    window.set_input_focus(X.RevertToParent, X.CurrentTime)
    def old_name_visible():
        pixels = window.get_image(250, 25, 180, 44, X.ZPixmap, 0xFFFFFFFF).data
        return len({pixels[i:i + 4] for i in range(0, len(pixels), 4)}) > 1
    def wait_frame(predicate):
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if predicate():
                return
            select.select([connection.fileno()], [], [], 0.05)
            while connection.pending_events():
                connection.next_event()
        raise AssertionError('Native frame did not paint the expected wordmark change')
    wait_frame(old_name_visible)
    def key(symbol):
        code = connection.keysym_to_keycode(XK.string_to_keysym(symbol))
        for event_type, mask in [(protocol.event.KeyPress, X.KeyPressMask), (protocol.event.KeyRelease, X.KeyReleaseMask)]:
            window.send_event(event_type(time=X.CurrentTime, root=xroot, window=window, same_screen=1,
                              child=X.NONE, root_x=0, root_y=0, event_x=0, event_y=0, state=0, detail=code),
                              event_mask=mask, propagate=True)
        connection.sync()
    key('Tab')
    key('Return')
    window = wait_window('Fieldwork')
    wait_frame(lambda: not old_name_visible())
    assert calls == ['/api/v1/application/bootstrap', '/health/ready'], calls
    previous = None
    stable_since = time.monotonic()
    end = time.monotonic() + 10
    while time.monotonic() < end:
        geometry = window.get_geometry()
        pixels = window.get_image(0, 0, geometry.width, geometry.height, X.ZPixmap, 0xFFFFFFFF).data
        digest = hashlib.sha256(pixels).hexdigest()
        if digest != previous:
            previous = digest
            stable_since = time.monotonic()
        elif time.monotonic() - stable_since >= 0.4:
            break
        select.select([connection.fileno()], [], [], 0.05)
        while connection.pending_events(): connection.next_event()
    else:
        raise AssertionError('Native frame did not settle')
    geometry = window.get_geometry()
    pixel = window.get_image(geometry.width - 16, geometry.height // 2, 1, 1, X.ZPixmap, 0xFFFFFFFF).data
    assert int.from_bytes(pixel[:3], 'little') == page_color, 'Connection panel overflowed the right window margin'
    subprocess.run(['import', '-display', ':' + number, '-window', str(window.id), str(output / 'workstation-native-settled.png')], check=True)
    result = dict(platform='Linux / X11 Xvfb; Windows runtime not qualified',
                  routes=calls,
                  result='Real native keyboard action fetched HTTP public branding/readiness and updated the title.')
    (output / 'native-results.json').write_text(json.dumps(result, indent=2))
    print(json.dumps(result, indent=2))
finally:
    if connection:
        connection.close()
    for process in [app, xserver]:
        if process and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
    os.close(read_fd)
    fixture.shutdown()
    fixture.server_close()
    log.close()
