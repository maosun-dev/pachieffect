#!/bin/sh
# Unity を裏で動かして、激アツの帯を captures/obi_*.png に撮る（1回2〜3分）
# PACHI_PHOTO=画像のパス sh obi.sh … 写真ありの画像にその写真を使う
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures"
rm -f "$ROOT/captures/unity.log"
unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- \
  -executeMethod PachiMoji.EditorTools.ObiCapture.Run -logFile "$ROOT/captures/unity.log"
code=$?
grep -E "error CS|\[ObiCapture\]|Exception" "$ROOT/captures/unity.log" | sort -u | head -40
echo "exit=$code"
