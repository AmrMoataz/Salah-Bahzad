import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/net/api_exception.dart';
import 'package:secure_player/features/player/player_state.dart';

/// Proves the `426 outdated_app` row of contract §H is mapped correctly by
/// `PlayerError.fromApi` (FR-APP-UPD-001, NFR-APP-UPD-002). Verbatim title
/// and label match the contract; storeUrl comes from ProblemDetails.detail.
void main() {
  PlayerError map(
    int? status, {
    String? reason,
    String? detail,
    bool net = false,
  }) => PlayerError.fromApi(
        ApiException(
          statusCode: status,
          reason: reason,
          detail: detail,
          isNetwork: net,
        ),
      );

  test('426 → updateRequired, verbatim §H title and label', () {
    final PlayerError e = map(426, detail: 'https://play.google.com/store');
    expect(e.kind, PlayerErrorKind.updateRequired);
    expect(e.title, 'Update required');
    expect(e.primaryActionLabel, 'Update the app');
    expect(e.primaryAction, PlayerAction.launchStore);
  });

  test('426 storeUrl is sourced from ProblemDetails.detail', () {
    const String url = 'https://apps.apple.com/app/id12345';
    final PlayerError e = map(426, detail: url);
    expect(e.storeUrl, url);
  });

  test('426 with no detail → updateRequired with null storeUrl', () {
    final PlayerError e = map(426);
    expect(e.kind, PlayerErrorKind.updateRequired);
    expect(e.storeUrl, isNull);
  });

  test('426 is not caught by the 5xx server block', () {
    final PlayerError e = map(426);
    expect(e.kind, isNot(PlayerErrorKind.server));
  });

  test('426 reason carries the backend reason code', () {
    final PlayerError e = map(426, reason: 'outdated_app', detail: '');
    expect(e.reason, 'outdated_app');
  });

  test('existing 5xx mapping is unaffected', () {
    expect(map(500).kind, PlayerErrorKind.server);
    expect(map(503).kind, PlayerErrorKind.server);
  });

  test('existing 401 mapping is unaffected', () {
    expect(map(401).kind, PlayerErrorKind.unauthorized);
  });
}
