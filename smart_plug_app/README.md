# Smart Plug — Flutter app (Smart Consuming Energy)

Mobile client for the ASP.NET Core API (`SmartEnergyApi-net10.zip`). 12 screens from the UI/UX board:
Splash, Log In (lamp), Sign Up, Home, Statistics, My Devices, Device Detail, Schedule, Alerts,
Add Device, Device Setup, Profile — plus Forgot Password and Change Password.

## Run

1. Start the backend:
   ```
   cd SmartEnergyApi && dotnet run        # listens on http://0.0.0.0:4000
   ```
2. In this folder generate the native platform folders once (they are not included):
   ```
   flutter create --project-name smart_plug_app --platforms android,ios .
   flutter pub get
   ```
3. Allow plain-HTTP to the dev server — in `android/app/src/main/AndroidManifest.xml`, add to the `<application` tag:
   ```
   android:usesCleartextTraffic="true"
   ```
   and make sure `<uses-permission android:name="android.permission.INTERNET"/>` is present above it
   (for iOS add an `NSAppTransportSecurity` › `NSAllowsArbitraryLoads` entry in `ios/Runner/Info.plist`).
4. `flutter run`

- Android emulator reaches your PC at `http://10.0.2.2:4000` (default).
- Real phone: same Wi-Fi as the PC, then Settings › Server Address › `http://<PC-IP>:4000`.
