import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/theme/sb_theme.dart';
import 'app_window_chrome.dart';
import 'providers.dart';
import 'router.dart';

/// The root widget. Hosts the go_router config and, on desktop, wraps every
/// screen in the custom window chrome.
class SecurePlayerApp extends ConsumerWidget {
  const SecurePlayerApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final GoRouter router = ref.watch(routerProvider);
    final bool isDesktop = ref.read(appPlatformProvider).isDesktop;

    return MaterialApp.router(
      title: 'Secure Player',
      debugShowCheckedModeBanner: false,
      theme: SbTheme.build(),
      routerConfig: router,
      builder: (BuildContext context, Widget? child) {
        final Widget content = child ?? const SizedBox.shrink();
        if (!isDesktop) return content;
        return AppWindowChrome(child: content);
      },
    );
  }
}
