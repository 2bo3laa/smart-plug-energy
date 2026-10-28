import 'package:flutter/material.dart';
import '../core/api.dart';
import '../core/models.dart';
import '../core/theme.dart';
import '../widgets/common.dart';

class ScheduleScreen extends StatefulWidget {
  final Device device;
  const ScheduleScreen({super.key, required this.device});
  @override
  State<ScheduleScreen> createState() => _ScheduleScreenState();
}

class _ScheduleScreenState extends State<ScheduleScreen> {
  List<Schedule>? _items;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final s = await Api.i.schedules(widget.device.id);
      if (mounted) setState(() { _items = s; _error = null; });
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  Future<void> _toggle(Schedule s) async {
    try {
      await Api.i.toggleSchedule(s.id);
      _load();
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
    }
  }

  Future<void> _delete(Schedule s) async {
    try {
      await Api.i.deleteSchedule(s.id);
      _load();
    } on ApiException catch (e) {
      if (mounted) toast(context, e.message, error: true);
      _load();
    }
  }

  Future<void> _openEditor([Schedule? s]) async {
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: C.card,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
      builder: (_) => _ScheduleEditor(deviceId: widget.device.id, existing: s),
    );
    if (saved == true) _load();
  }

  @override
  Widget build(BuildContext context) {
    final items = _items;
    final active = items?.where((e) => e.isActive).toList() ?? [];
    final inactive = items?.where((e) => !e.isActive).toList() ?? [];
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(22, 14, 22, 0),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            TopNav('Schedule', trailing: IconBtn(Icons.add_rounded, () => _openEditor(), size: 20)),
            Expanded(
              child: _error != null && items == null
                  ? ErrorRetry(_error!, () { setState(() => _error = null); _load(); })
                  : items == null
                      ? const Loader()
                      : items.isEmpty
                          ? const Center(child: Text('No schedules yet. Tap + to create one.', style: TextStyle(color: C.text3, fontSize: 13)))
                          : ListView(padding: const EdgeInsets.only(bottom: 16), children: [
                              if (active.isNotEmpty) ...[
                                const Padding(padding: EdgeInsets.only(bottom: 10), child: Text('Active Schedules', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: C.green))),
                                for (final s in active) _row(s),
                              ],
                              if (inactive.isNotEmpty) ...[
                                const Padding(padding: EdgeInsets.only(top: 16, bottom: 10), child: Text('Inactive Schedules', style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: C.text2))),
                                for (final s in inactive) _row(s),
                              ],
                              const Padding(
                                padding: EdgeInsets.only(top: 14),
                                child: Text('Tip: swipe a schedule left to delete it, tap to edit.', style: TextStyle(fontSize: 11, color: C.text3)),
                              ),
                            ]),
            ),
          ]),
        ),
      ),
    );
  }

  Widget _row(Schedule s) {
    final on = s.type.toUpperCase() == 'ON';
    final color = !s.isActive ? C.text2 : (on ? C.green : C.purple);
    return Dismissible(
      key: ValueKey('sch${s.id}'),
      direction: DismissDirection.endToStart,
      background: Container(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.only(right: 20),
        alignment: Alignment.centerRight,
        decoration: BoxDecoration(color: C.dim(C.red), borderRadius: BorderRadius.circular(14)),
        child: const Icon(Icons.delete_outline_rounded, color: C.red),
      ),
      onDismissed: (_) => _delete(s),
      child: AppCard(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        radius: 14,
        onTap: () => _openEditor(s),
        child: Row(children: [
          Container(
            width: 34, height: 34,
            decoration: BoxDecoration(color: s.isActive ? C.dim(color) : C.cardAlt, borderRadius: BorderRadius.circular(10)),
            child: Icon(on ? Icons.wb_sunny_outlined : Icons.nightlight_outlined, size: 16, color: color),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('${s.label} · ${s.time}', style: const TextStyle(fontSize: 12.5, fontWeight: FontWeight.w700)),
              const SizedBox(height: 2),
              Text('${s.type.toUpperCase()} · ${s.days}', style: const TextStyle(fontSize: 10.5, color: C.text3)),
            ]),
          ),
          AppToggle(s.isActive, onChanged: (_) => _toggle(s)),
        ]),
      ),
    );
  }
}

class _ScheduleEditor extends StatefulWidget {
  final int deviceId;
  final Schedule? existing;
  const _ScheduleEditor({required this.deviceId, this.existing});
  @override
  State<_ScheduleEditor> createState() => _ScheduleEditorState();
}

class _ScheduleEditorState extends State<_ScheduleEditor> {
  late final TextEditingController _label = TextEditingController(text: widget.existing?.label ?? '');
  late String _time = widget.existing?.time ?? '07:00';
  late String _days = widget.existing?.days ?? 'Everyday';
  late String _type = (widget.existing?.type ?? 'ON').toUpperCase();
  bool _saving = false;

  Future<void> _pickTime() async {
    final parts = _time.split(':');
    final init = TimeOfDay(hour: int.tryParse(parts[0]) ?? 7, minute: int.tryParse(parts.length > 1 ? parts[1] : '0') ?? 0);
    final t = await showTimePicker(context: context, initialTime: init);
    if (t != null) {
      setState(() => _time = '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}');
    }
  }

  Future<void> _save() async {
    final label = _label.text.trim();
    if (label.isEmpty) {
      toast(context, 'Give the schedule a name', error: true);
      return;
    }
    setState(() => _saving = true);
    try {
      if (widget.existing == null) {
        await Api.i.createSchedule(widget.deviceId, label, _time, _days, _type);
      } else {
        await Api.i.updateSchedule(widget.existing!.id, label, _time, _days, _type);
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
    return Padding(
      padding: EdgeInsets.fromLTRB(22, 20, 22, 22 + MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(widget.existing == null ? 'New Schedule' : 'Edit Schedule', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
          const SizedBox(height: 16),
          Field(label: 'Name', hint: 'e.g. Morning ON', controller: _label),
          const Text('Action', style: TextStyle(fontSize: 12, color: C.text2, fontWeight: FontWeight.w600)),
          const SizedBox(height: 8),
          Row(children: [
            AppChip('Turn ON', active: _type == 'ON', onTap: () => setState(() => _type = 'ON')),
            const SizedBox(width: 8),
            AppChip('Turn OFF', active: _type == 'OFF', onTap: () => setState(() => _type = 'OFF')),
          ]),
          const SizedBox(height: 16),
          const Text('Time', style: TextStyle(fontSize: 12, color: C.text2, fontWeight: FontWeight.w600)),
          const SizedBox(height: 8),
          GestureDetector(
            onTap: _pickTime,
            child: Container(
              width: double.infinity,
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(color: C.input, borderRadius: BorderRadius.circular(14), border: Border.all(color: C.border)),
              child: Row(children: [
                const Icon(Icons.schedule_rounded, size: 17, color: C.text3),
                const SizedBox(width: 10),
                Text(_time, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
              ]),
            ),
          ),
          const SizedBox(height: 16),
          const Text('Repeat', style: TextStyle(fontSize: 12, color: C.text2, fontWeight: FontWeight.w600)),
          const SizedBox(height: 8),
          Wrap(spacing: 8, runSpacing: 8, children: [
            for (final d in const ['Everyday', 'Weekdays', 'Weekends'])
              AppChip(d, active: _days == d, onTap: () => setState(() => _days = d)),
          ]),
          const SizedBox(height: 22),
          PrimaryButton('Save', onTap: _save, loading: _saving),
        ]),
      ),
    );
  }
}
