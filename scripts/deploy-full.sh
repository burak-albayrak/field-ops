#!/usr/bin/env bash
set -euo pipefail

readonly repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly expected_branch="main"

cd "$repo_root"

require_clean_main() {
  if [[ "$(git branch --show-current)" != "$expected_branch" ]]; then
    echo "Deployment requires branch '$expected_branch'." >&2
    exit 1
  fi

  if [[ -n "$(git status --porcelain)" ]]; then
    echo "Deployment stopped: the server working tree is dirty." >&2
    exit 1
  fi
}

# The one-time seed bootstrap is deliberately manual: start with Development,
# verify the idempotent demo seed, set .env to Production, then recreate only
# backend with its existing PostgreSQL volume. This script is for later updates.
require_clean_main
git fetch origin
git pull --ff-only origin "$expected_branch"
require_clean_main

docker compose --profile production build
docker compose --profile production up -d
docker compose --profile production ps

site_address="${SITE_ADDRESS:-}"
if [[ -z "$site_address" ]]; then
  site_address="$(docker compose --profile production exec -T caddy printenv SITE_ADDRESS)"
fi

curl --fail --silent --show-error --location --retry 6 --retry-delay 5 --retry-all-errors --output /dev/null "https://${site_address}/"
curl --fail --silent --show-error --location --retry 6 --retry-delay 5 --retry-all-errors --output /dev/null "https://${site_address}/api/visits"
