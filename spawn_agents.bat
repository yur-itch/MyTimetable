@echo off
set PI=C:\Users\PCyur\Downloads\pi-windows-x64\pi.exe
set BASE=C:\Users\PCyur\source\repos\MyTimetable

start /B cmd /c "cd /d %BASE%-A && %PI% --print @%BASE%\instruct_A.txt --no-session --provider openrouter --model deepseek/deepseek-v4-flash > %BASE%\agent_A.log 2>&1"
start /B cmd /c "cd /d %BASE%-B && %PI% --print @%BASE%\instruct_B.txt --no-session --provider openrouter --model deepseek/deepseek-v4-flash > %BASE%\agent_B.log 2>&1"
start /B cmd /c "cd /d %BASE%-C && %PI% --print @%BASE%\instruct_C.txt --no-session --provider openrouter --model deepseek/deepseek-v4-flash > %BASE%\agent_C.log 2>&1"
start /B cmd /c "cd /d %BASE%-D && %PI% --print @%BASE%\instruct_D.txt --no-session --provider openrouter --model deepseek/deepseek-v4-flash > %BASE%\agent_D.log 2>&1"

echo ALL 4 LAUNCHED
