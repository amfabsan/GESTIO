#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-https://localhost:7001/openapi/v1/openapi.json}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$ROOT/frontend/src/types"
npx openapi-typescript "$API_URL" -o "$ROOT/frontend/src/types/api.ts"
