import 'package:flutter/material.dart' hide Text;
import 'package:flutter/services.dart';
import '../core/api.dart';
import '../core/i18n.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/common.dart';

/// Settings > Backups: save the setup (devices, schedules, power limits, report settings), restore it,
/// send a backup to the account e-mail as a file, copy it as text, or import a backup from text.
class BackupsScreen extends StatefulWidget {
  const BackupsScreen({super.key});
  @override
  State<BackupsScreen> createState() => _BackupsScreenState();
}

class _BackupsScreenState extends State<BackupsScreen> {
  List<BackupItem> _items = [];
  int _max = 5;
  bool _loaded = false;
  bool _busy = false;
  String? _error;

  String get _lang => Lang.i.isAr ? 'ar' : 'en';

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final r = await Api.i.backups();
      if (!mounted) return;
      setState(() {
        _items = r.items;
        _max = r.max;
        _loaded = true;
        _error = null;
      });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  /// Runs one action with the screen locked, then reloads the list.
  Future<void> _run(Future<void> Function() action, String done, {bool reload = true}) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await action();
      if (!mounted) return;
      toast(context, done);
      if (reload) await _load();
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool> _confirm(String title, String body, String yes, {bool danger = false}) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: Text(title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: Text(body, style: const TextStyle(color: C.text2, fontSize: 13, height: 1.5)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(c, true), child: Text(yes, style: TextStyle(color: danger ? C.red : C.blue))),
        ],
      ),
    );
    return ok == true;
  }

  Future<void> _restore(BackupItem b) async {
    final ok = await _confirm(
      'Restore this backup?',
      'Your devices, schedules, power limits and report settings will be replaced with the ones saved in this backup. A copy of your current setup is saved first, so you can go back.',
      'Restore',
    );
    if (ok) await _run(() => Api.i.restoreBackup(b.id), 'Backup restored');
  }

  Future<void> _copy(BackupItem b) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      final text = await Api.i.backupText(b.id);
      await Clipboard.setData(ClipboardData(text: text));
      if (mounted) toast(context, 'Backup text copied');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _import() async {
    final c = TextEditingController();
    final text = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('Import from text', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Paste the backup text. It is added to your list and you can restore it from there.', style: TextStyle(fontSize: 12, color: C.text2, height: 1.5)),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            decoration: BoxDecoration(color: C.input, borderRadius: BorderRadius.circular(14), border: Border.all(color: C.border)),
            child: TextField(
              controller: c,
              minLines: 5,
              maxLines: 8,
              keyboardType: TextInputType.multiline,
              style: const TextStyle(fontSize: 12, color: C.text1),
              decoration: const InputDecoration(border: InputBorder.none, hintText: '{ "app": "smart-plug-energy", … }', hintStyle: TextStyle(color: C.text3)),
            ),
          ),
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: TextButton.icon(
              onPressed: () async {
                final d = await Clipboard.getData(Clipboard.kTextPlain);
                if (d?.text != null) c.text = d!.text!;
              },
              icon: const Icon(Icons.content_paste_rounded, size: 16),
              label: const Text('Paste'),
            ),
          ),
        ]),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(ctx, c.text.trim()), child: const Text('Import')),
        ],
      ),
    );
    if (text == null || text.isEmpty) return;
    await _run(() => Api.i.importBackup(text), 'Backup imported');
  }

  static String _two(int n) => n.toString().padLeft(2, '0');
  String _when(DateTime d) => '${d.year}-${_two(d.month)}-${_two(d.day)}  ${_two(d.hour)}:${_two(d.minute)}';

  String _kind(String k) {
    switch (k) {
      case 'auto':
        return 'Automatic backup';
      case 'import':
        return 'Imported backup';
      case 'before_restore':
        return 'Saved before a restore';
      default:
        return 'Manual backup';
    }
  }

  IconData _kindIcon(String k) {
    switch (k) {
      case 'auto':
        return Icons.update_rounded;
      case 'import':
        return Icons.file_download_outlined;
      case 'before_restore':
        return Icons.history_rounded;
      default:
        return Icons.backup_outlined;
    }
  }

  Widget _action(IconData icon, String label, VoidCallback onTap) => Expanded(
        child: InkWell(
          onTap: _busy ? null : onTap,
          borderRadius: BorderRadius.circular(10),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              Icon(icon, size: 18, color: _busy ? C.text3 : C.blue),
              const SizedBox(height: 3),
              Text(label, style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: _busy ? C.text3 : C.blue)),
            ]),
          ),
        ),
      );

  Widget _card(BackupItem b) => AppCard(
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.fromLTRB(14, 14, 14, 6),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Container(
              width: 36, height: 36,
              decoration: BoxDecoration(color: C.dim(C.blue), borderRadius: BorderRadius.circular(11)),
              child: Icon(_kindIcon(b.kind), size: 18, color: C.blue),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(_kind(b.kind), style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
                const SizedBox(height: 2),
                Text(_when(b.createdAt), style: const TextStyle(fontSize: 11.5, color: C.text2)),
              ]),
            ),
          ]),
          const SizedBox(height: 10),
          Text(tf('{d} devices · {s} schedules', {'d': b.devices, 's': b.schedules}), style: const TextStyle(fontSize: 11.5, color: C.text3)),
          const Divider(height: 18, color: C.border),
          Row(children: [
            _action(Icons.settings_backup_restore_rounded, 'Restore', () => _restore(b)),
            _action(Icons.mail_outline_rounded, 'Send by email', () => _run(() => Api.i.emailBackup(b.id, _lang), 'Backup sent. Check your inbox.', reload: false)),
            _action(Icons.copy_rounded, 'Copy text', () => _copy(b)),
          ]),
        ]),
      );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const TopNav('Backups'),
            Expanded(
              child: _error != null && !_loaded
                  ? ErrorRetry(_error!, () { setState(() => _error = null); _load(); })
                  : !_loaded
                      ? const Loader()
                      : ListView(padding: const EdgeInsets.only(bottom: 24), children: [
                          AppCard(
                            margin: const EdgeInsets.only(bottom: 14),
                            child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                              const Icon(Icons.info_outline_rounded, color: C.blue, size: 20),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Text(
                                  tf('A backup saves your devices, schedules, power limits and report settings. One is made automatically every week and the last {n} are kept. Your energy history is not included.', {'n': _max}),
                                  style: const TextStyle(fontSize: 12, color: C.text2, height: 1.55),
                                ),
                              ),
                            ]),
                          ),
                          PrimaryButton('Create backup now', onTap: _busy ? null : () => _run(Api.i.createBackup, 'Backup created'), loading: _busy, icon: Icons.backup_outlined),
                          const SizedBox(height: 10),
                          PrimaryButton('Import from text', onTap: _busy ? null : _import, outline: true, icon: Icons.file_download_outlined),
                          const SizedBox(height: 22),
                          const SectionLabel('SAVED BACKUPS'),
                          if (_items.isEmpty)
                            const Padding(
                              padding: EdgeInsets.symmetric(vertical: 24),
                              child: Center(child: Text('No backups yet', style: TextStyle(fontSize: 12.5, color: C.text3))),
                            )
                          else
                            for (final b in _items) _card(b),
                        ]),
            ),
          ]),
        ),
      ),
    );
  }
}
