import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/catalog_widgets.dart';
import '../widgets/common.dart';
import 'add_device.dart';
import 'alerts.dart';
import 'device_detail.dart';

class DevicesScreen extends StatefulWidget {
  const DevicesScreen({super.key});
  @override
  State<DevicesScreen> createState() => _DevicesScreenState();
}

class _DevicesScreenState extends State<DevicesScreen> {
  DeviceList? _list;
  String? _error;
  int _unread = 0;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final l = await Api.i.devices();
      final a = await Api.i.alerts();
      if (mounted) setState(() { _list = l; _unread = a.unreadCount; _error = null; });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _toggle(Device d) async {
    try {
      await Api.i.toggleDevice(d.id);
      await _load();
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
  }

  Future<void> _add() async {
    final ok = await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => const AddDeviceScreen()));
    if (ok == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    final l = _list;
    return Padding(
      padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Padding(
          padding: const EdgeInsets.only(bottom: 14),
          child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            IconBtn(Icons.add_rounded, _add, size: 20),
            const Text('My Devices', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
            IconBtn(Icons.notifications_none_rounded, () async {
              await Navigator.push(context, MaterialPageRoute(builder: (_) => const AlertsScreen()));
              _load();
            }, size: 19, badge: _unread),
          ]),
        ),
        Expanded(
          child: _error != null && l == null
              ? ErrorRetry(_error!, () { setState(() => _error = null); _load(); })
              : l == null
                  ? const Loader()
                  : RefreshIndicator(
                      color: C.blue, backgroundColor: C.card, onRefresh: _load,
                      child: ListView(padding: const EdgeInsets.only(bottom: 16), children: [
                        AppCard(
                          margin: const EdgeInsets.only(top: 2, bottom: 16),
                          padding: const EdgeInsets.all(15),
                          child: Row(children: [
                            Container(
                              width: 44, height: 44,
                              decoration: BoxDecoration(color: C.dim(C.purple), borderRadius: BorderRadius.circular(13)),
                              child: const Icon(Icons.devices_other_rounded, color: C.purple, size: 22),
                            ),
                            const SizedBox(width: 14),
                            Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                              Text('${l.total}', style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800)),
                              const SizedBox(height: 2),
                              Text('Total Devices · ${l.online} Online, ${l.offline} Offline', style: const TextStyle(fontSize: 11.5, color: C.text2)),
                            ]),
                          ]),
                        ),
                        if (l.devices.isEmpty)
                          const Padding(padding: EdgeInsets.only(top: 30), child: Center(child: Text('No devices yet. Tap + to add one.', style: TextStyle(color: C.text3, fontSize: 13)))),
                        for (final d in l.devices) _row(d),
                      ]),
                    ),
        ),
      ]),
    );
  }

  Widget _row(Device d) => AppCard(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        radius: 14,
        onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => DeviceDetailScreen(deviceId: d.id))).then((_) => _load()),
        child: Row(children: [
          TypeBadge(d.type),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(d.name, style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
              const SizedBox(height: 1),
              Text(d.brand == null ? d.room : '${d.room} · ${d.brand}', style: const TextStyle(fontSize: 11, color: C.text3)),
              const SizedBox(height: 2),
              Text(d.isOnline ? 'Online' : 'Offline', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: d.isOnline ? C.green : C.red)),
            ]),
          ),
          if (d.brand != null) Padding(padding: const EdgeInsets.only(right: 10), child: BrandLogo(d.brand!, size: 30)),
          AppToggle(d.isOn, onChanged: d.isOnline ? (_) => _toggle(d) : null),
        ]),
      );
}
