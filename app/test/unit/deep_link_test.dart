import 'package:flutter_test/flutter_test.dart';
import 'package:secure_player/core/deeplink/deep_link_service.dart';
import 'package:secure_player/core/deeplink/playback_request.dart';

void main() {
  group('PlaybackRequest.tryParse', () {
    test('parses the canonical Play deep link (contract §E)', () {
      final PlaybackRequest? r = PlaybackRequest.tryParseString(
        'salah-bahazad://stream?videoId=ALG-204&sessionId=S-1&handoff=abc123',
      );
      expect(r, isNotNull);
      expect(r!.videoId, 'ALG-204');
      expect(r.sessionId, 'S-1');
      expect(r.handoff, 'abc123');
    });

    test('sessionId is optional', () {
      final PlaybackRequest? r = PlaybackRequest.tryParseString(
        'salah-bahazad://stream?videoId=V1&handoff=H1',
      );
      expect(r, isNotNull);
      expect(r!.sessionId, isNull);
    });

    test('rejects the wrong scheme', () {
      expect(
        PlaybackRequest.tryParseString('https://stream?videoId=V1&handoff=H1'),
        isNull,
      );
    });

    test('rejects the wrong host', () {
      expect(
        PlaybackRequest.tryParseString(
          'salah-bahazad://open?videoId=V1&handoff=H1',
        ),
        isNull,
      );
    });

    test('rejects a missing handoff (the credential)', () {
      expect(
        PlaybackRequest.tryParseString('salah-bahazad://stream?videoId=V1'),
        isNull,
      );
    });

    test('rejects a missing videoId', () {
      expect(
        PlaybackRequest.tryParseString('salah-bahazad://stream?handoff=H1'),
        isNull,
      );
    });

    test('does not throw on garbage input', () {
      expect(PlaybackRequest.tryParseString('::::not a uri'), isNull);
      expect(PlaybackRequest.tryParseString(''), isNull);
    });

    test('toString redacts the handoff', () {
      final PlaybackRequest r = PlaybackRequest(
        videoId: 'V1',
        handoff: 'super-secret',
      );
      expect(r.toString(), isNot(contains('super-secret')));
      expect(r.toString(), contains('•••'));
    });
  });

  group('DeepLinkService.parse', () {
    test('maps a valid link to DeepLinkValid', () {
      final DeepLinkResult result = DeepLinkService.parse(
        Uri.parse('salah-bahazad://stream?videoId=V1&handoff=H1'),
      );
      expect(result, isA<DeepLinkValid>());
      expect((result as DeepLinkValid).request.videoId, 'V1');
    });

    test('maps a malformed link to DeepLinkMalformed (scheme only)', () {
      final DeepLinkResult result = DeepLinkService.parse(
        Uri.parse('salah-bahazad://stream?videoId=V1'),
      );
      expect(result, isA<DeepLinkMalformed>());
      expect((result as DeepLinkMalformed).scheme, 'salah-bahazad');
    });
  });
}
