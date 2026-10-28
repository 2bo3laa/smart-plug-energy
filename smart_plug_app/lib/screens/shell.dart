import 'package:flutter/material.dart';
import '../core/theme.dart';
import 'devices.dart';
import 'home.dart';
import 'profile.dart';
import 'statistics.dart';

class Shell extends StatefulWidget {
  const Shell({super.key});
  @override
  State<Shell> createState() => _ShellState();
}

class _ShellState extends State<Shell> {
  int _i = 0;

  static const _tabs = [
    (Icons.home_rounded, 'Home'),
    (Icons.bar_chart_rounded, 'History'),
    (Icons.devices_other_rounded, 'Devices'),
    (Icons.settings_rounded, 'Settings'),
  ];

  Widget _page() {
    switch (_i) {
      case 0:
        return HomeScreen(key: const ValueKey('home'), onOpenDevices: () => setState(() => _i = 2));
      case 1:
        return const StatisticsScreen(key: ValueKey('stats'));
      case 2:
        return const DevicesScreen(key: ValueKey('devices'));
      default:
        return const ProfileScreen(key: ValueKey('profile'));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(bottom: false, child: _page()),
      bottomNavigationBar: Container(
        decoration: const BoxDecoration(color: C.bg, border: Border(top: BorderSide(color: C.border))),
        child: SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(6, 9, 6, 8),
            child: Row(mainAxisAlignment: MainAxisAlignment.spaceAround, children: [
              for (var n = 0; n < _tabs.length; n++)
                Expanded(
                  child: GestureDetector(
                    behavior: HitTestBehavior.opaque,
                    onTap: () => setState(() => _i = n),
                    child: Column(mainAxisSize: MainAxisSize.min, children: [
                      Icon(_tabs[n].$1, size: 22, color: n == _i ? C.blue : C.text3),
                      const SizedBox(height: 4),
                      Text(_tabs[n].$2, style: TextStyle(fontSize: 9.5, fontWeight: FontWeight.w600, color: n == _i ? C.blue : C.text3)),
                    ]),
                  ),
                ),
            ]),
          ),
        ),
      ),
    );
  }
}
