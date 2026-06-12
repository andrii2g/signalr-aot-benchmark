#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"

RID_VALUE="$(get_rid)"
OUT_DIR="$ARTIFACTS_DIR/publish/aot"

rm -rf "$OUT_DIR"

dotnet publish "$ROOT_DIR/src/Chat.Aot.Web/Chat.Aot.Web.csproj" \
  -c Release \
  -r "$RID_VALUE" \
  --self-contained true \
  -p:PublishAot=true \
  -o "$OUT_DIR"

echo "AOT publish output: $OUT_DIR"
echo "AOT publish size bytes: $(publish_size_bytes "$OUT_DIR")"
