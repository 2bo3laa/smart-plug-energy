import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'device_setup.dart';

class AddDeviceScreen extends StatefulWidget {
  const AddDeviceScreen({super.key});
  @override
  State<AddDeviceScreen> createState() => _AddDeviceScreenState();
}

class _AddDeviceScreenState extends State<AddDeviceScreen> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 2600))..repeat();
  List<ScanResult>? _found;
  String? _error;

  @override
  void initState() {
    super.initState();
    _scan();
  }

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  Future<void> _scan() async {
    setState(() { _found = null; _error = null; });
    await Future.delayed(const Duration(milliseconds: 900)); // let the radar animation breathe
    try {
      final r = await Api.i.scan();
      if (mounted) setState(() => _found = r);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _setup(String? suggested) async {
    final ok = await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => DeviceSetupScreen(suggestedName: suggested)));
    if (ok == true && mounted) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 20),
          child: Column(children: [
            const Align(alignment: Alignment.centerLeft, child: TopNav('Add New Device')),
            SizedBox(
              width: 150, height: 150,
              child: AnimatedBuilder(
                animation: _c,
                builder: (_, __) => Stack(alignment: Alignment.center, children: [
                  for (var i = 0; i < 3; i++) _ring((_c.value + i / 3) % 1),
                  Container(
                    width: 60, height: 60,
                    decoration: BoxDecoration(color: C.card, borderRadius: BorderRadius.circular(18), border: Border.all(color: C.borderStrong)),
                    child: const Icon(Icons.wifi_tethering_rounded, color: C.blue, size: 26),
                  ),
                ]),
              ),
            ),
            const SizedBox(height: 22),
            Text(_found == null && _error == null ? 'Searching for nearby devices…' : (_error != null ? 'Scan failed' : '${_found!.length} device(s) found'),
                style: const TextStyle(fontSize: 13, color: C.text2)),
            const SizedBox(height: 18),
            Expanded(
              child: _error != null
                  ? ErrorRetry(_error!, _scan)
                  : ListView(children: [
                      if (_found != null)
                        for (final f in _found!)
                          AppCard(
                            margin: const EdgeInsets.only(bottom: 10),
                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                            radius: 14,
                            onTap: () => _setup(f.name),
                            child: Row(children: [
                              Container(
                                width: 38, height: 38,
                                decoration: BoxDecoration(color: C.dim(C.blue), borderRadius: BorderRadius.circular(11)),
                                child: const Icon(Icons.power_outlined, color: C.blue, size: 18),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                                  Text(f.name, style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
                                  const SizedBox(height: 2),
                                  Text('${f.signal} · Tap to connect', style: const TextStyle(fontSize: 11.5, color: C.text2)),
                                ]),
                              ),
                              const Icon(Icons.chevron_right_rounded, size: 18, color: C.text3),
                            ]),
                          ),
                    ]),
            ),
            GestureDetector(
              onTap: () => _setup(null),
              child: const Text.rich(TextSpan(style: TextStyle(fontSize: 13, color: C.text2), children: [
                TextSpan(text: "Can't find your device? "),
                TextSpan(text: 'Add manually', style: TextStyle(color: C.blue, fontWeight: FontWeight.w700)),
              ])),
            ),
          ]),
        ),
      ),
    );
  }

  Widget _ring(double t) => Transform.scale(
        scale: 0.34 + 0.66 * t,
        child: Opacity(
          opacity: (0.7 * (1 - t)).clamp(0.0, 1.0),
          child: Container(width: 150, height: 150, decoration: BoxDecoration(shape: BoxShape.circle, border: Border.all(color: C.blue))),
        ),
      );
}
