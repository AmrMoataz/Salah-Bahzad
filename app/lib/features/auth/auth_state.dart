import '../../data/dtos/student_summary.dart';

/// Why a sign-in attempt failed. The three `account_*` reasons are the
/// status-gate states (contract §A.2) the A0 exit criteria require to render.
enum AuthErrorReason {
  accountPending,
  accountRejected,
  accountInactive,
  noStudent, // 401 — Firebase UID maps to no student
  rateLimited, // 429
  invalidCredentials, // Firebase email/pw / Google failure
  network,
  server,
  unknown,
}

/// The auth machine (plan F4): `unknown → signedOut → signingIn → active`, with
/// `error(reason)` as a terminal-but-recoverable branch off `signingIn`.
sealed class AuthState {
  const AuthState();
}

/// Startup, before the keystore has been read.
class AuthUnknown extends AuthState {
  const AuthUnknown();
}

/// No (valid) session — show Sign in.
class AuthSignedOut extends AuthState {
  const AuthSignedOut();
}

/// A sign-in is in flight (Firebase → app-exchange).
class AuthSigningIn extends AuthState {
  const AuthSigningIn();
}

/// Signed in with a live session.
class AuthActive extends AuthState {
  const AuthActive(this.student);

  final StudentSummary student;
}

/// A sign-in attempt failed; [message] is display-ready, [reason] drives any
/// branching. The Sign-in screen stays visible with this banner.
class AuthError extends AuthState {
  const AuthError(this.reason, this.message);

  final AuthErrorReason reason;
  final String message;
}
