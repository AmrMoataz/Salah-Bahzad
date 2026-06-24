import 'dart:async';

import 'package:flutter/material.dart';

import 'splash_view.dart';

/// The live Splash route — shown while the app boots (and reads any cold-start
/// deep link). Animates the three-step checklist; the router redirects away as
/// soon as auth resolves.
class SplashPage extends StatefulWidget {
  const SplashPage({super.key});

  @override
  State<SplashPage> createState() => _SplashPageState();
}

class _SplashPageState extends State<SplashPage> {
  int _step = 0;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(milliseconds: 700), (Timer t) {
      if (!mounted) return;
      setState(() => _step++);
      if (_step >= 3) t.cancel();
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF1E3A5F),
      body: SplashView(step: _step.clamp(0, 3)),
    );
  }
}
