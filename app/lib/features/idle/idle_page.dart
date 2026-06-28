import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/providers.dart';
import '../auth/auth_state.dart';
import 'idle_view.dart';

/// The live Idle route. Reads the signed-in student, opens the portal in the
/// system browser (`FR-APP-NAV-001`), signs out, and surfaces the
/// `update_available` soft-nudge banner when a newer version exists.
class IdlePage extends ConsumerStatefulWidget {
  const IdlePage({super.key});

  @override
  ConsumerState<IdlePage> createState() => _IdlePageState();
}

class _IdlePageState extends ConsumerState<IdlePage> {
  bool _updateBannerDismissed = false;

  Future<void> _openUrl(String url) async {
    final Uri? uri = Uri.tryParse(url);
    if (uri == null) return;
    try {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final AuthState auth = ref.watch(authControllerProvider);
    final String fullName = auth is AuthActive ? auth.student.fullName : '';
    final bool isDesktop = ref.read(appPlatformProvider).isDesktop;
    final String portalUrl = ref.read(appConfigProvider).portalUrl;

    final String storeUrl =
        ref.watch(versionCheckProvider).asData?.value.storeUrl ?? '';
    final bool updateAvailable =
        ref.watch(versionCheckProvider).asData?.value.status ==
            'update_available' &&
        storeUrl.isNotEmpty &&
        !_updateBannerDismissed;

    return Scaffold(
      body: SafeArea(
        child: IdleView(
          fullName: fullName,
          signedInAs: fullName.isEmpty ? 'your account' : fullName,
          showAppBar: !isDesktop,
          onOpenPortal: () => _openUrl(portalUrl),
          onSignOut: () => ref.read(authControllerProvider.notifier).signOut(),
          updateAvailable: updateAvailable,
          onUpdate: () => _openUrl(storeUrl),
          onDismissUpdate: () =>
              setState(() => _updateBannerDismissed = true),
        ),
      ),
    );
  }
}
