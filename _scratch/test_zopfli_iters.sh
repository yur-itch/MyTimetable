#!/bin/bash
# Binary search to find where zopfli iterations stop decreasing size
ZOPFLI="../MyTimetable.Cli/zopfli.exe"
TARGET="../MyTimetable.Cli/mytimetable.exe"

compress_size() {
  "$ZOPFLI" --i$1 -c "$TARGET" 2>/dev/null | wc -c
}

# First, find an upper bound where size stabilizes by testing powers of 2
echo "=== Finding rough range ==="
prev=-1
for i in 1 2 4 8 16 32 64 128 256 512 1024 2048; do
  size=$(compress_size $i)
  echo "i=$i size=$size"
  if [ "$prev" != "-1" ] && [ "$size" = "$prev" ]; then
    echo "Size stabilized at i=$i"
    break
  fi
  prev=$size
done

# Now get precise convergence with finer steps around the plateau
echo ""
echo "=== Refining convergence point ==="
echo "i=1  size=$(compress_size 1)"
echo "i=2  size=$(compress_size 2)"
echo "i=3  size=$(compress_size 3)"
echo "i=4  size=$(compress_size 4)"
echo "i=5  size=$(compress_size 5)"
echo "i=6  size=$(compress_size 6)"
echo "i=7  size=$(compress_size 7)"
echo "i=8  size=$(compress_size 8)"
echo "i=9  size=$(compress_size 9)"
echo "i=10 size=$(compress_size 10)"
echo "i=12 size=$(compress_size 12)"
echo "i=15 size=$(compress_size 15)"
echo "i=20 size=$(compress_size 20)"
echo "i=25 size=$(compress_size 25)"
echo "i=30 size=$(compress_size 30)"
echo "i=50 size=$(compress_size 50)"
echo "i=100 size=$(compress_size 100)"
