FROM postgres:17-alpine@sha256:f9cf3e1f0066f0eb0c48707d864f0ac5941e9ff454a072077d97a4d278d07d73
RUN apk upgrade --no-cache && rm -f /usr/local/bin/gosu
# Volume and socket directories already belong to this user in the base image.
# Starting without root also removes the need for the Go-based gosu helper.
USER 70:70
