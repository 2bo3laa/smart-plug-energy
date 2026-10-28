import 'dart:math' as math;
import 'package:flutter/material.dart';
import '../core/theme.dart';

void toast(BuildContext c, String msg, {bool error = false}) {
  ScaffoldMessenger.of(c)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(
      content: Text(msg, style: const TextStyle(color: C.text1)),
      backgroundColor: error ? const Color(0xFF3A1A1F) : C.card,
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: error ? C.red : C.borderStrong),
      ),
    ));
}

class AppCard extends StatelessWidget {
  final Widget child;
  final EdgeInsets padding;
  final double radius;
  final EdgeInsets margin;
  final VoidCallback? onTap;
  const AppCard({required this.child, this.padding = const EdgeInsets.all(14), this.radius = 18, this.margin = EdgeInsets.zero, this.onTap});
  @override
  Widget build(BuildContext context) => Padding(
        padding: margin,
        child: Material(
          color: C.card,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radius), side: const BorderSide(color: C.border)),
          child: InkWell(
            borderRadius: BorderRadius.circular(radius),
            onTap: onTap,
            child: Padding(padding: padding, child: child),
          ),
        ),
      );
}

class PrimaryButton extends StatelessWidget {
  final String label;
  final VoidCallback? onTap;
  final bool loading;
  final bool outline;
  final Color? color;
  final Color? textColor;
  final IconData? icon;
  const PrimaryButton(this.label, {this.onTap, this.loading = false, this.outline = false, this.color, this.textColor, this.icon});
  @override
  Widget build(BuildContext context) {
    final disabled = onTap == null || loading;
    return GestureDetector(
      onTap: disabled ? null : onTap,
      child: Opacity(
        opacity: disabled && !loading ? 0.5 : 1,
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(vertical: 14),
          decoration: BoxDecoration(
            gradient: outline || color != null ? null : const LinearGradient(colors: [Color(0xFF4C8CFF), Color(0xFF2F6FEF)]),
            color: outline ? C.card : color,
            borderRadius: BorderRadius.circular(14),
            border: outline ? Border.all(color: C.borderStrong) : null,
            boxShadow: outline ? null : [BoxShadow(color: (color ?? C.blue).withOpacity(0.45), blurRadius: 22, offset: const Offset(0, 8), spreadRadius: -8)],
          ),
          child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
            if (loading)
              const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
            else ...[
              if (icon != null) ...[Icon(icon, size: 18, color: textColor ?? Colors.white), const SizedBox(width: 8)],
              Text(label, style: TextStyle(fontSize: 14.5, fontWeight: FontWeight.w700, color: textColor ?? Colors.white)),
            ],
          ]),
        ),
      ),
    );
  }
}

class Field extends StatelessWidget {
  final String label;
  final String hint;
  final IconData? icon;
  final TextEditingController controller;
  final bool obscure;
  final TextInputType? keyboard;
  final bool glass;
  final ValueChanged<String>? onChanged;
  const Field({required this.label, required this.hint, required this.controller, this.icon, this.obscure = false, this.keyboard, this.glass = false, this.onChanged});
  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: const TextStyle(fontSize: 12, color: C.text2, fontWeight: FontWeight.w600)),
        const SizedBox(height: 7),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 14),
          decoration: BoxDecoration(
            color: glass ? Colors.white.withOpacity(0.05) : C.input,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: glass ? Colors.white.withOpacity(0.14) : C.border),
          ),
          child: Row(children: [
            if (icon != null) ...[Icon(icon, size: 17, color: C.text3), const SizedBox(width: 10)],
            Expanded(
              child: TextField(
                controller: controller,
                obscureText: obscure,
                keyboardType: keyboard,
                onChanged: onChanged,
                style: const TextStyle(fontSize: 13.5, color: C.text1),
                decoration: InputDecoration(
                  border: InputBorder.none,
                  hintText: hint,
                  hintStyle: const TextStyle(color: C.text3),
                  contentPadding: const EdgeInsets.symmetric(vertical: 14),
                ),
              ),
            ),
          ]),
        ),
      ]),
    );
  }
}

