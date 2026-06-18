# RTL and Advanced i18n

## Directionality Widget

The `Directionality` widget sets the text direction for its entire subtree. Flutter sets this automatically based on the current locale when you use `GlobalWidgetsLocalizations.delegate`, but you can override it explicitly.

```dart
class DirectionalityExample extends StatelessWidget {
  const DirectionalityExample({super.key});

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(title: const Text('RTL Preview')),
        body: const Padding(
          padding: EdgeInsetsDirectional.only(start: 16, end: 8),
          child: Text('This text and its padding respect RTL direction.'),
        ),
      ),
    );
  }
}
```

### Reading the current direction

```dart
class DirectionAwareWidget extends StatelessWidget {
  const DirectionAwareWidget({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final direction = Directionality.of(context);
    final isRtl = direction == TextDirection.rtl;

    return Container(
      decoration: BoxDecoration(
        border: BorderDirectional(
          start: BorderSide(
            color: Theme.of(context).colorScheme.primary,
            width: isRtl ? 4 : 2,
          ),
        ),
      ),
      child: child,
    );
  }
}
```

---

## TextDirection Handling

Flutter provides two text direction values: `TextDirection.ltr` and `TextDirection.rtl`. The direction flows from `Directionality` and affects layout, alignment, and painting.

### Forcing direction for specific content

Some content (like code blocks, URLs, or brand names) should always be LTR regardless of the app's locale.

```dart
class CodeSnippet extends StatelessWidget {
  const CodeSnippet({super.key, required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Directionality(
        textDirection: TextDirection.ltr, // Code is always LTR
        child: SelectableText(
          code,
          style: const TextStyle(fontFamily: 'monospace', fontSize: 14),
        ),
      ),
    );
  }
}
```

### Direction-aware widget selection

```dart
class DirectionalIcon extends StatelessWidget {
  const DirectionalIcon({
    super.key,
    required this.ltrIcon,
    required this.rtlIcon,
    this.size,
    this.color,
  });

  final IconData ltrIcon;
  final IconData rtlIcon;
  final double? size;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final isRtl = Directionality.of(context) == TextDirection.rtl;

    return Icon(
      isRtl ? rtlIcon : ltrIcon,
      size: size,
      color: color,
    );
  }
}

// Usage:
const DirectionalIcon(
  ltrIcon: Icons.arrow_forward,
  rtlIcon: Icons.arrow_back,
  size: 24,
)
```

---

## RTL-Aware Padding and Margins

Use `EdgeInsetsDirectional` and `AlignmentDirectional` instead of their non-directional counterparts. `start` maps to `left` in LTR and `right` in RTL. `end` maps to the opposite.

### EdgeInsetsDirectional

```dart
class RtlAwareCard extends StatelessWidget {
  const RtlAwareCard({super.key, required this.title, required this.subtitle});

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        // start/end flip automatically in RTL
        padding: const EdgeInsetsDirectional.only(
          start: 16,
          end: 8,
          top: 12,
          bottom: 12,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start, // Also use CrossAxisAlignment.start -- it respects direction
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(subtitle, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}
```

### AlignmentDirectional

```dart
class DirectionalBadge extends StatelessWidget {
  const DirectionalBadge({super.key, required this.count, required this.child});

  final int count;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Stack(
      clipBehavior: Clip.none,
      children: [
        child,
        if (count > 0)
          PositionedDirectional(
            top: -4,
            end: -4, // Flips to "left" in RTL
            child: Container(
              padding: const EdgeInsets.all(4),
              decoration: BoxDecoration(
                color: Theme.of(context).colorScheme.error,
                shape: BoxShape.circle,
              ),
              child: Text(
                '$count',
                style: TextStyle(
                  color: Theme.of(context).colorScheme.onError,
                  fontSize: 10,
                ),
              ),
            ),
          ),
      ],
    );
  }
}
```

### Directional margin on a container

```dart
class IndentedSection extends StatelessWidget {
  const IndentedSection({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsetsDirectional.only(start: 24),
      padding: const EdgeInsetsDirectional.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        border: BorderDirectional(
          start: BorderSide(
            color: Theme.of(context).colorScheme.outlineVariant,
            width: 2,
          ),
        ),
      ),
      child: child,
    );
  }
}
```

---

## RTL-Aware Icons

