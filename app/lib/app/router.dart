import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/deeplink/pending_deep_link.dart';
import '../features/auth/auth_state.dart';
import '../features/errors/deep_link_error_page.dart';
import '../features/idle/idle_page.dart';
import '../features/player/player_page.dart';
import '../features/signin/sign_in_page.dart';
import '../features/splash/splash_page.dart';
import 'providers.dart';

/// The app router. A single redirect resolves (auth state × pending deep link)
/// to a destination; the [_RouterRefresh] re-evaluates it whenever either moves.
final routerProvider = Provider<GoRouter>((ref) {
  final _RouterRefresh refresh = _RouterRefresh();
  ref.listen(authControllerProvider, (_, _) => refresh.bump());
  ref.listen(pendingDeepLinkProvider, (_, _) => refresh.bump());
  ref.onDispose(refresh.dispose);

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, state) => _redirect(ref, state.matchedLocation),
    routes: <RouteBase>[
      GoRoute(path: '/splash', builder: (_, _) => const SplashPage()),
      GoRoute(path: '/signin', builder: (_, _) => const SignInPage()),
      GoRoute(path: '/idle', builder: (_, _) => const IdlePage()),
      GoRoute(path: '/player', builder: (_, _) => const PlayerPage()),
      GoRoute(path: '/error', builder: (_, _) => const DeepLinkErrorPage()),
    ],
  );
});

/// Resolves the destination. Priority: still booting → Splash; a malformed link
/// → Error; a valid link → Player (signed in) or Sign in first; otherwise the
/// home for the auth state.
String? _redirect(Ref ref, String location) {
  final AuthState auth = ref.read(authControllerProvider);
  final PendingDeepLink? pending = ref.read(pendingDeepLinkProvider);

  if (auth is AuthUnknown) {
    return location == '/splash' ? null : '/splash';
  }

  if (pending is PendingMalformed) {
    return location == '/error' ? null : '/error';
  }

  if (pending is PendingValid) {
    if (auth is AuthActive) {
      return location == '/player' ? null : '/player';
    }
    // Must sign in first; the pending link survives until handled.
    return location == '/signin' ? null : '/signin';
  }

  if (auth is AuthActive) {
    // Don't yank the user off the player back to idle.
    if (location == '/idle' || location == '/player') return null;
    return '/idle';
  }

  // signedOut / signingIn / error → the Sign-in screen.
  return location == '/signin' ? null : '/signin';
}

/// Bridges Riverpod state changes to go_router's [Listenable]-based refresh.
class _RouterRefresh extends ChangeNotifier {
  void bump() => notifyListeners();
}
