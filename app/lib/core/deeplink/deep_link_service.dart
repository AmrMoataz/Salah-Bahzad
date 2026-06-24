import 'package:app_links/app_links.dart';

import 'playback_request.dart';

/// The outcome of resolving a deep link. A malformed link is a first-class,
/// recoverable result — never an exception (`NFR-APP-REL-002`).
sealed class DeepLinkResult {
  const DeepLinkResult();
}

class DeepLinkValid extends DeepLinkResult {
  const DeepLinkValid(this.request);

  final PlaybackRequest request;
}

class DeepLinkMalformed extends DeepLinkResult {
  /// Only the scheme is retained — never the query (which may carry a handoff)
  /// (`NFR-APP-SEC-003`).
  const DeepLinkMalformed(this.scheme);

  final String scheme;
}

/// Wraps `app_links`: the **cold-start** link ([getInitial]) and the **warm**
/// link stream ([events]), both mapped through the contract §E parser.
class DeepLinkService {
  DeepLinkService({AppLinks? appLinks}) : _appLinks = appLinks ?? AppLinks();

  final AppLinks _appLinks;

  /// Pure mapping from a URI to a result. Shared by [getInitial]/[events] and
  /// directly unit-tested. Never throws.
  static DeepLinkResult parse(Uri uri) {
    final PlaybackRequest? request = PlaybackRequest.tryParse(uri);
    if (request != null) return DeepLinkValid(request);
    return DeepLinkMalformed(uri.scheme);
  }

  /// The link the app was launched with (cold start), or `null` if none.
  Future<DeepLinkResult?> getInitial() async {
    final Uri? uri = await _appLinks.getInitialLink();
    if (uri == null) return null;
    return parse(uri);
  }

  /// Links delivered while the app is already running (warm).
  Stream<DeepLinkResult> get events => _appLinks.uriLinkStream.map(parse);
}
