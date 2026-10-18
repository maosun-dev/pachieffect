#!/bin/sh
# Unity を裏で動かして演出を1コマずつ PNG に描き、ffmpeg で mp4 にまとめる（captures/）
# ※ Unity の MediaEncoder は画質が足りず崩れたので使わない
#
# 使い方：
#   sh movie.sh                 … 全体を高画質で（captures/movie_teiji.mp4 と movie_teiji_sheet.png）
#   sh movie.sh 7 9.5           … 7〜9.5 秒だけ（captures/movie_part.mp4 と movie_part_sheet.png。0.1 秒ごとの一覧）
#   sh movie.sh 7 9.5 quick     … さらに確認用の画質で速く（2倍で描いて縮めるのをやめる）
ROOT="C:/Users/USER/Desktop/game/pachieffect"
mkdir -p "$ROOT/captures"
rm -f "$ROOT/captures/unity.log"
export PACHI_FROM="${1:-0}" PACHI_TO="${2:--1}" PACHI_QUICK=0
[ "$3" = "quick" ] && PACHI_QUICK=1
OUT="$ROOT/captures/movie_teiji.mp4"
[ -n "$1" ] && OUT="$ROOT/captures/movie_part.mp4"
unity run "$ROOT/PachiMoji" --editor-version 6000.6.6f1 --no-banner -- \
  -executeMethod PachiMoji.EditorTools.PachiMovie.Run -logFile "$ROOT/captures/unity.log"
code=$?
grep -E "error CS|\[PachiMovie\]|Exception" "$ROOT/captures/unity.log" | sort -u | head -40
if [ $code -eq 0 ]; then
  # 画質優先（crf が小さいほど高画質）。iPhone でも再生できる形式にする
  ffmpeg -v error -y -framerate 30 -i "$ROOT/captures/frames/f%04d.png" \
    -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p -movflags +faststart \
    "$OUT" && rm -rf "$ROOT/captures/frames"
  code=$?
fi
echo "exit=$code"
