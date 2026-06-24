import 'package:flutter/material.dart';

import '../../core/responsive/responsive_builder.dart';
import '../../core/theme/sb_assets.dart';
import '../../core/theme/sb_text.dart';
import '../../core/theme/sb_tokens.dart';
import '../../widgets/math_doodles.dart';
import '../../widgets/secure_pill.dart';

/// Design anchor: `IDLE / HOME`. A calm way back to the portal — the app never
/// browses or plays from here (lessons arrive via the Play deep link). Pure &
/// deterministic for goldens. [showAppBar] is `false` when the desktop window
/// chrome already carries the account row.
class IdleView extends StatelessWidget {
  const IdleView({
    super.key,
    required this.fullName,
    required this.signedInAs,
    required this.onOpenPortal,
    required this.onSignOut,
    this.showAppBar = true,
  });

  final String fullName;

  /// What the "Session active" card shows after "Signed in as" — the student's
  /// name (the backend summary carries no email; contract §A.1).
  final String signedInAs;
  final VoidCallback onOpenPortal;
  final VoidCallback onSignOut;
  final bool showAppBar;

  String get _firstName => fullName.trim().isEmpty
      ? 'there'
      : fullName.trim().split(RegExp(r'\s+')).first;

  String get _initials {
    final List<String> parts = fullName
        .trim()
        .split(RegExp(r'\s+'))
        .where((s) => s.isNotEmpty)
        .toList();
    if (parts.isEmpty) return '··';
    if (parts.length == 1) return parts.first.characters.first.toUpperCase();
    return (parts.first.characters.first + parts.last.characters.first)
        .toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: SbColors.paperAlt,
      child: Column(
        children: <Widget>[
          if (showAppBar) _AccountBar(name: fullName, initials: _initials),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(20, 24, 20, 28),
              child: Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 760),
                  child: ResponsiveBuilder(
                    builder: (context, layout) {
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: <Widget>[
                          _Hero(
                            firstName: _firstName,
                            onOpenPortal: onOpenPortal,
                            compact: layout.isPhone,
                            padding: layout.heroPadding,
                          ),
                          const SizedBox(height: 18),
                          const _SectionLabel('How to start a lesson'),
                          const SizedBox(height: 8),
                          const _HowToCard(),
                          const SizedBox(height: 18),
                          const _SectionLabel('Your account is protected'),
                          const SizedBox(height: 8),
                          _SecurityStrip(
                            columns: layout.statusColumns,
                            signedInAs: signedInAs,
                          ),
                          const SizedBox(height: 18),
                          _Footer(onSignOut: onSignOut),
                        ],
                      );
                    },
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _AccountBar extends StatelessWidget {
  const _AccountBar({required this.name, required this.initials});

  final String name;
  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 14),
      decoration: const BoxDecoration(
        color: SbColors.white,
        border: Border(bottom: BorderSide(color: SbColors.borderSoft)),
      ),
      child: Row(
        children: <Widget>[
          Image.asset(SbAssets.logoSmall, height: 30),
          const Spacer(),
          const SecurePill.light(),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(
                name,
                style: const TextStyle(
                  fontFamily: SbFonts.sans,
                  fontSize: 13,
                  fontWeight: FontWeight.w800,
                  color: SbColors.ink,
                ),
              ),
              const Text(
                'Student',
                style: TextStyle(
                  fontFamily: SbFonts.sans,
                  fontSize: 11,
                  color: SbColors.ink5,
                ),
              ),
            ],
          ),
          const SizedBox(width: 8),
          _Avatar(initials: initials),
        ],
      ),
    );
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 36,
      height: 36,
      alignment: Alignment.center,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[SbColors.primary, SbColors.navy],
        ),
      ),
      child: Text(
        initials,
        style: const TextStyle(
          fontFamily: SbFonts.sans,
          fontSize: 13,
          fontWeight: FontWeight.w800,
          color: SbColors.white,
        ),
      ),
    );
  }
}

class _Hero extends StatelessWidget {
  const _Hero({
    required this.firstName,
    required this.onOpenPortal,
    required this.compact,
    required this.padding,
  });

  final String firstName;
  final VoidCallback onOpenPortal;
  final bool compact;
  final double padding;

  @override
  Widget build(BuildContext context) {
    final Widget text = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        const Text(
          "YOU'RE ALL SET",
          style: TextStyle(
            fontFamily: SbFonts.sans,
            fontSize: 12,
            fontWeight: FontWeight.w800,
            letterSpacing: 1,
            color: SbColors.accentBlueSoft,
          ),
        ),
        const SizedBox(height: 12),
        Text(
          'Welcome back,\n$firstName.',
          style: const TextStyle(
            fontFamily: SbFonts.marker,
            fontSize: 28,
            height: 1.08,
            color: SbColors.white,
          ),
        ),
        const SizedBox(height: 12),
        ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 340),
          child: Text(
            'This app plays your protected lessons. Browse courses, enroll and '
            'take quizzes in the web portal — then press Play to land right '
            'back here.',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 14,
              height: 1.55,
              color: SbColors.white.withValues(alpha: 0.82),
            ),
          ),
        ),
        const SizedBox(height: 16),
        _OpenPortalButton(onPressed: onOpenPortal),
      ],
    );

    final Widget mascot = Image.asset(
      SbAssets.relaxing,
      width: compact ? 130 : 168,
    );

    return Container(
      padding: EdgeInsets.all(padding),
      decoration: BoxDecoration(
        borderRadius: SbRadii.brCardLg,
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[SbColors.heroNavy, SbColors.navy],
        ),
      ),
      child: Stack(
        children: <Widget>[
          const MathDoodles(opacity: 0.08, fontSize: 28),
          if (compact)
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                text,
                const SizedBox(height: 12),
                Center(child: mascot),
              ],
            )
          else
            Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: <Widget>[
                Expanded(child: text),
                const SizedBox(width: 20),
                mascot,
              ],
            ),
        ],
      ),
    );
  }
}

