# Firebase Authentication Reference Guide

## Overview

Firebase Authentication provides backend services, SDKs, and ready-made UI
libraries to authenticate users. This reference covers the FlutterFire setup
process, all major sign-in providers, auth state management, custom claims,
account lifecycle, and integration with Riverpod and Bloc.

---

## Table of Contents

1. [Firebase CLI and FlutterFire CLI Setup](#firebase-cli-and-flutterfire-cli-setup)
2. [Email/Password Authentication](#emailpassword-authentication)
3. [Google Sign-In](#google-sign-in)
4. [Apple Sign-In](#apple-sign-in)
5. [Phone Authentication (OTP)](#phone-authentication-otp)
6. [Auth State Listeners](#auth-state-listeners)
7. [Custom Claims and Roles](#custom-claims-and-roles)
8. [Sign Out and Account Deletion](#sign-out-and-account-deletion)
9. [Auth with Riverpod](#auth-with-riverpod)
10. [Auth with Bloc](#auth-with-bloc)
11. [Security Rules Overview](#security-rules-overview)

---

## Firebase CLI and FlutterFire CLI Setup

### 1. Install the Firebase CLI

```bash
# macOS / Linux
curl -sL https://firebase.tools | bash

# Verify installation
firebase --version

# Login to Firebase
firebase login
```

### 2. Install the FlutterFire CLI

```bash
dart pub global activate flutterfire_cli
```

### 3. Configure your Flutter project

```bash
# Run from the root of your Flutter project
flutterfire configure
```

This generates `lib/firebase_options.dart` with platform-specific configuration.

### 4. Add dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_core: ^3.8.0
  firebase_auth: ^5.3.0
```

### 5. Initialize Firebase in main.dart

```dart
// lib/main.dart
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';

import 'firebase_options.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp(
    options: DefaultFirebaseOptions.currentPlatform,
  );
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Firebase App',
      theme: ThemeData(
        colorSchemeSeed: Colors.indigo,
        useMaterial3: true,
      ),
      routerConfig: appRouter,
    );
  }
}
```

---

## Email/Password Authentication

### Create a new user

```dart
import 'package:firebase_auth/firebase_auth.dart';

Future<User?> createUserWithEmail({
  required String email,
  required String password,
}) async {
  final credential =
      await FirebaseAuth.instance.createUserWithEmailAndPassword(
    email: email,
    password: password,
  );
  return credential.user;
}
```

### Sign in an existing user

```dart
Future<User?> signInWithEmail({
  required String email,
  required String password,
}) async {
  final credential = await FirebaseAuth.instance.signInWithEmailAndPassword(
    email: email,
    password: password,
  );
  return credential.user;
}
```

### Password reset

```dart
Future<void> sendPasswordReset({required String email}) async {
  await FirebaseAuth.instance.sendPasswordResetEmail(email: email);
}
```

### Email verification

```dart
Future<void> sendEmailVerification() async {
  final user = FirebaseAuth.instance.currentUser;
  if (user != null && !user.emailVerified) {
    await user.sendEmailVerification();
  }
}

bool isEmailVerified() {
  final user = FirebaseAuth.instance.currentUser;
  return user?.emailVerified ?? false;
}
```

### Complete email/password auth service

```dart
// lib/services/email_auth_service.dart
import 'package:firebase_auth/firebase_auth.dart';

class EmailAuthService {
  EmailAuthService({FirebaseAuth? auth})
      : _auth = auth ?? FirebaseAuth.instance;

  final FirebaseAuth _auth;

  Future<User?> signUp({
    required String email,
    required String password,
    String? displayName,
  }) async {
    final credential = await _auth.createUserWithEmailAndPassword(
      email: email,
      password: password,
    );
    final user = credential.user;
    if (user != null && displayName != null) {
      await user.updateDisplayName(displayName);
      await user.reload();
    }
    return _auth.currentUser;
  }

  Future<User?> signIn({
    required String email,
    required String password,
  }) async {
    final credential = await _auth.signInWithEmailAndPassword(
      email: email,
      password: password,
    );
    return credential.user;
  }

  Future<void> resetPassword({required String email}) async {
    await _auth.sendPasswordResetEmail(email: email);
  }

  Future<void> signOut() async {
    await _auth.signOut();
  }
}
```

---

## Google Sign-In

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_auth: ^5.3.0
  google_sign_in: ^6.2.0
```

### Implementation

```dart
// lib/services/google_auth_service.dart
import 'package:firebase_auth/firebase_auth.dart';
import 'package:google_sign_in/google_sign_in.dart';

class GoogleAuthService {
  GoogleAuthService({
    FirebaseAuth? auth,
    GoogleSignIn? googleSignIn,
  })  : _auth = auth ?? FirebaseAuth.instance,
        _googleSignIn = googleSignIn ?? GoogleSignIn();

  final FirebaseAuth _auth;
  final GoogleSignIn _googleSignIn;

  Future<User?> signIn() async {
    final googleUser = await _googleSignIn.signIn();
    if (googleUser == null) {
      // User cancelled the sign-in flow.
      return null;
    }

    final googleAuth = await googleUser.authentication;
    final credential = GoogleAuthProvider.credential(
      accessToken: googleAuth.accessToken,
      idToken: googleAuth.idToken,
    );

    final userCredential = await _auth.signInWithCredential(credential);
    return userCredential.user;
  }

  Future<void> signOut() async {
    await Future.wait([
      _auth.signOut(),
      _googleSignIn.signOut(),
    ]);
  }
}
```

### Android setup

Add your SHA-1 fingerprint to the Firebase console:

```bash
# Debug SHA-1
cd android && ./gradlew signingReport
```

Add the SHA-1 to **Firebase Console > Project Settings > Your Apps > Android app > SHA certificate fingerprints**.

### iOS setup

Add the reversed client ID to `ios/Runner/Info.plist`:

```xml
<key>CFBundleURLTypes</key>
<array>
  <dict>
    <key>CFBundleTypeRole</key>
    <string>Editor</string>
    <key>CFBundleURLSchemes</key>
    <array>
      <!-- Reversed client ID from GoogleService-Info.plist -->
      <string>com.googleusercontent.apps.YOUR_CLIENT_ID</string>
    </array>
  </dict>
</array>
```

---

## Apple Sign-In

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_auth: ^5.3.0
  sign_in_with_apple: ^6.1.0
  crypto: ^3.0.0
```

### Implementation

```dart
// lib/services/apple_auth_service.dart
import 'dart:convert';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:sign_in_with_apple/sign_in_with_apple.dart';

class AppleAuthService {
  AppleAuthService({FirebaseAuth? auth})
      : _auth = auth ?? FirebaseAuth.instance;

  final FirebaseAuth _auth;

  String _generateNonce([int length = 32]) {
    const charset =
        '0123456789ABCDEFGHIJKLMNOPQRSTUVXYZabcdefghijklmnopqrstuvwxyz-._';
    final random = Random.secure();
    return List.generate(length, (_) => charset[random.nextInt(charset.length)])
        .join();
  }

  String _sha256ofString(String input) {
    final bytes = utf8.encode(input);
    final digest = sha256.convert(bytes);
    return digest.toString();
  }

  Future<User?> signIn() async {
    final rawNonce = _generateNonce();
    final nonce = _sha256ofString(rawNonce);

    final appleCredential = await SignInWithApple.getAppleIDCredential(
      scopes: [
        AppleIDAuthorizationScopes.email,
        AppleIDAuthorizationScopes.fullName,
      ],
      nonce: nonce,
    );

    final oauthCredential = OAuthProvider('apple.com').credential(
      idToken: appleCredential.identityToken,
      rawNonce: rawNonce,
    );

    final userCredential = await _auth.signInWithCredential(oauthCredential);

    // Apple only sends the name on the first sign-in.
    final user = userCredential.user;
    if (user != null && user.displayName == null) {
      final givenName = appleCredential.givenName ?? '';
      final familyName = appleCredential.familyName ?? '';
      final fullName = '$givenName $familyName'.trim();
      if (fullName.isNotEmpty) {
        await user.updateDisplayName(fullName);
        await user.reload();
      }
    }

    return _auth.currentUser;
  }

  Future<void> signOut() async {
    await _auth.signOut();
  }
}
```

### iOS setup

1. Enable **Sign in with Apple** capability in Xcode:
   - Open `ios/Runner.xcworkspace`
   - Select the Runner target > **Signing & Capabilities**
   - Click **+ Capability** > **Sign in with Apple**

2. Enable the Apple provider in the Firebase Console:
   - **Authentication > Sign-in method > Apple > Enable**

### Android setup

Apple Sign-In on Android requires the OAuth redirect flow. Configure the
Firebase Console with your Apple Services ID and private key. The
`sign_in_with_apple` package handles the web-based redirect automatically.

---

## Phone Authentication (OTP)

### Implementation

```dart
// lib/services/phone_auth_service.dart
import 'dart:async';

import 'package:firebase_auth/firebase_auth.dart';

class PhoneAuthService {
  PhoneAuthService({FirebaseAuth? auth})
      : _auth = auth ?? FirebaseAuth.instance;

  final FirebaseAuth _auth;

  String? _verificationId;
  int? _resendToken;

  Future<void> sendOtp({
    required String phoneNumber,
    required void Function(String verificationId) onCodeSent,
    required void Function(FirebaseAuthException error) onError,
    required void Function(PhoneAuthCredential credential) onAutoVerify,
    Duration timeout = const Duration(seconds: 60),
  }) async {
    await _auth.verifyPhoneNumber(
      phoneNumber: phoneNumber,
      timeout: timeout,
      forceResendingToken: _resendToken,
      verificationCompleted: (PhoneAuthCredential credential) {
        onAutoVerify(credential);
      },
      verificationFailed: (FirebaseAuthException e) {
        onError(e);
      },
      codeSent: (String verificationId, int? resendToken) {
        _verificationId = verificationId;
        _resendToken = resendToken;
        onCodeSent(verificationId);
      },
      codeAutoRetrievalTimeout: (String verificationId) {
        _verificationId = verificationId;
      },
    );
  }

  Future<User?> verifySmsCode({required String smsCode}) async {
    if (_verificationId == null) {
      throw StateError('No verification ID. Call sendOtp first.');
    }

    final credential = PhoneAuthProvider.credential(
      verificationId: _verificationId!,
      smsCode: smsCode,
    );

    final userCredential = await _auth.signInWithCredential(credential);
    return userCredential.user;
  }

  Future<User?> signInWithAutoVerifiedCredential(
    PhoneAuthCredential credential,
  ) async {
    final userCredential = await _auth.signInWithCredential(credential);
    return userCredential.user;
  }

  Future<void> signOut() async {
    await _auth.signOut();
  }
}
```

### Phone auth UI example

```dart
// lib/screens/phone_auth_screen.dart
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';

import '../services/phone_auth_service.dart';

class PhoneAuthScreen extends StatefulWidget {
  const PhoneAuthScreen({super.key});

  @override
  State<PhoneAuthScreen> createState() => _PhoneAuthScreenState();
}

class _PhoneAuthScreenState extends State<PhoneAuthScreen> {
  final _phoneAuth = PhoneAuthService();
  final _phoneController = TextEditingController();
  final _otpController = TextEditingController();
  bool _codeSent = false;
  bool _loading = false;

  Future<void> _sendCode() async {
    setState(() => _loading = true);
    await _phoneAuth.sendOtp(
      phoneNumber: _phoneController.text.trim(),
      onCodeSent: (verificationId) {
        setState(() {
          _codeSent = true;
          _loading = false;
        });
      },
      onError: (error) {
        setState(() => _loading = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(error.message ?? 'Verification failed')),
          );
        }
      },
      onAutoVerify: (credential) async {
        await _phoneAuth.signInWithAutoVerifiedCredential(credential);
      },
    );
  }

  Future<void> _verifyCode() async {
    setState(() => _loading = true);
    try {
      await _phoneAuth.verifySmsCode(smsCode: _otpController.text.trim());
      // Navigate on success.
    } on FirebaseAuthException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.message ?? 'Invalid code')),
        );
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  void dispose() {
    _phoneController.dispose();
    _otpController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Phone Sign-In')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (!_codeSent) ...[
              TextField(
                controller: _phoneController,
                decoration: const InputDecoration(
                  labelText: 'Phone number',
                  hintText: '+1 650 555 1234',
                ),
                keyboardType: TextInputType.phone,
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _loading ? null : _sendCode,
                child: _loading
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Send Code'),
              ),
            ] else ...[
              TextField(
                controller: _otpController,
                decoration: const InputDecoration(labelText: 'Verification code'),
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _loading ? null : _verifyCode,
                child: _loading
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Verify'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
```

### iOS setup for phone auth

Add the following to `ios/Runner/Info.plist` if not already present:

```xml
<key>CFBundleURLTypes</key>
<array>
  <dict>
    <key>CFBundleTypeRole</key>
    <string>Editor</string>
    <key>CFBundleURLSchemes</key>
    <array>
      <!-- Reversed client ID from GoogleService-Info.plist -->
      <string>com.googleusercontent.apps.YOUR_CLIENT_ID</string>
    </array>
  </dict>
</array>
```

Enable **Push Notifications** and **Background Modes > Remote notifications**
in Xcode capabilities (required for APNs-based silent verification).

---

## Auth State Listeners

### authStateChanges

Emits events when the user signs in or signs out. Does **not** emit on token
refresh.

```dart
// Listen to auth state globally
FirebaseAuth.instance.authStateChanges().listen((User? user) {
  if (user == null) {
    // User is signed out.
  } else {
    // User is signed in.
  }
});
```

### idTokenChanges

Emits events on sign-in, sign-out, **and** when the ID token refreshes (e.g.,
custom claims change).

```dart
FirebaseAuth.instance.idTokenChanges().listen((User? user) async {
  if (user != null) {
    final idTokenResult = await user.getIdTokenResult();
    final isAdmin = idTokenResult.claims?['admin'] == true;
    // Update local state with fresh claims.
  }
});
```

### userChanges

Emits events on sign-in, sign-out, token refresh, **and** when the user profile
is updated (e.g., `updateDisplayName`, `updatePhotoURL`).

```dart
FirebaseAuth.instance.userChanges().listen((User? user) {
  if (user != null) {
    // user.displayName, user.photoURL, etc. are fresh.
  }
});
```

---

## Custom Claims and Roles

Custom claims are set via the Firebase Admin SDK (typically in a Cloud Function)
and read on the client via `getIdTokenResult()`.

### Setting claims (Cloud Function -- TypeScript)

```typescript
// functions/src/index.ts
import { onCall, HttpsError } from 'firebase-functions/v2/https';
import { getAuth } from 'firebase-admin/auth';
import { initializeApp } from 'firebase-admin/app';

initializeApp();

export const setAdminRole = onCall(async (request) => {
  // Only allow existing admins to grant admin role.
  if (request.auth?.token.admin !== true) {
    throw new HttpsError('permission-denied', 'Only admins can grant admin role.');
  }

  const { uid } = request.data;
  await getAuth().setCustomUserClaims(uid, { admin: true });
  return { message: `Admin role granted to ${uid}` };
});
```

### Reading claims on the client

```dart
Future<Map<String, dynamic>> getUserClaims() async {
  final user = FirebaseAuth.instance.currentUser;
  if (user == null) return {};

  // Force refresh to pick up newly set claims.
  final idTokenResult = await user.getIdTokenResult(true);
  return idTokenResult.claims ?? {};
}

Future<bool> isAdmin() async {
  final claims = await getUserClaims();
  return claims['admin'] == true;
}
```

### Role-based access in the app

```dart
// lib/widgets/admin_guard.dart
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';

class AdminGuard extends StatelessWidget {
  const AdminGuard({
    required this.child,
    this.fallback = const SizedBox.shrink(),
    super.key,
  });

  final Widget child;
  final Widget fallback;

  Future<bool> _checkAdmin() async {
    final user = FirebaseAuth.instance.currentUser;
    if (user == null) return false;
    final token = await user.getIdTokenResult();
    return token.claims?['admin'] == true;
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<bool>(
      future: _checkAdmin(),
      builder: (context, snapshot) {
        if (snapshot.data == true) return child;
        return fallback;
      },
    );
  }
}
```

---

## Sign Out and Account Deletion

### Sign out (all providers)

```dart
Future<void> signOut() async {
  // Sign out of third-party providers first, then Firebase.
  final user = FirebaseAuth.instance.currentUser;
  if (user != null) {
    for (final provider in user.providerData) {
      switch (provider.providerId) {
        case 'google.com':
          await GoogleSignIn().signOut();
        case 'apple.com':
          // Apple does not have a client-side sign-out API.
          break;
      }
    }
  }
  await FirebaseAuth.instance.signOut();
}
```

### Account deletion

Deleting an account requires recent authentication. Re-authenticate first if the
operation throws a `requires-recent-login` error.

```dart
Future<void> deleteAccount() async {
  final user = FirebaseAuth.instance.currentUser;
  if (user == null) return;

  try {
    await user.delete();
  } on FirebaseAuthException catch (e) {
    if (e.code == 'requires-recent-login') {
      // Re-authenticate before retrying. See _reauthenticate below.
      rethrow;
    }
    rethrow;
  }
}

Future<void> reauthenticateWithEmail({
  required String email,
  required String password,
}) async {
  final user = FirebaseAuth.instance.currentUser;
  if (user == null) return;

  final credential = EmailAuthProvider.credential(
    email: email,
    password: password,
  );
  await user.reauthenticateWithCredential(credential);
}

Future<void> reauthenticateWithGoogle() async {
  final googleUser = await GoogleSignIn().signIn();
  if (googleUser == null) return;

  final googleAuth = await googleUser.authentication;
  final credential = GoogleAuthProvider.credential(
    accessToken: googleAuth.accessToken,
    idToken: googleAuth.idToken,
  );

  final user = FirebaseAuth.instance.currentUser;
  await user?.reauthenticateWithCredential(credential);
}
```

### Cleaning up user data on deletion (Cloud Function)

```typescript
// functions/src/index.ts
import { onDocumentDeleted } from 'firebase-functions/v2/firestore';
import { getFirestore } from 'firebase-admin/firestore';
import { getStorage } from 'firebase-admin/storage';

export const cleanupUserData = onDocumentDeleted(
  'users/{userId}',
  async (event) => {
    const userId = event.params.userId;
    const db = getFirestore();
    const bucket = getStorage().bucket();

    // Delete user subcollections.
    const collections = await db.doc(`users/${userId}`).listCollections();
    for (const col of collections) {
      const docs = await col.listDocuments();
      const batch = db.batch();
      for (const doc of docs) {
        batch.delete(doc);
      }
      await batch.commit();
    }

    // Delete user files from Storage.
    await bucket.deleteFiles({ prefix: `users/${userId}/` });
  },
);
```

---

## Auth with Riverpod

### Auth state provider

```dart
// lib/providers/auth_providers.dart
import 'package:firebase_auth/firebase_auth.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'auth_providers.g.dart';

@riverpod
FirebaseAuth firebaseAuth(Ref ref) {
  return FirebaseAuth.instance;
}

@riverpod
Stream<User?> authState(Ref ref) {
  return ref.watch(firebaseAuthProvider).authStateChanges();
}

@riverpod
Stream<User?> idTokenState(Ref ref) {
  return ref.watch(firebaseAuthProvider).idTokenChanges();
}
```

### Auth controller

```dart
// lib/providers/auth_controller.dart
import 'package:firebase_auth/firebase_auth.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import 'auth_providers.dart';

part 'auth_controller.g.dart';

@riverpod
class AuthController extends _$AuthController {
  @override
  FutureOr<void> build() {}

  Future<void> signInWithEmail({
    required String email,
    required String password,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      await ref
          .read(firebaseAuthProvider)
          .signInWithEmailAndPassword(email: email, password: password);
    });
  }

  Future<void> signUpWithEmail({
    required String email,
    required String password,
    String? displayName,
  }) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      final credential = await ref
          .read(firebaseAuthProvider)
          .createUserWithEmailAndPassword(email: email, password: password);
      if (displayName != null) {
        await credential.user?.updateDisplayName(displayName);
      }
    });
  }

  Future<void> signOut() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      await ref.read(firebaseAuthProvider).signOut();
    });
  }

  Future<void> resetPassword({required String email}) async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(() async {
      await ref.read(firebaseAuthProvider).sendPasswordResetEmail(email: email);
    });
  }
}
```

### Using auth state in GoRouter redirect

```dart
// lib/router/app_router.dart
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../providers/auth_providers.dart';

