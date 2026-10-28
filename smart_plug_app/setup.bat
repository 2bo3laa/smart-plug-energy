@echo off
echo === Smart Plug app setup ===
call flutter create --project-name smart_plug_app --platforms android,ios .
if errorlevel 1 goto fail
python patch_android.py
call flutter pub get
echo.
echo Done. Now run:  flutter run
goto end
:fail
echo Flutter not found or failed. Install Flutter and make sure "flutter" works in this terminal.
:end