Icons that imply a direction (arrows, chevrons, forward/back) must be mirrored in RTL layouts.

### Icons that MUST flip in RTL

| LTR Icon | RTL Equivalent | Semantic Meaning |
|---|---|---|
| `Icons.arrow_forward` | `Icons.arrow_back` | Navigate forward |
| `Icons.arrow_back` | `Icons.arrow_forward` | Navigate back |
| `Icons.chevron_right` | `Icons.chevron_left` | Expand / drill in |
| `Icons.chevron_left` | `Icons.chevron_right` | Collapse / go back |
| `Icons.navigate_next` | `Icons.navigate_before` | Next page |
| `Icons.navigate_before` | `Icons.navigate_next` | Previous page |
| `Icons.send` | mirrored via Transform | Send (arrow points to the end) |
| `Icons.reply` | mirrored via Transform | Reply |

### Icons that MUST NOT flip

- `Icons.check` -- universal
- `Icons.close` -- universal
- `Icons.add` / `Icons.remove` -- universal
- `Icons.play_arrow` -- media convention (always points right)
- `Icons.redo` / `Icons.undo` -- spatial, not directional

### Reusable directional icon helper

```dart
class AdaptiveDirectionalIcon extends StatelessWidget {
  const AdaptiveDirectionalIcon({
    super.key,
    required this.icon,
    this.size = 24,
    this.color,
    this.mirrorInRtl = true,
  });

  final IconData icon;
  final double size;
  final Color? color;
  final bool mirrorInRtl;

  @override
  Widget build(BuildContext context) {
    final isRtl = Directionality.of(context) == TextDirection.rtl;

    Widget iconWidget = Icon(icon, size: size, color: color);

    if (mirrorInRtl && isRtl) {
      iconWidget = Transform.flip(flipX: true, child: iconWidget);
    }

    return iconWidget;
  }
}
```

### RTL-aware back button

```dart
class AdaptiveBackButton extends StatelessWidget {
  const AdaptiveBackButton({super.key, this.onPressed});

  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    final isRtl = Directionality.of(context) == TextDirection.rtl;

    return IconButton(
      icon: Icon(isRtl ? Icons.arrow_forward : Icons.arrow_back),
      onPressed: onPressed ?? () => Navigator.of(context).maybePop(),
      tooltip: MaterialLocalizations.of(context).backButtonTooltip,
    );
  }
}
```

---

## Date Formatting (intl DateFormat)

Always pass an explicit locale to `DateFormat` so it formats correctly regardless of the device locale.

### Common date formats

```dart
import 'package:intl/intl.dart';

class DateFormatExamples {
  const DateFormatExamples._();

  /// "Jun 15, 2025" (en) / "١٥ يونيو ٢٠٢٥" (ar)
  static String mediumDate(DateTime date, String locale) {
    return DateFormat.yMMMd(locale).format(date);
  }

  /// "Sunday, June 15, 2025" (en) / "الأحد، ١٥ يونيو ٢٠٢٥" (ar)
  static String fullDate(DateTime date, String locale) {
    return DateFormat.yMMMMEEEEd(locale).format(date);
  }

  /// "2:30 PM" (en) / "٢:٣٠ م" (ar)
  static String time(DateTime date, String locale) {
    return DateFormat.jm(locale).format(date);
  }

  /// "Jun 15, 2025 2:30 PM" (en)
  static String dateTime(DateTime date, String locale) {
    return DateFormat.yMMMd(locale).add_jm().format(date);
  }

  /// "06/15/2025" (en) / "١٥/٠٦/٢٠٢٥" (ar)
  static String shortDate(DateTime date, String locale) {
    return DateFormat.yMd(locale).format(date);
  }

  /// Custom pattern: "15-Jun-2025"
  static String customPattern(DateTime date, String locale) {
    return DateFormat('dd-MMM-yyyy', locale).format(date);
  }
}
```

### Using DateFormat in a widget

```dart
class EventDateWidget extends StatelessWidget {
  const EventDateWidget({super.key, required this.date});

  final DateTime date;

  @override
  Widget build(BuildContext context) {
    final locale = Localizations.localeOf(context).toString();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          DateFormat.yMMMMEEEEd(locale).format(date),
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 4),
        Text(
          DateFormat.jm(locale).format(date),
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ],
    );
  }
}
```

