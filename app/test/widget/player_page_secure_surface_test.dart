import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/app/providers.dart';
import 'package:secure_player/core/deeplink/pending_deep_link.dart';
import 'package:secure_player/core/deeplink/playback_request.dart';
import 'package:secure_player/core/platform/app_platform.dart';
import 'package:secure_player/core/secure_surface/secure_surface.dart';
import 'package:secure_player/data/dtos/student_profile.dart';
import 'package:secure_player/features/player/player_page.dart';
import 'package:secure_player/features/player/player_state.dart';

import '../support/playback_fakes.dart';

/// A2 page-lifecycle proof: protection toggles **ON before the engine opens**
/// (`NFR-APP-CAP-006`) and **OFF on leaving the player** (`FR-APP-CAP-003`), and
/// the COMPAT-002 **warn + refuse** holds on desktop (`NFR-APP-COMPAT-002`). The
/// native black-out is never invoked — a [FakeSecureSurface] stands in
/// (`NFR-APP-REL-003`).
const PlaybackRequest _request = PlaybackRequest(
  videoId: 'vid-1',
  handoff: 'handoff-abc',
  sessionId: 'sess-1',
);

/// Pins the pending deep link without touching the `app_links` platform channel.
class _FixedPending extends PendingDeepLinkController {
  _FixedPending(this._value);
  final PlaybackRequest? _value;
  @override
  PendingDeepLink? build() => _value == null ? null : PendingValid(_value);
}

ProviderContainer _container({
  required AppTarget target,
  required FakeSecureSurface secure,
  required FakePlaybackRepository repo,
  required FakeVideoEngine engine,
  PlaybackRequest? request = _request,
}) {
  return ProviderContainer(
    overrides: [
      appPlatformProvider.overrideWithValue(AppPlatform(target: target)),
      sessionStoreProvider.overrideWithValue(FakeSessionStore()),
      secureSurfaceProvider.overrideWithValue(secure),
      playbackRepositoryProvider.overrideWithValue(repo),
      // Avoid the real loopback HttpServer (its idle timer trips the
      // testWidgets pending-timer guard); the proxy round-trip is proven in the
      // unit + F1 spike tests.
      localManifestProxyProvider.overrideWith(
        (ref) => FakeLocalManifestProxy(),
      ),
      videoEngineProvider.overrideWith((ref) {
        ref.onDispose(() async => engine.dispose());
        return engine;
      }),
      // Keep the watermark profile fetch off the network (never resolves → the
      // overlay just uses the signed-in fallback name).
      studentProfileProvider.overrideWith(
        (ref) => Completer<StudentProfile>().future,
      ),
      pendingDeepLinkProvider.overrideWith(() => _FixedPending(request)),
    ],
  );
}

Future<void> _pumpPage(WidgetTester tester, ProviderContainer container) async {
  await tester.pumpWidget(
    UncontrolledProviderScope(
      container: container,
      child: const MaterialApp(home: PlayerPage()),
    ),
  );
  // Let initState → enable() → (start | refuse) settle. Fixed pumps, never
  // pumpAndSettle: the loading spinner animates forever.
  await tester.pump();
  await tester.pump(const Duration(milliseconds: 50));
  await tester.pump(const Duration(milliseconds: 50));
}

void main() {
  testWidgets('ON before the engine opens, OFF on leave (Android)', (
    WidgetTester tester,
  ) async {
    final FakeSecureSurface secure = FakeSecureSurface(
      enableStatus: SecureSurfaceStatus.protected,
    );
    final FakePlaybackRepository repo = FakePlaybackRepository(
      manifest: fixtureManifest(),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer container = _container(
      target: AppTarget.android,
      secure: secure,
      repo: repo,
      engine: engine,
    );
    addTearDown(container.dispose);

    await _pumpPage(tester, container);

    // Enabled exactly once on mount, and playback was started (redeem ran) —
    // start() only runs AFTER enable() resolves (structural ON-before-first-frame).
    expect(secure.enableCalls, 1);
    expect(repo.redeemCalls, 1, reason: 'engine open path ran after enable()');
    expect(
      container.read(secureSurfaceStatusProvider),
      SecureSurfaceStatus.protected,
    );

    // Leave the player → protection OFF.
    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: const MaterialApp(home: SizedBox.shrink()),
      ),
    );
    await tester.pump();
    expect(secure.disableCalls, 1, reason: 'disable() on dispose');
  });

  testWidgets('COMPAT-002: unsupported on desktop → refuse, no engine open', (
    WidgetTester tester,
  ) async {
    final FakeSecureSurface secure = FakeSecureSurface(
      enableStatus: SecureSurfaceStatus.unsupported,
    );
    final FakePlaybackRepository repo = FakePlaybackRepository(
      manifest: fixtureManifest(),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer container = _container(
      target: AppTarget.windows,
      secure: secure,
      repo: repo,
      engine: engine,
    );
    addTearDown(container.dispose);

    await _pumpPage(tester, container);

    expect(secure.enableCalls, 1);
    expect(repo.redeemCalls, 0, reason: 'REFUSE: start() never called');
    expect(engine.opened, isEmpty, reason: 'engine never opened unprotected');
    expect(
      container.read(secureSurfaceStatusProvider),
      SecureSurfaceStatus.unsupported,
    );

    final PlayerState state = container.read(playerControllerProvider);
    expect(state.hasError, isTrue);
    expect(state.error!.reason, 'capture_unsupported');
  });

  testWidgets('iOS exception: unsupported still PLAYS (best-effort)', (
    WidgetTester tester,
  ) async {
    final FakeSecureSurface secure = FakeSecureSurface(
      enableStatus: SecureSurfaceStatus.unsupported,
    );
    final FakePlaybackRepository repo = FakePlaybackRepository(
      manifest: fixtureManifest(),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer container = _container(
      target: AppTarget.ios,
      secure: secure,
      repo: repo,
      engine: engine,
    );
    addTearDown(container.dispose);

    await _pumpPage(tester, container);

    // iOS is NOT refused: it plays best-effort with the amber banner + watermark.
    expect(repo.redeemCalls, 1, reason: 'iOS plays despite unsupported');
    expect(
      container.read(secureSurfaceStatusProvider),
      SecureSurfaceStatus.unsupported,
    );
    expect(container.read(playerControllerProvider).hasError, isFalse);
  });
}
