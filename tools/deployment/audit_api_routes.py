"""Inventory documented routes against a locally exported OpenAPI document.

This read-only report establishes route presence, never permission or behavior.
Health probes are intentionally absent from OpenAPI and remain separate checks.
"""

import argparse
import json
from pathlib import Path
import re


def normalized(route):
    return re.sub(r"\{[^}]+\}", "{}", route.split("?")[0].rstrip("/"))


def audit(register, specification):
    methods = {"get", "post", "put", "patch", "delete"}
    operations = {
        (method.upper(), normalized(route))
        for route, details in specification["paths"].items()
        for method in details
        if method in methods
    }
    matched, unresolved, probes, missing = [], [], [], []
    for item in register:
        entry = {"method": item["method"], "route": item["document_route"]}
        route = entry["route"]
        if route.startswith("/health/"):
            probes.append(entry)
        elif "{resource}" in route or "[" in route:
            unresolved.append(entry)
        elif (entry["method"], normalized(route)) in operations:
            matched.append(entry)
        else:
            missing.append(entry)
    return {
        "register_entries": len(register),
        "actual_operations": len(operations),
        "matched_concrete_routes": matched,
        "unresolved_shorthand": unresolved,
        "separate_health_probes": probes,
        "missing_concrete_routes": missing,
        "limit": "Route presence only. No behavior, access rule or checklist completion is inferred.",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("openapi", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--register", type=Path, default=Path("docs/api-register.json"))
    arguments = parser.parse_args()
    report = audit(
        json.loads(arguments.register.read_text(encoding="utf-8-sig")),
        json.loads(arguments.openapi.read_text(encoding="utf-8-sig")),
    )
    arguments.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Matched {len(report['matched_concrete_routes'])} concrete routes; "
          f"{len(report['unresolved_shorthand'])} shorthand entries need resource reconciliation; "
          f"{len(report['missing_concrete_routes'])} concrete routes missing.")
    return 1 if report["missing_concrete_routes"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
