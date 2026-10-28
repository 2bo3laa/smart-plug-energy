import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/theme.dart';
import '../widgets/common.dart';

class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key});
  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final _email = TextEditingController();
  final _code = TextEditingController();
  final _pass = TextEditingController();
  bool _codeSent = false;
  bool _loading = false;

  Future<void> _send() async {
    if (_email.text.trim().isEmpty) {
      toast(context, 'Enter your email', error: true);
      return;
    }
    setState(() => _loading = true);
    try {
      final dev = await Api.i.forgotPassword(_email.text.trim());
      if (!mounted) return;
      setState(() => _codeSent = true);
      // The backend returns devCode only in development (no email provider yet).
      if (dev != null) _code.text = dev;
      toast(context, 'If the account exists, a reset code was sent.');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _reset() async {
    if (_code.text.trim().isEmpty || _pass.text.length < 6) {
      toast(context, 'Enter the code and a password of at least 6 characters', error: true);
      return;
    }
    setState(() => _loading = true);
    try {
      await Api.i.resetPassword(_email.text.trim(), _code.text.trim(), _pass.text);
      if (!mounted) return;
      toast(context, 'Password updated. Please log in.');
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
            padding: const EdgeInsets.fromLTRB(22, 16, 22, 24),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const TopNav('Reset Password'),
              const Text('Enter your email and we will send you a 6-digit code.', style: TextStyle(color: C.text2, fontSize: 13, height: 1.5)),
              const SizedBox(height: 18),
              Field(label: 'Email', hint: 'you@email.com', controller: _email, icon: Icons.mail_outline_rounded, keyboard: TextInputType.emailAddress),
              if (!_codeSent) PrimaryButton('Send Code', onTap: _send, loading: _loading),
              if (_codeSent) ...[
                Field(label: 'Reset Code', hint: '6-digit code', controller: _code, icon: Icons.pin_outlined, keyboard: TextInputType.number),
                Field(label: 'New Password', hint: '••••••••', controller: _pass, icon: Icons.lock_outline_rounded, obscure: true),
                PrimaryButton('Update Password', onTap: _reset, loading: _loading),
              ],
            ]),
          ),
        ),
      );
}
