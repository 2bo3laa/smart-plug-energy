import 'package:flutter/material.dart' hide Text;
import '../core/api.dart';
import '../core/i18n.dart';
import '../core/models.dart';
import '../core/theme.dart';
import 'catalog_widgets.dart';
import 'common.dart';

/// Home-screen card: the amount loaded on the prepaid electricity card, what is left (going down as the
/// plugs consume), and roughly how much each device has used.
class CardBalance extends StatelessWidget {
  final CardInfo? info;
  final bool highlight;
  final VoidCallback onEdit;
  const CardBalance({super.key, required this.info, required this.onEdit, this.highlight = false});

  @override
  Widget build(BuildContext context) {
    final i = info;
    return Highlight(
      active: highlight,
      radius: 18,
      child: AppCard(
        margin: const EdgeInsets.only(bottom: 16),
        padding: const EdgeInsets.all(16),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Container(
              width: 34, height: 34,
              decoration: BoxDecoration(color: C.dim(C.amber), borderRadius: BorderRadius.circular(10)),
              child: const Icon(Icons.credit_card_rounded, size: 18, color: C.amber),
            ),
            const SizedBox(width: 10),
            const Expanded(child: Text('Electricity Card', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700))),
            IconBtn(i != null && i.hasCard ? Icons.add_card_rounded : Icons.edit_outlined, onEdit, size: 17),
          ]),
          const SizedBox(height: 14),
          if (i == null)
            const Padding(padding: EdgeInsets.symmetric(vertical: 18), child: Center(child: SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2, color: C.blue))))
          else if (!i.hasCard)
            _empty()
          else
            ..._content(i),
        ]),
      ),
    );
  }

  Widget _empty() => Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const Text('Enter the amount you loaded on your electricity card. It will go down as your devices use power.',
            style: TextStyle(fontSize: 12, color: C.text2, height: 1.5)),
        const SizedBox(height: 14),
        PrimaryButton('Enter card value', onTap: onEdit, icon: Icons.add_rounded),
      ]);

  List<Widget> _content(CardInfo i) {
    final color = i.empty ? C.red : (i.low ? C.amber : C.green);
    final used = i.devices.where((d) => d.cost > 0).toList();
    return [
      Text.rich(TextSpan(children: [
        TextSpan(text: i.balance.toStringAsFixed(2), style: TextStyle(fontSize: 30, fontWeight: FontWeight.w800, color: color)),
        const TextSpan(text: '  EGP', style: TextStyle(fontSize: 13, color: C.text2, fontWeight: FontWeight.w600)),
      ])),
      const SizedBox(height: 10),
      ClipRRect(
        borderRadius: BorderRadius.circular(99),
        child: LinearProgressIndicator(value: i.remainingFraction, minHeight: 7, backgroundColor: Colors.white.withOpacity(0.07), valueColor: AlwaysStoppedAnimation<Color>(color)),
      ),
      const SizedBox(height: 8),
      Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
        Text(tf('Loaded: {v}', {'v': egp(i.charged)}), style: const TextStyle(fontSize: 11, color: C.text3)),
        Text(tf('Used: {v}', {'v': egp(i.spent)}), style: const TextStyle(fontSize: 11, color: C.text3)),
      ]),
      if (i.low || i.empty)
        Container(
          margin: const EdgeInsets.only(top: 12),
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(color: C.dim(color), borderRadius: BorderRadius.circular(10)),
          child: Row(children: [
            Icon(Icons.warning_amber_rounded, size: 16, color: color),
            const SizedBox(width: 8),
            Expanded(child: Text(i.empty ? 'Your card balance has run out. Recharge it soon.' : 'Your card balance is running low.', style: TextStyle(fontSize: 11.5, color: color))),
          ]),
        ),
      const SizedBox(height: 16),
      const Text('Where it went (approx.)', style: TextStyle(fontSize: 11.5, fontWeight: FontWeight.w700, color: C.text2)),
      const SizedBox(height: 8),
      if (used.isEmpty)
        const Padding(padding: EdgeInsets.symmetric(vertical: 6), child: Text('No consumption since you loaded the card yet.', style: TextStyle(fontSize: 11.5, color: C.text3)))
      else
        for (final d in used) _deviceRow(d),
    ];
  }

  Widget _deviceRow(CardDevice d) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(children: [
          TypeBadge(d.type, size: 30),
          const SizedBox(width: 10),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Expanded(child: Text(d.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w600))),
                Text('≈ ${egp(d.cost)}', style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w800)),
              ]),
              const SizedBox(height: 4),
              Row(children: [
                Expanded(
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(99),
                    child: LinearProgressIndicator(value: (d.percent / 100).clamp(0.0, 1.0), minHeight: 4, backgroundColor: Colors.white.withOpacity(0.07), valueColor: AlwaysStoppedAnimation<Color>(typeColor(d.type) == C.text2 ? C.blue : typeColor(d.type))),
                  ),
                ),
                const SizedBox(width: 8),
                Text('${kwhText(d.kwh)} · ${d.percent.toStringAsFixed(0)}%', style: const TextStyle(fontSize: 10.5, color: C.text3)),
              ]),
            ]),
          ),
        ]),
      );
}

/// Bottom sheet to add a recharge to the card, or to set the balance to an exact value.
Future<CardInfo?> showCardSheet(BuildContext context, {required bool hasCard}) => showModalBottomSheet<CardInfo>(
      context: context,
      isScrollControlled: true,
      backgroundColor: C.card,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
      builder: (_) => _CardSheet(hasCard: hasCard),
    );

class _CardSheet extends StatefulWidget {
  final bool hasCard;
  const _CardSheet({required this.hasCard});
  @override
  State<_CardSheet> createState() => _CardSheetState();
}

class _CardSheetState extends State<_CardSheet> {
  final _amount = TextEditingController();
  bool _set = false; // false = add a recharge, true = set the balance
  bool _saving = false;

  @override
  void dispose() {
    _amount.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final v = parseNum(_amount.text);
    if (v == null || v < 0 || (!_set && v <= 0)) {
      toast(context, 'Enter a valid amount', error: true);
      return;
    }
    setState(() => _saving = true);
    try {
      final r = (_set && widget.hasCard) ? await Api.i.setCardBalance(v) : await Api.i.rechargeCard(v);
      if (mounted) Navigator.pop(context, r);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.fromLTRB(22, 20, 22, 22 + MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(widget.hasCard ? 'Electricity Card' : 'Enter card value', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
          const SizedBox(height: 6),
          Text(
            widget.hasCard
                ? 'Add the amount you just loaded, or correct the balance to match your meter.'
                : 'How much is loaded on your electricity card right now?',
            style: const TextStyle(fontSize: 12, color: C.text2, height: 1.5),
          ),
          const SizedBox(height: 16),
          if (widget.hasCard) ...[
            Row(children: [
              AppChip('Add recharge', active: !_set, onTap: () => setState(() => _set = false)),
              const SizedBox(width: 8),
              AppChip('Set balance', active: _set, onTap: () => setState(() => _set = true)),
            ]),
            const SizedBox(height: 16),
          ],
          Field(
            label: (_set && widget.hasCard) ? 'New balance (EGP)' : 'Amount (EGP)',
            hint: 'e.g. 200',
            controller: _amount,
            icon: Icons.payments_outlined,
            keyboard: const TextInputType.numberWithOptions(decimal: true),
          ),
          const SizedBox(height: 4),
          PrimaryButton('Save', onTap: _save, loading: _saving),
        ]),
      ),
    );
  }
}
