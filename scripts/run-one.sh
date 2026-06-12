#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"

MODE="${1:?Usage: run-one.sh <jit|aot> <port>}"
PORT="${2:?Usage: run-one.sh <jit|aot> <port>}"

case "$MODE" in
  jit) APP_NAME="Chat.Jit.Web" ;;
  aot) APP_NAME="Chat.Aot.Web" ;;
  *) echo "Unknown mode: $MODE" >&2; exit 1 ;;
esac

APP_DIR="$ARTIFACTS_DIR/publish/$MODE"
APP_PATH="$APP_DIR/$APP_NAME"
if [[ ! -x "$APP_PATH" && -x "$APP_PATH.exe" ]]; then
  APP_PATH="$APP_PATH.exe"
fi
if [[ ! -x "$APP_PATH" ]]; then
  echo "Published app not found or not executable: $APP_PATH" >&2
  echo "Run ./scripts/publish-$MODE.sh first." >&2
  exit 1
fi

LOG_FILE="$LOGS_DIR/$MODE.server.log"
SERVER_JSON="$RESULTS_DIR/$MODE.server.json"
CLIENT_JSON="$RESULTS_DIR/$MODE.json"
PEAK_FILE="$RESULTS_DIR/$MODE.peak-rss.tmp"
: > "$LOG_FILE"
echo 0 > "$PEAK_FILE"

cleanup() {
  if [[ -n "${SAMPLER_PID:-}" ]]; then kill "$SAMPLER_PID" 2>/dev/null || true; fi
  if [[ -n "${SERVER_PID:-}" ]]; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT

START_NS="$(timestamp_ns)"
ASPNETCORE_URLS="http://127.0.0.1:$PORT" \
DOTNET_ENVIRONMENT=Production \
"$APP_PATH" > "$LOG_FILE" 2>&1 &
SERVER_PID=$!

for _ in {1..150}; do
  if curl -fsS "http://127.0.0.1:$PORT/ready" >/dev/null 2>&1; then
    break
  fi
  if ! kill -0 "$SERVER_PID" 2>/dev/null; then
    echo "Server exited before ready. Log:" >&2
    cat "$LOG_FILE" >&2
    exit 1
  fi
  sleep 0.1
done

if ! curl -fsS "http://127.0.0.1:$PORT/ready" >/dev/null 2>&1; then
  echo "Server did not become ready. Log:" >&2
  cat "$LOG_FILE" >&2
  exit 1
fi
END_NS="$(timestamp_ns)"
STARTUP_MS=$(( (END_NS - START_NS) / 1000000 ))

(
  while kill -0 "$SERVER_PID" 2>/dev/null; do
    rss="$(ps -o rss= -p "$SERVER_PID" 2>/dev/null | tr -d ' ' || echo 0)"
    rss="${rss:-0}"
    peak="$(cat "$PEAK_FILE")"
    if [[ "$rss" =~ ^[0-9]+$ && "$rss" -gt "$peak" ]]; then
      echo "$rss" > "$PEAK_FILE"
    fi
    sleep 0.2
  done
) &
SAMPLER_PID=$!

dotnet run -c Release --no-build --project "$ROOT_DIR/bench/Chat.LoadClient/Chat.LoadClient.csproj" -- \
  --name "$MODE" \
  --url "http://127.0.0.1:$PORT/chat" \
  --connections "${CONNECTIONS:-100}" \
  --messages-per-connection "${MESSAGES_PER_CONNECTION:-100}" \
  --message-size "${MESSAGE_SIZE:-128}" \
  --receive-timeout-seconds "${RECEIVE_TIMEOUT_SECONDS:-30}" \
  --output "$CLIENT_JSON"

PEAK_RSS_KB="$(cat "$PEAK_FILE")"
SIZE_BYTES="$(publish_size_bytes "$APP_DIR")"

python3 - <<PY
import json
from pathlib import Path
payload = {
  "mode": "$MODE",
  "port": $PORT,
  "startupMs": $STARTUP_MS,
  "peakRssKb": int("$PEAK_RSS_KB"),
  "publishSizeBytes": int("$SIZE_BYTES")
}
Path("$SERVER_JSON").write_text(json.dumps(payload, indent=2) + "\n")
PY

echo "$MODE server metrics: $SERVER_JSON"
echo "$MODE client metrics: $CLIENT_JSON"
