"""Delete one explicitly identified presentation server after its deadline."""

import argparse
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import re
import sys
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


API_ORIGIN = "https://api.digitalocean.com/v2/droplets"
EXPECTED_NAME = "lexigo-presentation"
REQUIRED_TAG = "lexigo-expiring"


def timestamp(value):
    if not isinstance(value, str):
        raise ValueError("A UTC timestamp is required.")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        raise ValueError("Expiry timestamps must explicitly use UTC.")
    return parsed


def validate_configuration(configuration):
    identifier = configuration.get("droplet_id")
    if type(identifier) is not int or identifier <= 0:
        raise ValueError("Configure one positive numeric server ID.")
    if configuration.get("name") != EXPECTED_NAME:
        raise ValueError("The expiry target must be lexigo-presentation.")
    created = timestamp(configuration.get("created_at"))
    deadline = timestamp(configuration.get("delete_after"))
    if not created < deadline <= created + timedelta(days=10):
        raise ValueError("The deletion deadline must be within ten days of creation.")
    return identifier, created, deadline


def request_api(method, identifier, token):
    request = Request(
        f"{API_ORIGIN}/{identifier}",
        method=method,
        headers={"Authorization": f"Bearer {token}", "Accept": "application/json"},
    )
    with urlopen(request, timeout=20) as response:
        if method == "DELETE":
            if response.status != 204:
                raise ValueError("The provider did not confirm the deletion request.")
            return None
        return json.load(response)["droplet"]


def expire(configuration, token, now, *, dry_run=False, api=request_api):
    identifier, created, deadline = validate_configuration(configuration)
    if now.tzinfo is None:
        raise ValueError("The current time must include its timezone.")
    if now < deadline:
        return "Not due; no provider request made."
    if not isinstance(token, str) or not re.fullmatch(r"[A-Za-z0-9_]{32,256}", token):
        raise ValueError("A private provider token is required.")

    droplet = api("GET", identifier, token)
    if (
        droplet.get("id") != identifier
        or droplet.get("name") != EXPECTED_NAME
        or REQUIRED_TAG not in droplet.get("tags", [])
        or timestamp(droplet.get("created_at")) != created
    ):
        raise ValueError("Provider identity does not match the approved expiry target.")
    if dry_run:
        return "Dry run: target identity and due deadline verified; nothing deleted."
    api("DELETE", identifier, token)
    return "Provider accepted deletion of the configured LexiGo server."


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--token-file", type=Path, required=True)
    parser.add_argument("--dry-run", action="store_true")
    arguments = parser.parse_args()
    try:
        configuration = json.loads(arguments.config.read_text(encoding="utf-8"))
        token = arguments.token_file.read_text(encoding="utf-8").strip()
        print(expire(configuration, token, datetime.now(timezone.utc), dry_run=arguments.dry_run))
        return 0
    except HTTPError as error:
        print(f"Expiry request failed with provider HTTP status {error.code}; retry required.", file=sys.stderr)
    except (OSError, URLError, ValueError, KeyError, TypeError):
        print("Expiry check failed; review protected configuration and retry.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
