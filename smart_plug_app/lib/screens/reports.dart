import 'package:flutter/material.dart' hide Text;
import '../core/api.dart';
import '../core/i18n.dart';
import '../core/theme.dart';
import '../widgets/common.dart';

/// Settings > Email Reports: choose weekly / monthly consumption reports sent to the account e-mail,
/// or send one right now.
class ReportsScreen extends StatefulWidget {
  const ReportsScreen({super.key});
  @override
  State<ReportsScreen> createState() => _ReportsScreenState();
}

class _ReportsScreenState extends State<ReportsScreen> {
  bool _weekly = false, _monthly = false, _mailConfigured = true;
  String _email = '';
  bool _loaded = false;
  String? _error;
  String _busy = ''; // '', 'save', 'weekly', 'monthly'

  String get _lang => Lang.i.isAr ? 'ar' : 'en';

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final d = await Api.i.reportSettings();
      if (!mounted) return;
      setState(() {
        _weekly = d['weekly'] == true;
        _monthly = d['monthly'] == true;
        _email = (d['email'] ?? '').toString();
        _mailConfigured = d['mailConfigured'] != false;
        _loaded = true;
        _error = null;
      });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _save({bool? weekly, bool? monthly}) async {
    final w = weekly ?? _weekly, m = monthly ?? _monthly;
    final oldW = _weekly, oldM = _monthly;
    setState(() {
      _weekly = w;
      _monthly = m;
      _busy = 'save';
    });
    try {
      await Api.i.saveReportSettings(w, m, _lang);
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _weekly = oldW;
          _monthly = oldM;
        });
        toast(context, e.message, error: true);
      }
    } finally {
      if (mounted) setState(() => _busy = '');
    }
  }

  Future<void> _sendNow(String period) async {
    if (_busy.isNotEmpty) return;
    setState(() => _busy = period);
    try {
      await Api.i.sendReportNow(period, _lang);
      if (mounted) toast(context, 'Report sent. Check your inbox.');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _busy = '');
    }
  }

  Widget _switchRow(IconData icon, Color color, String title, String subtitle, bool value, ValueChanged<bool> onChanged) {
    return AppCard(
      margin: const EdgeInsets.only(bottom: 10),
      child: Row(children: [
        Container(
          width: 40, height: 40,
          decoration: BoxDecoration(color: C.dim(color), borderRadius: BorderRadius.circular(12)),
          child: Icon(icon, size: 20, color: color),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(title, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
            const SizedBox(height: 2),
            Text(subtitle, style: const TextStyle(fontSize: 11.5, color: C.text2)),
          ]),
        ),
        Switch(value: value, activeColor: C.blue, onChanged: _busy.isEmpty ? onChanged : null),
      ]),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const TopNav('Email Reports'),
            Expanded(
              child: _error != null && !_loaded
                  ? ErrorRetry(_error!, () { setState(() => _error = null); _load(); })
                  : !_loaded
                      ? const Loader()
                      : ListView(padding: const EdgeInsets.only(bottom: 24), children: [
                          AppCard(
                            margin: const EdgeInsets.only(bottom: 14),
                            child: Row(children: [
                              const Icon(Icons.mail_outline_rounded, color: C.blue),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                                  const Text('Reports are sent to', style: TextStyle(fontSize: 11.5, color: C.text2)),
                                  const SizedBox(height: 2),
                                  Text(_email, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
                                ]),
                              ),
                            ]),
                          ),
                          if (!_mailConfigured)
                            const Padding(
                              padding: EdgeInsets.only(bottom: 12),
                              child: Text('Email sending is not configured on the server', style: TextStyle(fontSize: 12, color: C.amber)),
                            ),
                          _switchRow(Icons.calendar_view_week_rounded, C.green, 'Weekly report', 'Every Sunday morning', _weekly, (v) => _save(weekly: v)),
                          _switchRow(Icons.calendar_month_rounded, C.purple, 'Monthly report', 'On the 1st of every month', _monthly, (v) => _save(monthly: v)),
                          const SizedBox(height: 10),
                          const SectionLabel('TRY IT NOW'),
                          PrimaryButton('Send weekly report now', onTap: _busy.isEmpty ? () => _sendNow('weekly') : null, loading: _busy == 'weekly', outline: true, icon: Icons.send_rounded),
                          const SizedBox(height: 10),
                          PrimaryButton('Send monthly report now', onTap: _busy.isEmpty ? () => _sendNow('monthly') : null, loading: _busy == 'monthly', outline: true, icon: Icons.send_rounded),
                          const Padding(
                            padding: EdgeInsets.only(top: 14),
                            child: Text('Estimates based on your plug readings, not an official bill.', style: TextStyle(fontSize: 11, color: C.text3, height: 1.5)),
                          ),
                        ]),
            ),
          ]),
        ),
      ),
    );
  }
}
