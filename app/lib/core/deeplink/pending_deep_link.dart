import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/providers.dart';
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
  @override
  PendingDeepLink? build() {
    final DeepLinkService service = ref.read(deepLinkServiceProvider);
    final StreamSubscription<DeepLinkResult> sub =
        service.events.listen(_apply, onError: (_) {});
    ref.onDispose(sub.cancel);
    unawaited(_loadInitial(service));
    return null;
  }

  Future<void> _loadInitial(DeepLinkService service) async {
    try {
      final DeepLinkResult? result = await service.getInitial();
      if (result != null) _apply(result);
    } catch (_) {
      // A failure reading the initial link must never crash startup.
    }
  }

  void _apply(DeepLinkResult result) {
    state = switch (result) {
      DeepLinkValid(:final request) => PendingValid(request),
      DeepLinkMalformed() => const PendingMalformed(),
    };
  }

  /// Marks the pending link handled (the player captured it, or the error was
  /// dismissed), so the router stops redirecting to it.
  void consume() => state = null;
}
