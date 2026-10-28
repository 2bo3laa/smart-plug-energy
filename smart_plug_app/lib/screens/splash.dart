import 'package:flutter/material.dart';
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
    Future.delayed(const Duration(milliseconds: 1800), () {
      if (!mounted) return;
      Navigator.of(context).pushReplacement(MaterialPageRoute(
        builder: (_) => Api.i.loggedIn ? const Shell() : const LoginScreen(),
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
              width: 88, height: 88,
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(26),
                gradient: const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [Color(0xFF1A2333), Color(0xFF0E141F)]),
                border: Border.all(color: C.borderStrong),
                boxShadow: [
                  BoxShadow(color: C.blue.withOpacity(0.06), spreadRadius: 14),
                  BoxShadow(color: C.blue.withOpacity(0.25), blurRadius: 40),
                ],
              ),
              child: const Icon(Icons.bolt_rounded, size: 42, color: C.blue),
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
