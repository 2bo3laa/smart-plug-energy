import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/common.dart';

/// Shows the stepped residential tariff and highlights the tier the month's consumption is in.
class TariffScreen extends StatefulWidget {
  final double? currentKwh;
  const TariffScreen({super.key, this.currentKwh});
  @override
  State<TariffScreen> createState() => _TariffScreenState();
}

class _TariffScreenState extends State<TariffScreen> {
  List<TariffTier>? _tiers;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final t = await Api.i.tariffTiers();
      if (mounted) setState(() { _tiers = t; _error = null; });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  bool _isCurrent(TariffTier t) {
    final k = widget.currentKwh;
    if (k == null) return false;
    return k >= t.minKwh && (t.maxKwh == null || k < t.maxKwh!);
  }

  @override
  Widget build(BuildContext context) {
    final tiers = _tiers;
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const TopNav('Electricity Tariff'),
            if (widget.currentKwh != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 14),
                child: Text('This month so far: ${widget.currentKwh!.toStringAsFixed(1)} kWh', style: const TextStyle(fontSize: 12.5, color: C.text2)),
              ),
            Expanded(
              child: _error != null && tiers == null
                  ? ErrorRetry(_error!, () { setState(() => _error = null); _load(); })
                  : tiers == null
                      ? const Loader()
                      : ListView(padding: const EdgeInsets.only(bottom: 16), children: [
                          if (tiers.isEmpty)
                            const Padding(padding: EdgeInsets.only(top: 30), child: Center(child: Text('No tiers defined. A flat rate is used.', style: TextStyle(color: C.text3, fontSize: 13)))),
                          for (var i = 0; i < tiers.length; i++) _row(i + 1, tiers[i]),
                          const Padding(
                            padding: EdgeInsets.only(top: 14),
                            child: Text('Cost is calculated tier by tier: each band of kWh is charged at its own price. Estimates only, not an official bill.',
                                style: TextStyle(fontSize: 11, color: C.text3, height: 1.5)),
                          ),
                        ]),
            ),
          ]),
        ),
      ),
    );
  }

  Widget _row(int n, TariffTier t) {
    final cur = _isCurrent(t);
    final range = t.maxKwh == null ? '${t.minKwh.toStringAsFixed(0)}+ kWh' : '${t.minKwh.toStringAsFixed(0)} – ${t.maxKwh!.toStringAsFixed(0)} kWh';
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: cur ? C.dim(C.blue) : C.card,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: cur ? C.blue.withOpacity(0.5) : C.border),
      ),
      child: Row(children: [
        Container(
          width: 30, height: 30,
          decoration: BoxDecoration(color: cur ? C.blue : C.cardAlt, borderRadius: BorderRadius.circular(9)),
          alignment: Alignment.center,
          child: Text('$n', style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w800, color: cur ? Colors.white : C.text2)),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(range, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
            if (cur) const Padding(padding: EdgeInsets.only(top: 2), child: Text('You are here', style: TextStyle(fontSize: 10.5, color: C.blue, fontWeight: FontWeight.w600))),
          ]),
        ),
        Text('${t.price.toStringAsFixed(2)} EGP/kWh', style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w800)),
      ]),
    );
  }
}
