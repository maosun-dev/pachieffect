#!/bin/sh
# Unity を裏で動かして、EditMode の自動テストを流す（結果は captures/test-results.xml）
# ※ unity run は -quit を足してテストが走らないので、unity test を使う
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures"
rm -f "$ROOT/captures/unity-test.log" "$ROOT/captures/test-results.xml"
unity test "$ROOT/PachiMoji" --mode EditMode --output "$ROOT/captures/test-results.xml" --no-banner \
  > "$ROOT/captures/unity-test.log" 2>&1
code=$?
grep -E "error CS" "$ROOT/captures/unity-test.log" | sort -u | head -20
if [ -f "$ROOT/captures/test-results.xml" ]; then
  grep -o '<test-run [^>]*' "$ROOT/captures/test-results.xml" | grep -oE '(result|total|passed|failed)="[^"]*"' | tr '\n' ' '; echo
  # 失敗したテストの名前と理由
  grep -A3 '<test-case [^>]*result="Failed"' "$ROOT/captures/test-results.xml" | grep -oE ' name="[^"]*"|<message><!\[CDATA\[[^]]*' | head -40
else
  echo "結果のファイルができていない（コンパイルエラーなど）"; tail -15 "$ROOT/captures/unity-test.log"
fi
echo "exit=$code"