part 'app_router.g.dart';

@riverpod
GoRouter appRouter(Ref ref) {
  final authState = ref.watch(authStateProvider);

  return GoRouter(
    initialLocation: '/',
    redirect: (context, routerState) {
      final isLoggedIn = authState.valueOrNull != null;
      final isAuthRoute = routerState.matchedLocation == '/login' ||
          routerState.matchedLocation == '/register';

      if (!isLoggedIn && !isAuthRoute) return '/login';
      if (isLoggedIn && isAuthRoute) return '/';
      return null;
    },
    routes: [
      GoRoute(
        path: '/',
        builder: (context, state) => const HomeScreen(),
      ),
      GoRoute(
        path: '/login',
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: '/register',
        builder: (context, state) => const RegisterScreen(),
      ),
    ],
  );
}
```

---

## Auth with Bloc

### Auth state and events

```dart
// lib/auth/bloc/auth_event.dart
sealed class AuthEvent {
  const AuthEvent();
}

final class AuthCheckRequested extends AuthEvent {
  const AuthCheckRequested();
}

final class AuthSignInRequested extends AuthEvent {
  const AuthSignInRequested({
    required this.email,
    required this.password,
  });

  final String email;
  final String password;
}

final class AuthSignUpRequested extends AuthEvent {
  const AuthSignUpRequested({
    required this.email,
    required this.password,
    this.displayName,
  });

