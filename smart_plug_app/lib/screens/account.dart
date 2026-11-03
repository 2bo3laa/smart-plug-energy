import 'package:flutter/material.dart' hide Text;
import '../core/api.dart';
import '../core/google.dart';
import '../core/i18n.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'login.dart';

/// Account settings: name, change password, and linking a Google account.
class AccountScreen extends StatefulWidget {
  const AccountScreen({super.key});
  @override
  State<AccountScreen> createState() => _AccountScreenState();
}

class _AccountScreenState extends State<AccountScreen> {
  late final TextEditingController _name = TextEditingController(text: Api.i.user?.name ?? '');
  final _cur = TextEditingController();
  final _new = TextEditingController();
  final _confirm = TextEditingController();
  String? _googleEmail;
  bool _loadedGoogle = false;
  bool _savingName = false, _savingPass = false, _googleBusy = false, _deleting = false;

  @override
  void initState() {
    super.initState();
    _loadGoogle();
  }

  @override
  void dispose() {
    _name.dispose();
    _cur.dispose();
    _new.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _loadGoogle() async {
    try {
      final p = await Api.i.profile();
      if (mounted) setState(() { _googleEmail = p.googleEmail; _loadedGoogle = true; });
    } on ApiException catch (_) {
      if (mounted) setState(() => _loadedGoogle = true);
    }
  }

  Future<void> _saveName() async {
    final v = _name.text.trim();
    if (v.isEmpty) {
      toast(context, 'Enter your name', error: true);
      return;
    }
    setState(() => _savingName = true);
    try {
      await Api.i.updateName(v);
      if (mounted) toast(context, 'Name updated');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _savingName = false);
    }
  }

