"""Patches the Flutter-generated Android project (run after `flutter create .`):
  - INTERNET permission, cleartext HTTP, app label
  - phone notifications: POST_NOTIFICATIONS permission and, when android_config/google-services.json exists,
    the Firebase (Google services) Gradle plugin + minSdk 23 so firebase_messaging builds.
"""
import os, re, shutil, sys

# ---------------------------------------------------------------- manifest
p = "android/app/src/main/AndroidManifest.xml"
try:
    s = open(p, encoding="utf-8").read()
except FileNotFoundError:
    sys.exit("AndroidManifest.xml not found - run `flutter create --platforms android .` first")
if "android.permission.INTERNET" not in s:
    s = re.sub(r"(<manifest[^>]*>)", r'\1\n    <uses-permission android:name="android.permission.INTERNET"/>', s, count=1)
if "android.permission.POST_NOTIFICATIONS" not in s:
    s = re.sub(r"(<manifest[^>]*>)", r'\1\n    <uses-permission android:name="android.permission.POST_NOTIFICATIONS"/>', s, count=1)
if "usesCleartextTraffic" not in s:
    s = s.replace("<application", '<application\n        android:usesCleartextTraffic="true"', 1)
s = s.replace('android:label="smart_plug_app"', 'android:label="Smart Energy"')
open(p, "w", encoding="utf-8").write(s)
print("AndroidManifest patched OK")

# firebase_messaging needs minSdk 23 (applies to every build, because the package is always in the app)
for path in ["android/app/build.gradle.kts", "android/app/build.gradle"]:
    if os.path.exists(path):
        t = open(path, encoding="utf-8").read()
        n = t.replace("minSdk = flutter.minSdkVersion", "minSdk = maxOf(23, flutter.minSdkVersion)")
        n = n.replace("minSdkVersion flutter.minSdkVersion", "minSdkVersion Math.max(23, flutter.minSdkVersion)")
        if n != t:
            open(path, "w", encoding="utf-8").write(n)
            print("minSdk raised to 23 in", path)

# ---------------------------------------------------------------- firebase (optional)
src_json = "android_config/google-services.json"
if not os.path.exists(src_json):
    print("No android_config/google-services.json: building WITHOUT phone notifications")
    sys.exit(0)

shutil.copy(src_json, "android/app/google-services.json")


def add_to_plugins_block(text, line):
    """Insert `line` as the last entry of the top-level `plugins { ... }` block."""
    m = re.search(r"^plugins\s*\{", text, re.M)
    if not m:
        return None
    depth, i = 0, m.end() - 1
    while i < len(text):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[:i] + "    " + line + "\n" + text[i:]
        i += 1
    return None


def patch_file(paths, line, label):
    for path in paths:
        if os.path.exists(path):
            t = open(path, encoding="utf-8").read()
            if "com.google.gms.google-services" in t:
                return True
            n = add_to_plugins_block(t, line)
            if n:
                open(path, "w", encoding="utf-8").write(n)
                print(label, "patched:", path)
                return True
    return False


ok1 = patch_file(["android/settings.gradle.kts"], 'id("com.google.gms.google-services") version "4.4.2" apply false', "settings")
ok2 = patch_file(["android/app/build.gradle.kts"], 'id("com.google.gms.google-services")', "app")
if not (ok1 and ok2):
    print("WARNING: could not add the Google services plugin automatically (unexpected Gradle layout)")

print("Firebase wiring done")
