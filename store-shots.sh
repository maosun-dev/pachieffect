#!/bin/sh
# App Store の画面写真（1206×2622）を captures/store/ に撮る
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures/store"
rm -f "$ROOT/captures/unity-store.log"
unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- \
  -force-d3d11 -executeMethod PachiMoji.EditorTools.PachiAppCheck.RunStoreShots -logFile "$ROOT/captures/unity-store.log"
code=$?
grep -E "error CS|\[PachiAppCheck\]|Exception" "$ROOT/captures/unity-store.log" | sort -u | head -20
echo "exit=$code"
