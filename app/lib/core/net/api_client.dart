import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../logging/logging.dart';
import '../platform/app_platform.dart';
import '../storage/session_store.dart';
import 'app_config.dart';
import 'token_refresher.dart';

/// The authenticated HTTP surface. One Dio with:
///  * default `X-App-Version` + `X-Platform` headers (contract §G),
///  * a Bearer-token injector reading the in-memory [SessionStore],
///  * a **single-flight** 401 refresh that retries the original request once,
///  * **TLS validation always on** — `badCertificateCallback` is never set
///    (`NFR-APP-SEC-002`). Dev trusts the Aspire cert via the OS store.
class ApiClient {
  ApiClient({
    required AppConfig config,
    required SessionStore store,
    required TokenRefresher refresher,
    required AppPlatform platform,
    Dio? dio,
  }) : dio = dio ?? Dio() {
    this.dio.options = BaseOptions(
      baseUrl: config.apiBaseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 30),
      headers: <String, dynamic>{
        'X-App-Version': config.appVersion,
        'X-Platform': platform.target.wireName,
        'Accept': 'application/json',
      },
      // Let us inspect 4xx/5xx bodies (for `reason`) instead of throwing early.
      validateStatus: (int? code) => code != null && code < 400,
    );
    this.dio.interceptors.add(
      _AuthInterceptor(
        store: store,
        refresher: refresher,
        retryDio: this.dio,
        onRevoked: () {
          unawaited(store.clear());
          _sessionRevoked.value++;
        },
      ),
    );
  }

  final Dio dio;

  /// Bumps whenever an unrecoverable 401 forces a sign-out, so the
  /// `AuthController` can flip to the `unauthorized` state.
  final ValueNotifier<int> _sessionRevoked = ValueNotifier<int>(0);
  ValueListenable<int> get onSessionRevoked => _sessionRevoked;

  void dispose() => _sessionRevoked.dispose();
}

class _AuthInterceptor extends Interceptor {
  _AuthInterceptor({
    required this.store,
    required this.refresher,
    required this.retryDio,
    required this.onRevoked,
  });

  final SessionStore store;
  final TokenRefresher refresher;
  final Dio retryDio;
  final void Function() onRevoked;

  final AppLogger _log = Log.scoped('net');

  Future<bool>? _inflight;

  static const String _retriedFlag = '__sb_retried';

  bool _isAnonPath(String path) => path.contains('/api/auth/');

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    if (!_isAnonPath(options.path)) {
      final session = store.current;
      if (session != null && !options.headers.containsKey('Authorization')) {
        options.headers['Authorization'] = 'Bearer ${session.accessToken}';
      }
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final RequestOptions opts = err.requestOptions;
    final bool is401 = err.response?.statusCode == 401;
    final bool alreadyRetried = opts.extra[_retriedFlag] == true;
    final bool needsAuth = !_isAnonPath(opts.path);

    if (!is401 || alreadyRetried || !needsAuth || store.current == null) {
      handler.next(err);
      return;
    }

    _log.debug(
      '401 received; attempting single-flight refresh',
      fields: <String, Object?>{'path': opts.path},
    );
    final String? usedToken = _bearerOf(opts);
    final bool refreshed = await _singleFlightRefresh(usedToken);
    if (!refreshed) {
      _log.info(
        'Refresh failed; session revoked',
        fields: <String, Object?>{'path': opts.path},
      );
      onRevoked();
      handler.next(err);
      return;
    }

    final session = store.current;
    if (session == null) {
      _log.info(
        'Session cleared mid-refresh; revoking',
        fields: <String, Object?>{'path': opts.path},
      );
      onRevoked();
      handler.next(err);
      return;
    }

    opts.extra[_retriedFlag] = true;
    opts.headers['Authorization'] = 'Bearer ${session.accessToken}';
    try {
      final Response<dynamic> retried = await retryDio.fetch<dynamic>(opts);
      handler.resolve(retried);
    } on DioException catch (e) {
      _log.warning(
        'Retry after refresh failed',
        fields: <String, Object?>{
          'path': opts.path,
          'status': e.response?.statusCode,
        },
      );
      handler.next(e);
    }
  }

  /// One refresh at a time. If the session token already moved on since this
  /// request used it, a concurrent refresh already succeeded → just retry.
  Future<bool> _singleFlightRefresh(String? usedToken) {
    final current = store.current;
    if (current != null &&
        usedToken != null &&
        current.accessToken != usedToken) {
      return Future<bool>.value(true);
    }
    return _inflight ??= _doRefresh().whenComplete(() => _inflight = null);
  }

  Future<bool> _doRefresh() async {
    final current = store.current;
    if (current == null) return false;
    final next = await refresher.refresh(current.refreshToken);
    if (next == null) return false;
    await store.save(next);
    return true;
  }

  String? _bearerOf(RequestOptions opts) {
    final dynamic auth = opts.headers['Authorization'];
    if (auth is String && auth.startsWith('Bearer ')) {
      return auth.substring('Bearer '.length);
    }
    return null;
  }
}
