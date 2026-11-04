import 'package:flutter/material.dart' hide Text;
import '../core/i18n.dart';
import '../core/api.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'account.dart';
import 'alerts.dart';
import 'login.dart';
import 'tariff.dart';
import 'reports.dart';
import 'backups.dart';
import '../core/push.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});
  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  int _devices = 0, _schedules = 0;
  double _monthCost = 0;
  bool _loaded = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final p = await Api.i.profile();
      final s = await Api.i.statistics('month');
      if (mounted) setState(() { _devices = p.devices; _schedules = p.activeSchedules; _monthCost = s.totalCostMonth; _loaded = true; });
    } on ApiException catch (_) {
      if (mounted) setState(() => _loaded = true);
    }
  }

  Future<void> _openAccount() async {
    await Navigator.push(context, MaterialPageRoute(builder: (_) => const AccountScreen()));
    if (mounted) setState(() {});
  }

  Future<void> _pickLanguage() async {
    final pick = await showModalBottomSheet<String>(
      context: context,
      backgroundColor: C.card,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 20, 22, 16),
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text('Language', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
            const SizedBox(height: 12),
            for (final o in const [('ar', 'العربية'), ('en', 'English')])
              InkWell(
                onTap: () => Navigator.pop(ctx, o.$1),
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 14),
                  child: Row(children: [
                    Expanded(child: Text(o.$2, style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600))),
                    if (Lang.i.code == o.$1) const Icon(Icons.check_circle_rounded, color: C.blue, size: 20),
                  ]),
                ),
              ),
          ]),
        ),
      ),
    );
    if (pick != null) await Lang.i.set(pick, fromSettings: true);
  }

  void _help() => showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          backgroundColor: C.card,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: const Text('Help & Support', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
          content: Text(
            [
              tr('Smart Consuming Energy — graduation project SU26_402.'),
              '',
              tr('• Tap the power button to switch an appliance on/off.'),
              tr('• Set a power threshold in Device Detail to get alerts.'),
              '',
              tr('Educational prototype — not a replacement for the certified utility meter.'),
            ].join('\n'),
            style: const TextStyle(fontSize: 12.5, color: C.text2, height: 1.5),
          ),
          actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Close'))],
        ),
      );

  Future<void> _logout() async {
    await Push.unregister();
    await Api.i.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(MaterialPageRoute(builder: (_) => const LoginScreen()), (_) => false);
  }

  @override
  Widget build(BuildContext context) {
    final u = Api.i.user;
    return Padding(
      padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
      child: ListView(padding: const EdgeInsets.only(bottom: 20), children: [
        const TopNav('My Profile', back: false),
        Row(children: [
          Container(
            width: 58, height: 58,
            decoration: BoxDecoration(borderRadius: BorderRadius.circular(18), gradient: const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [Color(0xFF4C8CFF), Color(0xFF2653C7)])),
            alignment: Alignment.center,
            child: Text(u?.initials ?? '?', style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: Colors.white)),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(u?.name ?? '', style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800)),
              const SizedBox(height: 2),
              Text(u?.email ?? '', style: const TextStyle(fontSize: 12, color: C.text2)),
            ]),
          ),
          IconBtn(Icons.edit_outlined, _openAccount, size: 16),
        ]),
        const SizedBox(height: 18),
        Row(children: [
          Expanded(child: _stat(_loaded ? '$_devices' : '–', 'Devices')),
          const SizedBox(width: 10),
          Expanded(child: _stat(_loaded ? '$_schedules' : '–', 'Schedules')),
          const SizedBox(width: 10),
          Expanded(child: _stat(_loaded ? _monthCost.toStringAsFixed(1) : '–', 'EGP / month')),
        ]),
        const SizedBox(height: 18),
        _item(Icons.person_outline_rounded, C.blue, 'Account Settings', onTap: _openAccount),
        _item(Icons.notifications_none_rounded, C.amber, 'Notifications', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const AlertsScreen()))),
        _item(Icons.mail_outline_rounded, C.blue, 'Email Reports', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ReportsScreen()))),
        _item(Icons.backup_outlined, C.purple, 'Backups', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const BackupsScreen()))),
        _item(Icons.receipt_long_outlined, C.green, 'Electricity Tariff', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const TariffScreen()))),
        _item(Icons.language_rounded, C.purple, 'Language', value: Lang.i.isAr ? 'العربية' : 'English', onTap: _pickLanguage),
        _item(Icons.help_outline_rounded, C.text2, 'Help & Support', onTap: _help, last: true),
        const SizedBox(height: 14),
        GestureDetector(
          onTap: _logout,
          child: Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(color: C.dim(C.red), borderRadius: BorderRadius.circular(14)),
            child: const Row(mainAxisAlignment: MainAxisAlignment.center, children: [
              Icon(Icons.logout_rounded, size: 17, color: C.red),
              SizedBox(width: 8),
              Text('Log Out', style: TextStyle(color: C.red, fontWeight: FontWeight.w700, fontSize: 13.5)),
            ]),
          ),
        ),
      ]),
    );
  }

  Widget _stat(String n, String l) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(color: C.card, borderRadius: BorderRadius.circular(14), border: Border.all(color: C.border)),
        child: Column(children: [
          Text(n, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
          const SizedBox(height: 2),
          Text(l, style: const TextStyle(fontSize: 10.5, color: C.text2)),
        ]),
      );

  Widget _item(IconData i, Color c, String label, {String? value, VoidCallback? onTap, bool last = false}) => InkWell(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 4),
          decoration: BoxDecoration(border: last ? null : const Border(bottom: BorderSide(color: C.border))),
          child: Row(children: [
            Container(
              width: 34, height: 34,
              decoration: BoxDecoration(color: C.dim(c), borderRadius: BorderRadius.circular(10)),
              child: Icon(i, size: 17, color: c),
            ),
            const SizedBox(width: 12),
            Expanded(child: Text(label, style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w600))),
            if (value != null) Text(value, style: const TextStyle(fontSize: 12, color: C.text3)),
            if (onTap != null) const Icon(Icons.chevron_right_rounded, size: 18, color: C.text3),
          ]),
        ),
      );
}
