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
  key="$(echo "$key" | xargs)"
  value="$(echo "$value" | xargs)"
  export "$key=$value"
done < "$ENV_FILE"

# ── 1. PostgreSQL connection strings ────────────────────────────────

if [[ -z "${POSTGRES_PASSWORD:-}" ]]; then
  echo "ERROR: POSTGRES_PASSWORD is not set in .env"
  exit 1
fi

CONN_STRING="Host=${POSTGRES_HOST:-localhost};Port=${POSTGRES_PORT:-5432};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD}"

echo "PostgreSQL:"
echo "  Host:     ${POSTGRES_HOST:-localhost}"
echo "  Port:     ${POSTGRES_PORT:-5432}"
echo "  User:     ${POSTGRES_USER:-postgres}"
echo "  Password: ****"

# Service → database name
declare -A DB_NAMES=(
  [UserService]="userservice"
  [EventService]="eventservice"
  [BookingService]="bookingservice"
  [PaymentService]="paymentservice"
)

# Connection string key per service
declare -A DB_KEYS=(
  [UserService]="UserDb"
  [EventService]="EventDb"
  [BookingService]="BookingDb"
  [PaymentService]="PaymentDb"
)

# Design-time factory context name
declare -A CONTEXTS=(
  [UserService]="User"
  [EventService]="Event"
  [BookingService]="Booking"
  [PaymentService]="Payment"
)

echo ""
for SERVICE in "${!DB_NAMES[@]}"; do
  DB_NAME="${DB_NAMES[$SERVICE]}"
  APPSETTINGS="$REPO_ROOT/services/$SERVICE/src/${SERVICE}.API/appsettings.json"

  [[ -f "$APPSETTINGS" ]] || { echo "  SKIP  $SERVICE — appsettings.json not found"; continue; }

  FULL_CONN="${CONN_STRING};Database=${DB_NAME}"

  python3 -c "
import json, sys

path = sys.argv[1]; conn = sys.argv[2]
expected_key = sys.argv[3]; service_upper = sys.argv[4]

with open(path) as f:
    data = json.load(f)

if 'ConnectionStrings' not in data:
    data['ConnectionStrings'] = {}

data['ConnectionStrings'][expected_key] = conn

# Remove stale keys (e.g. BOOKINGSERVICEDb)
for key in list(data['ConnectionStrings'].keys()):
    if key != expected_key and service_upper in key.upper():
        del data['ConnectionStrings'][key]

with open(path, 'w') as f:
    json.dump(data, f, indent=2)
    f.write('\n')
" "$APPSETTINGS" "$FULL_CONN" "${DB_KEYS[$SERVICE]}" "${SERVICE^^}"

  echo "  DB    $SERVICE → ${DB_KEYS[$SERVICE]}"

  # Design-time factory
  CTX="${CONTEXTS[$SERVICE]}"
  FACTORY="$REPO_ROOT/services/$SERVICE/src/${SERVICE}.Infrastructure/Persistence/${CTX}DbContextFactory.cs"
  if [[ -f "$FACTORY" ]]; then
    python3 -c "
import re, sys

path = sys.argv[1]; conn = sys.argv[2]
with open(path) as f:
    content = f.read()
pattern = r'(UseNpgsql\(\")([^\"]*)(\"\))'
new_content = re.sub(pattern, r'\g<1>' + conn + r'\3', content)
with open(path, 'w') as f:
    f.write(new_content)
" "$FACTORY" "$FULL_CONN"
    echo "  FACT  $SERVICE → design-time factory"
  fi
done

# ── 2. RabbitMQ configuration ───────────────────────────────────────

RABBIT_HOST="${RABBITMQ_HOST:-localhost}"
RABBIT_PORT="${RABBITMQ_PORT:-5672}"
RABBIT_USER="${RABBITMQ_USER:-guest}"
RABBIT_PASS="${RABBITMQ_PASSWORD:-guest}"

echo ""
echo "RabbitMQ:"
echo "  Host:     $RABBIT_HOST"
echo "  Port:     $RABBIT_PORT"
echo "  User:     $RABBIT_USER"
echo "  Password: ****"

# Queue names per service (architecture topology, not secrets)
declare -A QUEUE_NAMES=(
  [BookingService]="booking.queue"
  [EventService]="event.queue"
  [PaymentService]="payment.queue"
)

# Services that need RabbitMQ config (UserService does NOT participate)
RABBIT_SERVICES=("BookingService" "EventService" "PaymentService")

for SERVICE in "${RABBIT_SERVICES[@]}"; do
  APPSETTINGS="$REPO_ROOT/services/$SERVICE/src/${SERVICE}.API/appsettings.json"

  [[ -f "$APPSETTINGS" ]] || { echo "  SKIP  $SERVICE — appsettings.json not found"; continue; }

  QNAME="${QUEUE_NAMES[$SERVICE]}"

  python3 -c "
import json, sys

path = sys.argv[1]
host = sys.argv[2]
port = int(sys.argv[3])
user = sys.argv[4]
password = sys.argv[5]
qname = sys.argv[6]

with open(path) as f:
    data = json.load(f)

data['RabbitMq'] = {
    'Host': host,
    'Port': port,
    'UserName': user,
    'Password': password,
    'VirtualHost': '/',
    'ExchangeName': 'massseats.events',
    'DeadLetterExchangeName': 'massseats.events.dead-letter',
    'QueueName': qname,
    'PrefetchCount': 16
}

with open(path, 'w') as f:
    json.dump(data, f, indent=2)
    f.write('\n')
" "$APPSETTINGS" "$RABBIT_HOST" "$RABBIT_PORT" "$RABBIT_USER" "$RABBIT_PASS" "$QNAME"

  echo "  RMQ   $SERVICE → $QNAME"
done

echo ""
echo "Done. Settings propagated to all services."
echo "Run 'dotnet run --project services/<Service>/src/<Service>.API' to start."
