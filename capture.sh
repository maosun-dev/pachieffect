#!/bin/sh
# Unity を裏で動かして、文字の確認用画像を captures/ に撮る
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures"
rm -f "$ROOT/captures/unity.log"
unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- \
  -executeMethod PachiMoji.EditorTools.PachiCapture.Run -logFile "$ROOT/captures/unity.log"
code=$?
grep -E "error CS|\[PachiCapture\]|Exception" "$ROOT/captures/unity.log" | sort -u | head -40
echo "exit=$code"
