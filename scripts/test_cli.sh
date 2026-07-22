#!/usr/bin/env bash
cd /c/Users/PCyur/source/repos/MyTimetable
dotnet run --project MyTimetable.Proxy --port 9155 > /dev/null 2>&1 &
sleep 3
./MyTimetable.Cli/mytimetable.exe login --user admin --password admin --host localhost --port 9155 > /dev/null 2>&1
sleep 5
./MyTimetable.Cli/mytimetable.exe schedule "$@" --host localhost --port 9155 2>&1
kill %1 2>/dev/null
