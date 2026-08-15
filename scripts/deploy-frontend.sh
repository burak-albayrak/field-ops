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

require_clean_main
git fetch origin
git pull --ff-only origin "$expected_branch"
require_clean_main

docker compose --profile production build frontend
docker compose --profile production up -d --no-deps frontend
docker compose --profile production ps frontend

site_address="${SITE_ADDRESS:-}"
if [[ -z "$site_address" ]]; then
  site_address="$(docker compose --profile production exec -T caddy printenv SITE_ADDRESS)"
fi

curl --fail --silent --show-error --location --retry 6 --retry-delay 5 --retry-all-errors --output /dev/null "https://${site_address}/"
