import 'package:flutter/material.dart';

import '../../core/responsive/responsive_builder.dart';
import '../../core/theme/sb_assets.dart';
import '../../core/theme/sb_text.dart';
import '../../core/theme/sb_tokens.dart';
import '../../widgets/math_doodles.dart';

/// Design anchor: `SIGN IN`. A responsive split — brand panel + form — that
/// stacks to one column below 560 px. Pure & deterministic for goldens; the
/// page wires controllers + the `AuthController`.
class SignInView extends StatelessWidget {
  const SignInView({
    super.key,
    required this.emailController,
    required this.passwordController,
    required this.rememberMe,
    required this.onRememberChanged,
    required this.onSignIn,
    required this.onGoogle,
    this.busy = false,
    this.errorMessage,
    this.googleSupported = true,
  });

  final TextEditingController emailController;
  final TextEditingController passwordController;
  final bool rememberMe;
  final ValueChanged<bool> onRememberChanged;
  final VoidCallback onSignIn;
  final VoidCallback onGoogle;
  final bool busy;
  final String? errorMessage;
  final bool googleSupported;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SbColors.paper,
      child: ResponsiveBuilder(
        builder: (context, layout) {
          final Widget brand = _BrandPanel(compact: layout.isPhone);
          final Widget form = _FormPanel(
            emailController: emailController,
            passwordController: passwordController,
            rememberMe: rememberMe,
            onRememberChanged: onRememberChanged,
            onSignIn: onSignIn,
            onGoogle: onGoogle,
            busy: busy,
            errorMessage: errorMessage,
            googleSupported: googleSupported,
          );

          if (layout.isPhone) {
            return SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[brand, form],
              ),
            );
          }
          return Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Expanded(flex: 4, child: brand),
              Expanded(flex: 5, child: SingleChildScrollView(child: form)),
            ],
          );
        },
      ),
    );
  }
}

class _BrandPanel extends StatelessWidget {
  const _BrandPanel({required this.compact});

  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: BoxConstraints(minHeight: compact ? 300 : 0),
      padding: EdgeInsets.all(compact ? 26 : 44),
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[SbColors.primary, SbColors.navy],
        ),
      ),
      child: Stack(
        children: <Widget>[
          const MathDoodles(opacity: 0.08, fontSize: 30),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Image.asset(SbAssets.logoWhite, height: 30),
              const SizedBox(height: 14),
              const Text(
                'Welcome\nback.',
                style: TextStyle(
                  fontFamily: SbFonts.marker,
                  fontSize: 36,
                  height: 1.1,
                  color: SbColors.white,
                ),
              ),
              const SizedBox(height: 14),
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 300),
                child: Text(
                  'Sign in to play your protected lessons. Everything else '
                  'lives in the web portal.',
                  style: TextStyle(
                    fontFamily: SbFonts.sans,
                    fontSize: 15,
                    height: 1.5,
                    color: SbColors.white.withValues(alpha: 0.82),
                  ),
                ),
              ),
              const SizedBox(height: 18),
              Align(
                alignment: Alignment.centerRight,
                child: Image.asset(SbAssets.mascot, width: compact ? 110 : 140),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _FormPanel extends StatelessWidget {
  const _FormPanel({
    required this.emailController,
    required this.passwordController,
    required this.rememberMe,
    required this.onRememberChanged,
    required this.onSignIn,
    required this.onGoogle,
    required this.busy,
    required this.errorMessage,
    required this.googleSupported,
  });

  final TextEditingController emailController;
  final TextEditingController passwordController;
  final bool rememberMe;
  final ValueChanged<bool> onRememberChanged;
  final VoidCallback onSignIn;
  final VoidCallback onGoogle;
  final bool busy;
  final String? errorMessage;
  final bool googleSupported;

  @override
  Widget build(BuildContext context) {
    return Container(
      color: SbColors.white,
      padding: const EdgeInsets.all(32),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 380),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              const Text(
                'Sign in',
                style: TextStyle(
                  fontFamily: SbFonts.sans,
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  color: SbColors.ink,
                ),
              ),
              const SizedBox(height: 4),
              const Text(
                'Use the same account as the web portal.',
                style: TextStyle(
                  fontFamily: SbFonts.sans,
                  fontSize: 14,
                  color: SbColors.ink4,
                ),
              ),
              if (errorMessage != null) ...<Widget>[
                const SizedBox(height: 16),
                _ErrorBanner(message: errorMessage!),
              ],
              const SizedBox(height: 16),
              _Field(
                label: 'Email',
                controller: emailController,
                keyboardType: TextInputType.emailAddress,
                enabled: !busy,
              ),
              const SizedBox(height: 16),
              _Field(
                label: 'Password',
                controller: passwordController,
                obscure: true,
                enabled: !busy,
              ),
              const SizedBox(height: 14),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: <Widget>[
                  _RememberCheckbox(
                    value: rememberMe,
                    onChanged: busy ? null : onRememberChanged,
                  ),
                  Text(
                    'Forgot?',
                    style: TextStyle(
                      fontFamily: SbFonts.sans,
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: SbColors.primary,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              _PrimaryButton(
                label: 'Sign in',
                busy: busy,
                onPressed: busy ? null : onSignIn,
              ),
              if (googleSupported) ...<Widget>[
                const SizedBox(height: 16),
                const _OrDivider(),
                const SizedBox(height: 16),
                _GoogleButton(onPressed: busy ? null : onGoogle),
              ],
              const SizedBox(height: 16),
              Text.rich(
                const TextSpan(
                  children: <InlineSpan>[
                    TextSpan(text: 'Only '),
                    TextSpan(
                      text: 'active students',
                      style: TextStyle(
                        fontWeight: FontWeight.w700,
                        color: SbColors.ink3,
                      ),
                    ),
                    TextSpan(
                      text: ' can sign in. Identity is verified with Firebase.',
                    ),
                  ],
                ),
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontFamily: SbFonts.sans,
                  fontSize: 12,
                  height: 1.5,
                  color: SbColors.ink5,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Field extends StatelessWidget {
  const _Field({
    required this.label,
    required this.controller,
    this.obscure = false,
    this.keyboardType,
    this.enabled = true,
  });

  final String label;
  final TextEditingController controller;
  final bool obscure;
  final TextInputType? keyboardType;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(label, style: SbText.label),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          obscureText: obscure,
          enabled: enabled,
          keyboardType: keyboardType,
          style: const TextStyle(
            fontFamily: SbFonts.sans,
            fontSize: 15,
            color: SbColors.ink,
          ),
          decoration: InputDecoration(
            isDense: true,
            contentPadding:
                const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
            filled: true,
            fillColor: SbColors.white,
            enabledBorder: const OutlineInputBorder(
              borderRadius: SbRadii.brInput,
              borderSide: BorderSide(color: SbColors.border, width: 1.5),
            ),
            focusedBorder: const OutlineInputBorder(
              borderRadius: SbRadii.brInput,
              borderSide: BorderSide(color: SbColors.primary, width: 1.5),
            ),
            disabledBorder: const OutlineInputBorder(
              borderRadius: SbRadii.brInput,
              borderSide: BorderSide(color: SbColors.borderSoft, width: 1.5),
            ),
          ),
        ),
      ],
    );
  }
}

class _RememberCheckbox extends StatelessWidget {
  const _RememberCheckbox({required this.value, required this.onChanged});

  final bool value;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onChanged == null ? null : () => onChanged!(!value),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          SizedBox(
            width: 18,
            height: 18,
            child: Checkbox(
              value: value,
              onChanged: onChanged == null
                  ? null
                  : (bool? v) => onChanged!(v ?? false),
              activeColor: SbColors.primary,
              materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
              visualDensity: VisualDensity.compact,
            ),
          ),
          const SizedBox(width: 7),
          const Text(
            'Keep me signed in',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 13,
              color: SbColors.ink3,
            ),
          ),
        ],
      ),
    );
  }
}