### DateFormat pattern reference

| Symbol | Meaning | Example (en) |
|---|---|---|
| `y` | Year | 2025 |
| `M` | Month number | 6 |
| `MM` | Month zero-padded | 06 |
| `MMM` | Month abbreviation | Jun |
| `MMMM` | Month full name | June |
| `d` | Day | 15 |
| `E` | Day abbreviation | Sun |
| `EEEE` | Day full name | Sunday |
| `H` | Hour (24h) | 14 |
| `h` | Hour (12h) | 2 |
| `m` | Minute | 30 |
| `s` | Second | 45 |
| `a` | AM/PM | PM |
| `j` | Locale-preferred hour | 2 PM (en), 14 (fr) |

---

## Number Formatting (intl NumberFormat)

Always pass an explicit locale to `NumberFormat`.

### Common number formats

```dart
import 'package:intl/intl.dart';

class NumberFormatExamples {
  const NumberFormatExamples._();

  /// "1,234.56" (en) / "١٬٢٣٤٫٥٦" (ar)
  static String decimal(num value, String locale) {
    return NumberFormat.decimalPattern(locale).format(value);
  }

  /// "1,235" (en) / "١٬٢٣٥" (ar)
  static String integer(num value, String locale) {
    return NumberFormat('#,##0', locale).format(value);
  }

  /// "12%" (en) / "١٢٪" (ar)
  static String percent(double value, String locale) {
    return NumberFormat.percentPattern(locale).format(value);
  }

  /// "1.2K" (en) / "١٫٢ ألف" (ar)
  static String compact(num value, String locale) {
    return NumberFormat.compact(locale: locale).format(value);
  }

  /// "1.2 thousand" (en) / "١٫٢ ألف" (ar)
  static String compactLong(num value, String locale) {
    return NumberFormat.compactLong(locale: locale).format(value);
  }

  /// Fixed decimal places: "3.14"
  static String fixedDecimal(double value, String locale, {int places = 2}) {
    return NumberFormat.decimalPatternDigits(
      locale: locale,
      decimalDigits: places,
    ).format(value);
  }
}
```

### Using NumberFormat in a widget

```dart
class StatisticTile extends StatelessWidget {
  const StatisticTile({
    super.key,
    required this.label,
    required this.value,
  });

  final String label;
  final num value;

  @override
  Widget build(BuildContext context) {
    final locale = Localizations.localeOf(context).toString();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              NumberFormat.compact(locale: locale).format(value),
              style: Theme.of(context).textTheme.headlineMedium,
            ),
            const SizedBox(height: 4),
            Text(label, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}
```

---

## Currency Formatting

Use `NumberFormat.currency` or `NumberFormat.simpleCurrency` with an explicit locale and currency code.

### Currency formatter utility

```dart
import 'package:intl/intl.dart';

class CurrencyFormatter {
  const CurrencyFormatter._();

  /// "$1,234.56" (en/USD) / "١٬٢٣٤٫٥٦ ر.س.‏" (ar/SAR)
  static String format(
    double amount, {
    required String locale,
    required String currencyCode,
  }) {
    return NumberFormat.currency(
      locale: locale,
      name: currencyCode,
    ).format(amount);
  }

  /// Uses the locale's default currency symbol
  static String simple(double amount, {required String locale}) {
    return NumberFormat.simpleCurrency(locale: locale).format(amount);
  }

  /// Custom symbol: "€ 1,234.56"
  static String withSymbol(
    double amount, {
    required String locale,
    required String symbol,
    int decimalDigits = 2,
  }) {
    return NumberFormat.currency(
      locale: locale,
      symbol: symbol,
      decimalDigits: decimalDigits,
    ).format(amount);
  }

  /// No-decimal currency: "¥1,235" for Japanese Yen
  static String zeroDecimal(
    double amount, {
    required String locale,
    required String currencyCode,
  }) {
    return NumberFormat.currency(
      locale: locale,
      name: currencyCode,
      decimalDigits: 0,
    ).format(amount);
  }
}
```

### Currency display in a widget

