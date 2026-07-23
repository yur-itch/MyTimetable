#!/bin/bash
Z="../MyTimetable.Cli/zopfli.exe"
T="../MyTimetable.Cli/mytimetable.exe"

size() { "$Z" --i$1 -c "$T" 2>/dev/null | wc -c; }

# Baseline: very high iteration to get "optimal" size
echo "=== Getting optimal (i=500) ==="
OPTIMAL=$(size 500)
echo "optimal size: $OPTIMAL"

# Binary search for lowest iteration that hits optimal
lo=1
hi=500
while [ $lo -lt $hi ]; do
  mid=$(( (lo + hi) / 2 ))
  s=$(size $mid)
  echo "i=$mid size=$s  [lo=$lo hi=$hi]"
  if [ "$s" -le "$OPTIMAL" ]; then
    hi=$mid
  else
    lo=$((mid + 1))
  fi
done

echo ""
echo "=== Convergence at i=$lo ==="
echo "i=$lo size=$(size $lo)"

# Also check one below and one above
echo "i=$((lo-1)) size=$(size $((lo-1)))"
echo "i=$((lo+1)) size=$(size $((lo+1)))"

# Show the curve around the convergence point
echo ""
echo "=== Full curve near convergence ==="
for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 25 30 40 50; do
  [ $i -gt 50 ] && break
  echo "i=$i $(size $i)"
done
