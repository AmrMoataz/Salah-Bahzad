import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/app/providers.dart';
import 'package:secure_player/core/deeplink/playback_request.dart';
import 'package:secure_player/core/net/api_exception.dart';
import 'package:secure_player/features/player/player_controller.dart';
import 'package:secure_player/features/player/player_state.dart';

import '../support/playback_fakes.dart';

/// Pumps the microtask queue so async controller steps settle.
Future<void> pump() => Future<void>.delayed(const Duration(milliseconds: 5));

const PlaybackRequest _request = PlaybackRequest(
  videoId: 'vid-1',
  handoff: 'handoff-abc',
  sessionId: 'sess-1',
);

ProviderContainer makeContainer({
  required FakePlaybackRepository repo,
  required FakeVideoEngine engine,
}) {
  final ProviderContainer container = ProviderContainer(
    overrides: [
      playbackRepositoryProvider.overrideWithValue(repo),
      videoEngineProvider.overrideWith((ref) {
        ref.onDispose(() async => engine.dispose());
        return engine;
      }),
    ],
  );
  addTearDown(container.dispose);
  // Keep the auto-disposed controller (and the engine/proxy it reads) alive for
  // the whole test — in the app the page watches it.
  final sub = container.listen(playerControllerProvider, (_, _) {});
  addTearDown(sub.close);
  return container;
}