  final String email;
  final String password;
  final String? displayName;
}

final class AuthSignOutRequested extends AuthEvent {
  const AuthSignOutRequested();
}
```

```dart
// lib/auth/bloc/auth_state.dart
import 'package:firebase_auth/firebase_auth.dart';

sealed class AuthState {
  const AuthState();
}

final class AuthInitial extends AuthState {
  const AuthInitial();
}

final class AuthLoading extends AuthState {
  const AuthLoading();
}

final class AuthAuthenticated extends AuthState {
  const AuthAuthenticated(this.user);
  final User user;
}

final class AuthUnauthenticated extends AuthState {
  const AuthUnauthenticated();
}

final class AuthError extends AuthState {
  const AuthError(this.message);
  final String message;
}
```

### Auth Bloc

```dart
// lib/auth/bloc/auth_bloc.dart
import 'dart:async';

import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'auth_event.dart';
import 'auth_state.dart';

class AuthBloc extends Bloc<AuthEvent, AuthState> {
  AuthBloc({FirebaseAuth? auth})
      : _auth = auth ?? FirebaseAuth.instance,
        super(const AuthInitial()) {
    on<AuthCheckRequested>(_onCheckRequested);
    on<AuthSignInRequested>(_onSignInRequested);
    on<AuthSignUpRequested>(_onSignUpRequested);
    on<AuthSignOutRequested>(_onSignOutRequested);

    _authSubscription = _auth.authStateChanges().listen((user) {
      if (user != null) {
        add(const AuthCheckRequested());
      }
    });
  }

