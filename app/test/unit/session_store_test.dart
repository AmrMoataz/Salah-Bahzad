import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:secure_player/core/storage/session.dart';
import 'package:secure_player/core/storage/session_store.dart';

class _MockSecureStorage extends Mock implements FlutterSecureStorage {}

void main() {
  late _MockSecureStorage storage;
  late SessionStore store;
  late Map<String, String> backing;

  Session sample() => Session(
        accessToken: 'access-1',
        refreshToken: 'refresh-1',
        accessTokenExpiresAt: DateTime.utc(2030, 1, 1, 0, 15),
        refreshTokenExpiresAt: DateTime.utc(2030, 1, 8),
      );

  setUp(() {
    storage = _MockSecureStorage();
    store = SessionStore(storage);
    backing = <String, String>{};

    when(() => storage.write(
          key: any(named: 'key'),
          value: any(named: 'value'),
        )).thenAnswer((Invocation i) async {
      backing[i.namedArguments[#key] as String] =
          i.namedArguments[#value] as String;
    });
    when(() => storage.read(key: any(named: 'key')))
        .thenAnswer((Invocation i) async => backing[i.namedArguments[#key]]);
    when(() => storage.delete(key: any(named: 'key')))
        .thenAnswer((Invocation i) async {
      backing.remove(i.namedArguments[#key]);
    });
  });

  test('round-trips a session through the keystore', () async {
    final Session original = sample();
    await store.save(original);
    expect(store.current, isNotNull, reason: 'cached after save');

    // A fresh store reads it back from the (mocked) keystore.
    final SessionStore reread = SessionStore(storage);
    final Session? loaded = await reread.load();
    expect(loaded, isNotNull);
    expect(loaded!.accessToken, original.accessToken);
    expect(loaded.refreshToken, original.refreshToken);
    expect(loaded.accessTokenExpiresAt, original.accessTokenExpiresAt);
  });

  test('clear() removes the session and student blob', () async {
    await store.save(sample());
    await store.saveStudent('{"id":"x"}');
    await store.clear();

    expect(store.current, isNull);
    final SessionStore reread = SessionStore(storage);
    expect(await reread.load(), isNull);
    expect(await reread.loadStudent(), isNull);
  });

  test('memory-only sessions are never written to disk', () async {
    await store.save(sample(), persist: false);
    expect(store.current, isNotNull, reason: 'available in memory this run');
    expect(backing, isEmpty, reason: 'nothing persisted to the keystore');

    // A new run sees no session.
    final SessionStore nextRun = SessionStore(storage);
    expect(await nextRun.load(), isNull);
  });
}
