import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'alerts.dart';
import 'login.dart';
import 'tariff.dart';

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

  Future<void> _editName() async {
    final c = TextEditingController(text: Api.i.user?.name ?? '');
    final v = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('Account Settings', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: Field(label: 'Full Name', hint: 'Your name', controller: c),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(ctx, c.text.trim()), child: const Text('Save')),
        ],
      ),
    );
    if (v == null || v.isEmpty) return;
    try {
      await Api.i.updateName(v);
      if (mounted) setState(() {});
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
  }

  Future<void> _server() async {
    final c = TextEditingController(text: Api.i.baseUrl);
    final v = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('Server Address', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Emulator: http://10.0.2.2:4000\nReal phone: http://<your-PC-IP>:4000', style: TextStyle(fontSize: 12, color: C.text2, height: 1.5)),
          const SizedBox(height: 14),
          Field(label: 'Base URL', hint: 'http://192.168.1.10:4000', controller: c, keyboard: TextInputType.url),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(ctx, c.text.trim()), child: const Text('Save')),
        ],
      ),
    );
    if (v == null || v.isEmpty) return;
    await Api.i.setBaseUrl(v);
    if (mounted) {
      toast(context, 'Server set to ${Api.i.baseUrl}');
      _load();
    }
  }

  void _help() => showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          backgroundColor: C.card,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: const Text('Help & Support', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
          content: const Text(
            'Smart Consuming Energy — graduation project SU26_402.\n\n'
            '• Tap the power button to switch an appliance on/off.\n'
            '• Set a power threshold in Device Detail to get alerts.\n'
            '• If the app cannot connect, check Settings › Server Address.\n\n'
            'Educational prototype — not a replacement for the certified utility meter.',
            style: TextStyle(fontSize: 12.5, color: C.text2, height: 1.5),
          ),
          actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Close'))],
        ),
      );

  Future<void> _logout() async {
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
          IconBtn(Icons.edit_outlined, _editName, size: 16),
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
        _item(Icons.person_outline_rounded, C.blue, 'Account Settings', onTap: _editName),
        _item(Icons.notifications_none_rounded, C.amber, 'Notifications', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const AlertsScreen()))),
        _item(Icons.shield_outlined, C.green, 'Security & Privacy', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ChangePasswordScreen()))),
        _item(Icons.receipt_long_outlined, C.green, 'Electricity Tariff', onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const TariffScreen()))),
        _item(Icons.language_rounded, C.purple, 'Language', value: 'English'),
        _item(Icons.dns_outlined, C.blue, 'Server Address', onTap: _server),
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

class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key});
  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _cur = TextEditingController();
  final _new = TextEditingController();
  final _confirm = TextEditingController();
  bool _loading = false;

  Future<void> _save() async {
    if (_cur.text.isEmpty || _new.text.length < 6) {
      toast(context, 'New password must be at least 6 characters', error: true);
      return;
    }
    if (_new.text != _confirm.text) {
      toast(context, 'Passwords do not match', error: true);
      return;
    }
    setState(() => _loading = true);
    try {
      await Api.i.changePassword(_cur.text, _new.text);
      if (!mounted) return;
      toast(context, 'Password updated');
      Navigator.pop(context);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        body: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(22, 14, 22, 24),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const TopNav('Security & Privacy'),
              Field(label: 'Current Password', hint: '••••••••', controller: _cur, icon: Icons.lock_outline_rounded, obscure: true),
              Field(label: 'New Password', hint: '••••••••', controller: _new, icon: Icons.lock_outline_rounded, obscure: true),
              Field(label: 'Confirm New Password', hint: '••••••••', controller: _confirm, icon: Icons.lock_outline_rounded, obscure: true),
              const SizedBox(height: 8),
              PrimaryButton('Update Password', onTap: _save, loading: _loading),
            ]),
          ),
        ),
      );
}
