import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'forgot_password.dart';
import 'shell.dart';
import 'signup.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});
  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _email = TextEditingController();
  final _pass = TextEditingController();
  bool _on = false;
  bool _loading = false;
  double _pull = 0;
  static const _maxPull = 60.0;
  static const _threshold = 32.0;

  @override
  void dispose() {
    _email.dispose();
    _pass.dispose();
    super.dispose();
  }

  void _endPull() {
    if (_pull > _threshold) setState(() => _on = !_on);
    setState(() => _pull = 0);
  }

  Future<void> _login() async {
    final email = _email.text.trim();
    final pass = _pass.text;
    if (email.isEmpty || pass.isEmpty) {
      toast(context, 'Enter your email and password', error: true);
      return;
    }
    setState(() => _loading = true);
    try {
      await Api.i.login(email, pass);
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
      body: Stack(children: [
        // room light overlay
        AnimatedContainer(
          duration: const Duration(milliseconds: 700),
          decoration: BoxDecoration(
            gradient: _on
                ? const RadialGradient(center: Alignment(0, -0.65), radius: 1.1, colors: [Color(0xFF3A2C18), Color(0xFF1C150C), C.bg], stops: [0, 0.42, 0.78])
                : const LinearGradient(colors: [C.bg, C.bg]),
          ),
        ),
        SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(22, 18, 22, 24),
            child: Column(children: [
              SizedBox(
                height: 250,
                width: 160,
                child: Stack(alignment: Alignment.topCenter, children: [
                  AnimatedOpacity(
                    duration: const Duration(milliseconds: 600),
                    opacity: _on ? 1 : 0,
                    child: Container(
                      width: 190, height: 190,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        gradient: RadialGradient(colors: [C.amber.withOpacity(0.55), C.amber.withOpacity(0)], stops: const [0, 0.7]),
                      ),
                    ),
                  ),
                  CustomPaint(size: const Size(120, 160), painter: _LampPainter(_on)),
                  // pull cord
                  Positioned(
                    left: 112, top: 84,
                    child: GestureDetector(
                      behavior: HitTestBehavior.opaque,
                      onTap: () => setState(() => _on = !_on),
                      onVerticalDragUpdate: (d) => setState(() => _pull = (_pull + d.delta.dy).clamp(0, _maxPull)),
                      onVerticalDragEnd: (_) => _endPull(),
                      onVerticalDragCancel: _endPull,
                      child: SizedBox(
                        width: 36,
                        child: Column(children: [
                          AnimatedContainer(
                            duration: Duration(milliseconds: _pull == 0 ? 350 : 0),
                            curve: Curves.elasticOut,
                            width: 2, height: 36 + _pull,
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(2),
                              gradient: const LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0xFFF0B84A), Color(0xFFC68A28)]),
                            ),
                          ),
                          Container(
                            width: 14, height: 14,
                            decoration: const BoxDecoration(color: Color(0xFFE0A233), shape: BoxShape.circle, boxShadow: [BoxShadow(color: Colors.black38, blurRadius: 5, offset: Offset(0, 2))]),
                          ),
                        ]),
                      ),
                    ),
                  ),
                ]),
              ),
              AnimatedOpacity(
                duration: const Duration(milliseconds: 300),
                opacity: _on ? 0 : 1,
                child: const Text('Pull the cord to sign in', style: TextStyle(fontSize: 11.5, color: C.text3)),
              ),
              const SizedBox(height: 10),
              AnimatedOpacity(
                duration: const Duration(milliseconds: 500),
                opacity: _on ? 1 : 0,
                child: IgnorePointer(
                  ignoring: !_on,
                  child: Container(
                    padding: const EdgeInsets.all(20),
                    decoration: BoxDecoration(
                      color: Colors.white.withOpacity(0.055),
                      borderRadius: BorderRadius.circular(20),
                      border: Border.all(color: Colors.white.withOpacity(0.12)),
                    ),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      const Text('Welcome', style: TextStyle(fontSize: 19, fontWeight: FontWeight.w800)),
                      const SizedBox(height: 4),
                      const Text('Sign in to control your home', style: TextStyle(fontSize: 11.5, color: C.text2)),
                      const SizedBox(height: 16),
                      Field(label: 'Email', hint: 'Enter Email', controller: _email, glass: true, keyboard: TextInputType.emailAddress),
                      Field(label: 'Password', hint: 'Enter Password', controller: _pass, glass: true, obscure: true),
                      Align(
                        alignment: Alignment.centerRight,
                        child: GestureDetector(
                          onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const ForgotPasswordScreen())),
                          child: const Padding(
                            padding: EdgeInsets.only(bottom: 14),
                            child: Text('Forgot password?', style: TextStyle(fontSize: 12, color: Color(0xFFF0B84A), fontWeight: FontWeight.w600)),
                          ),
                        ),
                      ),
                      PrimaryButton('Sign In', onTap: _login, loading: _loading, color: const Color(0xFFE8AE45), textColor: const Color(0xFF241A05)),
                      const SizedBox(height: 14),
                      Center(
                        child: GestureDetector(
                          onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const SignupScreen())),
                          child: const Text.rich(TextSpan(style: TextStyle(fontSize: 12, color: C.text2), children: [
                            TextSpan(text: "Don't have an account? "),
                            TextSpan(text: 'Sign Up', style: TextStyle(color: Color(0xFFF0B84A), fontWeight: FontWeight.w700)),
                          ])),
                        ),
                      ),
                    ]),
                  ),
                ),
              ),
            ]),
          ),
        ),
      ]),
    );
  }
}

class _LampPainter extends CustomPainter {
  final bool on;
  _LampPainter(this.on);
  @override
  void paint(Canvas canvas, Size s) {
    final cx = s.width / 2;
    // hanging wire
    canvas.drawLine(Offset(cx, 0), Offset(cx, 34), Paint()..color = C.borderStrong..strokeWidth = 2);
    // shade
    final shade = Path()
      ..moveTo(cx - 14, 34)
      ..lineTo(cx + 14, 34)
      ..lineTo(cx + 52, 84)
      ..quadraticBezierTo(cx, 96, cx - 52, 84)
      ..close();
    canvas.drawPath(
      shade,
      Paint()..shader = LinearGradient(colors: on ? const [Color(0xFFF0B84A), Color(0xFFC68A28)] : const [Color(0xFF2A3347), Color(0xFF1A2130)]).createShader(Rect.fromLTWH(cx - 52, 34, 104, 62)),
    );
    canvas.drawPath(shade, Paint()..style = PaintingStyle.stroke..strokeWidth = 1.2..color = Colors.white.withOpacity(0.12));
    // bulb
    final bulb = Offset(cx, 98);
    if (on) {
      canvas.drawOval(Rect.fromCenter(center: Offset(cx, 90), width: 52, height: 16), Paint()..color = const Color(0xFFFFD98A));
      canvas.drawCircle(bulb, 11, Paint()..color = const Color(0xFFFFE9B5));
    } else {
      canvas.drawCircle(bulb, 11, Paint()..color = const Color(0xFF3A4358));
    }
  }

  @override
  bool shouldRepaint(_LampPainter o) => o.on != on;
}
