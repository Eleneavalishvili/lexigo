#!/bin/bash
set -euo pipefail
test "$(hostname)" = "lexigo-presentation"

docker run --rm --network host --memory 192m --pids-limit 64 \
    --mount type=bind,source=/etc/letsencrypt,target=/etc/letsencrypt \
    --mount type=bind,source=/var/lib/letsencrypt,target=/var/lib/letsencrypt \
    --mount type=bind,source=/var/log/letsencrypt,target=/var/log/letsencrypt \
    --mount type=bind,source=/var/www/lexigo-acme,target=/var/www/acme \
    lexigo-certbot:patched renew --non-interactive --quiet

compose=(docker compose --env-file /etc/lexigo/private/compose.env
    -f /opt/lexigo/config/deployment/presentation.compose.yml)
"${compose[@]}" exec -T edge nginx -t
"${compose[@]}" exec -T edge nginx -s reload
