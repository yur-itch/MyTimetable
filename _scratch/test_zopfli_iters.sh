#!/bin/bash
cd c:/Users/PCyur/source/repos/MyTimetable/_scratch
ZOPFLI="../MyTimetable.Cli/zopfli.exe"
TARGET="../MyTimetable.Cli/mytimetable.exe"

for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 22 25 28 30 35 40 50 60 75 100 150 200 300 500 1000; do
  size=$("$ZOPFLI" --i$i -c "$TARGET" 2>/dev/null | wc -c)
  echo "$i $size"
done
