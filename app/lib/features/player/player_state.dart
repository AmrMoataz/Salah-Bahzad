import 'package:flutter/foundation.dart';

import '../../core/net/api_exception.dart';

/// The supported playback speeds (`FR-APP-VID-005`): 1× / 1.25× / 1.5× / 2×.
const List<double> kPlaybackSpeeds = <double>[1.0, 1.25, 1.5, 2.0];

/// Coarse playback status the view branches on.
enum PlayerStatus { loading, playing, paused, ended, error }

/// The player-reachable failure states (`FR-APP-ERR-002`). The named kinds are
/// the contract §H rows (verbatim titles live in [PlayerError.fromApi]);
/// [generic] is the fall-through for reasons with **no** §H row (`409 not_ready`,
/// `403 quiz_required`) — their server `detail` is rendered inline and **no**
/// §H title/action is invented for them.
enum PlayerErrorKind {
  unauthorized,
  forbidden,
  maxviews,
  expired,
  notfound,
  offline,
  server,
  updateRequired, // 426 outdated_app — A4; requires the student to update before playback
  generic,
}

/// What the failure state's primary button does. The page maps these to a
/// navigation / retry behaviour.
enum PlayerAction { signIn, openPortal, backToPortal, launchStore, retry }

/// A resolved failure surface: the §H verbatim [title] + [message] + the
/// [primaryActionLabel]/[primaryAction]. [reason]/[detail] are carried for the
/// generic case (rendered inline) and for diagnostics. [storeUrl] is non-null
/// only for [PlayerErrorKind.updateRequired].
@immutable
class PlayerError {
  const PlayerError({
    required this.kind,
    required this.title,
    required this.message,
    required this.primaryActionLabel,
    required this.primaryAction,
    this.reason,
    this.detail,
    this.storeUrl,
  });

  final PlayerErrorKind kind;
  final String title;
  final String message;
  final String primaryActionLabel;
  final PlayerAction primaryAction;
  final String? reason;
  final String? detail;
  final String? storeUrl;

  /// Maps a gate failure (contract §D.2) to a §H state. Titles are **verbatim**
  /// from contract §H. `409 not_ready` and `403 quiz_required` have **no** §H
  /// row → [generic], which renders the server `detail` inline (no invented
  /// title/action).
  factory PlayerError.fromApi(ApiException e) {
    final int? status = e.statusCode;
    final String? reason = e.reason;
    final String? detail = e.detail;

    if (e.isNetwork) {
      return PlayerError(
        kind: PlayerErrorKind.offline,
        title: "You're offline",
        message: "Check your connection — your place is saved and we'll pick "
            'up right where you left off.',
        primaryActionLabel: 'Try again',
        primaryAction: PlayerAction.retry,
        reason: reason,
        detail: detail,
      );
    }

    if (status == 401) {
      return PlayerError(
        kind: PlayerErrorKind.unauthorized,
        title: 'Your session expired',
        message: 'Sign in again to keep watching your lessons.',
        primaryActionLabel: 'Sign in again',
        primaryAction: PlayerAction.signIn,
        reason: reason,
        detail: detail,
      );
    }

    if (status == 403 && reason == 'no_views_remaining') {
      return PlayerError(
        kind: PlayerErrorKind.maxviews,
        title: 'No views left for this lesson',
        message: "You've used all your views for this video. Reach out if you "
            'need another look.',
        primaryActionLabel: 'Back to portal',
        primaryAction: PlayerAction.backToPortal,
        reason: reason,
        detail: detail,
      );
    }

    if (status == 403 && reason == 'enrollment_expired') {
      return PlayerError(
        kind: PlayerErrorKind.expired,
        title: 'Your enrollment expired',
        message: 'Renew this course in the web portal to unlock the lesson '
            'again.',
        primaryActionLabel: 'Open the portal',
        primaryAction: PlayerAction.openPortal,
        reason: reason,
        detail: detail,
      );
    }

    if (status == 403 && reason == 'not_enrolled') {
      return PlayerError(
        kind: PlayerErrorKind.forbidden,
        title: "You're not enrolled in this",
        message: 'Enroll in this course in the web portal to watch the lesson.',
        primaryActionLabel: 'Open the portal',
        primaryAction: PlayerAction.openPortal,
        reason: reason,
        detail: detail,
      );
    }

    // 404 (not found / wrong tenant — IDOR-safe) and 410 handoff_expired both
    // map to `notfound` per §H. For an expired handoff the message nudges the
    // student to press Play again (master §10.1) — the app never silently
    // re-mints (no double-decrement).
    if (status == 404 || (status == 410 && reason == 'handoff_expired')) {
      final bool expiredLink = status == 410;
      return PlayerError(
        kind: PlayerErrorKind.notfound,
        title: "We can't find this lesson",
        message: expiredLink
            ? 'This play link expired. Press Play again in the portal to start '
                  'a fresh session.'
            : 'The link may be old, or the lesson was moved. Head back and try '
                  'again.',
        primaryActionLabel: 'Back to portal',
        primaryAction: PlayerAction.backToPortal,
        reason: reason,
        detail: detail,
      );
    }

    // 426 Upgrade Required — update required (contract §H / §F.2).
    // The backend sends the store URL in ProblemDetails.detail.
    if (status == 426) {
      return PlayerError(
        kind: PlayerErrorKind.updateRequired,
        title: 'Update required',
        message: 'A newer version of the app is needed to play this lesson. '
            'Update and try again.',
        primaryActionLabel: 'Update the app',
        primaryAction: PlayerAction.launchStore,
        reason: reason,
        detail: detail,
        storeUrl: detail,
      );
    }

    if (status != null && status >= 500) {
      return PlayerError(
        kind: PlayerErrorKind.server,
        title: 'Something went wrong',
        message: "That one's on us, not you. Give it another go in a moment.",
        primaryActionLabel: 'Try again',
        primaryAction: PlayerAction.retry,
        reason: reason,
        detail: detail,
      );
    }

    // No §H row (e.g. 409 not_ready, 403 quiz_required, or any unmapped gate
    // failure): render the server detail inline; DO NOT invent a §H title.
    return PlayerError(
      kind: PlayerErrorKind.generic,
      title: "We couldn't start this lesson",
      message: (detail != null && detail.isNotEmpty)
          ? detail
          : 'Please try again, or head back to the portal.',
      primaryActionLabel: 'Try again',
      primaryAction: PlayerAction.retry,
      reason: reason,
      detail: detail,
    );
  }
}

