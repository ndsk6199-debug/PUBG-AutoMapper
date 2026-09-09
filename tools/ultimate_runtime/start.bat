@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

set "ADB=C:\platform-tools\adb.exe"
set "SERIAL=RF8N20ZRDFF"
set "SCRCPY=C:\Users\Atef\Downloads\scrcpy-win64-v3.3.4\scrcpy-win64-v3.3.4\scrcpy.exe"
set "PORT=27184"
set "AGENT_APK=%~dp0agent.apk"
set "REMOTE_APK=/data/local/tmp/pubg_automapper_agent.apk"

if not exist "%ADB%" (
  echo ERROR: adb.exe was not found at %ADB%
  pause
  exit /b 1
)
if not exist "%SCRCPY%" (
  echo ERROR: scrcpy.exe was not found at %SCRCPY%
  pause
  exit /b 1
)
if not exist "%AGENT_APK%" (
  echo ERROR: agent.apk was not found next to start.bat.
  echo The final package must contain the built Android agent.
  pause
  exit /b 1
)

"%ADB%" -s %SERIAL% get-state 1>nul 2>nul
if errorlevel 1 (
  echo ERROR: Android device %SERIAL% is not authorized/connected.
  echo Run: adb devices
  pause
  exit /b 1
)

python -m pip show pynput >nul 2>&1
if errorlevel 1 (
  echo Installing required Python package...
  python -m pip install -r requirements.txt
  if errorlevel 1 (
    echo Failed to install requirements.
    pause
    exit /b 1
  )
)

REM Push the exact APK used as the app_process classpath.
echo Preparing Android Input Agent...
"%ADB%" -s %SERIAL% push "%AGENT_APK%" %REMOTE_APK% >nul
if errorlevel 1 (
  echo ERROR: Could not push Android Input Agent APK.
  pause
  exit /b 1
)

REM Kill only a previous copy of this agent, when present.
for /f "tokens=2" %%P in ('"%ADB%" -s %SERIAL% shell "ps -A ^| grep PrivilegedInputMain ^| grep -v grep" 2^>nul') do (
  "%ADB%" -s %SERIAL% shell kill %%P >nul 2>&1
)

REM The mapper is a host TCP client, so use adb FORWARD (host -> device).
"%ADB%" -s %SERIAL% forward --remove tcp:%PORT% >nul 2>&1
"%ADB%" -s %SERIAL% forward tcp:%PORT% tcp:%PORT% >nul 2>&1
if errorlevel 1 (
  echo ERROR: Could not create ADB forward for port %PORT%.
  pause
  exit /b 1
)

REM Launch the privileged Java main class from the pushed APK as the classpath.
start "PUBG Android Input Agent" /b "%ADB%" -s %SERIAL% shell "CLASSPATH=%REMOTE_APK% app_process / com.pubgautomapper.agent.PrivilegedInputMain >/data/local/tmp/pubg_automapper_agent.log 2>&1 &"

timeout /t 2 /nobreak >nul

start "PUBG Mobile - S10 Lite" "%SCRCPY%" -s %SERIAL% --stay-awake --no-audio --max-fps=60 --window-title="PUBG Mobile - S10 Lite"

timeout /t 1 /nobreak >nul
python main.py
pause
