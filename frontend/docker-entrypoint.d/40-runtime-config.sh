#!/bin/sh
# Writes the browser's runtime config from environment variables when the container starts,
# so the same image can point at any backend URL without a rebuild.
set -eu

api_base_url="${API_BASE_URL:-http://localhost:8080}"
# Quotes and backslashes never belong in a URL; dropping them keeps the JS string well-formed.
safe_url=$(printf '%s' "$api_base_url" | tr -d '"\\')

printf 'window.__APP_CONFIG__ = { API_BASE_URL: "%s" };\n' "$safe_url" > /usr/share/nginx/html/config.js
echo "runtime-config: API_BASE_URL=$safe_url"
