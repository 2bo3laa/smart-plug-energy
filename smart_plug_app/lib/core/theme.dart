import 'package:flutter/material.dart';

class C {
  static const bg = Color(0xFF0B0F1A);
  static const card = Color(0xFF141926);
  static const cardAlt = Color(0xFF10141F);
  static const input = Color(0xFF10151F);
  static const border = Color(0x12FFFFFF);
  static const borderStrong = Color(0x24FFFFFF);
  static const text1 = Color(0xFFF3F5F9);
  static const text2 = Color(0xFF8A93A6);
  static const text3 = Color(0xFF575F70);
  static const blue = Color(0xFF3B82F6);
  static const green = Color(0xFF22C55E);
  static const purple = Color(0xFF8B5CF6);
  static const amber = Color(0xFFF5A623);
  static const red = Color(0xFFEF4444);
  static Color dim(Color c) => c.withOpacity(0.15);
}

ThemeData buildTheme() => ThemeData(
      brightness: Brightness.dark,
      scaffoldBackgroundColor: C.bg,
      colorScheme: const ColorScheme.dark(primary: C.blue, surface: C.card),
      useMaterial3: true,
      dialogBackgroundColor: C.card,
      bottomSheetTheme: const BottomSheetThemeData(backgroundColor: C.card),
    );

Color severityColor(String s) {
  switch (s) {
    case 'error':
      return C.red;
    case 'warning':
      return C.amber;
    case 'success':
      return C.green;
    default:
      return C.blue;
  }
}

IconData severityIcon(String type, String severity) {
  switch (type) {
    case 'power':
      return Icons.bolt_rounded;
    case 'device_on':
      return Icons.power_settings_new_rounded;
    case 'device_off':
      return Icons.power_off_rounded;
    case 'tier':
      return Icons.trending_up_rounded;
    case 'overload':
      return Icons.speed_rounded;
    case 'voltage':
      return Icons.waves_rounded;
    default:
      return severity == 'error' || severity == 'warning'
          ? Icons.warning_amber_rounded
          : Icons.info_outline_rounded;
  }
}
