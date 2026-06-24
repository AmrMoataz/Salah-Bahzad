import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/providers.dart';
import '../auth/auth_state.dart';
import 'idle_view.dart';

/// The live Idle route. Reads the signed-in student, opens the portal in the
/// system browser (`FR-APP-NAV-001`), and signs out.
class IdlePage extends ConsumerWidget {
  const IdlePage({super.key});

  Future<void> _openPortal(String url) async {
    final Uri? uri = Uri.tryParse(url);
    if (uri == null) return;
    try {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    } catch (_) {
      // Opening the portal must never crash the app.
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final AuthState auth = ref.watch(authControllerProvider);
    final String fullName = auth is AuthActive ? auth.student.fullName : '';
    final bool isDesktop = ref.read(appPlatformProvider).isDesktop;
    final String portalUrl = ref.read(appConfigProvider).portalUrl;

    return Scaffold(
      body: SafeArea(
        child: IdleView(
          fullName: fullName,
          signedInAs: fullName.isEmpty ? 'your account' : fullName,
          showAppBar: !isDesktop,
          onOpenPortal: () => _openPortal(portalUrl),
          onSignOut: () => ref.read(authControllerProvider.notifier).signOut(),
        ),
      ),
    );
  }
}
