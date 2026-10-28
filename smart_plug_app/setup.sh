#!/bin/sh
set -e
flutter create --project-name smart_plug_app --platforms android,ios .
python3 patch_android.py
flutter pub get
echo "Done. Now run: flutter run"
