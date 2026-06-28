import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/providers.dart';
import 'error_state_view.dart';

/// Hard-block screen shown when this build of the app is below the enforced
/// minimum version floor (contract §F / §H — `update_required`). There is no
/// back navigation; the student must update before the app routes anywhere else.
class UpdateRequiredPage extends ConsumerWidget {
  const UpdateRequiredPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final String storeUrl =
        ref.watch(versionCheckProvider).asData?.value.storeUrl ?? '';

    return Scaffold(
      body: SafeArea(
        child: ErrorStateView(
          title: 'Update required',
          message: 'A new version of this app is required. '
              'Update to continue watching your lessons.',
          primaryLabel: 'Update the app',
          onPrimary: () async {
            if (storeUrl.isEmpty) return;
            final Uri? uri = Uri.tryParse(storeUrl);
            if (uri == null) return;
            try {
              await launchUrl(uri, mode: LaunchMode.externalApplication);
            } catch (_) {}
          },
          footer: 'Nothing has been lost — press Update to continue.',
        ),
      ),
    );
  }
}
