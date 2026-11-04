import 'package:flutter/material.dart' hide Text;
import '../core/i18n.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'device_detail.dart';
import 'tariff.dart';
import '../core/push.dart';

class AlertsScreen extends StatefulWidget {
  const AlertsScreen({super.key});
  @override
  State<AlertsScreen> createState() => _AlertsScreenState();
}

class _AlertsScreenState extends State<AlertsScreen> {
  static const _tabs = ['All', 'Warnings', 'Errors', 'Info'];
  static const _sev = [null, 'warning', 'error', 'info'];
  int _t = 0;
  List<AlertItem>? _items;
  int _unread = 0;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final r = await Api.i.alerts(severity: _sev[_t]);
      if (mounted) setState(() { _items = r.items; _unread = r.unreadCount; _error = null; });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _readAll() async {
    try {
      await Api.i.markAllRead();
      _load();
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
  }

  /// Tap on a notification: mark it read, then go to where it came from and light that spot up.
  Future<void> _open(AlertItem a) async {
    if (!a.isRead) {
      try {
        await Api.i.markAlertRead(a.id);
      } on ApiException catch (_) {}
    }
    if (!mounted) return;
    switch (a.type) {
      case 'card':
        AppNav.highlight.value = 'card';
        AppNav.tab.value = 0;
        Navigator.of(context).popUntil((r) => r.isFirst);
        return;
      case 'tier':
        double? kwh;
        try {
          kwh = (await Api.i.statistics('month')).totalEnergyMonth;
        } on ApiException catch (_) {}
        if (!mounted) return;
        await Navigator.push(context, MaterialPageRoute(builder: (_) => TariffScreen(currentKwh: kwh, highlightCurrent: true, note: a)));
        break;
      default:
        const spots = {
          'power': 'power',
          'overload': 'overload',
          'voltage': 'voltage',
          'device_on': 'toggle',
          'device_off': 'toggle',
          'energy': 'energy',
        };
        if (a.deviceId == null) {
          toast(context, 'This device no longer exists', error: true);
        } else {
          await Navigator.push(context, MaterialPageRoute(builder: (_) => DeviceDetailScreen(deviceId: a.deviceId!, highlight: spots[a.type], note: a)));
        }
    }
    if (mounted) _load();
  }

  Future<void> _delete(AlertItem a) async {
    try {
      await Api.i.deleteAlert(a.id);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
    _load();
  }

  String _ago(DateTime t) {
    final d = DateTime.now().difference(t);
    if (d.inMinutes < 1) return tr('Just now');
    if (d.inMinutes < 60) return tf('{n} min ago', {'n': d.inMinutes});
    if (d.inHours < 24) return tf('{n} h ago', {'n': d.inHours});
    if (d.inDays == 1) return tr('Yesterday');
    return tf('{n} days ago', {'n': d.inDays});
  }

  /// Sends a test notification to this phone (to check that phone notifications work).
  Future<void> _testPush() async {
    if (!Push.ready) {
      toast(context, 'Phone notifications are not enabled in this build', error: true);
      return;
    }
    try {
      await Push.registerForUser();
      await Api.i.testPush();
      if (mounted) toast(context, 'Test sent. Check your notification bar.');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final items = _items;
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            TopNav('Alerts', trailing: Row(mainAxisSize: MainAxisSize.min, children: [
              IconBtn(Icons.notifications_active_outlined, _testPush, size: 18),
              const SizedBox(width: 8),
              IconBtn(Icons.done_all_rounded, _unread > 0 ? _readAll : null, size: 18),
            ])),
            FilterTabs(_tabs, _t, (i) { setState(() { _t = i; _items = null; }); _load(); }),
            Expanded(
              child: _error != null && items == null
                  ? ErrorRetry(_error!, () { setState(() => _error = null); _load(); })
                  : items == null
                      ? const Loader()
                      : items.isEmpty
                          ? const Center(child: Text('No alerts', style: TextStyle(color: C.text3, fontSize: 13)))
                          : RefreshIndicator(
                              color: C.blue, backgroundColor: C.card, onRefresh: _load,
                              child: ListView.builder(
                                itemCount: items.length,
                                itemBuilder: (_, i) => _row(items[i], last: i == items.length - 1),
                              ),
                            ),
            ),
          ]),
        ),
      ),
    );
  }

  Widget _row(AlertItem a, {bool last = false}) {
    final c = severityColor(a.severity);
    return Dismissible(
      key: ValueKey('al${a.id}'),
      direction: DismissDirection.endToStart,
      background: Container(
        padding: const EdgeInsets.only(right: 20),
        alignment: Alignment.centerRight,
        color: C.dim(C.red),
        child: const Icon(Icons.delete_outline_rounded, color: C.red),
      ),
      onDismissed: (_) => _delete(a),
      child: InkWell(
        onTap: () => _open(a),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 11, horizontal: 2),
          decoration: BoxDecoration(border: last ? null : const Border(bottom: BorderSide(color: C.border))),
          child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Container(
              width: 34, height: 34,
              decoration: BoxDecoration(color: C.dim(c), borderRadius: BorderRadius.circular(10)),
              child: Icon(severityIcon(a.type, a.severity), size: 16, color: c),
            ),
            const SizedBox(width: 11),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(a.title, style: TextStyle(fontSize: 12.5, fontWeight: a.isRead ? FontWeight.w600 : FontWeight.w800)),
                const SizedBox(height: 2),
                Text(a.message, style: const TextStyle(fontSize: 11, color: C.text2)),
                const SizedBox(height: 3),
                Text(_ago(a.createdAt), style: const TextStyle(fontSize: 10, color: C.text3)),
              ]),
            ),
            if (!a.isRead) Container(width: 7, height: 7, margin: const EdgeInsets.only(top: 4), decoration: BoxDecoration(shape: BoxShape.circle, color: c)),
          ]),
        ),
      ),
    );
  }
}
