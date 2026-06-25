import 'secure_surface.dart';

/// The **safe default** [SecureSurface] for any platform left unwired (web,
/// Linux, or a host with no native handler). It reports
/// [SecureSurfaceStatus.unsupported] so the app never crashes over protection
/// **and** never plays unprotected silently — the COMPAT-002 gate refuses
/// (`NFR-APP-COMPAT-002`). Mirrors how the `Noop…`/fake engines keep tests and
/// unsupported hosts off native.
class NoopSecureSurface implements SecureSurface {
  const NoopSecureSurface();

  @override
  Future<SecureSurfaceStatus> enable() async => SecureSurfaceStatus.unsupported;

  @override
  Future<void> disable() async {}

  @override
  Stream<SecureSurfaceEvent> get captureEvents =>
      const Stream<SecureSurfaceEvent>.empty();
}