class TopNav extends StatelessWidget {
  final String title;
  final bool back;
  final Widget? trailing;
  const TopNav(this.title, {this.back = true, this.trailing});
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: Row(children: [
          if (back) ...[IconBtn(Icons.arrow_back_ios_new_rounded, () => Navigator.maybePop(context), size: 16), const SizedBox(width: 14)],
          Expanded(child: Text(title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w700))),
          if (trailing != null) trailing!,
        ]),
      );
}

class IconBtn extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;
  final double size;
  final int badge;
  const IconBtn(this.icon, this.onTap, {this.size = 17, this.badge = 0});
  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: Stack(clipBehavior: Clip.none, children: [
          Container(
            width: 36, height: 36,
            decoration: BoxDecoration(color: C.card, borderRadius: BorderRadius.circular(10)),
            child: Icon(icon, size: size, color: C.text1),
          ),
          if (badge > 0)
            Positioned(
              right: -3, top: -3,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                decoration: BoxDecoration(color: C.red, borderRadius: BorderRadius.circular(10)),
                child: Text(badge > 9 ? '9+' : '$badge', style: const TextStyle(fontSize: 9, fontWeight: FontWeight.w800, color: Colors.white)),
              ),
            ),
        ]),
      );
}

class AppToggle extends StatelessWidget {
  final bool on;
  final ValueChanged<bool>? onChanged;
  const AppToggle(this.on, {this.onChanged});
  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onChanged == null ? null : () => onChanged!(!on),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          width: 42, height: 24,
          padding: const EdgeInsets.all(2),
          decoration: BoxDecoration(color: on ? C.blue : C.borderStrong, borderRadius: BorderRadius.circular(99)),
          alignment: on ? Alignment.centerRight : Alignment.centerLeft,
          child: Container(width: 20, height: 20, decoration: const BoxDecoration(color: Colors.white, shape: BoxShape.circle)),
        ),
      );
}

class AppChip extends StatelessWidget {
  final String label;
  final bool active;
  final bool dashed;
  final VoidCallback? onTap;
  const AppChip(this.label, {this.active = false, this.dashed = false, this.onTap});
  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
          decoration: BoxDecoration(
            color: active ? C.dim(C.blue) : C.card,
            borderRadius: BorderRadius.circular(99),
            border: Border.all(color: active ? C.blue.withOpacity(0.45) : C.border),
          ),
          child: Text(label, style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600, color: active ? C.blue : (dashed ? C.text3 : C.text2))),
        ),
      );
}

class FilterTabs extends StatelessWidget {
  final List<String> items;
  final int index;
  final ValueChanged<int> onChanged;
  const FilterTabs(this.items, this.index, this.onChanged);
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 16),
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(children: [
            for (var i = 0; i < items.length; i++)
              Padding(
                padding: const EdgeInsets.only(right: 8),
                child: GestureDetector(
                  onTap: () => onChanged(i),
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
                    decoration: BoxDecoration(
                      color: i == index ? C.blue : C.card,
                      borderRadius: BorderRadius.circular(99),
                      border: Border.all(color: i == index ? C.blue : C.border),
                    ),
                    child: Text(items[i], style: TextStyle(fontSize: 11.5, fontWeight: FontWeight.w700, color: i == index ? Colors.white : C.text2)),
                  ),
                ),
              ),
          ]),
        ),
      );
}

class MeasCard extends StatelessWidget {
  final IconData icon;
  final Color color;
  final String value, label;
  const MeasCard(this.icon, this.color, this.value, this.label);
  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(color: C.card, borderRadius: BorderRadius.circular(14), border: Border.all(color: C.border)),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Container(
            width: 26, height: 26,
            decoration: BoxDecoration(color: C.dim(color), borderRadius: BorderRadius.circular(7)),
            child: Icon(icon, size: 14, color: color),
          ),
          const SizedBox(height: 8),
          Text(value, style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w800)),
          const SizedBox(height: 1),
          Text(label, style: const TextStyle(fontSize: 10.5, color: C.text2)),
        ]),
      );
}

class SectionLabel extends StatelessWidget {
  final String text;
  const SectionLabel(this.text);
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(top: 2, bottom: 10),
        child: Text(text, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: C.text2)),
      );
}

