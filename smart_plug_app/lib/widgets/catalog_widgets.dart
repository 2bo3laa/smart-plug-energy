import 'package:flutter/material.dart';
import '../core/theme.dart';

/// Material icon used for each appliance type key (keys come from the API).
const Map<String, IconData> _typeIcons = {
  'refrigerator': Icons.kitchen_rounded,
  'freezer': Icons.kitchen_outlined,
  'washing_machine': Icons.local_laundry_service_rounded,
  'air_conditioner': Icons.ac_unit_rounded,
  'tv': Icons.tv_rounded,
  'blender': Icons.blender_rounded,
  'microwave': Icons.microwave_rounded,
  'water_heater': Icons.hot_tub_rounded,
  'dishwasher': Icons.local_dining_rounded,
  'fan': Icons.air_rounded,
  'iron': Icons.iron_rounded,
  'vacuum': Icons.cleaning_services_rounded,
  'kettle': Icons.coffee_rounded,
  'toaster': Icons.bakery_dining_rounded,
  'computer': Icons.computer_rounded,
  'other': Icons.power_outlined,
};

const Map<String, Color> _typeColors = {
  'refrigerator': Color(0xFF38BDF8),
  'freezer': Color(0xFF60A5FA),
  'washing_machine': Color(0xFF818CF8),
  'air_conditioner': Color(0xFF22D3EE),
  'tv': Color(0xFFA78BFA),
  'blender': Color(0xFFF472B6),
  'microwave': Color(0xFFFB923C),
  'water_heater': Color(0xFFF87171),
  'dishwasher': Color(0xFF2DD4BF),
  'fan': Color(0xFF4ADE80),
  'iron': Color(0xFFFBBF24),
  'vacuum': Color(0xFF94A3B8),
  'kettle': Color(0xFFF59E0B),
  'toaster': Color(0xFFD97706),
  'computer': Color(0xFF3B82F6),
};

IconData typeIcon(String? key) => _typeIcons[key] ?? Icons.power_outlined;
Color typeColor(String? key) => _typeColors[key] ?? C.text2;

/// "Black+Decker" -> "blackdecker". The brand logo file must be assets/brands/<slug>.png
String brandSlug(String brand) => brand.toLowerCase().replaceAll(RegExp(r'[^a-z0-9]'), '');

/// Rounded tile with the appliance type icon.
class TypeBadge extends StatelessWidget {
  final String? type;
  final double size;
  final bool selected;
  const TypeBadge(this.type, {super.key, this.size = 40, this.selected = false});
  @override
  Widget build(BuildContext context) {
    final c = typeColor(type);
    return Container(
      width: size, height: size,
      decoration: BoxDecoration(
        color: C.dim(c),
        borderRadius: BorderRadius.circular(size * 0.28),
        border: Border.all(color: selected ? c : Colors.transparent, width: 1.5),
      ),
      child: Icon(typeIcon(type), size: size * 0.5, color: c),
    );
  }
}

/// Brand logo from assets/brands/<slug>.png; falls back to coloured initials if the file is missing.
class BrandLogo extends StatelessWidget {
  final String brand;
  final double size;
  const BrandLogo(this.brand, {super.key, this.size = 36});

  Color get _color {
    const palette = [Color(0xFF2563EB), Color(0xFF7C3AED), Color(0xFFDB2777), Color(0xFFEA580C), Color(0xFF059669), Color(0xFF0891B2), Color(0xFFCA8A04)];
    var h = 0;
    for (final u in brand.codeUnits) {
      h = (h * 31 + u) & 0x7fffffff;
    }
    return palette[h % palette.length];
  }

  @override
  Widget build(BuildContext context) {
    final initials = brand.trim().isEmpty ? '?' : brand.trim().substring(0, brand.trim().length >= 2 ? 2 : 1);
    return Container(
      width: size, height: size,
      padding: EdgeInsets.all(size * 0.12),
      decoration: BoxDecoration(color: const Color(0xFFF3F5F9), borderRadius: BorderRadius.circular(size * 0.28)),
      child: Image.asset(
        'assets/brands/${brandSlug(brand)}.png',
        fit: BoxFit.contain,
        errorBuilder: (_, __, ___) => Center(
          child: Text(initials, style: TextStyle(fontSize: size * 0.36, fontWeight: FontWeight.w800, color: _color)),
        ),
      ),
    );
  }
}
