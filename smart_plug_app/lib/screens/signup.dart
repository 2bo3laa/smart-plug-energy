import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'shell.dart';

class SignupScreen extends StatefulWidget {
  const SignupScreen({super.key});
  @override
  State<SignupScreen> createState() => _SignupScreenState();
}

class _SignupScreenState extends State<SignupScreen> {
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _pass = TextEditingController();
  final _confirm = TextEditingController();
  bool _agree = true;
  bool _loading = false;

  Future<void> _submit() async {
    final name = _name.text.trim();
    final email = _email.text.trim();
    if (name.isEmpty || email.isEmpty || _pass.text.isEmpty) {
      toast(context, 'Please fill in all fields', error: true);
      return;
    }
    if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email)) {
      toast(context, 'Enter a valid email address', error: true);
      return;
    }
    if (_pass.text.length < 6) {
      toast(context, 'Password must be at least 6 characters', error: true);
      return;
    }
    if (_pass.text != _confirm.text) {
      toast(context, 'Passwords do not match', error: true);
      return;
    }
    if (!_agree) {
      toast(context, 'Please accept the Terms of Service', error: true);
      return;
    }
    setState(() => _loading = true);
    try {
      await Api.i.signup(name, email, _pass.text);
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(MaterialPageRoute(builder: (_) => const Shell()), (_) => false);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(22, 16, 22, 24),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const TopNav('Create Account'),
            Field(label: 'Full Name', hint: 'Osama Ahmed', controller: _name, icon: Icons.person_outline_rounded),
            Field(label: 'Email', hint: 'osama@email.com', controller: _email, icon: Icons.mail_outline_rounded, keyboard: TextInputType.emailAddress),
            Field(label: 'Password', hint: '••••••••', controller: _pass, icon: Icons.lock_outline_rounded, obscure: true),
            Field(label: 'Confirm Password', hint: '••••••••', controller: _confirm, icon: Icons.lock_outline_rounded, obscure: true),
            GestureDetector(
              onTap: () => setState(() => _agree = !_agree),
              child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Container(
                  width: 18, height: 18,
                  margin: const EdgeInsets.only(top: 1),
                  decoration: BoxDecoration(
                    color: _agree ? C.blue : Colors.transparent,
                    borderRadius: BorderRadius.circular(5),
                    border: Border.all(color: _agree ? C.blue : C.borderStrong, width: 1.6),
                  ),
                  child: _agree ? const Icon(Icons.check_rounded, size: 13, color: Colors.white) : null,
                ),
                const SizedBox(width: 10),
                const Expanded(
                  child: Text.rich(TextSpan(style: TextStyle(fontSize: 12, color: C.text2, height: 1.5), children: [
                    TextSpan(text: 'I agree to the '),
                    TextSpan(text: 'Terms of Service', style: TextStyle(color: C.text1, fontWeight: FontWeight.w600)),
                    TextSpan(text: ' and '),
                    TextSpan(text: 'Privacy Policy', style: TextStyle(color: C.text1, fontWeight: FontWeight.w600)),
                  ])),
                ),
              ]),
            ),
            const SizedBox(height: 20),
            PrimaryButton('Create Account', onTap: _submit, loading: _loading),
            const SizedBox(height: 16),
            Center(
              child: GestureDetector(
                onTap: () => Navigator.pop(context),
                child: const Text.rich(TextSpan(style: TextStyle(fontSize: 13, color: C.text2), children: [
                  TextSpan(text: 'Already have an account? '),
                  TextSpan(text: 'Log In', style: TextStyle(color: C.blue, fontWeight: FontWeight.w700)),
                ])),
              ),
            ),
          ]),
        ),
      ),
    );
  }
}
