double _d(dynamic v) => v == null ? 0.0 : (v as num).toDouble();

class User {
  final int id;
  final String name, email;
  User(this.id, this.name, this.email);
  factory User.fromJson(Map<String, dynamic> j) => User(j['id'], j['name'] ?? '', j['email'] ?? '');
  String get initials {
    final p = name.trim().split(RegExp(r'\s+')).where((e) => e.isNotEmpty).toList();
    if (p.isEmpty) return '?';
    if (p.length == 1) return p[0][0].toUpperCase();
    return (p[0][0] + p[1][0]).toUpperCase();
  }
}

class Device {
  final int id;
  final String name, room;
  final bool isOnline, isOn;
  final double voltage, current, power, powerFactor, todayKwh, todayCost;
  final double? ratedWatts;
  final String? type, brand, model;
  Device({
    this.ratedWatts, this.type, this.brand, this.model,
    required this.id, required this.name, required this.room, required this.isOnline,
    required this.isOn, required this.voltage, required this.current, required this.power,
    required this.powerFactor, required this.todayKwh, required this.todayCost,
  });
  factory Device.fromJson(Map<String, dynamic> j) => Device(
        id: j['id'], name: j['name'] ?? '', room: j['room'] ?? '',
        isOnline: j['isOnline'] ?? false, isOn: j['isOn'] ?? false,
        voltage: _d(j['voltage']), current: _d(j['current']), power: _d(j['power']),
        powerFactor: _d(j['powerFactor']), todayKwh: _d(j['todayKwh']), todayCost: _d(j['todayCost']),
        ratedWatts: j['ratedWatts'] == null ? null : _d(j['ratedWatts']),
        type: j['type'], brand: j['brand'], model: j['model'],
      );
}

class DeviceList {
  final List<Device> devices;
  final int total, online, offline;
  DeviceList(this.devices, this.total, this.online, this.offline);
  factory DeviceList.fromJson(Map<String, dynamic> j) => DeviceList(
        (j['devices'] as List).map((e) => Device.fromJson(e)).toList(),
        j['total'] ?? 0, j['online'] ?? 0, j['offline'] ?? 0,
      );
}

class ScanResult {
  final String tempId, name, signal;
  ScanResult(this.tempId, this.name, this.signal);
  factory ScanResult.fromJson(Map<String, dynamic> j) =>
      ScanResult(j['tempId'] ?? '', j['name'] ?? '', j['signal'] ?? '');
}

class Schedule {
  final int id, deviceId;
  final String label, time, days, type;
  final bool isActive;
  Schedule(this.id, this.deviceId, this.label, this.time, this.days, this.type, this.isActive);
  factory Schedule.fromJson(Map<String, dynamic> j) => Schedule(
        j['id'], j['deviceId'], j['label'] ?? '', j['time'] ?? '', j['days'] ?? '',
        j['type'] ?? 'ON', j['isActive'] ?? false,
      );
}

class AlertItem {
  final int id;
  final int? deviceId;
  final String type, title, message, severity;
  final bool isRead;
  final DateTime createdAt;
  AlertItem(this.id, this.deviceId, this.type, this.title, this.message, this.severity, this.isRead, this.createdAt);
  factory AlertItem.fromJson(Map<String, dynamic> j) {
    var raw = (j['createdAt'] ?? '').toString();
    if (raw.isNotEmpty && !raw.endsWith('Z') && !raw.contains('+')) raw = '${raw}Z';
    return AlertItem(
      j['id'], j['deviceId'], j['type'] ?? '', j['title'] ?? '', j['message'] ?? '',
      j['severity'] ?? 'info', j['isRead'] ?? false,
      DateTime.tryParse(raw)?.toLocal() ?? DateTime.now(),
    );
  }
}

class Stats {
  final String range;
  final double energyToday, todayCost, changeVsYesterday, totalEnergyMonth, totalCostMonth;
  final List<double> chart;
  final List<MapEntry<String, double>> breakdown;
  final Forecast? forecast;
  Stats(this.range, this.energyToday, this.todayCost, this.changeVsYesterday, this.chart,
      this.totalEnergyMonth, this.totalCostMonth, this.breakdown, [this.forecast]);
  factory Stats.fromJson(Map<String, dynamic> j) => Stats(
        j['range'] ?? 'day', _d(j['energyToday']), _d(j['todayCost']), _d(j['changeVsYesterday']),
        ((j['chart'] ?? []) as List).map((e) => _d(e)).toList(),
        _d(j['totalEnergyMonth']), _d(j['totalCostMonth']),
        ((j['applianceBreakdown'] ?? []) as List)
            .map((e) => MapEntry<String, double>(e['label'] ?? '', _d(e['percent'])))
            .toList(),
        j['forecast'] == null ? null : Forecast.fromJson(j['forecast']),
      );
}

class Threshold {
  final double limit;
  final bool isActive;
  Threshold(this.limit, this.isActive);
}

class Forecast {
  final double projectedEnergy, projectedCost, currentPrice;
  final double? nextPrice, kwhUntilNext;
  final bool willCross;
  Forecast(this.projectedEnergy, this.projectedCost, this.currentPrice, this.nextPrice, this.kwhUntilNext, this.willCross);
  factory Forecast.fromJson(Map<String, dynamic> j) => Forecast(
        _d(j['projectedEnergyMonth']), _d(j['projectedCostMonth']), _d(j['currentPricePerKwh']),
        j['nextPricePerKwh'] == null ? null : _d(j['nextPricePerKwh']),
        j['kwhUntilNextTier'] == null ? null : _d(j['kwhUntilNextTier']),
        j['willCrossNextTier'] ?? false,
      );
}

/// Result of GET /alerts - a named class instead of a (List<AlertItem>, int) record.
class AlertsResult {
  final List<AlertItem> items;
  final int unreadCount;
  AlertsResult(this.items, this.unreadCount);
}

/// Result of GET /profile - a named class instead of a (User, int, int) record.
class ProfileResult {
  final User user;
  final int devices;
  final int activeSchedules;
  ProfileResult(this.user, this.devices, this.activeSchedules);
}

class TariffTier {
  final int id;
  final double minKwh, price;
  final double? maxKwh;
  TariffTier(this.id, this.minKwh, this.maxKwh, this.price);
  factory TariffTier.fromJson(Map<String, dynamic> j) =>
      TariffTier(j['id'], _d(j['minKwh']), j['maxKwh'] == null ? null : _d(j['maxKwh']), _d(j['pricePerKwh']));
}


class CatalogType {
  final String key, name;
  final double defaultWatts;
  final int count;
  CatalogType(this.key, this.name, this.defaultWatts, this.count);
  factory CatalogType.fromJson(Map<String, dynamic> j) =>
      CatalogType(j['key'] ?? '', j['name'] ?? '', _d(j['defaultWatts']), j['count'] ?? 0);
}

class CatalogBrand {
  final String name;
  final int count;
  CatalogBrand(this.name, this.count);
  factory CatalogBrand.fromJson(Map<String, dynamic> j) => CatalogBrand(j['name'] ?? '', j['count'] ?? 0);
}

class CatalogModel {
  final int id;
  final String model, source;
  final double ratedWatts;
  CatalogModel(this.id, this.model, this.ratedWatts, this.source);
  bool get verified => source == 'manufacturer';
  factory CatalogModel.fromJson(Map<String, dynamic> j) =>
      CatalogModel(j['id'] ?? 0, j['model'] ?? '', _d(j['ratedWatts']), j['source'] ?? 'typical');
}
