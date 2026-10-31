@echo off
cd /d "%~dp0"
where git >nul 2>nul
if errorlevel 1 (
  echo Git is not installed. Install it from https://git-scm.com/download/win and run this file again.
  pause
  exit /b 1
)
echo This REPLACES the whole content of the GitHub repo with this folder.
pause
if not exist .git git init
git config user.name "smart-plug"
git config user.email "smart-plug@example.com"
git checkout -B main
git add -A
git commit -m "Full project update"
git remote remove origin 2>nul
git remote add origin https://github.com/2bo3laa/smart-plug-energy.git
git push -f origin main
echo.
echo Done. Now open GitHub - Actions - Publish API - Run workflow.
pause
