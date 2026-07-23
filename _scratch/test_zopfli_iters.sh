#!/bin/bash
Z="../MyTimetable.Cli/zopfli.exe"
T="../MyTimetable.Cli/mytimetable.exe"

# Run all in parallel, write to temp files
outdir=$(mktemp -d)
for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 20 25 30 40 50 100 200 500; do
  ("$Z" --i$i -c "$T" 2>/dev/null | wc -c > "$outdir/i$i") &
done
wait

echo "iteration size  delta_from_best"
best=999999999
# First pass: find minimum
for f in "$outdir"/i*; do
  s=$(cat "$f")
  [ "$s" -lt "$best" ] && best=$s
done

# Second pass: print sorted
for f in "$outdir"/i*; do
  i=$(basename "$f" | sed 's/i//')
  s=$(cat "$f")
  delta=$((s - best))
  printf "%-10s %-8s %+d\n" "i=$i" "$s" "$delta"
done | sort -t= -k2 -n

rm -rf "$outdir"
