import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/providers.dart';
import 'error_state_view.dart';

/// Shown when an incoming deep link is malformed (wrong scheme/host or a missing
/// `videoId`/`handoff`). A clear, recoverable state — never a crash
/// (`NFR-APP-REL-002`). Dismissing releases the pending link so the router
/// returns to Idle/Sign-in.
class DeepLinkErrorPage extends ConsumerWidget {
  const DeepLinkErrorPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      body: SafeArea(
        child: ErrorStateView(
          title: "This link didn't work",
          message:
              'The link looks incomplete or was changed. Head back and press '
              'Play again from the student portal.',
          primaryLabel: 'Back to home',
          onPrimary: () => ref.read(pendingDeepLinkProvider.notifier).consume(),
        ),
      ),
    );
  }
}
