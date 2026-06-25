import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/net/api_exception.dart';
import 'package:secure_player/features/player/player_state.dart';

/// Asserts the §H verbatim titles + primary actions, and that the two reasons
/// with **no** §H row fall through to `generic` (detail inline, no invented
/// title). Mirrors contract §H exactly.
void main() {
  PlayerError map(int? status, {String? reason, String? detail, bool net = false}) =>
      PlayerError.fromApi(
        ApiException(
          statusCode: status,
          reason: reason,
          detail: detail,
          isNetwork: net,
        ),
      );

  test('401 → unauthorized "Your session expired" / Sign in again', () {
    final PlayerError e = map(401);
    expect(e.kind, PlayerErrorKind.unauthorized);
    expect(e.title, 'Your session expired');
    expect(e.primaryAction, PlayerAction.signIn);
  });

  test('403 not_enrolled → forbidden "You\'re not enrolled in this"', () {
    final PlayerError e = map(403, reason: 'not_enrolled');
    expect(e.kind, PlayerErrorKind.forbidden);
    expect(e.title, "You're not enrolled in this");
    expect(e.primaryAction, PlayerAction.openPortal);
  });

  test('403 no_views_remaining → maxviews "No views left for this lesson"', () {
    final PlayerError e = map(403, reason: 'no_views_remaining');
    expect(e.kind, PlayerErrorKind.maxviews);
    expect(e.title, 'No views left for this lesson');
    expect(e.primaryAction, PlayerAction.backToPortal);
  });

  test('403 enrollment_expired → expired "Your enrollment expired"', () {
    final PlayerError e = map(403, reason: 'enrollment_expired');
    expect(e.kind, PlayerErrorKind.expired);
    expect(e.title, 'Your enrollment expired');
    expect(e.primaryAction, PlayerAction.openPortal);
  });

  test('404 → notfound "We can\'t find this lesson"', () {
    final PlayerError e = map(404);
    expect(e.kind, PlayerErrorKind.notfound);
    expect(e.title, "We can't find this lesson");
    expect(e.primaryAction, PlayerAction.backToPortal);
  });

  test('410 handoff_expired → notfound + "press Play again" message', () {
    final PlayerError e = map(410, reason: 'handoff_expired');
    expect(e.kind, PlayerErrorKind.notfound);
    expect(e.title, "We can't find this lesson");
    expect(e.message, contains('Press Play again'));
  });

  test('network → offline "You\'re offline" / Try again', () {
    final PlayerError e = map(null, net: true);
    expect(e.kind, PlayerErrorKind.offline);
    expect(e.title, "You're offline");
    expect(e.primaryAction, PlayerAction.retry);
  });

  test('5xx → server "Something went wrong" / Try again', () {
    final PlayerError e = map(503);
    expect(e.kind, PlayerErrorKind.server);
    expect(e.title, 'Something went wrong');
    expect(e.primaryAction, PlayerAction.retry);
  });

  test('409 not_ready → generic, detail rendered inline (no invented §H title)', () {
    final PlayerError e = map(409, reason: 'not_ready', detail: 'Still encoding');
    expect(e.kind, PlayerErrorKind.generic);
    expect(e.message, 'Still encoding');
  });

  test('403 quiz_required → generic, detail inline', () {
    final PlayerError e = map(
      403,
      reason: 'quiz_required',
      detail: 'Pass the gating quiz first',
    );
    expect(e.kind, PlayerErrorKind.generic);
    expect(e.message, 'Pass the gating quiz first');
  });
}
