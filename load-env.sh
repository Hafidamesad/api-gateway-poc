#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
if [ ! -f "$ENV_FILE" ]; then
  echo "ERROR: .env not found at $ENV_FILE. Copy .env.example to .env and fill in real values."
  return 1
fi
set -a
source "$ENV_FILE"
set +a
echo "Environment loaded: JWT_SECRET, HMAC_SECRET, JAVA_HOME, SPRING_PROFILES_ACTIVE"
