#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"

if [[ ! -d "$ARTIFACTS_DIR/publish/jit" ]]; then
  "$ROOT_DIR/scripts/publish-jit.sh"
fi
if [[ ! -d "$ARTIFACTS_DIR/publish/aot" ]]; then
  "$ROOT_DIR/scripts/publish-aot.sh"
fi

dotnet build "$ROOT_DIR/bench/Chat.LoadClient/Chat.LoadClient.csproj" -c Release

JIT_STATUS=0
AOT_STATUS=0

"$ROOT_DIR/scripts/run-one.sh" jit "${JIT_PORT:-5201}" || JIT_STATUS=$?
"$ROOT_DIR/scripts/run-one.sh" aot "${AOT_PORT:-5202}" || AOT_STATUS=$?

if [[ -f "$RESULTS_DIR/jit.json" && -f "$RESULTS_DIR/jit.server.json" && -f "$RESULTS_DIR/aot.json" && -f "$RESULTS_DIR/aot.server.json" ]]; then
  python3 "$ROOT_DIR/scripts/make-report.py" \
    --jit-client "$RESULTS_DIR/jit.json" \
    --jit-server "$RESULTS_DIR/jit.server.json" \
    --aot-client "$RESULTS_DIR/aot.json" \
    --aot-server "$RESULTS_DIR/aot.server.json" \
    --output "$RESULTS_DIR/report.md"

  echo "Benchmark report: $RESULTS_DIR/report.md"
else
  echo "Skipping report generation because one or more result JSON files are missing." >&2
fi

if [[ "$JIT_STATUS" -ne 0 || "$AOT_STATUS" -ne 0 ]]; then
  echo "One or more benchmark runs failed: jit=$JIT_STATUS aot=$AOT_STATUS" >&2
  exit 1
fi
