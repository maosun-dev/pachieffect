#!/bin/sh
# Unity を裏で動かして演出を1コマずつ PNG に描き、ffmpeg で mp4 にまとめる（captures/）
# ※ Unity の MediaEncoder は画質が足りず崩れたので使わない
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures"
rm -f "$ROOT/captures/unity.log"
unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- \
  -executeMethod PachiMoji.EditorTools.PachiMovie.Run -logFile "$ROOT/captures/unity.log"
code=$?
grep -E "error CS|\[PachiMovie\]|Exception" "$ROOT/captures/unity.log" | sort -u | head -40
if [ $code -eq 0 ]; then
  # 画質優先（crf が小さいほど高画質）。iPhone でも再生できる形式にする
  ffmpeg -v error -y -framerate 30 -i "$ROOT/captures/frames/f%04d.png" \
    -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p -movflags +faststart \
    "$ROOT/captures/movie_teiji.mp4" && rm -rf "$ROOT/captures/frames"
  code=$?
fi
echo "exit=$code"
