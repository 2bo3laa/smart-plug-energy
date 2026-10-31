import 'package:flutter/material.dart' hide Text;
import '../core/i18n.dart';
import '../core/api.dart';
import '../core/theme.dart';
import 'login.dart';
import 'shell.dart';

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});
  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 1200))..repeat();

  @override
  void initState() {
    super.initState();
    // After a language switch the UI restarts: skip the wait and land back on Settings.
    final back = Lang.i.openSettingsAfterRestart && Api.i.loggedIn;
    Lang.i.openSettingsAfterRestart = false;
    Future.delayed(Duration(milliseconds: back ? 50 : 1800), () {
      if (!mounted) return;
      Navigator.of(context).pushReplacement(MaterialPageRoute(
        builder: (_) => Api.i.loggedIn ? Shell(initialTab: back ? 3 : 0) : const LoginScreen(),
      ));
    });
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Stack(children: [
        Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Container(
              width: 104, height: 104,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(30),
                boxShadow: [
                  BoxShadow(color: C.blue.withOpacity(0.06), spreadRadius: 14),
                  BoxShadow(color: const Color(0xFF22C55E).withOpacity(0.22), blurRadius: 44),
                ],
              ),
              child: ClipRRect(borderRadius: BorderRadius.circular(30), child: Image.asset('assets/logo.png', fit: BoxFit.cover)),
            ),
            const SizedBox(height: 26),
            const Text('Smart Plug', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800, letterSpacing: -0.2)),
            const SizedBox(height: 6),
            const Text('Control your home, anywhere', style: TextStyle(fontSize: 13, color: C.text2)),
            const SizedBox(height: 34),
            AnimatedBuilder(
              animation: _c,
              builder: (_, __) {
                final active = (_c.value * 3).floor();
                return Row(mainAxisSize: MainAxisSize.min, children: [
                  for (var i = 0; i < 3; i++)
                    Container(
                      margin: const EdgeInsets.symmetric(horizontal: 3),
                      width: 6, height: 6,
                      decoration: BoxDecoration(shape: BoxShape.circle, color: i == active ? C.blue : C.borderStrong),
                    ),
                ]);
              },
            ),
          ]),
        ),
        const Positioned(
          bottom: 26, left: 0, right: 0,
          child: Text('v1.0.0', textAlign: TextAlign.center, style: TextStyle(fontSize: 11, color: C.text3)),
        ),
      ]),
    );
  }
}
