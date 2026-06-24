import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/providers.dart';
import '../logging/logging.dart';
import 'deep_link_service.dart';
import 'playback_request.dart';

/// A deep link awaiting handling by the router.
sealed class PendingDeepLink {
  const PendingDeepLink();
}

class PendingValid extends PendingDeepLink {
  const PendingValid(this.request);

  final PlaybackRequest request;
}

class PendingMalformed extends PendingDeepLink {
  const PendingMalformed();
}

/// Holds the current pending deep link. Activates on first watch: reads the
/// **cold-start** link and subscribes to **warm** links, mapping both through
/// the contract §E parser. The router redirects on its value; screens
/// [consume] it once handled.
class PendingDeepLinkController extends Notifier<PendingDeepLink?> {
  AppLogger get _log => ref.read(loggerProvider).scoped('deeplink');

  @override
  PendingDeepLink? build() {
    final DeepLinkService service = ref.read(deepLinkServiceProvider);
    final StreamSubscription<DeepLinkResult> sub = service.events.listen(
      _apply,
      onError: (Object error, StackTrace stack) => _log.warning(
        'Warm deep-link stream error',
        error: error,
        stackTrace: stack,
      ),
    );
    ref.onDispose(sub.cancel);
    unawaited(_loadInitial(service));
    return null;
  }

  Future<void> _loadInitial(DeepLinkService service) async {
    try {
      final DeepLinkResult? result = await service.getInitial();
      if (result != null) _apply(result);
    } catch (error, stack) {
      // A failure reading the initial link must never crash startup.
      _log.warning(
        'Failed to read cold-start deep link',
        error: error,
        stackTrace: stack,
      );
    }
  }

  void _apply(DeepLinkResult result) {
    // Note: the handoff/query is never logged (NFR-APP-SEC-003) — only the
    // outcome and, for malformed links, the bare scheme.
    switch (result) {
      case DeepLinkValid():
        _log.info('Valid Play deep link received');
      case DeepLinkMalformed(:final scheme):
        _log.warning(
          'Malformed deep link',
          fields: <String, Object?>{'scheme': scheme},
        );
    }
    state = switch (result) {
      DeepLinkValid(:final request) => PendingValid(request),
      DeepLinkMalformed() => const PendingMalformed(),
    };
  }

  /// Marks the pending link handled (the player captured it, or the error was
  /// dismissed), so the router stops redirecting to it.
  void consume() => state = null;
}