/// Immutable player state (`NFR-APP-REL-001` — survives resize/rotation because
/// nothing here is tied to the widget tree). [viewsLeft]/[viewsTotal] are the
/// "N of M views left" budget (`FR-APP-VID-004`), sourced from the redeem
/// manifest (`accessRemaining`/`accessAllowed`, contract §D — the gate already
/// spent this view, so [viewsLeft] is the post-Play count). Both `null` until the
/// manifest arrives (or if an older API omits them) → the view renders a
/// clearly-marked fallback, never an invented number.
@immutable
class PlayerState {
  const PlayerState({
    this.status = PlayerStatus.loading,
    this.position = Duration.zero,
    this.duration = Duration.zero,
    this.buffered = Duration.zero,
    this.speed = 1.0,
    this.muted = false,
    this.volume = 1.0,
    this.fullscreen = false,
    this.buffering = false,
    this.viewsLeft,
    this.viewsTotal,
    this.videoTitle,
    this.watermark,
    this.error,
  });

  final PlayerStatus status;
  final Duration position;
  final Duration duration;
  final Duration buffered;
  final double speed;
  final bool muted;
  final double volume;
  final bool fullscreen;
  final bool buffering;

  /// Remaining views after this Play (the "N"); `null` until the manifest arrives.
  final int? viewsLeft;

  /// Total views granted for this enrollment+video (the "M"); `null` until known.
  final int? viewsTotal;

  /// The video's own title (from the redeem manifest); `null` until it arrives.
  final String? videoTitle;

  /// The "{serial} · {fullName}" watermark identity (from the redeem manifest,
  /// FR-APP-VID-003); `null` until it arrives → the page falls back to the name.
  final String? watermark;

  final PlayerError? error;

  bool get isPlaying => status == PlayerStatus.playing;
  bool get hasError => status == PlayerStatus.error && error != null;

  PlayerState copyWith({
    PlayerStatus? status,
    Duration? position,
    Duration? duration,
    Duration? buffered,
    double? speed,
    bool? muted,
    double? volume,
    bool? fullscreen,
    bool? buffering,
    int? viewsLeft,
    int? viewsTotal,
    String? videoTitle,
    String? watermark,
    PlayerError? error,
    bool clearError = false,
  }) {
    return PlayerState(
      status: status ?? this.status,
      position: position ?? this.position,
      duration: duration ?? this.duration,
      buffered: buffered ?? this.buffered,
      speed: speed ?? this.speed,
      muted: muted ?? this.muted,
      volume: volume ?? this.volume,
      fullscreen: fullscreen ?? this.fullscreen,
      buffering: buffering ?? this.buffering,
      viewsLeft: viewsLeft ?? this.viewsLeft,
      viewsTotal: viewsTotal ?? this.viewsTotal,
      videoTitle: videoTitle ?? this.videoTitle,
      watermark: watermark ?? this.watermark,
      error: clearError ? null : (error ?? this.error),
    );
  }
}
