import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/catalog_widgets.dart';
import '../widgets/common.dart';
import 'appliance_selector.dart';

/// Add a new device, or (when [editing] is given) edit an existing one.
class DeviceSetupScreen extends StatefulWidget {
  final String? suggestedName;
  final Device? editing;
  const DeviceSetupScreen({super.key, this.suggestedName, this.editing});
  @override
  State<DeviceSetupScreen> createState() => _DeviceSetupScreenState();
}

class _DeviceSetupScreenState extends State<DeviceSetupScreen> {
  late final TextEditingController _name = TextEditingController(text: widget.editing?.name ?? widget.suggestedName ?? '');
  late final TextEditingController _watts = TextEditingController(
      text: widget.editing?.ratedWatts == null ? '' : widget.editing!.ratedWatts!.toStringAsFixed(0));
  final List<String> _rooms = ['Living Room', 'Bedroom', 'Kitchen', 'Office'];
  late String _room = widget.editing?.room ?? 'Living Room';
  bool _saving = false;

  // appliance selection
  late String? _type = widget.editing?.type;
  late String? _brand = widget.editing?.brand;
  late String? _model = widget.editing?.model;
  String? _autoName;            // last name we filled in automatically
  String? _wattsNote;           // hint shown under the watts field
  Color _wattsNoteColor = C.text3;

  bool get _edit => widget.editing != null;

  @override
  void initState() {
    super.initState();
    if (!_rooms.contains(_room)) _rooms.add(_room);
  }

  void _onAppliance(ApplianceChoice c) {
    setState(() {
      _type = c.type;
      _brand = c.brand;
      _model = c.model;
      // suggest a name unless the user typed their own
      if (_name.text.trim().isEmpty || _name.text == _autoName) {
        final parts = [if (c.brand != null) c.brand!, if (c.type != null && c.type != 'other') _typeLabel(c.type!)];
        _autoName = parts.join(' ');
        _name.text = _autoName!;
      }
      if (c.watts != null) {
        _watts.text = c.watts!.toStringAsFixed(0);
        if (c.source == 'manufacturer') {
          _wattsNote = 'From manufacturer data. You can still edit it.';
          _wattsNoteColor = C.green;
        } else {
          _wattsNote = 'Typical value for this size — please check the label on your appliance and edit if it differs.';
          _wattsNoteColor = C.amber;
        }
      }
    });
  }

  String _typeLabel(String key) => key
      .split('_')
      .map((w) => w.isEmpty ? w : w[0].toUpperCase() + w.substring(1))
      .join(' ')
      .replaceAll('Tv', 'TV');

  Future<void> _addRoom() async {
    final c = TextEditingController();
    final v = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: C.card,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
        title: const Text('New Room', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
        content: Field(label: 'Room name', hint: 'e.g. Garage', controller: c),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel', style: TextStyle(color: C.text2))),
          TextButton(onPressed: () => Navigator.pop(ctx, c.text.trim()), child: const Text('Add')),
        ],
      ),
    );
    if (v != null && v.isNotEmpty) {
      setState(() {
        if (!_rooms.contains(v)) _rooms.add(v);
        _room = v;
      });
    }
  }

  Future<void> _finish() async {
    final name = _name.text.trim();
    if (name.isEmpty) {
      toast(context, 'Enter a device name', error: true);
      return;
    }
    final txt = _watts.text.trim();
    final w = double.tryParse(txt);
    if (txt.isNotEmpty && (w == null || w <= 0)) {
      toast(context, 'Rated power must be a positive number', error: true);
      return;
    }
    setState(() => _saving = true);
    try {
      if (_edit) {
        final old = widget.editing!;
        double? sendW;
        if (txt.isEmpty) {
          sendW = old.ratedWatts != null ? 0 : null; // empty clears an existing value
        } else {
          sendW = w;
        }
        await Api.i.updateDevice(old.id,
            name: name, room: _room, ratedWatts: sendW,
            type: _type ?? '', brand: _brand ?? '', model: _model ?? '');
      } else {
        await Api.i.createDevice(name, _room,
            ratedWatts: (w != null && w > 0) ? w : null, type: _type, brand: _brand, model: _model);
      }
      if (mounted) Navigator.pop(context, true);
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            TopNav(_edit ? 'Edit Device' : 'Set Up Device'),
            Expanded(
              child: SingleChildScrollView(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Center(
                    child: Stack(clipBehavior: Clip.none, children: [
                      Container(
                        width: 88, height: 88,
                        margin: const EdgeInsets.only(bottom: 22),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(24),
                          gradient: const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [Color(0xFF1B2434), Color(0xFF0E141F)]),
                          border: Border.all(color: C.borderStrong),
                          boxShadow: [BoxShadow(color: C.blue.withOpacity(0.05), spreadRadius: 10)],
                        ),
                        child: Icon(typeIcon(_type), size: 40, color: _type == null ? C.blue : typeColor(_type)),
                      ),
                      if (_brand != null) Positioned(right: -12, bottom: 14, child: BrandLogo(_brand!, size: 32)),
                    ]),
                  ),
                  ApplianceSelector(
                    initial: ApplianceChoice(type: _type, brand: _brand, model: _model),
                    onChanged: _onAppliance,
                  ),
                  const SizedBox(height: 8),
                  Field(label: 'Device Name', hint: 'e.g. Living Room Fridge', controller: _name),
                  Field(
                    label: 'Rated Power (Watts) - optional',
                    hint: 'e.g. 1500 (printed on the appliance label)',
                    controller: _watts,
                    keyboard: TextInputType.number,
                    onChanged: (_) => setState(() {
                      _wattsNote = 'Edited manually.';
                      _wattsNoteColor = C.text3;
                    }),
                  ),
                  if (_wattsNote != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 14),
                      child: Text(_wattsNote!, style: TextStyle(fontSize: 11.5, color: _wattsNoteColor)),
                    ),
                  const Text('Assign Room', style: TextStyle(fontSize: 12, color: C.text2, fontWeight: FontWeight.w600)),
                  const SizedBox(height: 10),
                  Wrap(spacing: 8, runSpacing: 8, children: [
                    for (final r in _rooms) AppChip(r, active: r == _room, onTap: () => setState(() => _room = r)),
                    AppChip('+ Add Room', dashed: true, onTap: _addRoom),
                  ]),
                  const SizedBox(height: 10),
                ]),
              ),
            ),
            PrimaryButton(_edit ? 'Save Changes' : 'Finish Setup', onTap: _finish, loading: _saving),
          ]),
        ),
      ),
    );
  }
}