  final FirebaseAuth _auth;
  late final StreamSubscription<User?> _authSubscription;

  Future<void> _onCheckRequested(
    AuthCheckRequested event,
    Emitter<AuthState> emit,
  ) async {
    final user = _auth.currentUser;
    if (user != null) {
      emit(AuthAuthenticated(user));
    } else {
      emit(const AuthUnauthenticated());
    }
  }

  Future<void> _onSignInRequested(
    AuthSignInRequested event,
    Emitter<AuthState> emit,
  ) async {
    emit(const AuthLoading());
    try {
      final credential = await _auth.signInWithEmailAndPassword(
        email: event.email,
        password: event.password,
      );
      final user = credential.user;
      if (user != null) {
        emit(AuthAuthenticated(user));
      } else {
        emit(const AuthUnauthenticated());
      }
    } on FirebaseAuthException catch (e) {
      emit(AuthError(e.message ?? 'Sign-in failed'));
    }
  }

  Future<void> _onSignUpRequested(
    AuthSignUpRequested event,
    Emitter<AuthState> emit,
  ) async {
    emit(const AuthLoading());
    try {
      final credential = await _auth.createUserWithEmailAndPassword(
        email: event.email,
        password: event.password,
      );
      if (event.displayName != null) {
        await credential.user?.updateDisplayName(event.displayName);
      }
      final user = _auth.currentUser;
      if (user != null) {
        emit(AuthAuthenticated(user));
      } else {
        emit(const AuthUnauthenticated());
      }
    } on FirebaseAuthException catch (e) {
      emit(AuthError(e.message ?? 'Sign-up failed'));
    }
  }

