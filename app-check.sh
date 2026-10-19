#!/bin/sh
# Unity を裏で動かして、アプリ化の手元確認（くり返し再生・設定・絵）をする
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures"
rm -f "$ROOT/captures/unity.log"
unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- \
  -force-d3d11 -executeMethod PachiMoji.EditorTools.PachiAppCheck.Run -logFile "$ROOT/captures/unity.log"
code=$?
grep -E "error CS|\[PachiAppCheck\]|Exception" "$ROOT/captures/unity.log" | sort -u | head -40
echo "exit=$code"
