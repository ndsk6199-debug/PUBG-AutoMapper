@echo off
cd /d "%~dp0"
if "%~1"=="" (
  echo Usage: calibrate_hud.bat path-to-pubg-screenshot.jpg
  pause
  exit /b 1
)
python -m pip install -r requirements-hud.txt
python hud_auto_calibrator.py "%~1" -o hud_profile_auto.json --preview hud_preview.png
pause
