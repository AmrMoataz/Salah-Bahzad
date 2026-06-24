import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/providers.dart';
import '../auth/auth_state.dart';
import 'sign_in_view.dart';

/// The live Sign-in route. Owns the text controllers and drives the
/// [AuthController]; reflects its state as busy/error.
class SignInPage extends ConsumerStatefulWidget {
  const SignInPage({super.key});

  @override
  ConsumerState<SignInPage> createState() => _SignInPageState();
}

class _SignInPageState extends ConsumerState<SignInPage> {
  final TextEditingController _email = TextEditingController();
  final TextEditingController _password = TextEditingController();
  bool _remember = true;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  void _signIn() {
    FocusScope.of(context).unfocus();
    ref
        .read(authControllerProvider.notifier)
        .signInWithEmail(
          email: _email.text,
          password: _password.text,
          rememberMe: _remember,
        );
  }

  void _google() {
    FocusScope.of(context).unfocus();
    ref
        .read(authControllerProvider.notifier)
        .signInWithGoogle(rememberMe: _remember);
  }

  @override
  Widget build(BuildContext context) {
    final AuthState auth = ref.watch(authControllerProvider);
    final bool busy = auth is AuthSigningIn;
    final String? error = auth is AuthError ? auth.message : null;
    final bool googleSupported = ref.read(identityProvider).googleSupported;

    return Scaffold(
      body: SafeArea(
        child: SignInView(
          emailController: _email,
          passwordController: _password,
          rememberMe: _remember,
          onRememberChanged: (bool v) => setState(() => _remember = v),
          onSignIn: _signIn,
          onGoogle: _google,
          busy: busy,
          errorMessage: error,
          googleSupported: googleSupported,
        ),
      ),
    );
  }
}
