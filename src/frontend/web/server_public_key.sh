#!/bin/sh
set -eu

KEY_FILE=/shared/opaque_server_public_key.key
CONFIG_JS=/usr/share/nginx/html/config.js

i=0
while [ ! -s "$KEY_FILE" ]; do
  i=$((i+1))
  if [ "$i" -gt 60 ]; then
    echo "FATAL: OPAQUE public key not available from backend within 60s" >&2
    exit 1
  fi
  sleep 1
done

PUBLIC_KEY=$(cat "$KEY_FILE")

cat > "$CONFIG_JS" <<EOF
window.__NK_CONFIG__ = {
  OPAQUE_SERVER_PUBLIC_KEY: "${PUBLIC_KEY}"
};
EOF

exec nginx -g "daemon off;"
