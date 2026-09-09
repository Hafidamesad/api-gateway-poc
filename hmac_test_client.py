#!/usr/bin/env python3
"""
Reusable HMAC-signed request client for validating HmacVerificationMiddleware
across backends. Mirrors the canonical string / signing logic used by
HmacSigningFilter.java (Gateway) and HmacVerificationMiddleware.cs (backends):

    METHOD\nPATH\nTIMESTAMP\nNONCE\nSHA256(BODY)

Usage examples:
  # valid signed request
  python3 hmac_test_client.py --host localhost --port 5101 --path /api/etudiants \
      --method GET --token "$TOKEN" --secret "$HMAC_SECRET" \
      --cert security/pki/gateway/gateway.crt --key security/pki/gateway/gateway.key \
      --ca security/pki/root-ca/ca.crt

  # tampered signature
  python3 hmac_test_client.py ... --tamper

  # stale timestamp (outside 5-min freshness window)
  python3 hmac_test_client.py ... --stale

  # exact replay (same nonce, sent twice)
  python3 hmac_test_client.py ... --replay

NOTE: server-cert verification is disabled (verify=False) below. This script
is for testing JWT/HMAC/role enforcement, not for testing TLS trust chains --
Python's ssl module is stricter about CA keyUsage extensions than openssl
s_client or .NET's X509Chain, and rejects this project's CA for reasons
unrelated to what we're actually validating here. The client certificate
(mTLS) is still presented and still must be valid for the backend to accept
the connection.
"""

import argparse
import hashlib
import hmac
import base64
import time
import uuid
import sys
import requests
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


def build_canonical_string(method, path, timestamp, nonce, body_bytes):
    body_hash = hashlib.sha256(body_bytes).hexdigest()
    return f"{method}\n{path}\n{timestamp}\n{nonce}\n{body_hash}"


def compute_signature(canonical_string, secret):
    mac = hmac.new(secret.encode("utf-8"), canonical_string.encode("utf-8"), hashlib.sha256)
    return base64.b64encode(mac.digest()).decode("utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="localhost")
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--path", required=True, help="e.g. /api/etudiants")
    parser.add_argument("--method", default="GET")
    parser.add_argument("--body", default="", help="raw request body, empty for GET")
    parser.add_argument("--token", required=True, help="JWT bearer token")
    parser.add_argument("--secret", required=True, help="HMAC shared secret")
    parser.add_argument("--cert", required=True, help="client cert (gateway.crt)")
    parser.add_argument("--key", required=True, help="client key (gateway.key)")
    parser.add_argument("--ca", required=False, help="CA cert (unused -- server verify is disabled, see module docstring)")
    parser.add_argument("--tamper", action="store_true", help="corrupt the signature after computing it")
    parser.add_argument("--stale", action="store_true", help="use a timestamp 10 minutes in the past")
    parser.add_argument("--replay", action="store_true", help="send the exact same signed request twice")
    args = parser.parse_args()

    body_bytes = args.body.encode("utf-8")

    if args.stale:
        # 10 minutes ago -> outside the 5-minute freshness window
        timestamp_ms = int((time.time() - 600) * 1000)
    else:
        timestamp_ms = int(time.time() * 1000)

    nonce = str(uuid.uuid4())
    canonical = build_canonical_string(args.method.upper(), args.path, str(timestamp_ms), nonce, body_bytes)
    signature = compute_signature(canonical, args.secret)

    if args.tamper:
        # flip a character to guarantee mismatch, keep it well-formed base64-ish
        signature = signature[:-1] + ("A" if signature[-1] != "A" else "B")

    headers = {
        "Authorization": f"Bearer {args.token}",
        "X-Timestamp": str(timestamp_ms),
        "X-Nonce": nonce,
        "X-Signature": signature,
        "Content-Type": "application/json",
    }

    url = f"https://{args.host}:{args.port}{args.path}"

    def send():
        resp = requests.request(
            args.method.upper(),
            url,
            headers=headers,
            data=body_bytes if body_bytes else None,
            cert=(args.cert, args.key),
            verify=False,
            timeout=10,
        )
        print(f"--> {args.method.upper()} {url}")
        print(f"    Status: {resp.status_code}")
        print(f"    Body:   {resp.text[:300]}")
        print()

    send()
    if args.replay:
        print("Replaying identical request (same nonce)...")
        send()


if __name__ == "__main__":
    main()