class ErrorRetry extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;
  const ErrorRetry(this.message, this.onRetry);
  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(28),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            const Icon(Icons.cloud_off_rounded, size: 40, color: C.text3),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: const TextStyle(color: C.text2, fontSize: 13)),
            const SizedBox(height: 16),
            SizedBox(width: 140, child: PrimaryButton('Retry', onTap: onRetry)),
          ]),
        ),
      );
}

class Loader extends StatelessWidget {
  const Loader();
  @override
  Widget build(BuildContext context) => const Center(child: CircularProgressIndicator(color: C.blue, strokeWidth: 2.5));
}

// ---------------- painters ----------------

class RingPainter extends CustomPainter {
  final double progress;
  final Color color;
  RingPainter(this.progress, this.color);
  @override
  void paint(Canvas canvas, Size size) {
    final c = size.center(Offset.zero);
    final r = size.width / 2 - 5;
    final base = Paint()..style = PaintingStyle.stroke..strokeWidth = 7..color = Colors.white.withOpacity(0.07);
    final fg = Paint()..style = PaintingStyle.stroke..strokeWidth = 7..strokeCap = StrokeCap.round..color = color;
    canvas.drawCircle(c, r, base);
    canvas.drawArc(Rect.fromCircle(center: c, radius: r), -math.pi / 2, 2 * math.pi * progress.clamp(0.0, 1.0), false, fg);
  }

  @override
  bool shouldRepaint(RingPainter oldDelegate) => oldDelegate.progress != progress || oldDelegate.color != color;
}

class DonutPainter extends CustomPainter {
  final List<double> values;
  final List<Color> colors;
  DonutPainter(this.values, this.colors);
  @override
  void paint(Canvas canvas, Size size) {
    final rect = Rect.fromCircle(center: size.center(Offset.zero), radius: size.width / 2 - 9);
    final total = values.fold<double>(0, (a, b) => a + b);
    final track = Paint()..style = PaintingStyle.stroke..strokeWidth = 18..color = Colors.white.withOpacity(0.06);
    canvas.drawArc(rect, 0, 2 * math.pi, false, track);
    if (total <= 0) return;
    var start = -math.pi / 2;
    for (var i = 0; i < values.length; i++) {
      final sweep = 2 * math.pi * (values[i] / total);
      final p = Paint()..style = PaintingStyle.stroke..strokeWidth = 18..color = colors[i % colors.length];
      canvas.drawArc(rect, start, math.max(0, sweep - 0.03), false, p);
      start += sweep;
    }
  }

  @override
  bool shouldRepaint(DonutPainter oldDelegate) => true;
}

class AreaChartPainter extends CustomPainter {
  final List<double> data;
  AreaChartPainter(this.data);
  @override
  void paint(Canvas canvas, Size size) {
    final grid = Paint()..color = Colors.white.withOpacity(0.05)..strokeWidth = 1;
    for (var i = 1; i < 4; i++) {
      final y = size.height * i / 4;
      canvas.drawLine(Offset(0, y), Offset(size.width, y), grid);
    }
    if (data.length < 2) return;
    final maxV = math.max(data.reduce(math.max), 0.0001);
    final pts = <Offset>[
      for (var i = 0; i < data.length; i++)
        Offset(size.width * i / (data.length - 1), size.height - (data[i] / maxV) * (size.height - 8) - 4)
    ];
    final line = Path()..moveTo(pts.first.dx, pts.first.dy);
    for (var i = 1; i < pts.length; i++) {
      final mid = (pts[i - 1].dx + pts[i].dx) / 2;
      line.cubicTo(mid, pts[i - 1].dy, mid, pts[i].dy, pts[i].dx, pts[i].dy);
    }
    final area = Path.from(line)..lineTo(size.width, size.height)..lineTo(0, size.height)..close();
    canvas.drawPath(
      area,
      Paint()..shader = const LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0x593B82F6), Color(0x003B82F6)]).createShader(Offset.zero & size),
    );
    canvas.drawPath(line, Paint()..style = PaintingStyle.stroke..strokeWidth = 2.4..strokeCap = StrokeCap.round..color = C.blue);
  }

  @override
  bool shouldRepaint(AreaChartPainter oldDelegate) => oldDelegate.data != data;
}
