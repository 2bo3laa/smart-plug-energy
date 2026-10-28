import 'dart:async';
import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/catalog_widgets.dart';
import '../widgets/common.dart';
import 'add_device.dart';
import 'alerts.dart';
import 'device_detail.dart';

class HomeScreen extends StatefulWidget {
  final VoidCallback onOpenDevices;
  const HomeScreen({super.key, required this.onOpenDevices});
  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  DeviceList? _list;
  String? _error;
  int _sel = 0;
  int _unread = 0;
  bool _busy = false;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _load();
    // Near real-time live readings (FR7): poll every 5 s, matching the ESP32 push interval.
    _timer = Timer.periodic(const Duration(seconds: 5), (_) => _load(silent: true));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    try {
      final l = await Api.i.devices();
      final a = await Api.i.alerts();
      if (!mounted) return;
      setState(() {
        _list = l;
        _unread = a.unreadCount;
        _error = null;
        if (_sel >= l.devices.length) _sel = 0;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      if (e.status == 401) {
        Navigator.of(context).popUntil((r) => r.isFirst);
        return;
      }
      if (!silent || _list == null) setState(() => _error = e.message);
    }
  }

  Future<void> _toggle(Device d) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await Api.i.toggleDevice(d.id);
      await _load(silent: true);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _addDevice() async {
    final ok = await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => const AddDeviceScreen()));
    if (ok == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    final name = (Api.i.user?.name ?? '').split(' ').first;
    return Padding(
      padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          IconBtn(Icons.add_rounded, _addDevice, size: 20),
          const Text('Smart Plug', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
          IconBtn(Icons.notifications_none_rounded, () async {
            await Navigator.push(context, MaterialPageRoute(builder: (_) => const AlertsScreen()));
            _load(silent: true);
          }, size: 19, badge: _unread),
        ]),
        Padding(
          padding: const EdgeInsets.only(top: 6, bottom: 14),
          child: Text('Welcome back${name.isEmpty ? '' : ', $name'} 👋', style: const TextStyle(fontSize: 12.5, color: C.text2)),
        ),
        Expanded(child: _body()),
      ]),
    );
  }

  Widget _body() {
    if (_error != null && _list == null) return ErrorRetry(_error!, () { setState(() => _error = null); _load(); });
    if (_list == null) return const Loader();
    final devices = _list!.devices;
    if (devices.isEmpty) {
      return Center(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          Container(
            width: 84, height: 84,
            decoration: BoxDecoration(color: C.card, borderRadius: BorderRadius.circular(24), border: Border.all(color: C.borderStrong)),
            child: const Icon(Icons.power_outlined, size: 38, color: C.blue),
          ),
          const SizedBox(height: 18),
          const Text('No devices yet', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
          const SizedBox(height: 6),
          const Text('Add your first smart plug to start monitoring.', style: TextStyle(fontSize: 13, color: C.text2)),
          const SizedBox(height: 20),
          SizedBox(width: 180, child: PrimaryButton('Add Device', onTap: _addDevice, icon: Icons.add_rounded)),
        ]),
      );
    }
    final d = devices[_sel.clamp(0, devices.length - 1)];
    final dead = !d.isOnline;
    return RefreshIndicator(
      color: C.blue,
      backgroundColor: C.card,
      onRefresh: _load,
      child: ListView(padding: const EdgeInsets.only(bottom: 16), children: [
        if (devices.length > 1)
          Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(children: [
                for (var i = 0; i < devices.length; i++)
                  Padding(padding: const EdgeInsets.only(right: 8), child: AppChip(devices[i].name, active: i == _sel, onTap: () => setState(() => _sel = i))),
              ]),
            ),
          ),
        AppCard(
          margin: const EdgeInsets.only(bottom: 16),
          padding: const EdgeInsets.all(16),
          onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => DeviceDetailScreen(deviceId: d.id))).then((_) => _load(silent: true)),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(d.name, style: const TextStyle(fontSize: 14.5, fontWeight: FontWeight.w700)),
            const SizedBox(height: 3),
            Row(children: [
              Container(width: 6, height: 6, decoration: BoxDecoration(shape: BoxShape.circle, color: dead ? C.red : C.green)),
              const SizedBox(width: 5),
              Text(dead ? 'Offline' : 'Online', style: TextStyle(fontSize: 11.5, color: dead ? C.red : C.green)),
              const SizedBox(width: 8),
              Text(d.room, style: const TextStyle(fontSize: 11.5, color: C.text3)),
            ]),
            const SizedBox(height: 14),
            Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
              Container(
                width: 120, height: 96,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [Color(0xFF1B2434), Color(0xFF0E141F)]),
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(color: C.borderStrong),
                  boxShadow: [BoxShadow(color: C.blue.withOpacity(0.06), spreadRadius: 10)],
                ),
                child: Icon(typeIcon(d.type), size: 36, color: d.isOn ? C.blue : C.text3),
              ),
              Column(children: [
                GestureDetector(
                  onTap: dead ? null : () => _toggle(d),
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 200),
                    width: 48, height: 48,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: d.isOn ? C.blue : C.cardAlt,
                      border: d.isOn ? null : Border.all(color: C.borderStrong),
                      boxShadow: d.isOn ? [BoxShadow(color: C.blue.withOpacity(0.55), blurRadius: 22)] : null,
                    ),
                    child: _busy
                        ? const Padding(padding: EdgeInsets.all(14), child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                        : Icon(Icons.power_settings_new_rounded, size: 22, color: d.isOn ? Colors.white : C.text2),
                  ),
                ),
                const SizedBox(height: 8),
                Text(d.isOn ? 'ON' : 'OFF', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, letterSpacing: 0.6, color: d.isOn ? C.blue : C.text3)),
              ]),
            ]),
          ]),
        ),
        const SectionLabel('Live Measurements'),
        GridView.count(
          crossAxisCount: 2, shrinkWrap: true, physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 10, crossAxisSpacing: 10, childAspectRatio: 1.9,
          children: [
            MeasCard(Icons.bolt_rounded, C.blue, '${d.voltage.toStringAsFixed(1)} V', 'Voltage'),
            MeasCard(Icons.show_chart_rounded, C.green, '${d.current.toStringAsFixed(2)} A', 'Current'),
            MeasCard(Icons.flash_on_rounded, C.purple, '${d.power.toStringAsFixed(1)} W', 'Power'),
            MeasCard(Icons.speed_rounded, C.amber, d.powerFactor.toStringAsFixed(2), 'Power Factor'),
          ],
        ),
        const SizedBox(height: 16),
        const SectionLabel('Today Energy'),
        AppCard(
          child: Row(children: [
            SizedBox(
              width: 76, height: 76,
              child: Stack(alignment: Alignment.center, children: [
                CustomPaint(size: const Size(76, 76), painter: RingPainter(d.todayKwh / 5, C.green)),
                Column(mainAxisSize: MainAxisSize.min, children: [
                  Text(d.todayKwh.toStringAsFixed(2), style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w800)),
                  const Text('kWh', style: TextStyle(fontSize: 8.5, color: C.text2)),
                ]),
              ]),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const Text('Cost', style: TextStyle(fontSize: 11, color: C.text2)),
                Padding(
                  padding: const EdgeInsets.only(top: 2, bottom: 6),
                  child: Text('${d.todayCost.toStringAsFixed(2)} EGP', style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800, color: C.green)),
                ),
                GestureDetector(
                  onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => DeviceDetailScreen(deviceId: d.id))).then((_) => _load(silent: true)),
                  child: const Text('View Details →', style: TextStyle(fontSize: 11.5, color: C.blue, fontWeight: FontWeight.w600)),
                ),
              ]),
            ),
          ]),
        ),
      ]),
    );
  }
}
