"""Adds INTERNET permission + cleartext HTTP to the Flutter-generated AndroidManifest (run after `flutter create .`)."""
import re, sys
p = "android/app/src/main/AndroidManifest.xml"
try:
    s = open(p, encoding="utf-8").read()
except FileNotFoundError:
    sys.exit("AndroidManifest.xml not found - run `flutter create --platforms android,ios .` first")
if "android.permission.INTERNET" not in s:
    s = s.replace("<manifest", "<manifest", 1)
    s = re.sub(r"(<manifest[^>]*>)", r'\1\n    <uses-permission android:name="android.permission.INTERNET"/>', s, count=1)
if "usesCleartextTraffic" not in s:
    s = s.replace("<application", '<application\n        android:usesCleartextTraffic="true"', 1)
s = s.replace('android:label="smart_plug_app"', 'android:label="Smart Energy"')
open(p, "w", encoding="utf-8").write(s)
print("AndroidManifest patched OK")
