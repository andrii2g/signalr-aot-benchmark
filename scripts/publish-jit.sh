#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"

RID_VALUE="$(get_rid)"
OUT_DIR="$ARTIFACTS_DIR/publish/jit"

rm -rf "$OUT_DIR"

dotnet publish "$ROOT_DIR/src/Chat.Jit.Web/Chat.Jit.Web.csproj" \
  -c Release \
  -r "$RID_VALUE" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o "$OUT_DIR"

echo "JIT publish output: $OUT_DIR"
echo "JIT publish size bytes: $(publish_size_bytes "$OUT_DIR")"
