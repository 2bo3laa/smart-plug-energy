import 'dart:convert';
import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'models.dart';

class ApiException implements Exception {
  final String message;
  final int? status;
  ApiException(this.message, [this.status]);
  @override
  String toString() => message;
}

/// Singleton API client. Base URL is editable at runtime (Profile > Server).
class Api {
  Api._();
  static final Api i = Api._();

  // Chrome / desktop reach the backend on localhost; the Android emulator reaches the host
  // through 10.0.2.2. Change from Profile > Server for a real phone (use the PC's LAN IP).
  // Set at build time with --dart-define=API_URL=https://your-server/api (used by the GitHub workflow).
  static const String _envUrl = String.fromEnvironment('API_URL');
  static String get defaultBaseUrl {
    if (_envUrl.isNotEmpty) return _envUrl;
    if (kIsWeb) {
      // The web build is served by the API itself -> same origin. Local dev keeps localhost:4000.
      final u = Uri.base;
      if (u.host == 'localhost' || u.host == '127.0.0.1') return 'http://localhost:4000/api';
      return '${u.origin}/api';
    }
    return 'http://smart-plug.runasp.net/api';
  }

  String baseUrl = defaultBaseUrl;
  String? token;
  User? user;
  late SharedPreferences _prefs;

  Future<void> init() async {
    _prefs = await SharedPreferences.getInstance();
    baseUrl = _prefs.getString('baseUrl') ?? defaultBaseUrl;
    token = _prefs.getString('token');
    final u = _prefs.getString('user');
    if (u != null) user = User.fromJson(jsonDecode(u));
  }

  bool get loggedIn => token != null;

  Future<void> setBaseUrl(String url) async {
    var v = url.trim();
    while (v.endsWith('/')) {
      v = v.substring(0, v.length - 1);
    }
    if (!v.endsWith('/api')) v = '$v/api';
    baseUrl = v;
    await _prefs.setString('baseUrl', v);
  }

  Future<void> _saveSession(String t, User u) async {
    token = t;
    user = u;
    await _prefs.setString('token', t);
    await _prefs.setString('user', jsonEncode({'id': u.id, 'name': u.name, 'email': u.email}));
  }

  Future<void> logout() async {
    token = null;
    user = null;
    await _prefs.remove('token');
    await _prefs.remove('user');
  }

  Map<String, String> get _headers => {
        'Content-Type': 'application/json',
        if (token != null) 'Authorization': 'Bearer $token',
      };

  Future<dynamic> _send(String method, String path, [Object? body]) async {
    final uri = Uri.parse('$baseUrl$path');
    http.Response res;
    const t = Duration(seconds: 12);
    try {
      final enc = body == null ? null : jsonEncode(body);
      switch (method) {
        case 'GET':
          res = await http.get(uri, headers: _headers).timeout(t);
          break;
        case 'POST':
          res = await http.post(uri, headers: _headers, body: enc).timeout(t);
          break;
        case 'PUT':
          res = await http.put(uri, headers: _headers, body: enc).timeout(t);
          break;
        case 'PATCH':
          res = await http.patch(uri, headers: _headers, body: enc).timeout(t);
          break;
        default:
          res = await http.delete(uri, headers: _headers).timeout(t);
      }
    } catch (_) {
      throw ApiException('Cannot reach the server. Check your connection and server address.');
    }
    dynamic data;
    try {
      data = res.body.isEmpty ? null : jsonDecode(res.body);
    } catch (_) {
      data = null;
    }
    if (res.statusCode >= 200 && res.statusCode < 300) return data;
    if (res.statusCode == 401 && token != null && !path.startsWith('/auth/')) {
      await logout();
      throw ApiException('Session expired. Please log in again.', 401);
    }
    final msg = (data is Map && data['error'] != null) ? data['error'].toString() : 'Request failed (${res.statusCode})';
    throw ApiException(msg, res.statusCode);
  }

  // ---- auth ----
  Future<void> login(String email, String password) async {
    final d = await _send('POST', '/auth/login', {'email': email, 'password': password});
    await _saveSession(d['token'], User.fromJson(d['user']));
  }

  Future<void> signup(String name, String email, String password) async {
    final d = await _send('POST', '/auth/signup', {'name': name, 'email': email, 'password': password});
    await _saveSession(d['token'], User.fromJson(d['user']));
  }

  Future<void> changePassword(String current, String next) =>
      _send('POST', '/auth/change-password', {'currentPassword': current, 'newPassword': next});

  /// Returns devCode (dev only) if the backend includes it.
  Future<String?> forgotPassword(String email) async {
    final d = await _send('POST', '/auth/forgot-password', {'email': email});
    return d is Map ? d['devCode']?.toString() : null;
  }

  Future<void> resetPassword(String email, String code, String pass) =>
      _send('POST', '/auth/reset-password', {'email': email, 'code': code, 'newPassword': pass});

