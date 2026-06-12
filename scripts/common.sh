#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACTS_DIR="$ROOT_DIR/artifacts"
RESULTS_DIR="$ROOT_DIR/results"
LOGS_DIR="$ROOT_DIR/logs"

mkdir -p "$ARTIFACTS_DIR/publish" "$RESULTS_DIR" "$LOGS_DIR"

get_rid() {
  if [[ -n "${RID:-}" ]]; then
    echo "$RID"
    return
  fi

  local os arch
  os="$(uname -s)"
  arch="$(uname -m)"

  case "$arch" in
    x86_64|amd64) arch="x64" ;;
    arm64|aarch64) arch="arm64" ;;
    *) echo "Unsupported architecture: $arch" >&2; exit 1 ;;
  esac

  case "$os" in
    Linux) echo "linux-$arch" ;;
    Darwin) echo "osx-$arch" ;;
    *) echo "Unsupported OS for Bash scripts: $os. Use WSL for benchmarks, or set RID manually for publish scripts only." >&2; exit 1 ;;
  esac
}

timestamp_ns() {
  python3 - <<'PY'
import time
print(time.time_ns())
PY
}

publish_size_bytes() {
  local dir="$1"
  if command -v python3 >/dev/null 2>&1; then
    python3 - <<PY
import os
root = "$dir"
print(sum(os.path.getsize(os.path.join(dp, f)) for dp, _, fs in os.walk(root) for f in fs))
PY
  else
    find "$dir" -type f -exec wc -c {} + | awk 'END { print $1 }'
  fi
}
