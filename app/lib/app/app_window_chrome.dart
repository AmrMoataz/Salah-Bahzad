import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:window_manager/window_manager.dart';

import '../core/theme/sb_assets.dart';
import '../core/theme/sb_text.dart';
import '../core/theme/sb_tokens.dart';
import '../features/auth/auth_state.dart';
import '../widgets/secure_pill.dart';
import 'providers.dart';

/// The frameless desktop window chrome (design anchors: `macOS · controls left`,
/// `Windows · controls right`). A 44 px navy title bar — draggable, with the
/// brand, the Secure pill, the account row, and platform-correct window
/// controls — hosting the app [child]. Only mounted on desktop.
class AppWindowChrome extends ConsumerWidget {
  const AppWindowChrome({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final bool controlsLeft = ref.read(appPlatformProvider).controlsOnLeft;
    final AuthState auth = ref.watch(authControllerProvider);
    final String? name = auth is AuthActive ? auth.student.fullName : null;

    return Column(
      children: <Widget>[
        SizedBox(
          height: 44,
          child: DragToMoveArea(
            child: Container(
              color: SbColors.navyDeep,
              padding: EdgeInsets.only(
                left: controlsLeft ? 12 : 14,
                right: controlsLeft ? 12 : 4,
              ),
              child: Row(
                children: <Widget>[
                  if (controlsLeft) ...<Widget>[
                    const _MacControls(),
                    const SizedBox(width: 12),
                  ],
                  const _BrandMark(),
                  const Spacer(),
                  const SecurePill.dark(),
                  if (name != null) ...<Widget>[
                    const SizedBox(width: 12),
                    _Account(name: name),
                  ],
                  if (!controlsLeft) ...<Widget>[
                    const SizedBox(width: 8),
                    const _WinDivider(),
                    const _WindowsControls(),
                  ],
                ],
              ),
            ),
          ),
        ),
        Expanded(child: child),
      ],
    );
  }
}

class _BrandMark extends StatelessWidget {
  const _BrandMark();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Image.asset(SbAssets.logoWhite, height: 24),
        const SizedBox(width: 8),
        Text(
          'Secure Player',
          style: TextStyle(
            fontFamily: SbFonts.sans,
            fontSize: 12.5,
            fontWeight: FontWeight.w700,
            color: SbColors.white.withValues(alpha: 0.82),
          ),
        ),
      ],
    );
  }
}

class _Account extends StatelessWidget {
  const _Account({required this.name});

  final String name;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Text(
              name,
              style: TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 12,
                fontWeight: FontWeight.w800,
                color: SbColors.white.withValues(alpha: 0.92),
              ),
            ),
            Text(
              'Student',
              style: TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 10,
                color: SbColors.white.withValues(alpha: 0.45),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _MacControls extends StatelessWidget {
  const _MacControls();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        _MacDot(color: SbColors.macClose, onTap: () => windowManager.close()),
        const SizedBox(width: 8),
        _MacDot(
          color: SbColors.macMin,
          onTap: () => windowManager.minimize(),
        ),
        const SizedBox(width: 8),
        _MacDot(color: SbColors.macZoom, onTap: _toggleMaximize),
      ],
    );
  }
}

Future<void> _toggleMaximize() async {
  if (await windowManager.isMaximized()) {
    await windowManager.unmaximize();
  } else {
    await windowManager.maximize();
  }
}

class _MacDot extends StatelessWidget {
  const _MacDot({required this.color, required this.onTap});

  final Color color;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 13,
        height: 13,
        decoration: BoxDecoration(color: color, shape: BoxShape.circle),
      ),
    );
  }
}

class _WinDivider extends StatelessWidget {
  const _WinDivider();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 1,
      height: 20,
      margin: const EdgeInsets.symmetric(horizontal: 4),
      color: SbColors.white.withValues(alpha: 0.14),
    );
  }
}

class _WindowsControls extends StatelessWidget {
  const _WindowsControls();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        _WinButton(
          icon: Icons.minimize,
          onTap: () => windowManager.minimize(),
        ),
        _WinButton(icon: Icons.crop_square, onTap: _toggleMaximize),
        _WinButton(
          icon: Icons.close,
          onTap: () => windowManager.close(),
          hoverColor: SbColors.windowClose,
        ),
      ],
    );
  }
}

class _WinButton extends StatefulWidget {
  const _WinButton({
    required this.icon,
    required this.onTap,
    this.hoverColor,
  });

  final IconData icon;
  final VoidCallback onTap;
  final Color? hoverColor;

  @override
  State<_WinButton> createState() => _WinButtonState();
}

class _WinButtonState extends State<_WinButton> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    final Color bg = _hover
        ? (widget.hoverColor ?? SbColors.white.withValues(alpha: 0.12))
        : Colors.transparent;
    return MouseRegion(
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Container(
          width: 46,
          height: 38,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: bg,
            borderRadius: BorderRadius.circular(6),
          ),
          child: Icon(
            widget.icon,
            size: 16,
            color: SbColors.white.withValues(alpha: 0.8),
          ),
        ),
      ),
    );
  }
}
