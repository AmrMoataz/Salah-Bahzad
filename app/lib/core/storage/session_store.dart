import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'session.dart';

/// Persists the [Session] in the **OS keystore** (`flutter_secure_storage`) —
/// never `SharedPreferences`, never a file (`NFR-APP-SEC-001/004`). Keeps a
/// single in-memory copy so the request path never awaits the keystore.
class SessionStore {
  SessionStore(this._storage);

  static const String _key = 'sb_session';
  static const String _studentKey = 'sb_student';

  final FlutterSecureStorage _storage;
  Session? _cached;

  /// When `false` ("Keep me signed in" unchecked), the session lives only in
  /// memory for this run — never written to the keystore — so the next launch
  /// starts signed-out. Set at sign-in and honoured by subsequent refreshes.
  bool _persist = true;

  /// The last-loaded session, available synchronously to the request path.
  Session? get current => _cached;

  /// Loads the session from the keystore into the in-memory cache.
  Future<Session?> load() async {
    final String? raw = await _storage.read(key: _key);
    _cached = Session.tryParse(raw);
    return _cached;
  }

  /// Saves a session. [persist] sets the keep-signed-in preference (defaults to
  /// the current preference, so a silent refresh inherits the sign-in choice).
  Future<void> save(Session session, {bool? persist}) async {
    if (persist != null) _persist = persist;
    _cached = session;
    if (_persist) {
      await _storage.write(key: _key, value: session.toJson());
    }
  }

  Future<void> clear() async {
    _cached = null;
    _persist = true;
    await _storage.delete(key: _key);
    await _storage.delete(key: _studentKey);
  }

  /// Persists an opaque student-summary blob alongside the session, so a cold
  /// start with a valid session can render the Idle greeting without a network
  /// round-trip. The store stays DTO-agnostic — it only sees a string. Skipped
  /// for memory-only sessions ("Keep me signed in" unchecked).
  Future<void> saveStudent(String json) async {
    if (!_persist) return;
    await _storage.write(key: _studentKey, value: json);
  }

  Future<String?> loadStudent() => _storage.read(key: _studentKey);
}
