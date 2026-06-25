import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/secure_surface/method_channel_secure_surface.dart';
import 'package:secure_player/core/secure_surface/noop_secure_surface.dart';
import 'package:secure_player/core/secure_surface/secure_surface.dart';

import '../support/playback_fakes.dart';

/// A2 capture-protection facade + COMPAT-capability mapping, against a **fake
/// channel** — the real `MethodChannel`/native black-out is never invoked
/// (`NFR-APP-REL-003`); the OS black-out itself is manual-only (the wiring
/// stream's matrix).
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const MethodChannel channel = MethodChannel('salah_bahzad/secure_surface');
  final TestDefaultBinaryMessenger messenger =
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger;

  group('FakeSecureSurface (the hand fake used by player tests)', () {
    test('records enable/disable and returns the scripted status', () async {
      final FakeSecureSurface fake = FakeSecureSurface(
        enableStatus: SecureSurfaceStatus.protected,
      );
      addTearDown(fake.dispose);

      expect(await fake.enable(), SecureSurfaceStatus.protected);
      await fake.disable();

      expect(fake.enableCalls, 1);
      expect(fake.disableCalls, 1);
    });

    test('can be scripted to report unsupported (the refuse path)', () async {
      final FakeSecureSurface fake = FakeSecureSurface(
        enableStatus: SecureSurfaceStatus.unsupported,
      );
      addTearDown(fake.dispose);
      expect(await fake.enable(), SecureSurfaceStatus.unsupported);
    });
  });

  group('NoopSecureSurface (safe default for unwired hosts)', () {
    test('reports unsupported so the COMPAT-002 gate refuses', () async {
      const NoopSecureSurface noop = NoopSecureSurface();
      expect(await noop.enable(), SecureSurfaceStatus.unsupported);
      await noop.disable(); // never throws
      expect(noop.captureEvents, emitsDone);
    });
  });

  group('MethodChannelSecureSurface (prod impl over a faked messenger)', () {
    const MethodChannelSecureSurface surface = MethodChannelSecureSurface();

    tearDown(() {
      messenger.setMockMethodCallHandler(channel, null);
    });

    test('enable sends "enable" and maps "protected" → protected', () async {
      final List<String> calls = <String>[];
      messenger.setMockMethodCallHandler(channel, (MethodCall call) async {
        calls.add(call.method);
        return 'protected';
      });

      expect(await surface.enable(), SecureSurfaceStatus.protected);
      expect(calls, <String>['enable']);
    });

    test('maps a native "unsupported" reply → unsupported', () async {
      messenger.setMockMethodCallHandler(
        channel,
        (MethodCall call) async => 'unsupported',
      );
      expect(await surface.enable(), SecureSurfaceStatus.unsupported);
    });

    test('maps an unknown/null reply → unsupported (fail-safe)', () async {
      messenger.setMockMethodCallHandler(channel, (MethodCall call) async {
        return null;
      });
      expect(await surface.enable(), SecureSurfaceStatus.unsupported);
    });

    test('a PlatformException maps → unsupported (never throws)', () async {
      messenger.setMockMethodCallHandler(channel, (MethodCall call) async {
        throw PlatformException(code: 'boom');
      });
      expect(await surface.enable(), SecureSurfaceStatus.unsupported);
    });

    test('a missing handler maps → unsupported (unwired runner)', () async {
      // No mock handler registered → MissingPluginException.
      expect(await surface.enable(), SecureSurfaceStatus.unsupported);
    });

    test('disable sends "disable" and never throws', () async {
      final List<String> calls = <String>[];
      messenger.setMockMethodCallHandler(channel, (MethodCall call) async {
        calls.add(call.method);
        return null;
      });
      await surface.disable();
      expect(calls, <String>['disable']);
    });
  });
}