```dart
class PriceTag extends StatelessWidget {
  const PriceTag({
    super.key,
    required this.amount,
    required this.currencyCode,
    this.originalAmount,
  });

  final double amount;
  final String currencyCode;
  final double? originalAmount;

  @override
  Widget build(BuildContext context) {
    final locale = Localizations.localeOf(context).toString();
    final formatter = NumberFormat.currency(
      locale: locale,
      name: currencyCode,
    );

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (originalAmount != null && originalAmount != amount) ...[
          Text(
            formatter.format(originalAmount),
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  decoration: TextDecoration.lineThrough,
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
          ),
          const SizedBox(width: 8),
        ],
        Text(
          formatter.format(amount),
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
                color: Theme.of(context).colorScheme.primary,
              ),
        ),
      ],
    );
  }
}
```

---

## Relative Time Formatting

Flutter does not ship a built-in relative time formatter, but you can build one using `intl` and ARB messages.

### Relative time utility

```dart
import 'package:flutter/widgets.dart';

import '../l10n/generated/app_localizations.dart';

class RelativeTimeFormatter {
  const RelativeTimeFormatter._();

  /// Formats a duration as a human-readable relative time string.
  /// Requires the following ARB keys:
  ///   relativeTimeNow, relativeTimeMinutes, relativeTimeHours,
  ///   relativeTimeDays, relativeTimeWeeks, relativeTimeMonths, relativeTimeYears
  static String format(BuildContext context, DateTime dateTime) {
    final l10n = AppLocalizations.of(context);
    final now = DateTime.now();
    final difference = now.difference(dateTime);

    if (difference.isNegative) {
      return _formatFuture(l10n, difference.abs());
    }

    return _formatPast(l10n, difference);
  }

  static String _formatPast(AppLocalizations l10n, Duration difference) {
    if (difference.inMinutes < 1) return l10n.relativeTimeNow;
    if (difference.inMinutes < 60) return l10n.relativeTimeMinutesAgo(difference.inMinutes);
    if (difference.inHours < 24) return l10n.relativeTimeHoursAgo(difference.inHours);
    if (difference.inDays < 7) return l10n.relativeTimeDaysAgo(difference.inDays);
    if (difference.inDays < 30) return l10n.relativeTimeWeeksAgo(difference.inDays ~/ 7);
    if (difference.inDays < 365) return l10n.relativeTimeMonthsAgo(difference.inDays ~/ 30);
    return l10n.relativeTimeYearsAgo(difference.inDays ~/ 365);
  }

  static String _formatFuture(AppLocalizations l10n, Duration difference) {
    if (difference.inMinutes < 1) return l10n.relativeTimeNow;
    if (difference.inMinutes < 60) return l10n.relativeTimeMinutesFromNow(difference.inMinutes);
    if (difference.inHours < 24) return l10n.relativeTimeHoursFromNow(difference.inHours);
    if (difference.inDays < 7) return l10n.relativeTimeDaysFromNow(difference.inDays);
    if (difference.inDays < 30) return l10n.relativeTimeWeeksFromNow(difference.inDays ~/ 7);
    if (difference.inDays < 365) return l10n.relativeTimeMonthsFromNow(difference.inDays ~/ 30);
    return l10n.relativeTimeYearsFromNow(difference.inDays ~/ 365);
  }
}
```

### Corresponding ARB entries