  Future<void> _savePassword() async {
    if (_cur.text.isEmpty || _new.text.length < 6) {
      toast(context, 'New password must be at least 6 characters', error: true);
      return;
    }
    if (_new.text != _confirm.text) {
      toast(context, 'Passwords do not match', error: true);
      return;
    }
    setState(() => _savingPass = true);
    try {
      await Api.i.changePassword(_cur.text, _new.text);
      if (!mounted) return;
      _cur.clear();
      _new.clear();
      _confirm.clear();
      FocusScope.of(context).unfocus();
      toast(context, 'Password updated');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _savingPass = false);
    }
  }

  Future<void> _linkGoogle() async {
    setState(() => _googleBusy = true);
    try {
      final token = await GoogleAuth.idToken();
      if (token == null) return; // cancelled
      final email = await Api.i.linkGoogle(token);
      if (!mounted) return;
      setState(() => _googleEmail = email ?? _googleEmail);
      toast(context, 'Google account linked');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _googleBusy = false);
    }
  }

  Future<void> _unlinkGoogle() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('Unlink Google account?', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: const Text('You will no longer be able to sign in with Google. You can link it again any time.', style: TextStyle(color: C.text2, fontSize: 13)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(c, true), child: const Text('Unlink', style: TextStyle(color: C.red))),
        ],
      ),
    );
    if (ok != true) return;
    setState(() => _googleBusy = true);
    try {
      await Api.i.unlinkGoogle();
      if (!mounted) return;
      setState(() => _googleEmail = null);
      toast(context, 'Google account unlinked');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _googleBusy = false);
    }
  }

  static String _date(DateTime d) =>
      '${d.year}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

  /// Delete the account: the user retypes the e-mail and the password. It is wiped for good after 30 days unless restored.
  Future<void> _deleteAccount() async {
    final email = TextEditingController();
    final pass = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('Delete your account?', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: SingleChildScrollView(
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Text(
              'Your account will be closed now and all its data (devices, readings, schedules, backups) is deleted permanently after 30 days. Sign in again within 30 days to restore it.',
              style: TextStyle(color: C.text2, fontSize: 12.5, height: 1.5),
            ),
            const SizedBox(height: 16),
            Field(label: 'Email', hint: 'Enter Email', controller: email, keyboard: TextInputType.emailAddress),
            Field(label: 'Password', hint: 'Enter Password', controller: pass, obscure: true),
          ]),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(c, true), child: const Text('Delete Account', style: TextStyle(color: C.red))),
        ],
      ),
    );
    final e = email.text.trim(), p = pass.text;
    email.dispose();
    pass.dispose();
    if (ok != true) return;
    if (e.isEmpty || p.isEmpty) {
      toast(context, 'Enter your email and password', error: true);
      return;
    }
    setState(() => _deleting = true);
    try {
      final purge = await Api.i.deleteAccount(e, p);
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        barrierDismissible: false,
        builder: (c) => AlertDialog(
          backgroundColor: C.card,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: const Text('Account scheduled for deletion', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
          content: Text(
            purge == null
                ? 'Sign in again within 30 days to restore your account.'
                : tf('Your data will be deleted permanently on {date}. Sign in again before then to restore your account.', {'date': _date(purge)}),
            style: const TextStyle(color: C.text2, fontSize: 13, height: 1.5),
          ),
          actions: [TextButton(onPressed: () => Navigator.pop(c), child: const Text('OK'))],
        ),
      );
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(MaterialPageRoute(builder: (_) => const LoginScreen()), (_) => false);
    } on ApiException catch (ex) {
      if (mounted) toast(context, ex.message, error: true);
    } finally {
      if (mounted) setState(() => _deleting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final u = Api.i.user;
    final linked = _googleEmail != null && _googleEmail!.isNotEmpty;
    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 28),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const TopNav('Account Settings'),
            const SectionLabel('Profile'),
            Field(label: 'Full Name', hint: 'Your name', controller: _name, icon: Icons.person_outline_rounded),
            Padding(
              padding: const EdgeInsets.only(bottom: 14),
              child: Row(children: [
                const Icon(Icons.mail_outline_rounded, size: 16, color: C.text3),
                const SizedBox(width: 8),
                Expanded(child: Text(u?.email ?? '', style: const TextStyle(fontSize: 12.5, color: C.text2))),
              ]),
            ),
            PrimaryButton('Save Name', onTap: _saveName, loading: _savingName, outline: true),
            const SizedBox(height: 26),
            const SectionLabel('Change Password'),
            Field(label: 'Current Password', hint: '••••••••', controller: _cur, icon: Icons.lock_outline_rounded, obscure: true),
            Field(label: 'New Password', hint: '••••••••', controller: _new, icon: Icons.lock_outline_rounded, obscure: true),
            Field(label: 'Confirm New Password', hint: '••••••••', controller: _confirm, icon: Icons.lock_outline_rounded, obscure: true),
            PrimaryButton('Update Password', onTap: _savePassword, loading: _savingPass),
            const SizedBox(height: 26),
            const SectionLabel('Linked Accounts'),
            AppCard(
              padding: const EdgeInsets.all(14),
              child: Row(children: [
                Container(
                  width: 40, height: 40,
                  decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(12)),
                  alignment: Alignment.center,
                  child: const GoogleMark(),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    const Text('Google', style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
                    const SizedBox(height: 2),
                    Text(!_loadedGoogle ? '…' : (linked ? _googleEmail! : 'Not linked'),
                        maxLines: 1, overflow: TextOverflow.ellipsis,
                        style: TextStyle(fontSize: 11.5, color: linked ? C.green : C.text3)),
                  ]),
                ),
                if (_googleBusy)
                  const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: C.blue))
                else if (_loadedGoogle)
                  GestureDetector(
                    onTap: linked ? _unlinkGoogle : _linkGoogle,
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                      decoration: BoxDecoration(
                        color: linked ? C.dim(C.red) : C.dim(C.blue),
                        borderRadius: BorderRadius.circular(99),
                      ),
                      child: Text(linked ? 'Unlink' : 'Link', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: linked ? C.red : C.blue)),
                    ),
                  ),
              ]),
            ),
            const SizedBox(height: 10),
            const Text('Link your Google account to sign in with one tap from the login screen.', style: TextStyle(fontSize: 11, color: C.text3, height: 1.5)),
            const SizedBox(height: 26),
            const SectionLabel('Delete Account'),
            const Text('You can restore a deleted account for 30 days. After that everything is removed for good.', style: TextStyle(fontSize: 11.5, color: C.text2, height: 1.5)),
            const SizedBox(height: 12),
            GestureDetector(
              onTap: _deleting ? null : _deleteAccount,
              child: Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(color: C.dim(C.red), borderRadius: BorderRadius.circular(14)),
                child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                  if (_deleting)
                    const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: C.red))
                  else ...[
                    const Icon(Icons.delete_outline_rounded, size: 17, color: C.red),
                    const SizedBox(width: 8),
                    const Text('Delete Account', style: TextStyle(color: C.red, fontWeight: FontWeight.w700, fontSize: 13.5)),
                  ],
                ]),
              ),
            ),
          ]),
        ),
      ),
    );
  }
}

/// A plain coloured "G" mark (no brand asset needed).
class GoogleMark extends StatelessWidget {
  const GoogleMark();
  @override
  Widget build(BuildContext context) => const Text('G', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900, color: Color(0xFF4285F4)));
}
