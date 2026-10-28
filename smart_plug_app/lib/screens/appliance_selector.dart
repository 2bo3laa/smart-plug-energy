import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/catalog_widgets.dart';
import '../widgets/common.dart';

/// What the user picked. [watts]/[source] are only set when a catalogue model was chosen.
class ApplianceChoice {
  final String? type, brand, model;
  final double? watts;
  final String? source; // typical | manufacturer
  const ApplianceChoice({this.type, this.brand, this.model, this.watts, this.source});
}

class _Opt {
  final Widget leading;
  final String title;
  final String? subtitle, trailing;
  final Object value;
  const _Opt(this.leading, this.title, this.value, {this.subtitle, this.trailing});
}

class _PickerSheet extends StatefulWidget {
  final String title;
  final List<_Opt> options;
  final bool searchable;
  const _PickerSheet(this.title, this.options, {this.searchable = true});
  @override
  State<_PickerSheet> createState() => _PickerSheetState();
}

class _PickerSheetState extends State<_PickerSheet> {
  String _q = '';
  @override
  Widget build(BuildContext context) {
    final list = widget.options.where((o) => o.title.toLowerCase().contains(_q.toLowerCase())).toList();
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: SizedBox(
          height: MediaQuery.of(context).size.height * 0.72,
          child: Column(children: [
            const SizedBox(height: 10),
            Container(width: 38, height: 4, decoration: BoxDecoration(color: C.borderStrong, borderRadius: BorderRadius.circular(9))),
            Padding(
              padding: const EdgeInsets.fromLTRB(22, 14, 22, 8),
              child: Align(alignment: Alignment.centerLeft, child: Text(widget.title, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800))),
            ),
            if (widget.searchable)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 22),
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  decoration: BoxDecoration(color: C.input, borderRadius: BorderRadius.circular(12), border: Border.all(color: C.border)),
                  child: Row(children: [
                    const Icon(Icons.search_rounded, size: 18, color: C.text3),
                    const SizedBox(width: 8),
                    Expanded(
                      child: TextField(
                        onChanged: (v) => setState(() => _q = v),
                        style: const TextStyle(fontSize: 13.5, color: C.text1),
                        decoration: const InputDecoration(border: InputBorder.none, hintText: 'Search', hintStyle: TextStyle(color: C.text3), contentPadding: EdgeInsets.symmetric(vertical: 12)),
                      ),
                    ),
                  ]),
                ),
              ),
            const SizedBox(height: 8),
            Expanded(
              child: ListView.builder(
                padding: const EdgeInsets.fromLTRB(22, 0, 22, 14),
                itemCount: list.length,
                itemBuilder: (_, i) {
                  final o = list[i];
                  return InkWell(
                    borderRadius: BorderRadius.circular(12),
                    onTap: () => Navigator.pop(context, o.value),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 9),
                      child: Row(children: [
                        o.leading,
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                            Text(o.title, style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
                            if (o.subtitle != null) Padding(padding: const EdgeInsets.only(top: 2), child: Text(o.subtitle!, style: const TextStyle(fontSize: 11.5, color: C.text3))),
                          ]),
                        ),
                        if (o.trailing != null) Text(o.trailing!, style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700, color: C.text2)),
                      ]),
                    ),
                  );
                },
              ),
            ),
          ]),
        ),
      ),
    );
  }
}

/// Type grid -> brand -> model. Calls [onChanged] on every change; the parent decides what to do with watts/name.
class ApplianceSelector extends StatefulWidget {
  final ApplianceChoice initial;
  final ValueChanged<ApplianceChoice> onChanged;
  const ApplianceSelector({super.key, required this.initial, required this.onChanged});
  @override
  State<ApplianceSelector> createState() => _ApplianceSelectorState();
}

class _ApplianceSelectorState extends State<ApplianceSelector> {
  List<CatalogType>? _types;
  String? _error;
  late String? _type = widget.initial.type;
  late String? _brand = widget.initial.brand;
  late String? _model = widget.initial.model;

  @override
  void initState() {
    super.initState();
    _loadTypes();
  }

  Future<void> _loadTypes() async {
    try {
      final t = await Api.i.catalogTypes();
      if (mounted) setState(() { _types = t; _error = null; });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  void _emit({double? watts, String? source}) =>
      widget.onChanged(ApplianceChoice(type: _type, brand: _brand, model: _model, watts: watts, source: source));

  void _pickType(CatalogType t) {
    if (t.key == _type) return;
    setState(() { _type = t.key; _brand = null; _model = null; });
    // Type default is only a fallback; no watts are pushed until a model is chosen.
    _emit();
  }

  Future<void> _pickBrand() async {
    if (_type == null) return;
    List<CatalogBrand> brands;
    try {
      brands = await Api.i.catalogBrands(_type!);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
      return;
    }
    if (!mounted) return;
    final res = await showModalBottomSheet<Object>(
      context: context, isScrollControlled: true, backgroundColor: C.card,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(22))),
      builder: (_) => _PickerSheet('Select brand', [
        const _Opt(Icon(Icons.edit_outlined, color: C.text2), 'My brand is not listed…', '__other__'),
        for (final b in brands) _Opt(BrandLogo(b.name, size: 38), b.name, b.name, subtitle: '${b.count} model${b.count == 1 ? '' : 's'}'),
      ]),
    );
    if (res == null || !mounted) return;
    if (res == '__other__') {
      final c = TextEditingController();
      final v = await showDialog<String>(
        context: context,
        builder: (ctx) => AlertDialog(
          backgroundColor: C.card,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
          title: const Text('Brand name', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
          content: Field(label: 'Brand', hint: 'e.g. Zanussi', controller: c),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel', style: TextStyle(color: C.text2))),
            TextButton(onPressed: () => Navigator.pop(ctx, c.text.trim()), child: const Text('OK')),
          ],
        ),
      );
      if (v == null || v.isEmpty) return;
      setState(() { _brand = v; _model = null; });
      _emit();
      return;
    }
    setState(() { _brand = res as String; _model = null; });
    _emit();
    _pickModel();
  }

