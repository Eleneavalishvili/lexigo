"""Prepare private configuration on the approved, newly created Linux server.

Read SMTP credentials and pinned release references from standard input.
Never print generated passwords, signing keys or SMTP credentials.
"""

import ipaddress
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys


PRIVATE = Path("/etc/lexigo/private")


def write_private(name, value):
    path = PRIVATE / name
    with path.open("x", encoding="utf-8", newline="\n") as output:
        output.write(value)
    path.chmod(0o600)
    return path


def run_openssl(*arguments):
    subprocess.run(["openssl", *arguments], check=True, stdout=subprocess.DEVNULL,
                   stderr=subprocess.DEVNULL)


def database_certificates(database_uid):
    run_openssl(
        "req", "-x509", "-newkey", "rsa:3072", "-nodes", "-days", "30",
        "-subj", "/CN=LexiGo private database CA", "-sha256",
        "-keyout", str(PRIVATE / "database-ca.key"),
        "-out", str(PRIVATE / "database-ca.crt"),
        "-addext", "basicConstraints=critical,CA:TRUE",
        "-addext", "keyUsage=critical,keyCertSign,cRLSign",
    )
    run_openssl(
        "req", "-new", "-newkey", "rsa:3072", "-nodes",
        "-subj", "/CN=database", "-keyout", str(PRIVATE / "database.key"),
        "-out", str(PRIVATE / "database.csr"),
    )
    extensions = write_private("database-extensions.conf", "\n".join([
        "basicConstraints=critical,CA:FALSE", "keyUsage=critical,digitalSignature,keyEncipherment",
        "extendedKeyUsage=serverAuth", "subjectAltName=DNS:database", "",
    ]))
    run_openssl(
        "x509", "-req", "-in", str(PRIVATE / "database.csr"),
        "-CA", str(PRIVATE / "database-ca.crt"),
        "-CAkey", str(PRIVATE / "database-ca.key"), "-CAcreateserial", "-days", "30",
        "-sha256", "-extfile", str(extensions), "-out", str(PRIVATE / "database.crt"),
    )
    os.chown(PRIVATE / "database.key", database_uid, database_uid)
    (PRIVATE / "database.key").chmod(0o600)
    for name in ("database.crt", "database-ca.crt"):
        (PRIVATE / name).chmod(0o644)


def validate_input(settings):
    address = ipaddress.ip_address(settings["public_ip"])
    if address.version != 4 or not address.is_global:
        raise ValueError("A public IPv4 address is required.")
    if settings["database_image"] != "lexigo-postgres:patched":
        raise ValueError("Use the tested PostgreSQL release with its pinned base image.")
    if settings["api_image"] != "lexigo-api:deployment":
        raise ValueError("Use the locally tested API release.")
    database_uid = settings.get("database_uid")
    if type(database_uid) is not int or database_uid != 70:
        raise ValueError("Configure the verified non-root PostgreSQL image user.")
    smtp = settings["smtp"]
    for field in ("Username", "Password", "SenderAddress"):
        if not isinstance(smtp.get(field), str) or not smtp[field].strip():
            raise ValueError("Approved SMTP credentials and sender are required.")
    return str(address), smtp, database_uid


def prepare(settings):
    if os.geteuid() != 0 or socket.gethostname() != "lexigo-presentation":
        raise ValueError("Run only as root on the approved presentation server.")
    address, smtp, database_uid = validate_input(settings)
    os.umask(0o077)
    PRIVATE.mkdir(mode=0o700, parents=True, exist_ok=True)
    if any((PRIVATE / name).exists() for name in (
        "compose.env", "appsettings.Production.json", "database.key", "jwt-signing-key.pem",
    )):
        raise ValueError("Configuration already exists; review it instead of rotating credentials.")

    owner_password = secrets.token_hex(32)
    runtime_password = secrets.token_hex(32)
    rpc_secret = secrets.token_hex(32)
    storage_access = "GK" + secrets.token_hex(16)
    storage_secret = secrets.token_hex(32)
    database_certificates(database_uid)
    run_openssl("genpkey", "-algorithm", "RSA", "-pkeyopt", "rsa_keygen_bits:3072",
                "-out", str(PRIVATE / "jwt-signing-key.pem"))
    connection_tail = (
        ";SSL Mode=VerifyFull;Root Certificate=/run/lexigo/database-ca.crt"
        ";GSS Encryption Mode=Disable;Maximum Pool Size=20"
    )
    runtime_connection = (
        f"Host=database;Database=lexigo;Username=lexigo_app;Password={runtime_password}"
        + connection_tail
    )
    owner_connection = (
        f"Host=database;Database=lexigo;Username=lexigo_owner;Password={owner_password}"
        + connection_tail
    )
    application = {
        "ConnectionStrings": {"DefaultConnection": runtime_connection},
        "Authentication": {"SigningPrivateKeyPem": (PRIVATE / "jwt-signing-key.pem").read_text()},
        "ObjectStorage": {
            "Enabled": True, "ServiceUrl": f"https://{address}", "Region": "garage",
            "Bucket": "lexigo-media", "AccessKey": storage_access, "SecretKey": storage_secret,
        },
        "Smtp": {
            "Host": "smtp-relay.brevo.com", "Port": 2525, "DeliveryEnabled": True,
            "Username": smtp["Username"], "Password": smtp["Password"],
            "SenderAddress": smtp["SenderAddress"],
        },
        "Logging": {"LogLevel": {
            "Default": "Information", "Microsoft.AspNetCore": "Warning",
            "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        }},
    }
    application_path = write_private("appsettings.Production.json", json.dumps(application, indent=2) + "\n")
    os.chown(application_path, 0, 1654)
    application_path.chmod(0o640)
    write_private("compose.env", "\n".join([
        f"LEXIGO_HOST={address}", f"LEXIGO_API_IMAGE={settings['api_image']}",
        f"LEXIGO_DATABASE_IMAGE={settings['database_image']}",
        f"LEXIGO_DATABASE_PASSWORD={owner_password}", f"LEXIGO_STORAGE_RPC_SECRET={rpc_secret}",
        f"LEXIGO_STORAGE_ACCESS_KEY={storage_access}", f"LEXIGO_STORAGE_SECRET_KEY={storage_secret}", "",
    ]))
    write_private("migration.env", f"export ConnectionStrings__DefaultConnection='{owner_connection}'\n")
    write_private("runtime-permissions.sql", f"""CREATE ROLE lexigo_app LOGIN PASSWORD '{runtime_password}';
REVOKE ALL ON DATABASE lexigo FROM PUBLIC;
GRANT CONNECT ON DATABASE lexigo TO lexigo_app;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO lexigo_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO lexigo_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO lexigo_app;
ALTER DEFAULT PRIVILEGES FOR ROLE lexigo_owner IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO lexigo_app;
ALTER DEFAULT PRIVILEGES FOR ROLE lexigo_owner IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO lexigo_app;
""")


def main():
    try:
        prepare(json.load(sys.stdin))
        print("Private database TLS, runtime identity, storage, signing and SMTP settings prepared.")
        return 0
    except (OSError, ValueError, KeyError, TypeError, subprocess.CalledProcessError):
        print("Private configuration preparation failed; inspect protected server state before retrying.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
