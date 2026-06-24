import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/providers.dart';
import '../../core/deeplink/pending_deep_link.dart';
import '../../core/deeplink/playback_request.dart';
import '../../core/theme/sb_text.dart';
import '../../core/theme/sb_tokens.dart';
import '../../widgets/secure_pill.dart';

/// A0 stand-in for the real Player (A1). Proves the deep-link route end-to-end:
/// it captures the parsed [PlaybackRequest], then releases the pending link so
/// the router stops redirecting here. Real redeem / HLS / watermark / capture
/// protection arrive in A1/A2.
class PlayerPlaceholderPage extends ConsumerStatefulWidget {
  const PlayerPlaceholderPage({super.key});

  @override
  ConsumerState<PlayerPlaceholderPage> createState() =>
      _PlayerPlaceholderPageState();
}

class _PlayerPlaceholderPageState extends ConsumerState<PlayerPlaceholderPage> {
  PlaybackRequest? _request;

  @override
  void initState() {
    super.initState();
    final PendingDeepLink? pending = ref.read(pendingDeepLinkProvider);
    if (pending is PendingValid) _request = pending.request;
    // Release the pending link after the first frame so the router does not
    // bounce back here once it is handled.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(pendingDeepLinkProvider.notifier).consume();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: SbColors.playerBg,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            _TopBar(onBack: () => context.go('/idle')),
            Expanded(
              child: Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(24),
                  child: _PlaceholderCard(request: _request),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 12, 16, 12),
      child: Row(
        children: <Widget>[
          _IconChip(icon: Icons.chevron_left, onTap: onBack),
          const SizedBox(width: 10),
          const Expanded(
            child: Text(
              'Secure Player',
              style: TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 15,
                fontWeight: FontWeight.w700,
                color: SbColors.white,
              ),
            ),
          ),
          const _EncryptedChip(),
        ],
      ),
    );
  }
}

class _IconChip extends StatelessWidget {
  const _IconChip({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SbColors.white.withValues(alpha: 0.1),
      borderRadius: SbRadii.brInput,
      child: InkWell(
        borderRadius: SbRadii.brInput,
        onTap: onTap,
        child: SizedBox(
          width: 38,
          height: 38,
          child: Icon(icon, color: SbColors.white, size: 22),
        ),
      ),
    );
  }
}

class _EncryptedChip extends StatelessWidget {
  const _EncryptedChip();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 6),
      decoration: BoxDecoration(
        color: SbColors.white.withValues(alpha: 0.1),
        borderRadius: SbRadii.brPill,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.shield_outlined, size: 14, color: SbColors.greenSoft),
          const SizedBox(width: 6),
          const Text(
            'Encrypted',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 12,
              fontWeight: FontWeight.w700,
              color: SbColors.white,
            ),
          ),
        ],
      ),
    );
  }
}

class _PlaceholderCard extends StatelessWidget {
  const _PlaceholderCard({required this.request});

  final PlaybackRequest? request;

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 460),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 74,
            height: 74,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SbColors.primary.withValues(alpha: 0.92),
              shape: BoxShape.circle,
            ),
            child: const Icon(Icons.play_arrow, size: 36, color: SbColors.white),
          ),
          const SizedBox(height: 20),
          const Text(
            'Secure playback lands in A1',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: SbColors.white,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            request == null
                ? 'No active lesson — press Play in the student portal to open '
                    'one here.'
                : 'The deep link was received and parsed. Encrypted HLS, the '
                    'moving watermark and the capture black-out arrive in the '
                    'next phase.',
            textAlign: TextAlign.center,
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 14,
              height: 1.5,
              color: SbColors.white.withValues(alpha: 0.6),
            ),
          ),
          if (request != null) ...<Widget>[
            const SizedBox(height: 20),
            _RequestPanel(request: request!),
          ],
        ],
      ),
    );
  }
}

class _RequestPanel extends StatelessWidget {
  const _RequestPanel({required this.request});

  final PlaybackRequest request;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: SbColors.white.withValues(alpha: 0.06),
        borderRadius: SbRadii.brCard,
        border: Border.all(color: SbColors.white.withValues(alpha: 0.12)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: const <Widget>[
              SecurePill.dark(),
              Spacer(),
            ],
          ),
          const SizedBox(height: 12),
          _Kv('videoId', request.videoId),
          _Kv('sessionId', request.sessionId ?? '—'),
          // The handoff is the credential — shown redacted, never logged.
          const _Kv('handoff', '•••••••• (one-time)'),
        ],
      ),
    );
  }
}

class _Kv extends StatelessWidget {
  const _Kv(this.k, this.v);

  final String k;
  final String v;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(
            width: 84,
            child: Text(
              k,
              style: TextStyle(
                fontFamily: SbFonts.mono,
                fontSize: 12,
                color: SbColors.white.withValues(alpha: 0.5),
              ),
            ),
          ),
          Expanded(
            child: Text(
              v,
              style: const TextStyle(
                fontFamily: SbFonts.mono,
                fontSize: 12,
                color: SbColors.white,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