  Future<void> _pickModel() async {
    if (_type == null || _brand == null) return;
    List<CatalogModel> models;
    try {
      models = await Api.i.catalogModels(_type!, _brand!);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
      return;
    }
    if (!mounted) return;
    final res = await showModalBottomSheet<Object>(
      context: context, isScrollControlled: true, backgroundColor: C.card,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(22))),
      builder: (_) => _PickerSheet('Select model / size', [
        const _Opt(Icon(Icons.edit_outlined, color: C.text2), 'Not listed — I will enter the watts', '__manual__'),
        for (final m in models)
          _Opt(
            Icon(m.verified ? Icons.verified_rounded : Icons.help_outline_rounded, size: 22, color: m.verified ? C.green : C.text3),
            m.model, m,
            subtitle: m.verified ? 'From manufacturer data' : 'Typical value — check your label',
            trailing: '${m.ratedWatts.toStringAsFixed(0)} W',
          ),
      ], searchable: models.length > 8),
    );
    if (res == null || !mounted) return;
    if (res == '__manual__') {
      setState(() => _model = null);
      _emit();
      return;
    }
    final m = res as CatalogModel;
    setState(() => _model = m.model);
    _emit(watts: m.ratedWatts, source: m.source);
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) {
      return Padding(
        padding: const EdgeInsets.only(bottom: 14),
        child: Row(children: [
          Expanded(child: Text('Could not load appliance list ($_error)', style: const TextStyle(fontSize: 12, color: C.red))),
          TextButton(onPressed: _loadTypes, child: const Text('Retry')),
        ]),
      );
    }
    if (_types == null) {
      return const Padding(padding: EdgeInsets.symmetric(vertical: 20), child: Loader());
    }
    final isOther = _type == null || _type == 'other';
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      const Text('Appliance Type', style: TextStyle(fontSize: 12, color: C.text2, fontWeight: FontWeight.w600)),
      const SizedBox(height: 10),
      Wrap(spacing: 8, runSpacing: 8, children: [
        for (final t in _types!)
          GestureDetector(
            onTap: () => _pickType(t),
            child: Container(
              width: 78,
              padding: const EdgeInsets.symmetric(vertical: 10),
              decoration: BoxDecoration(
                color: t.key == _type ? C.dim(typeColor(t.key)) : C.card,
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: t.key == _type ? typeColor(t.key) : C.border, width: t.key == _type ? 1.5 : 1),
              ),
              child: Column(children: [
                Icon(typeIcon(t.key), size: 24, color: t.key == _type ? typeColor(t.key) : C.text2),
                const SizedBox(height: 5),
                Text(t.name, textAlign: TextAlign.center, maxLines: 2, overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 10.5, fontWeight: FontWeight.w600, color: t.key == _type ? C.text1 : C.text2)),
              ]),
            ),
          ),
      ]),
      if (!isOther) ...[
        const SizedBox(height: 16),
        _tile(
          'Brand',
          _brand == null ? 'Select brand' : _brand!,
          leading: _brand == null ? const Icon(Icons.storefront_outlined, size: 20, color: C.text3) : BrandLogo(_brand!, size: 34),
          onTap: _pickBrand,
        ),
        if (_brand != null)
          _tile(
            'Model / Size',
            _model ?? 'Select model / size',
            leading: const Icon(Icons.tune_rounded, size: 20, color: C.text3),
            onTap: _pickModel,
          ),
      ],
      const SizedBox(height: 6),
    ]);
  }

  Widget _tile(String label, String value, {required Widget leading, required VoidCallback onTap}) => Padding(
        padding: const EdgeInsets.only(bottom: 10),
        child: GestureDetector(
          onTap: onTap,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(color: C.input, borderRadius: BorderRadius.circular(14), border: Border.all(color: C.border)),
            child: Row(children: [
              SizedBox(width: 34, height: 34, child: Center(child: leading)),
              const SizedBox(width: 12),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(label, style: const TextStyle(fontSize: 11, color: C.text3)),
                  const SizedBox(height: 2),
                  Text(value, style: const TextStyle(fontSize: 13.5, fontWeight: FontWeight.w700)),
                ]),
              ),
              const Icon(Icons.keyboard_arrow_down_rounded, color: C.text3),
            ]),
          ),
        ),
      );
}