class _OpenPortalButton extends StatelessWidget {
  const _OpenPortalButton({required this.onPressed});

  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 46,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          backgroundColor: SbColors.white,
          foregroundColor: SbColors.navy,
          shape: const RoundedRectangleBorder(borderRadius: SbRadii.brInput),
          padding: const EdgeInsets.symmetric(horizontal: 20),
        ),
        child: const Row(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(
              'Open the student portal',
              style: TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 14,
                fontWeight: FontWeight.w800,
              ),
            ),
            SizedBox(width: 9),
            Icon(Icons.open_in_new, size: 16),
          ],
        ),
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  const _SectionLabel(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text.toUpperCase(),
      style: const TextStyle(
        fontFamily: SbFonts.sans,
        fontSize: 11,
        fontWeight: FontWeight.w800,
        letterSpacing: 0.6,
        color: SbColors.ink5,
      ),
    );
  }
}

class _HowToCard extends StatelessWidget {
  const _HowToCard();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
      decoration: BoxDecoration(
        color: SbColors.white,
        borderRadius: SbRadii.brCard,
        border: Border.all(color: SbColors.borderSoft),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          Container(
            width: 44,
            height: 44,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SbColors.infoBg,
              borderRadius: BorderRadius.circular(11),
            ),
            child: const Icon(Icons.link, size: 22, color: SbColors.primary),
          ),
          const SizedBox(width: 14),
          const Expanded(
            child: Text(
              'In the same device, open a session video in the student portal '
              'and press Play. The portal will redirect you to this application '
              '— the video will open here automatically. There’s nothing '
              'to browse or resume from inside the app.',
              style: TextStyle(
                fontFamily: SbFonts.sans,
                fontSize: 13,
                height: 1.5,
                color: SbColors.ink2,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _SecurityStrip extends StatelessWidget {
  const _SecurityStrip({required this.columns, required this.signedInAs});

  final int columns;
  final String signedInAs;

  @override
  Widget build(BuildContext context) {
    final List<Widget> cards = <Widget>[
      const _SecurityCard(
        icon: Icons.verified_user_outlined,
        iconColor: SbColors.green,
        iconBg: SbColors.greenBg,
        title: 'Capture protected',
        subtitle: 'Screenshots & recordings blocked',
      ),
      const _SecurityCard(
        icon: Icons.lock_outline,
        iconColor: SbColors.primary,
        iconBg: SbColors.infoBg,
        title: 'Encrypted playback',
        subtitle: 'HLS + AES-128 · capture black-out on',
      ),
      _SecurityCard(
        icon: Icons.circle,
        iconColor: SbColors.green,
        iconBg: SbColors.greenBg,
        title: 'Session active',
        subtitle: 'Signed in as $signedInAs',
      ),
    ];

    if (columns == 1) {
      return Column(
        children: <Widget>[
          for (int i = 0; i < cards.length; i++) ...<Widget>[
            cards[i],
            if (i < cards.length - 1) const SizedBox(height: 10),
          ],
        ],
      );
    }
    // IntrinsicHeight bounds the row's (otherwise scroll-unbounded) height so
    // the cards can stretch to an equal height.
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          for (int i = 0; i < cards.length; i++) ...<Widget>[
            Expanded(child: cards[i]),
            if (i < cards.length - 1) const SizedBox(width: 10),
          ],
        ],
      ),
    );
  }
}

class _SecurityCard extends StatelessWidget {
  const _SecurityCard({
    required this.icon,
    required this.iconColor,
    required this.iconBg,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final Color iconColor;
  final Color iconBg;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: SbColors.white,
        borderRadius: BorderRadius.circular(13),
        border: Border.all(color: SbColors.borderSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 34,
            height: 34,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: iconBg,
              borderRadius: BorderRadius.circular(9),
            ),
            child: Icon(icon, size: 18, color: iconColor),
          ),
          const SizedBox(height: 8),
          Text(
            title,
            style: const TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 13.5,
              fontWeight: FontWeight.w800,
              color: SbColors.ink,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            subtitle,
            style: const TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 12,
              height: 1.45,
              color: SbColors.ink4,
            ),
          ),
        ],
      ),
    );
  }
}

class _Footer extends StatelessWidget {
  const _Footer({required this.onSignOut});

  final VoidCallback onSignOut;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        const Expanded(
          child: Text(
            "Lessons open straight from the student portal — there's nothing to "
            'set up here.',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 12,
              color: SbColors.ink6,
            ),
          ),
        ),
        const SizedBox(width: 10),
        OutlinedButton(
          onPressed: onSignOut,
          style: OutlinedButton.styleFrom(
            foregroundColor: SbColors.ink4,
            side: const BorderSide(color: SbColors.border),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(9),
            ),
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          ),
          child: const Text(
            'Sign out',
            style: TextStyle(
              fontFamily: SbFonts.sans,
              fontSize: 13,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ],
    );
  }
}
