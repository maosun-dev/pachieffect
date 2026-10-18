#!/bin/sh
# Unity を裏で動かして、演出の動画（mp4）とコマの一覧を captures/ に書き出す
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures"
rm -f "$ROOT/captures/unity.log"
unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- \
  -executeMethod PachiMoji.EditorTools.PachiMovie.Run -logFile "$ROOT/captures/unity.log"
code=$?
grep -E "error CS|\[PachiMovie\]|Exception" "$ROOT/captures/unity.log" | sort -u | head -40
echo "exit=$code"