class _PrimaryButton extends StatelessWidget {
  const _PrimaryButton({
    required this.label,
    required this.onPressed,
    this.busy = false,
  });

  final String label;
  final VoidCallback? onPressed;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: SbColors.primary,
          disabledBackgroundColor: SbColors.primary.withValues(alpha: 0.55),
          foregroundColor: SbColors.white,
          shape: const RoundedRectangleBorder(borderRadius: SbRadii.brInput),
        ),
        child: busy
            ? const SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(
                  strokeWidth: 2.4,
                  valueColor: AlwaysStoppedAnimation<Color>(SbColors.white),
                ),
              )
            : Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: <Widget>[
                  Text(
                    label,
                    style: const TextStyle(
                      fontFamily: SbFonts.sans,
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(width: 8),
                  const Icon(Icons.arrow_forward, size: 18),
                ],
              ),
      ),
    );
  }
}

class _OrDivider extends StatelessWidget {
  const _OrDivider();

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        const Expanded(child: Divider(color: SbColors.borderSoft)),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Text(
            'or',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 12,
              color: SbColors.ink5,
            ),
          ),
        ),
        const Expanded(child: Divider(color: SbColors.borderSoft)),
      ],
    );
  }
}

class _GoogleButton extends StatelessWidget {
  const _GoogleButton({required this.onPressed});

  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 46,
      child: OutlinedButton(
        onPressed: onPressed,
        style: OutlinedButton.styleFrom(
          backgroundColor: SbColors.white,
          foregroundColor: SbColors.ink2,
          side: const BorderSide(color: SbColors.border, width: 1.5),
          shape: const RoundedRectangleBorder(borderRadius: SbRadii.brInput),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Container(
              width: 18,
              height: 18,
              alignment: Alignment.center,
              child: const Text(
                'G',
                style: TextStyle(
                  fontFamily: SbFonts.sans,
                  fontSize: 16,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF4285F4),
                ),
              ),
            ),
            const SizedBox(width: 10),
            const Text(
              'Continue with Google',
              style: TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 14,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: const Color(0xFFFBEAE9),
        borderRadius: SbRadii.brInput,
        border: Border.all(color: SbColors.dangerBorder),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const Icon(Icons.error_outline, size: 18, color: SbColors.danger),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 13,
                height: 1.4,
                color: SbColors.danger,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
