import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../screens/alerts.dart';
import 'api.dart';
import 'i18n.dart';

/// Phone notifications (Firebase Cloud Messaging). The server pushes every new alert to the registered phone,
/// so it rings even when the app is closed. If this build has no Firebase file, everything here quietly does nothing.
class Push {
  static final GlobalKey<NavigatorState> navKey = GlobalKey<NavigatorState>();
  static bool _ready = false;

  static Future<void> init() async {
    if (kIsWeb) return;
    try {
      await Firebase.initializeApp();
      _ready = true;
      FirebaseMessaging.onTokenRefresh.listen(_register);
      // Tapping a notification while the app is in the background.
      FirebaseMessaging.onMessageOpenedApp.listen((_) => _openAlerts());
      // Tapping a notification while the app was closed.
      final initial = await FirebaseMessaging.instance.getInitialMessage();
      if (initial != null) {
        Future.delayed(const Duration(seconds: 3), _openAlerts);
      }
    } catch (_) {
      _ready = false; // no google-services.json in this build: the app works without push
    }
  }

  static bool get ready => _ready;

  static void _openAlerts() {
    if (!Api.i.loggedIn) return;
    navKey.currentState?.push(MaterialPageRoute(builder: (_) => const AlertsScreen()));
  }

  /// Asks for notification permission, then tells the server which phone to notify. Call after every login.
  static Future<void> registerForUser() async {
    if (!_ready || !Api.i.loggedIn) return;
    try {
      await FirebaseMessaging.instance.requestPermission();
      final t = await FirebaseMessaging.instance.getToken();
      if (t != null) await _register(t);
    } catch (_) {}
  }

  static Future<void> _register(String token) async {
    if (!Api.i.loggedIn) return;
    try {
      await Api.i.registerPush(token, Lang.i.isAr ? 'ar' : 'en');
    } catch (_) {}
  }

  /// Call BEFORE logging out so this phone stops receiving the account's notifications.
  static Future<void> unregister() async {
    if (!Api.i.loggedIn) return;
    try {
      await Api.i.unregisterPush();
    } catch (_) {}
  }
}
