import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/common.dart';
import 'tariff.dart';

class StatisticsScreen extends StatefulWidget {
  const StatisticsScreen({super.key});
  @override
  State<StatisticsScreen> createState() => _StatisticsScreenState();
}

class _StatisticsScreenState extends State<StatisticsScreen> {
  static const _ranges = ['day', 'week', 'month', 'year'];
  static const _names = ['Day', 'Week', 'Month', 'Year'];
  static const _axis = {
    'day': ['00:00', '06:00', '12:00', '18:00', '24:00'],
    'week': ['-6d', '-4d', '-2d', 'Today'],
    'month': ['-30d', '-20d', '-10d', 'Today'],
    'year': ['-12m', '-9m', '-6m', '-3m', 'Now'],
  };
  static const _palette = [C.blue, C.green, C.purple, C.amber, C.red];

  int _r = 0;
  Stats? _s;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _error = null);
    try {
      final s = await Api.i.statistics(_ranges[_r]);
      if (mounted) setState(() => _s = s);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final s = _s;
    return Padding(
      padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const TopNav('Statistics', back: false),
        FilterTabs(_names, _r, (i) { setState(() => _r = i); _load(); }),
        Expanded(
          child: _error != null && s == null
              ? ErrorRetry(_error!, _load)
              : s == null
                  ? const Loader()
                  : RefreshIndicator(color: C.blue, backgroundColor: C.card, onRefresh: _load, child: _content(s)),
        ),
      ]),
    );
  }

  Widget _content(Stats s) {
    final total = _ranges[_r] == 'day' ? s.energyToday : s.chart.fold<double>(0, (a, b) => a + b);
    final up = s.changeVsYesterday >= 0;
    final axis = _axis[_ranges[_r]]!;
    final values = s.breakdown.map((e) => e.value).toList();
    return ListView(padding: const EdgeInsets.only(bottom: 16), children: [
      Row(crossAxisAlignment: CrossAxisAlignment.start, mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
        Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Energy Consumption', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: C.text2)),
          const SizedBox(height: 2),
          Text.rich(TextSpan(children: [
            TextSpan(text: total.toStringAsFixed(2), style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800)),
            const TextSpan(text: '  kWh', style: TextStyle(fontSize: 12, color: C.text2, fontWeight: FontWeight.w600)),
          ])),
        ]),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
          decoration: BoxDecoration(color: C.dim(up ? C.red : C.green), borderRadius: BorderRadius.circular(99)),
          child: Text('${up ? '▲' : '▼'} ${s.changeVsYesterday.abs().toStringAsFixed(0)}% vs Yesterday',
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: up ? C.red : C.green)),
        ),
      ]),
      const SizedBox(height: 14),
      SizedBox(height: 100, width: double.infinity, child: CustomPaint(painter: AreaChartPainter(s.chart))),
      Padding(
        padding: const EdgeInsets.only(top: 4, bottom: 14),
        child: Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          for (final a in axis) Text(a, style: const TextStyle(fontSize: 9.5, color: C.text3)),
        ]),
      ),
      Row(children: [
        Expanded(child: _mini('Total Energy · This Month', s.totalEnergyMonth.toStringAsFixed(2), 'kWh')),
        const SizedBox(width: 10),
        Expanded(child: _mini('Total Cost · This Month', s.totalCostMonth.toStringAsFixed(2), 'EGP')),
      ]),
      const SizedBox(height: 14),
      _forecast(s),
      const SectionLabel('Device Breakdown'),
      if (s.breakdown.isEmpty)
        const Padding(padding: EdgeInsets.symmetric(vertical: 12), child: Text('No consumption data yet.', style: TextStyle(color: C.text3, fontSize: 12.5)))
      else
        Row(children: [
          SizedBox(width: 100, height: 100, child: CustomPaint(painter: DonutPainter(values, _palette))),
          const SizedBox(width: 18),
          Expanded(
            child: Column(children: [
              for (var i = 0; i < s.breakdown.length; i++)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 4),
                  child: Row(children: [
                    Container(width: 8, height: 8, decoration: BoxDecoration(shape: BoxShape.circle, color: _palette[i % _palette.length])),
                    const SizedBox(width: 8),
                    Expanded(child: Text(s.breakdown[i].key, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 11.5, color: C.text2))),
                    Text('${s.breakdown[i].value.toStringAsFixed(0)}%', style: const TextStyle(fontSize: 11.5, fontWeight: FontWeight.w700)),
                  ]),
                ),
            ]),
          ),
        ]),
    ]);
  }

  Widget _forecast(Stats s) {
    final f = s.forecast;
    if (f == null) return const SizedBox.shrink();
    final hasNext = f.nextPrice != null && f.kwhUntilNext != null;
    return AppCard(
      margin: const EdgeInsets.only(bottom: 18),
      padding: const EdgeInsets.all(14),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Container(
            width: 30, height: 30,
            decoration: BoxDecoration(color: C.dim(C.purple), borderRadius: BorderRadius.circular(9)),
            child: const Icon(Icons.trending_up_rounded, size: 16, color: C.purple),
          ),
          const SizedBox(width: 10),
          const Expanded(child: Text('Month Forecast', style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700))),
          GestureDetector(
            onTap: () => Navigator.push(context, MaterialPageRoute(builder: (_) => TariffScreen(currentKwh: s.totalEnergyMonth))),
            child: const Text('Tariff →', style: TextStyle(fontSize: 11.5, color: C.blue, fontWeight: FontWeight.w600)),
          ),
        ]),
        const SizedBox(height: 12),
        Row(children: [
          Expanded(child: _mini('Projected Energy', f.projectedEnergy.toStringAsFixed(1), 'kWh')),
          const SizedBox(width: 10),
          Expanded(child: _mini('Projected Cost', f.projectedCost.toStringAsFixed(2), 'EGP')),
        ]),
        const SizedBox(height: 10),
        Text(
          'Current tier: ${f.currentPrice.toStringAsFixed(2)} EGP/kWh'
          '${hasNext ? ' · ${f.kwhUntilNext!.toStringAsFixed(0)} kWh left before ${f.nextPrice!.toStringAsFixed(2)} EGP/kWh' : ''}',
          style: const TextStyle(fontSize: 11.5, color: C.text2, height: 1.4),
        ),
        if (f.willCross)
          Container(
            margin: const EdgeInsets.only(top: 10),
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: C.dim(C.amber), borderRadius: BorderRadius.circular(10)),
            child: const Row(children: [
              Icon(Icons.warning_amber_rounded, size: 16, color: C.amber),
              SizedBox(width: 8),
              Expanded(child: Text('At this pace you will enter a pricier tariff tier this month.', style: TextStyle(fontSize: 11.5, color: C.amber))),
            ]),
          ),
      ]),
    );
  }

  Widget _mini(String l, String v, String u) => Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(color: C.card, borderRadius: BorderRadius.circular(14), border: Border.all(color: C.border)),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(l, style: const TextStyle(fontSize: 10, color: C.text2)),
          const SizedBox(height: 4),
          Text.rich(TextSpan(children: [
            TextSpan(text: v, style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w800)),
            TextSpan(text: ' $u', style: const TextStyle(fontSize: 10.5, color: C.text2, fontWeight: FontWeight.w600)),
          ])),
        ]),
      );
}
