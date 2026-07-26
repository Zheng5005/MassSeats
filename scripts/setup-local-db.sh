#!/usr/bin/env bash
# setup-local-db.sh — Propagates .env to all service appsettings.json files.
#
# Usage:
#   ./scripts/setup-local-db.sh
#
# Requires: .env in the repo root (copy .env.example if missing).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "ERROR: .env not found at $ENV_FILE"
  echo "Copy .env.example to .env and edit the password."
  exit 1
fi

# Read .env (ignore comments and empty lines)
while IFS='=' read -r key value; do
  [[ -z "$key" || "$key" =~ ^# ]] && continue
  # Trim whitespace
  key="$(echo "$key" | xargs)"
  value="$(echo "$value" | xargs)"
  export "$key=$value"
done < "$ENV_FILE"

if [[ -z "${POSTGRES_PASSWORD:-}" ]]; then
  echo "ERROR: POSTGRES_PASSWORD is not set in .env"
  exit 1
fi

CONN_STRING="Host=${POSTGRES_HOST:-localhost};Port=${POSTGRES_PORT:-5432};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD}"

echo "Using connection string components:"
echo "  Host:     ${POSTGRES_HOST:-localhost}"
echo "  Port:     ${POSTGRES_PORT:-5432}"
echo "  User:     ${POSTGRES_USER:-postgres}"
echo "  Password: ****"
echo ""

# Service name → database name mapping
declare -A SERVICES=(
  ["UserService"]="userservice"
  ["EventService"]="eventservice"
  ["BookingService"]="bookingservice"
  ["PaymentService"]="paymentservice"
)

# Connection string key name per service (matches appsettings.json keys)
declare -A DB_KEYS=(
  ["UserService"]="UserDb"
  ["EventService"]="EventDb"
  ["BookingService"]="BookingDb"
  ["PaymentService"]="PaymentDb"
)

# Design-time factory context name (removes "Service" suffix)
declare -A CONTEXTS=(
  ["UserService"]="User"
  ["EventService"]="Event"
  ["BookingService"]="Booking"
  ["PaymentService"]="Payment"
)

UPDATED=0

for SERVICE in "${!SERVICES[@]}"; do
  DB_NAME="${SERVICES[$SERVICE]}"
  APPSETTINGS="$REPO_ROOT/services/$SERVICE/src/${SERVICE}.API/appsettings.json"

  if [[ ! -f "$APPSETTINGS" ]]; then
    echo "  SKIP  $SERVICE — appsettings.json not found"
    continue
  fi

  FULL_CONN="${CONN_STRING};Database=${DB_NAME}"

  # Use python3 for reliable JSON manipulation (available on all dev machines)
  python3 -c "
import json, sys

path = sys.argv[1]
conn = sys.argv[2]

with open(path) as f:
    data = json.load(f)

if 'ConnectionStrings' not in data:
    data['ConnectionStrings'] = {}

data['ConnectionStrings']['${DB_KEYS[$SERVICE]}'] = conn

# Remove any stale keys from previous script runs (e.g. BOOKINGSERVICEDb)
expected_key = '${DB_KEYS[$SERVICE]}'
service_upper = '${SERVICE}'.upper()
for key in list(data['ConnectionStrings'].keys()):
    if key != expected_key and service_upper in key.upper():
        del data['ConnectionStrings'][key]

with open(path, 'w') as f:
    json.dump(data, f, indent=2)
    f.write('\n')
" "$APPSETTINGS" "$FULL_CONN"

  echo "  OK    $SERVICE → $APPSETTINGS"
  UPDATED=$((UPDATED + 1))

  # Also update the design-time factory (used by `dotnet ef` CLI)
  CTX="${CONTEXTS[$SERVICE]}"
  FACTORY="$REPO_ROOT/services/$SERVICE/src/${SERVICE}.Infrastructure/Persistence/${CTX}DbContextFactory.cs"

  if [[ -f "$FACTORY" ]]; then
    # Replace the UseNpgsql("...") connection string
    python3 -c "
import re, sys

path = sys.argv[1]
conn = sys.argv[2]

with open(path) as f:
    content = f.read()

# Match UseNpgsql(\"...\") and replace the connection string inside
pattern = r'(UseNpgsql\(\")([^\"]*)(\"\))'
replacement = r'\g<1>' + conn + r'\3'
new_content = re.sub(pattern, replacement, content)

with open(path, 'w') as f:
    f.write(new_content)
" "$FACTORY" "$FULL_CONN"
    echo "  OK    $SERVICE → $FACTORY (design-time)"
  fi
done

echo ""
echo "Updated $UPDATED services. Connection strings are ready."
echo "Run 'dotnet run --project services/<Service>/src/<Service>.API' to start."