```json
{
  "relativeTimeNow": "Just now",
  "@relativeTimeNow": {
    "description": "Relative time label for something that happened moments ago"
  },
  "relativeTimeMinutesAgo": "{count, plural, =1{1 minute ago} other{{count} minutes ago}}",
  "@relativeTimeMinutesAgo": {
    "description": "Relative time for minutes in the past",
    "placeholders": { "count": { "type": "int", "example": "5" } }
  },
  "relativeTimeHoursAgo": "{count, plural, =1{1 hour ago} other{{count} hours ago}}",
  "@relativeTimeHoursAgo": {
    "description": "Relative time for hours in the past",
    "placeholders": { "count": { "type": "int", "example": "3" } }
  },
  "relativeTimeDaysAgo": "{count, plural, =1{Yesterday} other{{count} days ago}}",
  "@relativeTimeDaysAgo": {
    "description": "Relative time for days in the past",
    "placeholders": { "count": { "type": "int", "example": "2" } }
  },
  "relativeTimeWeeksAgo": "{count, plural, =1{1 week ago} other{{count} weeks ago}}",
  "@relativeTimeWeeksAgo": {
    "description": "Relative time for weeks in the past",
    "placeholders": { "count": { "type": "int", "example": "2" } }
  },
  "relativeTimeMonthsAgo": "{count, plural, =1{1 month ago} other{{count} months ago}}",
  "@relativeTimeMonthsAgo": {
    "description": "Relative time for months in the past",
    "placeholders": { "count": { "type": "int", "example": "3" } }
  },
  "relativeTimeYearsAgo": "{count, plural, =1{1 year ago} other{{count} years ago}}",
  "@relativeTimeYearsAgo": {
    "description": "Relative time for years in the past",
    "placeholders": { "count": { "type": "int", "example": "2" } }
  },
  "relativeTimeMinutesFromNow": "{count, plural, =1{In 1 minute} other{In {count} minutes}}",
  "@relativeTimeMinutesFromNow": {
    "description": "Relative time for minutes in the future",
    "placeholders": { "count": { "type": "int", "example": "5" } }
  },
  "relativeTimeHoursFromNow": "{count, plural, =1{In 1 hour} other{In {count} hours}}",
  "@relativeTimeHoursFromNow": {
    "description": "Relative time for hours in the future",
    "placeholders": { "count": { "type": "int", "example": "3" } }
  },
  "relativeTimeDaysFromNow": "{count, plural, =1{Tomorrow} other{In {count} days}}",
  "@relativeTimeDaysFromNow": {
    "description": "Relative time for days in the future",
    "placeholders": { "count": { "type": "int", "example": "2" } }
  },
  "relativeTimeWeeksFromNow": "{count, plural, =1{In 1 week} other{In {count} weeks}}",
  "@relativeTimeWeeksFromNow": {
    "description": "Relative time for weeks in the future",
    "placeholders": { "count": { "type": "int", "example": "2" } }
  },
  "relativeTimeMonthsFromNow": "{count, plural, =1{In 1 month} other{In {count} months}}",
  "@relativeTimeMonthsFromNow": {
    "description": "Relative time for months in the future",
    "placeholders": { "count": { "type": "int", "example": "3" } }
  },
  "relativeTimeYearsFromNow": "{count, plural, =1{In 1 year} other{In {count} years}}",
  "@relativeTimeYearsFromNow": {
    "description": "Relative time for years in the future",
    "placeholders": { "count": { "type": "int", "example": "2" } }
  }
}
```

### Usage in a widget

```dart
class ActivityTimestamp extends StatelessWidget {
  const ActivityTimestamp({super.key, required this.dateTime});

  final DateTime dateTime;

  @override
  Widget build(BuildContext context) {
    return Text(
      RelativeTimeFormatter.format(context, dateTime),
      style: Theme.of(context).textTheme.bodySmall?.copyWith(
            color: Theme.of(context).colorScheme.onSurfaceVariant,
          ),
    );
  }
}
```

---

## Testing RTL Layouts

### Force RTL in a widget test

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Widget buildRtlTestHarness(Widget child) {
    return MaterialApp(
      home: Directionality(
        textDirection: TextDirection.rtl,
        child: Scaffold(body: child),
      ),
    );
  }

  testWidgets('card has correct directional padding in RTL', (tester) async {
    await tester.pumpWidget(
      buildRtlTestHarness(
        const RtlAwareCard(title: 'Test', subtitle: 'Subtitle'),
      ),
    );

    // The Padding widget should have its start/end resolved for RTL
    final padding = tester.widget<Padding>(find.byType(Padding).first);
    final resolvedPadding = (padding.padding as EdgeInsetsDirectional).resolve(TextDirection.rtl);

    // In RTL, "start" becomes "right"
    expect(resolvedPadding.right, 16);
    expect(resolvedPadding.left, 8);
  });

  testWidgets('back button icon flips in RTL', (tester) async {
    await tester.pumpWidget(
      buildRtlTestHarness(
        const AdaptiveBackButton(),
      ),
    );

    // In RTL, back should show arrow_forward (pointing right = going back in RTL)
    final icon = tester.widget<Icon>(find.byType(Icon));
    expect(icon.icon, Icons.arrow_forward);
  });
}
```

### Golden test for RTL layout

```dart
testWidgets('profile page renders correctly in RTL', (tester) async {
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('ar'),
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: const ProfilePage(),
    ),
  );
  await tester.pumpAndSettle();

  await expectLater(
    find.byType(ProfilePage),
    matchesGoldenFile('goldens/profile_page_rtl.png'),
  );
});
```

### Testing both directions in a loop

```dart
void main() {
  for (final direction in TextDirection.values) {
    final directionName = direction == TextDirection.ltr ? 'LTR' : 'RTL';

    testWidgets('navigation bar renders correctly in $directionName', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Directionality(
            textDirection: direction,
            child: const AppNavigationBar(),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byType(NavigationBar), findsOneWidget);
    });
  }
}
```

---

## Bidirectional Text Handling

When LTR and RTL text appear in the same paragraph, Unicode bidirectional (bidi) algorithm rules apply. Sometimes you need explicit control.

### Embedding LTR text in an RTL context

Use Unicode bidi characters or the `intl` `Bidi` class to ensure correct rendering.

```dart
import 'package:intl/intl.dart' as intl;

