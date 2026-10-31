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

## New in this version

- **Logo** – new launcher icon + splash logo (`assets/icon/*`, `assets/logo.png`). Run `dart run flutter_launcher_icons` after `flutter pub get`.
- **Electricity card** – Home shows the amount loaded on the prepaid card, the remaining balance (it goes down as the plugs
  consume, priced with the tiered tariff) and roughly how much each device used. API: `GET /api/card`, `POST /api/card/recharge`, `PUT /api/card`.
- **Arabic / English** – Settings › Language (also on the login screen). Arabic switches the whole UI to RTL.
  Translations live in `lib/core/ar.dart`; new UI strings must be added there.
- **Notifications** – tapping one opens the screen it came from and makes the relevant item glow
  (device power/voltage/toggle/energy, the tariff tier, or the card on Home).
- **Account settings** – change name, change password, link / unlink a Google account, and "Continue with Google" on login.
- **Tier alert** – the API raises an alert when your month's consumption moves you into a higher tariff tier (and when the card gets low/empty).

### Google sign-in setup
1. Google Cloud Console › APIs & Services › Credentials: create a **Web** OAuth client id and an **Android** client id
   (package name + your signing SHA-1).
2. API server: set `Google__ClientIds=<web client id>` (env var / `appsettings.json` › `Google:ClientIds`).
3. App build: `flutter build apk --dart-define=GOOGLE_CLIENT_ID=<web client id>` (GitHub secret `GOOGLE_CLIENT_ID` for the workflow).
Without these, the Google buttons show a "not set up" message and everything else still works.
