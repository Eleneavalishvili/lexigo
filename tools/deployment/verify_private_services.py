"""Verify the deployed private bucket and SMTP authentication without email delivery."""

from datetime import datetime, timezone
import hashlib
import hmac
import json
from pathlib import Path
import smtplib
import ssl
import sys
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen
import uuid


def signed_request(settings, method, path, data=None):
    origin = settings["ServiceUrl"].rstrip("/")
    host = urlsplit(origin).netloc
    now = datetime.now(timezone.utc)
    date = now.strftime("%Y%m%d")
    timestamp = now.strftime("%Y%m%dT%H%M%SZ")
    payload_hash = hashlib.sha256(data or b"").hexdigest()
    header_names = "host;x-amz-content-sha256;x-amz-date"
    canonical_headers = (
        f"host:{host}\nx-amz-content-sha256:{payload_hash}\nx-amz-date:{timestamp}\n"
    )
    canonical = "\n".join([method, path, "", canonical_headers, header_names, payload_hash])
    scope = f"{date}/{settings['Region']}/s3/aws4_request"
    to_sign = "\n".join([
        "AWS4-HMAC-SHA256", timestamp, scope, hashlib.sha256(canonical.encode()).hexdigest(),
    ])
    key = ("AWS4" + settings["SecretKey"]).encode()
    for component in (date, settings["Region"], "s3", "aws4_request"):
        key = hmac.new(key, component.encode(), hashlib.sha256).digest()
    signature = hmac.new(key, to_sign.encode(), hashlib.sha256).hexdigest()
    authorization = (
        f"AWS4-HMAC-SHA256 Credential={settings['AccessKey']}/{scope}, "
        f"SignedHeaders={header_names}, Signature={signature}"
    )
    request = Request(origin + path, data=data, method=method, headers={
        "Authorization": authorization, "x-amz-date": timestamp,
        "x-amz-content-sha256": payload_hash, "Content-Type": "application/octet-stream",
    })
    return urlopen(request, timeout=20)


def verify_storage(settings):
    path = f"/{settings['Bucket']}/deployment-probes/{uuid.uuid4().hex}.txt"
    body = b"LexiGo private-storage deployment check"
    created = False
    try:
        with signed_request(settings, "PUT", path, body) as response:
            if response.status not in (200, 201, 204):
                raise ValueError("Private upload failed.")
        created = True
        with signed_request(settings, "GET", path) as response:
            if response.read() != body:
                raise ValueError("Private storage changed the uploaded content.")
        try:
            with urlopen(settings["ServiceUrl"].rstrip("/") + path, timeout=20):
                raise ValueError("Unsigned access to private content was allowed.")
        except HTTPError as error:
            if error.code != 403:
                raise
    finally:
        if created:
            with signed_request(settings, "DELETE", path):
                pass
    print("Private storage: signed upload/download/delete and unsigned denial passed.")


def verify_smtp(settings):
    with smtplib.SMTP(settings["Host"], settings["Port"], timeout=20) as client:
        client.ehlo()
        client.starttls(context=ssl.create_default_context())
        client.ehlo()
        client.login(settings["Username"], settings["Password"])
        if client.noop()[0] != 250:
            raise ValueError("SMTP authenticated NOOP failed.")
    print("SMTP: verified TLS, authentication and NOOP; no email sent.")


def main():
    try:
        configuration = json.loads(Path("/etc/lexigo/private/appsettings.Production.json").read_text())
        verify_storage(configuration["ObjectStorage"])
        verify_smtp(configuration["Smtp"])
        return 0
    except Exception as error:
        status = f" HTTP {error.code}" if isinstance(error, HTTPError) else ""
        print(f"Private-service verification failed: {type(error).__name__}{status}.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
