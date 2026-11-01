import 'package:flutter/material.dart' hide Text;
import 'package:flutter/services.dart';
import '../core/api.dart';
import '../core/i18n.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/common.dart';

/// Estimated invoice for all devices (from Statistics) or for one device (from the device page).
/// The user picks Weekly (last 7 days) or Monthly (this month so far); it can be copied or e-mailed.
class InvoiceScreen extends StatefulWidget {
  final int? deviceId;
  final String? deviceName;
  const InvoiceScreen({super.key, this.deviceId, this.deviceName});
  @override
  State<InvoiceScreen> createState() => _InvoiceScreenState();
}

class _InvoiceScreenState extends State<InvoiceScreen> {
  String _period = 'weekly';
  Invoice? _inv;
  String? _error;
  bool _loading = true;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final p = _period;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final i = await Api.i.invoice(p, deviceId: widget.deviceId);
      if (mounted && p == _period) setState(() { _inv = i; _loading = false; });
    } on ApiException catch (e) {
      if (mounted && p == _period) setState(() { _error = e.message; _loading = false; });
    }
  }

  void _setPeriod(String p) {
    if (p == _period) return;
    setState(() => _period = p);
    _load();
  }

  String _d(DateTime d) {
    final l = d.toLocal();
    return '${l.year}-${l.month.toString().padLeft(2, '0')}-${l.day.toString().padLeft(2, '0')}';
  }

  String _text(Invoice i) {
    final b = StringBuffer();
    b.writeln('${tr(i.singleDevice ? 'Estimated Device Invoice' : 'Estimated Invoice')} · ${i.number}');
    b.writeln('${tr(i.period == 'weekly' ? 'Weekly' : 'Monthly')}: ${_d(i.from)} → ${_d(i.to)}');
    b.writeln('');
    for (final l in i.lines) {
      b.writeln('${l.name}${l.room.isEmpty ? '' : ' (${l.room})'}: ${l.hours.toStringAsFixed(1)} ${tr('h')} · ${kwhText(l.kwh)} · ${egp(l.costEgp)}');
    }
    b.writeln('');
    b.writeln('${tr('Total')}: ${kwhText(i.totalKwh)} · ${egp(i.totalCostEgp)}');
    for (final x in i.bands) {
      b.writeln('${tr('Tier')} ${x.tier}: ${kwhText(x.kwh)} × ${x.price.toStringAsFixed(2)} = ${egp(x.costEgp)}');
    }
    b.writeln('');
    b.writeln(tr('Estimated invoice, not an official bill.'));
    return b.toString();
  }

  Future<void> _copy(Invoice i) async {
    await Clipboard.setData(ClipboardData(text: _text(i)));
    if (mounted) toast(context, 'Invoice copied');
  }

  Future<void> _email() async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await Api.i.emailInvoice(_period, deviceId: widget.deviceId, lang: Lang.i.isAr ? 'ar' : 'en');
      if (mounted) toast(context, 'Invoice sent. Check your inbox.');
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _segment() {
    Widget item(String p, String label) {
      final on = _period == p;
      return Expanded(
        child: GestureDetector(
          onTap: () => _setPeriod(p),
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 180),
            padding: const EdgeInsets.symmetric(vertical: 11),
            alignment: Alignment.center,
            decoration: BoxDecoration(color: on ? C.blue : Colors.transparent, borderRadius: BorderRadius.circular(11)),
            child: Text(label, style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700, color: on ? Colors.white : C.text2)),
          ),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(color: C.card, borderRadius: BorderRadius.circular(15), border: Border.all(color: C.border)),
      child: Row(children: [item('weekly', 'Weekly'), item('monthly', 'Monthly')]),
    );
  }

  Widget _row4(String a, String b, String c, String d, {bool head = false, bool bold = false}) {
    final st = TextStyle(
      fontSize: head ? 11 : 12.5,
      color: head ? C.text3 : C.text1,
      fontWeight: bold ? FontWeight.w800 : FontWeight.w500,
    );
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(children: [
        Expanded(flex: 5, child: Text(a, style: st, overflow: TextOverflow.ellipsis)),
        Expanded(flex: 2, child: Text(b, style: st, textAlign: TextAlign.end)),
        Expanded(flex: 3, child: Text(c, style: st, textAlign: TextAlign.end)),
        Expanded(flex: 3, child: Text(d, style: st, textAlign: TextAlign.end)),
      ]),
    );
  }

  Widget _invoiceCard(Invoice i) {
    final single = i.singleDevice;
    final hoursTotal = i.lines.fold<double>(0, (s, l) => s + l.hours);
    return AppCard(
      padding: const EdgeInsets.all(18),
      radius: 22,
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          const Icon(Icons.receipt_long_rounded, color: C.green, size: 22),
          const SizedBox(width: 10),
          Expanded(
            child: Text(single ? (widget.deviceName ?? 'Estimated Device Invoice') : 'Estimated Invoice',
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800)),
          ),
        ]),
        const SizedBox(height: 6),
        Text('${i.number} · ${tr(i.period == 'weekly' ? 'Weekly' : 'Monthly')}', style: const TextStyle(fontSize: 11.5, color: C.text3)),
        Text('${_d(i.from)} → ${_d(i.to)}', style: const TextStyle(fontSize: 11.5, color: C.text2)),
        const Padding(padding: EdgeInsets.symmetric(vertical: 10), child: Divider(color: C.border, height: 1)),
        if (i.lines.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 14),
            child: Text('No consumption in this period yet.', style: TextStyle(fontSize: 12.5, color: C.text3)),
          )
        else ...[
          _row4('Device', 'Hours', 'kWh', 'EGP', head: true),
          for (final l in i.lines)
            _row4(l.room.isEmpty ? l.name : '${l.name} · ${l.room}', l.hours.toStringAsFixed(1), l.kwh.toStringAsFixed(2), l.costEgp.toStringAsFixed(2)),
          const Divider(color: C.border, height: 14),
          _row4('Total', hoursTotal.toStringAsFixed(1), i.totalKwh.toStringAsFixed(2), i.totalCostEgp.toStringAsFixed(2), bold: true),
        ],
        if (i.bands.isNotEmpty) ...[
          const SizedBox(height: 14),
          const Text('Tariff breakdown', style: TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700, color: C.text2)),
          const SizedBox(height: 4),
          _row4('Tier', 'EGP/kWh', 'kWh', 'EGP', head: true),
          for (final x in i.bands) _row4('${tr('Tier')} ${x.tier}', x.price.toStringAsFixed(2), x.kwh.toStringAsFixed(2), x.costEgp.toStringAsFixed(2)),
        ],
        const SizedBox(height: 12),
        Text(
          i.period == 'weekly'
              ? '${tr('Weekly estimate uses your current tier price')}: ${i.pricePerKwh.toStringAsFixed(2)} ${tr('EGP/kWh')}'
              : tr('Monthly estimate is calculated tier by tier'),
          style: const TextStyle(fontSize: 11.5, color: C.amber, height: 1.5),
        ),
        if (single && i.lines.isNotEmpty) ...[
          const SizedBox(height: 8),
          Text('${tr('Share of your home consumption')}: ${i.lines.first.sharePercent.toStringAsFixed(0)}%', style: const TextStyle(fontSize: 11.5, color: C.text2)),
          Text('${tr('Whole home in this period')}: ${kwhText(i.homeKwh)} · ${egp(i.homeCostEgp)}', style: const TextStyle(fontSize: 11.5, color: C.text2)),
        ],
      ]),
    );
  }

  @override
  Widget build(BuildContext context) {
    final i = _inv;
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            TopNav(widget.deviceName == null ? 'Estimated Invoice' : 'Estimated Device Invoice'),
            Expanded(
              child: ListView(padding: const EdgeInsets.only(bottom: 24), children: [
                _segment(),
                const SizedBox(height: 14),
                if (_loading && i == null)
                  const Padding(padding: EdgeInsets.only(top: 60), child: Loader())
                else if (_error != null && i == null)
                  ErrorRetry(_error!, _load)
                else if (i != null) ...[
                  Opacity(opacity: _loading ? 0.5 : 1, child: _invoiceCard(i)),
                  const SizedBox(height: 14),
                  PrimaryButton('Copy invoice text', onTap: () => _copy(i), outline: true, textColor: C.text1, icon: Icons.copy_rounded),
                  const SizedBox(height: 10),
                  PrimaryButton('Email me this invoice', onTap: _busy ? null : _email, loading: _busy, icon: Icons.mail_outline_rounded),
                  const Padding(
                    padding: EdgeInsets.only(top: 12),
                    child: Text('Estimated invoice, not an official bill.', style: TextStyle(fontSize: 11, color: C.text3, height: 1.5)),
                  ),
                ],
              ]),
            ),
          ]),
        ),
      ),
    );
  }
}
