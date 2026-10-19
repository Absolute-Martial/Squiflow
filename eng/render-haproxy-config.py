#!/usr/bin/env python3
"""Render and validate the current TLS-passthrough ingress from deployment-owned values."""

import argparse
import ipaddress
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


FIELDS = {
    "publicBind", "privateAdminBind", "webHost", "apiHost", "adminHost",
    "upstreamCaFile", "webUpstream", "coreApiUpstreams", "adminApiUpstreams",
}
DNS_NAME = re.compile(r"(?=.{1,253}$)[a-zA-Z0-9](?:[a-zA-Z0-9.-]*[a-zA-Z0-9])?\Z")
ADMIN_NETWORKS = tuple(ipaddress.ip_network(value) for value in (
    "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8",
    "100.64.0.0/10", "fc00::/7", "::1/128",
))


def dns_name(value):
    if not isinstance(value, str) or not DNS_NAME.fullmatch(value):
        raise ValueError("Hosts must be explicit DNS names without URLs or whitespace.")
    if any(not label or len(label) > 63 or label.startswith("-") or label.endswith("-") for label in value.split(".")):
        raise ValueError("Host contains an invalid DNS label.")
    return value.lower()


def address(value, *, bind=False):
    if not isinstance(value, str) or len(value) > 320:
        raise ValueError("Listener and upstream addresses must use host:port syntax.")
    match = re.fullmatch(r"(?:\[([0-9a-fA-F:]+)\]|([a-zA-Z0-9.-]+)):([0-9]{1,5})", value)
    if not match or not 1 <= int(match[3]) <= 65535:
        raise ValueError("Listener and upstream addresses must include a valid explicit port.")
    host = match[1] or match[2]
    try:
        ip = ipaddress.ip_address(host)
    except ValueError:
        if bind:
            raise ValueError("Listeners require a literal IP address.") from None
        host = dns_name(host)
        ip = None
    if ip is not None:
        if ip.is_multicast or (not bind and ip.is_unspecified):
            raise ValueError("Address is not usable for this endpoint.")
        host = f"[{ip}]" if ip.version == 6 else str(ip)
    return f"{host}:{int(match[3])}", ip, int(match[3])


def render(config):
    if not isinstance(config, dict) or set(config) != FIELDS:
        raise ValueError("Supply exactly the documented ingress configuration fields.")
    public, public_ip, public_port = address(config["publicBind"], bind=True)
    private, private_ip, private_port = address(config["privateAdminBind"], bind=True)
    if not any(private_ip in network for network in ADMIN_NETWORKS):
        raise ValueError("Admin must bind a concrete loopback or private-network IP.")
    if public_port == private_port and (public_ip.is_unspecified or public_ip == private_ip):
        raise ValueError("Public and private listeners overlap.")
    web_host, api_host, admin_host = (dns_name(config[name]) for name in ("webHost", "apiHost", "adminHost"))
    if len({web_host, api_host, admin_host}) != 3:
        raise ValueError("Public Web, public API and private Admin require distinct hosts.")
    ca = config["upstreamCaFile"]
    if not isinstance(ca, str) or not re.fullmatch(r"/[a-zA-Z0-9_./-]+", ca) or not Path(ca).is_file():
        raise ValueError("Supply an existing absolute upstream CA-file path without configuration metacharacters.")
    web = address(config["webUpstream"])[0]
    groups = {}
    for key in ("coreApiUpstreams", "adminApiUpstreams"):
        raw = config[key]
        if not isinstance(raw, list) or not 1 <= len(raw) <= 8:
            raise ValueError("Supply one through eight upstreams for each API.")
        groups[key] = [address(value)[0] for value in raw]
        if len(set(groups[key])) != len(groups[key]):
            raise ValueError("Duplicate upstream addresses are not allowed.")
    if set(groups["adminApiUpstreams"]) & {web, *groups["coreApiUpstreams"]}:
        raise ValueError("Private Admin upstreams must be separate from public upstreams.")
    if web in groups["coreApiUpstreams"]:
        raise ValueError("Web and CoreApi require separate upstream endpoints.")

    lines = [
        "# Generated from deployment configuration; no TLS keys are stored in this file.",
        "global", "    log stdout format raw local0", "    maxconn 1024", "",
        "defaults", "    mode tcp", "    log global", "    option tcplog",
        "    timeout connect 5s", "    timeout client 1h", "    timeout server 1h",
        "    timeout check 5s", "",
        "frontend public_application", f"    bind {public}",
        "    tcp-request inspect-delay 5s",
        f"    acl web_sni req.ssl_sni -i {web_host}",
        f"    acl api_sni req.ssl_sni -i {api_host}",
        "    tcp-request content reject if { req.ssl_hello_type 1 } !web_sni !api_sni",
        "    tcp-request content accept if { req.ssl_hello_type 1 }",
        "    tcp-request content reject if WAIT_END",
        "    use_backend public_web if web_sni", "    use_backend public_core_api if api_sni",
        "    default_backend reject_unknown", "",
        "frontend private_admin", f"    bind {private}",
        "    tcp-request inspect-delay 5s",
        f"    acl admin_sni req.ssl_sni -i {admin_host}",
        "    tcp-request content reject if { req.ssl_hello_type 1 } !admin_sni",
        "    tcp-request content accept if { req.ssl_hello_type 1 }",
        "    tcp-request content reject if WAIT_END",
        "    default_backend private_admin_api", "", "backend reject_unknown", "",
    ]
    for name, host, path, upstreams in (
        ("public_web", web_host, "/health/live", [web]),
        ("public_core_api", api_host, "/health/ready", groups["coreApiUpstreams"]),
        ("private_admin_api", admin_host, "/health/ready", groups["adminApiUpstreams"]),
    ):
        lines.extend([
            f"backend {name}", "    balance roundrobin", "    option httpchk",
            f"    http-check send meth GET uri {path} ver HTTP/1.1 hdr Host {host}",
            "    http-check expect status 200",
        ])
        for index, upstream in enumerate(upstreams, start=1):
            lines.append(f"    server node{index} {upstream} check check-ssl verify required ca-file {ca} verifyhost {host} check-sni {host} inter 5s fall 2 rise 2")
        lines.append("")
    return "\n".join(lines)


def unique_fields(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate ingress configuration field.")
        result[key] = value
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--haproxy", default=shutil.which("haproxy"))
    args = parser.parse_args()
    if not args.haproxy:
        parser.error("HAProxy is required to validate the generated configuration.")
    temporary = None
    try:
        if args.config.stat().st_size > 64 * 1024:
            raise ValueError("Ingress configuration exceeds 64 KiB.")
        contents = render(json.loads(args.config.read_text(), object_pairs_hook=unique_fields))
        with tempfile.NamedTemporaryFile(mode="w", dir=args.output.parent, prefix=".haproxy-", delete=False) as target:
            temporary = Path(target.name)
            target.write(contents)
        subprocess.run([args.haproxy, "-c", "-f", str(temporary)], check=True)
        os.replace(temporary, args.output)
        temporary = None
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Ingress configuration was not installed: {error}\n")
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
