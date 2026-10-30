import 'dart:async';
import 'package:flutter/material.dart' hide Threshold;
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/catalog_widgets.dart';
import '../widgets/common.dart';
import 'device_setup.dart';
import 'schedule.dart';

class DeviceDetailScreen extends StatefulWidget {
  final int deviceId;
  const DeviceDetailScreen({super.key, required this.deviceId});
  @override
  State<DeviceDetailScreen> createState() => _DeviceDetailScreenState();
}

class _DeviceDetailScreenState extends State<DeviceDetailScreen> {
  Device? _d;
  Threshold? _th;
  int _activeSchedules = 0;
  String? _error;
  bool _busy = false;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(const Duration(seconds: 5), (_) => _load(silent: true));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    try {
      final d = await Api.i.device(widget.deviceId);
      final th = await Api.i.threshold(widget.deviceId);
      final sc = await Api.i.schedules(widget.deviceId);
      if (!mounted) return;
      setState(() {
        _d = d;
        _th = th;
        _activeSchedules = sc.where((s) => s.isActive).length;
        _error = null;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      if (!silent || _d == null) setState(() => _error = e.message);
    }
  }

  Future<void> _toggle() async {
    if (_busy || _d == null || !_d!.isOnline) return;
    setState(() => _busy = true);
    try {
      final d = await Api.i.toggleDevice(widget.deviceId);
      if (mounted) setState(() => _d = d);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _edit() async {
    final d = _d!;
    final ok = await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => DeviceSetupScreen(editing: d)));
    if (ok == true) _load(silent: true);
  }

  Future<void> _delete() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('Remove device?', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: const Text('Its readings, schedules and threshold will be deleted. Past alerts are kept.', style: TextStyle(color: C.text2, fontSize: 13)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(c, true), child: const Text('Remove', style: TextStyle(color: C.red))),
        ],
      ),
    );
    if (ok != true) return;
    try {
      await Api.i.deleteDevice(widget.deviceId);
      if (mounted) Navigator.pop(context);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
  }

  Future<void> _editThreshold() async {
    final ctrl = TextEditingController(text: _th == null ? '' : _th!.limit.toStringAsFixed(0));
    bool active = _th?.isActive ?? true;
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => StatefulBuilder(
        builder: (c, setS) => AlertDialog(
          backgroundColor: C.card,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: const Text('Power Threshold', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
          content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('You will get an alert when power exceeds this limit.', style: TextStyle(color: C.text2, fontSize: 12.5, height: 1.4)),
            const SizedBox(height: 14),
            Field(label: 'Limit (Watts)', hint: 'e.g. 1500', controller: ctrl, keyboard: TextInputType.number),
            Row(children: [
              const Expanded(child: Text('Alerts enabled', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600))),
              AppToggle(active, onChanged: (v) => setS(() => active = v)),
            ]),
          ]),
          actions: [
            TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancel', style: TextStyle(color: C.text2))),
            TextButton(onPressed: () => Navigator.pop(c, true), child: const Text('Save')),
          ],
        ),
      ),
    );
    if (ok != true) return;
    final v = double.tryParse(ctrl.text.trim());
    if (v == null || v <= 0) {
      if (mounted) toast(context, 'Enter a valid limit in watts', error: true);
      return;
    }
    try {
      await Api.i.setThreshold(widget.deviceId, v, active);
      _load(silent: true);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final d = _d;
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            TopNav(
              d?.name ?? 'Device',
              trailing: d == null
                  ? null
                  : PopupMenuButton<String>(
                      color: C.card,
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                      onSelected: (v) => v == 'edit' ? _edit() : _delete(),
                      itemBuilder: (_) => const [
                        PopupMenuItem(value: 'edit', child: Text('Edit device')),
                        PopupMenuItem(value: 'delete', child: Text('Remove device', style: TextStyle(color: C.red))),
                      ],
                      child: const IconBtn(Icons.more_horiz_rounded, null),
                    ),
            ),
            Expanded(
              child: _error != null && d == null
                  ? ErrorRetry(_error!, () { setState(() => _error = null); _load(); })
                  : d == null
                      ? const Loader()
                      : ListView(padding: const EdgeInsets.only(bottom: 20), children: [
                          Center(
                            child: GestureDetector(
                              onTap: _toggle,
                              child: Container(
                                width: 148, height: 148,
                                margin: const EdgeInsets.only(top: 4, bottom: 18),
                                decoration: BoxDecoration(
                                  shape: BoxShape.circle,
                                  border: Border.all(color: d.isOn ? C.green : C.borderStrong, width: 2),
                                  boxShadow: d.isOn ? [BoxShadow(color: C.green.withOpacity(0.32), blurRadius: 34)] : null,
                                ),
                                alignment: Alignment.center,
                                child: Container(
                                  width: 110, height: 110,
                                  decoration: const BoxDecoration(color: C.card, shape: BoxShape.circle),
                                  child: _busy
                                      ? const Center(child: SizedBox(width: 26, height: 26, child: CircularProgressIndicator(strokeWidth: 2.5, color: C.green)))
                                      : Column(mainAxisAlignment: MainAxisAlignment.center, children: [
                                          Icon(Icons.power_settings_new_rounded, size: 28, color: d.isOn ? C.green : C.text3),
                                          const SizedBox(height: 6),
                                          Text(d.isOn ? 'ON' : 'OFF', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w800, letterSpacing: 1.1, color: d.isOn ? C.green : C.text3)),
                                        ]),
                                ),
                              ),
                            ),
                          ),
                          if (!d.isOnline)
                            Container(
                              margin: const EdgeInsets.only(bottom: 14),
                              padding: const EdgeInsets.all(11),
                              decoration: BoxDecoration(color: C.dim(C.red), borderRadius: BorderRadius.circular(12)),
                              child: const Row(children: [
                                Icon(Icons.wifi_off_rounded, size: 16, color: C.red),
                                SizedBox(width: 8),
                                Expanded(child: Text('Device is not connected. Check its power and Wi-Fi.', style: TextStyle(fontSize: 11.5, color: C.red))),
                              ]),
                            ),
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
                          const SizedBox(height: 8),
                          _applianceRow(d),
                          _row(Icons.speed_rounded, C.purple, 'Rated Power',
                              d.ratedWatts == null ? 'Not set' : '${d.ratedWatts!.toStringAsFixed(0)} W', onTap: _edit),
                          _row(Icons.bolt_rounded, C.amber, 'Energy Usage', '${d.todayKwh.toStringAsFixed(2)} kWh'),
                          _row(Icons.attach_money_rounded, C.green, 'Cost', '${d.todayCost.toStringAsFixed(2)} EGP'),
                          _row(Icons.schedule_rounded, C.blue, 'Schedule', '$_activeSchedules Active', onTap: () async {
                            await Navigator.push(context, MaterialPageRoute(builder: (_) => ScheduleScreen(device: d)));
                            _load(silent: true);
                          }),
                          _row(Icons.notifications_active_outlined, C.red, 'Power Threshold',
                              _th == null ? 'Not set' : '${_th!.limit.toStringAsFixed(0)} W${_th!.isActive ? '' : ' (off)'}',
                              onTap: _editThreshold, last: true),
                        ]),
            ),
          ]),
        ),
      ),
    );
  }

  Widget _applianceRow(Device d) {
    final label = [
      if (d.brand != null) d.brand!,
      if (d.model != null) d.model! else if (d.type != null) d.type!.replaceAll('_', ' '),
    ].join(' · ');
    return InkWell(
      onTap: _edit,
      child: Container(
        padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 2),
        decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: C.border))),
        child: Row(children: [
          TypeBadge(d.type, size: 30),
          const SizedBox(width: 11),
          const Expanded(child: Text('Appliance', style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600))),
          if (d.brand != null) Padding(padding: const EdgeInsets.only(right: 8), child: BrandLogo(d.brand!, size: 26)),
          Flexible(
            child: Text(
              label.isEmpty ? 'Not set' : label,
              textAlign: TextAlign.right, maxLines: 2, overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 12, color: C.text3),
            ),
          ),
          const Padding(padding: EdgeInsets.only(left: 6), child: Icon(Icons.chevron_right_rounded, size: 18, color: C.text3)),
        ]),
      ),
    );
  }

  Widget _row(IconData i, Color c, String l, String v, {VoidCallback? onTap, bool last = false}) => InkWell(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 2),
          decoration: BoxDecoration(border: last ? null : const Border(bottom: BorderSide(color: C.border))),
          child: Row(children: [
            Container(
              width: 30, height: 30,
              decoration: BoxDecoration(color: C.dim(c), borderRadius: BorderRadius.circular(9)),
              child: Icon(i, size: 15, color: c),
            ),
            const SizedBox(width: 11),
            Expanded(child: Text(l, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600))),
            Text(v, style: const TextStyle(fontSize: 12, color: C.text3)),
            if (onTap != null) const Padding(padding: EdgeInsets.only(left: 6), child: Icon(Icons.chevron_right_rounded, size: 18, color: C.text3)),
          ]),
        ),
      );
}