void main() {
  test('happy path: redeem (D2) → play; D1 is never called', () async {
    final FakePlaybackRepository repo = FakePlaybackRepository(
      manifest: fixtureManifest(),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer c = makeContainer(repo: repo, engine: engine);
    final PlayerController ctrl = c.read(playerControllerProvider.notifier);

    await ctrl.start(_request);
    await pump();
    engine.emitPlaying(true);
    await pump();

    final PlayerState state = c.read(playerControllerProvider);
    expect(state.status, PlayerStatus.playing);
    expect(state.viewsLeft, 2, reason: 'N from manifest accessRemaining (§D)');
    expect(state.viewsTotal, 3, reason: 'M from manifest accessAllowed (§D)');
    expect(repo.redeemCalls, 1, reason: 'D2 redeem once');
    expect(repo.startPlaybackCalls, 0, reason: 'deep-link path never calls D1');
    expect(engine.opened.length, 1);
    expect(
      engine.opened.single.host,
      '127.0.0.1',
      reason: 'engine opens the loopback proxy URL, not the backend',
    );
  });

  test(
    'retry within TTL reuses the SAME manifest — no second redeem, no D1 '
    '(FR-APP-AUTH-003)',
    () async {
      final FakePlaybackRepository repo = FakePlaybackRepository(
        manifest: fixtureManifest(),
      );
      final FakeVideoEngine engine = FakeVideoEngine();
      final ProviderContainer c = makeContainer(repo: repo, engine: engine);
      final PlayerController ctrl = c.read(playerControllerProvider.notifier);

      await ctrl.start(_request);
      await pump();
      // A transient engine failure flips to an error state.
      engine.emitError('network blip');
      await pump();
      expect(c.read(playerControllerProvider).hasError, isTrue);

      // Retry: must NOT re-redeem (no double-decrement) — just re-open.
      await ctrl.retry();
      await pump();

      expect(repo.redeemCalls, 1, reason: 'retry reuses the same manifest');
      expect(repo.startPlaybackCalls, 0);
      expect(engine.opened.length, 2, reason: 'engine re-opened the same URL');
      expect(engine.opened[0], engine.opened[1]);
    },
  );

  test('expired manifest on retry → "press Play again" (no re-mint)', () async {
    final FakePlaybackRepository repo = FakePlaybackRepository(
      // Already expired the instant it is handed out.
      manifest: fixtureManifest(ttl: const Duration(seconds: -1)),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer c = makeContainer(repo: repo, engine: engine);
    final PlayerController ctrl = c.read(playerControllerProvider.notifier);

    await ctrl.start(_request);
    await pump();
    engine.emitError('blip');
    await pump();

    await ctrl.retry();
    await pump();

    final PlayerState state = c.read(playerControllerProvider);
    expect(state.error?.kind, PlayerErrorKind.notfound);
    expect(state.error?.reason, 'handoff_expired');
    expect(repo.redeemCalls, 1, reason: 'expired → no silent re-redeem');
  });

  group('gate reasons map to the right state (contract §H / §D.2)', () {
    final List<(int, String?, PlayerErrorKind)> cases =
        <(int, String?, PlayerErrorKind)>[
          (403, 'not_enrolled', PlayerErrorKind.forbidden),
          (403, 'no_views_remaining', PlayerErrorKind.maxviews),
          (403, 'enrollment_expired', PlayerErrorKind.expired),
          (404, null, PlayerErrorKind.notfound),
          (410, 'handoff_expired', PlayerErrorKind.notfound),
          // No §H row → generic (detail rendered inline, no invented title).
          (409, 'not_ready', PlayerErrorKind.generic),
          (403, 'quiz_required', PlayerErrorKind.generic),
        ];

    for (final (int status, String? reason, PlayerErrorKind kind) in cases) {
      test('$status ${reason ?? '(none)'} → $kind', () async {
        final FakePlaybackRepository repo = FakePlaybackRepository(
          redeemError: ApiException(
            statusCode: status,
            reason: reason,
            detail: 'server detail text',
          ),
        );
        final FakeVideoEngine engine = FakeVideoEngine();
        final ProviderContainer c = makeContainer(repo: repo, engine: engine);
        final PlayerController ctrl = c.read(playerControllerProvider.notifier);

        await ctrl.start(_request);
        await pump();

        final PlayerState state = c.read(playerControllerProvider);
        expect(state.status, PlayerStatus.error);
        expect(state.error?.kind, kind);
        if (kind == PlayerErrorKind.generic) {
          // The server detail is surfaced inline.
          expect(state.error?.message, 'server detail text');
        }
      });
    }
  });

  test('network failure on redeem → offline', () async {
    final FakePlaybackRepository repo = FakePlaybackRepository(
      redeemError: ApiException(statusCode: null, isNetwork: true),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer c = makeContainer(repo: repo, engine: engine);
    final PlayerController ctrl = c.read(playerControllerProvider.notifier);

    await ctrl.start(_request);
    await pump();
    expect(
      c.read(playerControllerProvider).error?.kind,
      PlayerErrorKind.offline,
    );
  });

  test('controls mutate state + drive the engine', () async {
    final FakePlaybackRepository repo = FakePlaybackRepository(
      manifest: fixtureManifest(),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer c = makeContainer(repo: repo, engine: engine);
    final PlayerController ctrl = c.read(playerControllerProvider.notifier);

    await ctrl.start(_request);
    await pump();
    engine.emitPlaying(true);
    await pump();

    // Speed cycles 1 → 1.25 → 1.5 → 2 → 1.
    await ctrl.cycleSpeed();
    expect(c.read(playerControllerProvider).speed, 1.25);
    expect(engine.lastRate, 1.25);
    await ctrl.cycleSpeed();
    await ctrl.cycleSpeed();
    expect(c.read(playerControllerProvider).speed, 2.0);
    await ctrl.cycleSpeed();
    expect(c.read(playerControllerProvider).speed, 1.0);

    // Mute.
    await ctrl.toggleMute();
    expect(c.read(playerControllerProvider).muted, isTrue);
    expect(engine.lastVolume, 0);

    // Play/pause delegates to the engine.
    await ctrl.togglePlay();
    expect(engine.pauseCalls, 1);

    // Fullscreen flag.
    ctrl.toggleFullscreen();
    expect(c.read(playerControllerProvider).fullscreen, isTrue);

    // Seek clamps + delegates.
    engine.emitDuration(const Duration(seconds: 100));
    await pump();
    await ctrl.seek(const Duration(seconds: 250));
    expect(engine.lastSeek, const Duration(seconds: 100));
  });

  test('position/duration come from the engine stream (the clock)', () async {
    final FakePlaybackRepository repo = FakePlaybackRepository(
      manifest: fixtureManifest(),
    );
    final FakeVideoEngine engine = FakeVideoEngine();
    final ProviderContainer c = makeContainer(repo: repo, engine: engine);
    final PlayerController ctrl = c.read(playerControllerProvider.notifier);

    await ctrl.start(_request);
    await pump();
    engine.emitDuration(const Duration(seconds: 90));
    engine.emitPosition(const Duration(seconds: 12));
    engine.emitBuffer(const Duration(seconds: 30));
    await pump();

    final PlayerState s = c.read(playerControllerProvider);
    expect(s.duration, const Duration(seconds: 90));
    expect(s.position, const Duration(seconds: 12));
    expect(s.buffered, const Duration(seconds: 30));
  });

  test(
    'hygiene: the manifest body / key URL / handoff never reach the log '
    '(NFR-APP-SEC-005)',
    () async {
      final CapturingLogSink sink = CapturingLogSink();
      final FakePlaybackRepository repo = FakePlaybackRepository(
        manifest: fixtureManifest(),
      );
      final FakeVideoEngine engine = FakeVideoEngine();
      final ProviderContainer container = ProviderContainer(
        overrides: [
          playbackRepositoryProvider.overrideWithValue(repo),
          videoEngineProvider.overrideWith((ref) {
            ref.onDispose(() async => engine.dispose());
            return engine;
          }),
          loggerProvider.overrideWithValue(capturingLogger(sink)),
        ],
      );
      addTearDown(container.dispose);
      final sub = container.listen(playerControllerProvider, (_, _) {});
      addTearDown(sub.close);
      final PlayerController ctrl = container.read(
        playerControllerProvider.notifier,
      );

      await ctrl.start(_request);
      await pump();
      engine.emitError('mpv: failed to open http://127.0.0.1/key');
      await pump();

      final String? leak = firstLeak(sink.blob, <String>[
        'handoff-abc',
        'hls.key',
        'SIGNED',
        '#EXT-X-KEY',
        'seg0.ts',
      ]);
      expect(leak, isNull, reason: 'no secret may be logged');
    },
  );
}