class BidiTextExample extends StatelessWidget {
  const BidiTextExample({super.key, required this.productName, required this.price});

  final String productName; // English product name in Arabic UI
  final String price;

  @override
  Widget build(BuildContext context) {
    final isRtl = Directionality.of(context) == TextDirection.rtl;

    // Wrap LTR content with bidi embedding when inside an RTL context
    final displayName = isRtl
        ? intl.Bidi.enforceRtlInText(productName) // Or use LRE/PDF markers
        : productName;

    return Text('$displayName - $price');
  }
}
```

### Mixed-direction RichText

```dart
class MixedDirectionText extends StatelessWidget {
  const MixedDirectionText({
    super.key,
    required this.arabicLabel,
    required this.englishValue,
  });

  final String arabicLabel;
  final String englishValue;

  @override
  Widget build(BuildContext context) {
    return RichText(
      textDirection: TextDirection.rtl,
      text: TextSpan(
        style: Theme.of(context).textTheme.bodyMedium,
        children: [
          TextSpan(text: '$arabicLabel: '),
          TextSpan(
            text: englishValue,
            style: const TextStyle(
              // Explicitly set LTR text style if needed
              fontFeatures: [FontFeature.tabularFigures()],
            ),
          ),
        ],
      ),
    );
  }
}
```

### Handling user-entered mixed-direction text

```dart
class BidiTextField extends StatelessWidget {
  const BidiTextField({
    super.key,
    required this.controller,
    required this.label,
  });

  final TextEditingController controller;
  final String label;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      decoration: InputDecoration(labelText: label),
      // Let the system determine text direction based on content
      textDirection: null, // Null means auto-detect from first strong character
    );
  }
}
```

### Full RTL-aware page layout example

```dart
class OrderDetailPage extends StatelessWidget {
  const OrderDetailPage({super.key, required this.order});

  final Order order;

  @override
  Widget build(BuildContext context) {
    final locale = Localizations.localeOf(context).toString();
    final l10n = AppLocalizations.of(context);
    final currencyFormat = NumberFormat.currency(
      locale: locale,
      name: order.currencyCode,
    );
    final dateFormat = DateFormat.yMMMd(locale);

    return Scaffold(
      appBar: AppBar(
        leading: const AdaptiveBackButton(),
        title: Text(l10n.orderDetailTitle),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsetsDirectional.all(16),
          children: [
            // Order header
            Card(
              child: Padding(
                padding: const EdgeInsetsDirectional.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      l10n.orderNumber(order.id),
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    const SizedBox(height: 8),
                    Text(
                      l10n.orderDate(dateFormat.format(order.createdAt)),
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),

            // Order items
            for (final item in order.items) ...[
              ListTile(
                contentPadding: const EdgeInsetsDirectional.only(
                  start: 16,
                  end: 8,
                ),
                title: Text(item.name),
                subtitle: Text(l10n.quantity(item.quantity)),
                trailing: Text(
                  currencyFormat.format(item.totalPrice),
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
            ],

            const Divider(height: 32),

            // Total
            Padding(
              padding: const EdgeInsetsDirectional.symmetric(horizontal: 16),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    l10n.totalLabel,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text(
                    currencyFormat.format(order.total),
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          fontWeight: FontWeight.bold,
                          color: Theme.of(context).colorScheme.primary,
                        ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
```
