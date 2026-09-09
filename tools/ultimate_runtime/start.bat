@echo off
setlocal
cd /d "%~dp0"

if not exist "C:\platform-tools\adb.exe" (
  echo ERROR: adb.exe was not found at C:\platform-tools\adb.exe
  pause
  exit /b 1
)

if not exist "C:\Users\Atef\Downloads\scrcpy-win64-v3.3.4\scrcpy-win64-v3.3.4\scrcpy.exe" (
  echo ERROR: scrcpy.exe was not found at the configured path.
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

adb -s RF8N20ZRDFF reverse tcp:27183 localabstract:pubg_automapper
if errorlevel 1 (
  echo Failed to create ADB reverse tunnel. Check the phone connection.
  pause
  exit /b 1
)

start "PUBG Mobile - S10 Lite" "C:\Users\Atef\Downloads\scrcpy-win64-v3.3.4\scrcpy-win64-v3.3.4\scrcpy.exe" -s RF8N20ZRDFF --stay-awake --no-audio --max-fps=60 --window-title="PUBG Mobile - S10 Lite"

timeout /t 2 /nobreak >nul
python main.py
pause
