#!/usr/bin/env python3
"""Exercise ingress validation, TLS passthrough, private certificate proof and API balancing."""

import copy
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import ssl
import subprocess
import tempfile
import threading
import time
import unittest


spec = importlib.util.spec_from_file_location("ingress", Path(__file__).with_name("render-haproxy-config.py"))
ingress = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ingress)


def free_port():
    with socket.socket() as connection:
        connection.bind(("127.0.0.1", 0))
        return connection.getsockname()[1]


def eventually(action, timeout=20):
    deadline = time.monotonic() + timeout
    last = None
    while time.monotonic() < deadline:
        try:
            result = action()
            if result:
                return result
        except (OSError, ssl.SSLError) as error:
            last = error
        threading.Event().wait(0.1)
    raise AssertionError(f"Ingress condition did not complete within {timeout}s: {last}")


class IngressTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.binary = os.environ.get("HAPROXY_BIN") or shutil.which("haproxy")
        if not cls.binary:
            raise unittest.SkipTest("HAProxy is not installed; live ingress checks were not run.")
        cls.temporary = tempfile.TemporaryDirectory(prefix="application-ingress-")
        cls.root = Path(cls.temporary.name)

        def openssl(*args):
            subprocess.run(["openssl", *args], cwd=cls.root, check=True, capture_output=True)

        openssl("req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1", "-subj", "/CN=Application ingress test CA",
                "-keyout", "ca.key", "-out", "ca.pem")
        for name in ("server", "client"):
            openssl("req", "-newkey", "rsa:2048", "-nodes", "-subj", f"/CN=Application ingress {name} test",
                    "-keyout", f"{name}.key", "-out", f"{name}.csr")
            extension = "subjectAltName=DNS:web.ingress.test,DNS:api.ingress.test,DNS:admin.ingress.test\nextendedKeyUsage=serverAuth\n" if name == "server" else "extendedKeyUsage=clientAuth\n"
            (cls.root / f"{name}.ext").write_text(extension)
            openssl("x509", "-req", "-in", f"{name}.csr", "-CA", "ca.pem", "-CAkey", "ca.key", "-CAcreateserial",
                    "-days", "1", "-extfile", f"{name}.ext", "-out", f"{name}.pem")
        cls.fingerprint = hashlib.sha256(ssl.PEM_cert_to_DER_cert((cls.root / "client.pem").read_text())).hexdigest()
        cls.servers = []
        cls.threads = []

        def serve(label, private=False):
            class Handler(BaseHTTPRequestHandler):
                def do_GET(self):
                    peer = self.connection.getpeercert(binary_form=True)
                    health = self.path.startswith("/health/")
                    status = 200 if (health and self.server.ready) or (not health and (not private or peer)) else 403 if not health else 503
                    body = json.dumps({"node": label, "fingerprint": hashlib.sha256(peer).hexdigest() if peer else None}).encode()
                    self.send_response(status)
                    self.send_header("Content-Length", str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)

                def log_message(self, *_):
                    pass

            server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
            server.ready = True
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
            context.load_cert_chain(cls.root / "server.pem", cls.root / "server.key")
            context.load_verify_locations(cls.root / "ca.pem")
            context.verify_mode = ssl.CERT_OPTIONAL if private else ssl.CERT_NONE
            server.socket = context.wrap_socket(server.socket, server_side=True)
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            cls.servers.append(server)
            cls.threads.append(thread)
            return f"127.0.0.1:{server.server_port}"

        cls.public_port, cls.private_port = free_port(), free_port()
        cls.config = {
            "publicBind": f"127.0.0.1:{cls.public_port}",
            "privateAdminBind": f"127.0.0.1:{cls.private_port}",
            "webHost": "web.ingress.test", "apiHost": "api.ingress.test", "adminHost": "admin.ingress.test",
            "upstreamCaFile": str(cls.root / "ca.pem"),
            "webUpstream": serve("web"),
            "coreApiUpstreams": [serve("core1"), serve("core2")],
            "adminApiUpstreams": [serve("admin", private=True)],
        }
        cls.config_path = cls.root / "deployment.json"
        cls.config_path.write_text(json.dumps(cls.config))
        cls.output = cls.root / "haproxy.cfg"
        cls.render_cli()
        cls.log = (cls.root / "haproxy.log").open("w")
        cls.proxy = subprocess.Popen([cls.binary, "-db", "-f", str(cls.output)], stdout=cls.log, stderr=subprocess.STDOUT)
        eventually(lambda: cls.request("web.ingress.test")[0] == 200)

    @classmethod
    def render_cli(cls, success=True):
        result = subprocess.run(["python3", str(Path(__file__).with_name("render-haproxy-config.py")),
                                 "--config", str(cls.config_path), "--output", str(cls.output), "--haproxy", cls.binary],
                                capture_output=True, text=True)
        if (result.returncode == 0) != success:
            raise AssertionError(result.stdout + result.stderr)

    @classmethod
    def tearDownClass(cls):
        if hasattr(cls, "proxy"):
            cls.proxy.terminate()
            cls.proxy.wait(timeout=10)
            cls.log.close()
        for server in getattr(cls, "servers", []):
            server.shutdown()
            server.server_close()
        if hasattr(cls, "temporary"):
            cls.temporary.cleanup()

    @classmethod
    def request(cls, host, *, private=False, certificate=False, forged=False):
        context = ssl.create_default_context(cafile=str(cls.root / "ca.pem"))
        if certificate:
            context.load_cert_chain(cls.root / "client.pem", cls.root / "client.key")
        port = cls.private_port if private else cls.public_port
        with socket.create_connection(("127.0.0.1", port), timeout=3) as raw:
            with context.wrap_socket(raw, server_hostname=host) as connection:
                headers = "X-Client-Certificate: forged\r\n" if forged else ""
                connection.sendall(f"GET /api/probe HTTP/1.1\r\nHost: {host}\r\n{headers}Connection: close\r\n\r\n".encode())
                response = b""
                while part := connection.recv(4096):
                    response += part
        head, body = response.split(b"\r\n\r\n", 1)
        return int(head.split()[1]), json.loads(body)

    def test_private_tls_keeps_per_person_certificate_and_rejects_header_substitution(self):
        status, result = self.request("admin.ingress.test", private=True, certificate=True)
        self.assertEqual(200, status)
        self.assertEqual(self.fingerprint, result["fingerprint"])
        self.assertEqual("admin", result["node"])
        self.assertEqual(403, self.request("admin.ingress.test", private=True, forged=True)[0])

    def test_public_listener_never_routes_to_private_admin(self):
        with self.assertRaises((ssl.SSLError, ConnectionError)):
            self.request("admin.ingress.test", certificate=True)

    def test_api_balances_and_stops_routing_to_an_unready_node(self):
        self.assertEqual({"core1", "core2"}, {self.request("api.ingress.test")[1]["node"] for _ in range(6)})
        self.servers[1].ready = False
        try:
            eventually(lambda: all(self.request("api.ingress.test")[1]["node"] == "core2" for _ in range(4)))
        finally:
            self.servers[1].ready = True
        eventually(lambda: {self.request("api.ingress.test")[1]["node"] for _ in range(4)} == {"core1", "core2"})

    def test_invalid_configuration_preserves_previous_valid_output(self):
        original = self.output.read_bytes()
        invalid = copy.deepcopy(self.config)
        invalid["upstreamCaFile"] = str(self.root / "invalid-ca.pem")
        (self.root / "invalid-ca.pem").write_text("invalid certificate")
        self.config_path.write_text(json.dumps(invalid))
        try:
            self.render_cli(success=False)
        finally:
            self.config_path.write_text(json.dumps(self.config))
        self.assertEqual(original, self.output.read_bytes())
        self.assertEqual([], list(self.root.glob(".haproxy-*")))

    def test_unsafe_or_incomplete_deployment_values_fail_closed(self):
        cases = [
            {"publicBind": ""}, {"privateAdminBind": "0.0.0.0:8443"},
            {"privateAdminBind": "8.8.8.8:8443"}, {"privateAdminBind": self.config["publicBind"]},
            {"privateAdminBind": "192.0.2.1:8443"}, {"privateAdminBind": "0.0.0.1:8443"},
            {"privateAdminBind": "255.255.255.255:8443"}, {"privateAdminBind": "[2001:db8::1]:8443"},
            {"privateAdminBind": "169.254.1.1:8443"}, {"privateAdminBind": "[fe80::1]:8443"},
            {"apiHost": self.config["webHost"]}, {"webHost": "https://web.ingress.test"},
            {"coreApiUpstreams": []}, {"coreApiUpstreams": ["api:443\nbackend bypass"]},
            {"coreApiUpstreams": [self.config["webUpstream"]]},
            {"adminApiUpstreams": self.config["coreApiUpstreams"]},
            {"webUpstreams": ["node1:443", "node2:443"]}, {"upstreamCaFile": "/missing.pem"},
        ]
        for overrides in cases:
            with self.subTest(overrides=overrides), self.assertRaises(ValueError):
                ingress.render(self.config | overrides)
        with self.assertRaises(ValueError):
            ingress.render({})
        with self.assertRaises(ValueError):
            json.loads('{"publicBind":"a","publicBind":"b"}', object_pairs_hook=ingress.unique_fields)
        for host in ("10.1.2.3", "172.31.1.2", "192.168.1.2", "127.0.0.2", "100.64.1.2", "[fd01::1]", "[::1]"):
            with self.subTest(private_host=host):
                self.assertIn(f"bind {host}:8443", ingress.render(self.config | {"privateAdminBind": f"{host}:8443"}))


if __name__ == "__main__":
    unittest.main(verbosity=2)