  // ---- devices ----
  Future<DeviceList> devices() async => DeviceList.fromJson(await _send('GET', '/devices'));
  Future<Device> device(int id) async => Device.fromJson((await _send('GET', '/devices/$id'))['device']);
  Future<List<ScanResult>> scan() async =>
      ((await _send('GET', '/devices/scan'))['found'] as List).map((e) => ScanResult.fromJson(e)).toList();
  Future<Device> createDevice(String name, String room,
          {double? ratedWatts, String? type, String? brand, String? model}) async =>
      Device.fromJson((await _send('POST', '/devices', {
        'name': name,
        'room': room,
        if (ratedWatts != null) 'ratedWatts': ratedWatts,
        if (type != null) 'type': type,
        if (brand != null) 'brand': brand,
        if (model != null) 'model': model,
      }))['device']);
  /// Pass ratedWatts = 0 to clear the rated power. Pass '' for type/brand/model to clear them.
  Future<Device> updateDevice(int id,
          {String? name, String? room, double? ratedWatts, String? type, String? brand, String? model}) async =>
      Device.fromJson((await _send('PATCH', '/devices/$id', {
        if (name != null) 'name': name,
        if (room != null) 'room': room,
        if (ratedWatts != null) 'ratedWatts': ratedWatts,
        if (type != null) 'type': type,
        if (brand != null) 'brand': brand,
        if (model != null) 'model': model,
      }))['device']);

  // ---- appliance catalogue ----
  Future<List<CatalogType>> catalogTypes() async =>
      ((await _send('GET', '/catalog/types'))['types'] as List).map((e) => CatalogType.fromJson(e)).toList();
  Future<List<CatalogBrand>> catalogBrands(String type) async =>
      ((await _send('GET', '/catalog/brands?type=${Uri.encodeQueryComponent(type)}'))['brands'] as List)
          .map((e) => CatalogBrand.fromJson(e)).toList();
  Future<List<CatalogModel>> catalogModels(String type, String brand) async =>
      ((await _send('GET', '/catalog/models?type=${Uri.encodeQueryComponent(type)}&brand=${Uri.encodeQueryComponent(brand)}'))['models'] as List)
          .map((e) => CatalogModel.fromJson(e)).toList();
  Future<void> deleteDevice(int id) => _send('DELETE', '/devices/$id');
  Future<Device> toggleDevice(int id) async => Device.fromJson((await _send('POST', '/devices/$id/toggle'))['device']);

  Future<Threshold?> threshold(int id) async {
    final t = (await _send('GET', '/devices/$id/threshold'))['threshold'];
    if (t == null) return null;
    return Threshold((t['limitValue'] as num).toDouble(), t['isActive'] ?? true);
  }

  Future<void> setThreshold(int id, double limit, bool active) =>
      _send('PUT', '/devices/$id/threshold', {'limitValue': limit, 'isActive': active});

  // ---- schedules ----
  Future<List<Schedule>> schedules(int deviceId) async =>
      ((await _send('GET', '/devices/$deviceId/schedules'))['schedules'] as List).map((e) => Schedule.fromJson(e)).toList();
  Future<void> createSchedule(int deviceId, String label, String time, String days, String type) =>
      _send('POST', '/devices/$deviceId/schedules', {'label': label, 'time': time, 'days': days, 'type': type});
  Future<void> updateSchedule(int id, String label, String time, String days, String type) =>
      _send('PATCH', '/schedules/$id', {'label': label, 'time': time, 'days': days, 'type': type});
  Future<void> toggleSchedule(int id) => _send('PATCH', '/schedules/$id/toggle');
  Future<void> deleteSchedule(int id) => _send('DELETE', '/schedules/$id');

  // ---- alerts ----
  Future<AlertsResult> alerts({String? severity}) async {
    final q = severity == null ? '' : '?severity=$severity';
    final d = await _send('GET', '/alerts$q');
    final items = (d['alerts'] as List).map((e) => AlertItem.fromJson(e)).toList();
    final unread = (d['unreadCount'] ?? 0) as int;
    return AlertsResult(items, unread);
  }

  Future<void> markAlertRead(int id) => _send('PATCH', '/alerts/$id/read');
  Future<void> markAllRead() => _send('PATCH', '/alerts/read-all');
  Future<void> deleteAlert(int id) => _send('DELETE', '/alerts/$id');

  // ---- tariff ----
  Future<List<TariffTier>> tariffTiers() async =>
      ((await _send('GET', '/tariff-tiers'))['tiers'] as List).map((e) => TariffTier.fromJson(e)).toList();

  // ---- stats / profile ----
  Future<Stats> statistics(String range) async => Stats.fromJson(await _send('GET', '/statistics?range=$range'));

  Future<ProfileResult> profile() async {
    final d = await _send('GET', '/profile');
    final user = User.fromJson(d['user']);
    final devices = (d['stats']['devices'] ?? 0) as int;
    final schedules = (d['stats']['activeSchedules'] ?? 0) as int;
    return ProfileResult(user, devices, schedules, d['googleEmail']?.toString());
  }

  // ---- prepaid card ----
  Future<CardInfo> card() async => CardInfo.fromJson(await _send('GET', '/card'));
  Future<CardInfo> rechargeCard(double amount) async => CardInfo.fromJson(await _send('POST', '/card/recharge', {'amount': amount}));
  Future<CardInfo> setCardBalance(double amount) async => CardInfo.fromJson(await _send('PUT', '/card', {'amount': amount}));

  // ---- Google account ----
  Future<String?> linkGoogle(String idToken) async {
    final d = await _send('POST', '/auth/google-link', {'idToken': idToken});
    return d is Map ? d['googleEmail']?.toString() : null;
  }

  Future<void> unlinkGoogle() => _send('DELETE', '/auth/google-link');

  Future<void> googleLogin(String idToken) async {
    final d = await _send('POST', '/auth/google', {'idToken': idToken});
    await _saveSession(d['token'], User.fromJson(d['user']));
  }

  Future<void> updateName(String name) async {
    final d = await _send('PATCH', '/profile', {'name': name});
    final u = User.fromJson(d['user']);
    user = u;
    await _prefs.setString('user', jsonEncode({'id': u.id, 'name': u.name, 'email': u.email}));
  }
}