  Future<void> _onSignOutRequested(
    AuthSignOutRequested event,
    Emitter<AuthState> emit,
  ) async {
    emit(const AuthLoading());
    await _auth.signOut();
    emit(const AuthUnauthenticated());
  }

  @override
  Future<void> close() {
    _authSubscription.cancel();
    return super.close();
  }
}
```

### Providing the Bloc

```dart
// lib/main.dart (Bloc variant)
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'auth/bloc/auth_bloc.dart';
import 'auth/bloc/auth_event.dart';

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => AuthBloc()..add(const AuthCheckRequested()),
      child: MaterialApp.router(
        routerConfig: appRouter,
      ),
    );
  }
}
```

---

## Security Rules Overview

### Firestore rules for authenticated users

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // Users can only read/write their own document.
    match /users/{userId} {
      allow read, update, delete: if request.auth != null
                                  && request.auth.uid == userId;
      allow create: if request.auth != null;
    }

    // Admin-only collection.
    match /admin/{document=**} {
      allow read, write: if request.auth != null
                         && request.auth.token.admin == true;
    }

    // Public read, authenticated write.
    match /posts/{postId} {
      allow read: if true;
      allow create: if request.auth != null;
      allow update, delete: if request.auth != null
                            && resource.data.authorId == request.auth.uid;
    }
  }
}
```

### Storage rules for authenticated uploads

```
rules_version = '2';
service firebase.storage {
  match /b/{bucket}/o {

    // Users can manage their own files.
    match /users/{userId}/{allPaths=**} {
      allow read: if request.auth != null;
      allow write: if request.auth != null
                   && request.auth.uid == userId
                   && request.resource.size < 10 * 1024 * 1024  // 10 MB
                   && request.resource.contentType.matches('image/.*');
    }

    // Public read for shared assets.
    match /public/{allPaths=**} {
      allow read: if true;
      allow write: if request.auth != null
                   && request.auth.token.admin == true;
    }
  }
}
```
