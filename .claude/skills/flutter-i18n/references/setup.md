# i18n Setup

## Enabling flutter_localizations in pubspec.yaml

Add the localization dependencies and the `intl` package to your project.

```yaml
# pubspec.yaml
dependencies:
  flutter:
    sdk: flutter
  flutter_localizations:
    sdk: flutter
  intl: any # version is managed by flutter_localizations

flutter:
  generate: true # required for gen-l10n code generation
```

Run `flutter pub get` after updating.

---

## l10n.yaml Configuration

Create an `l10n.yaml` file at the project root. This tells `gen-l10n` where to find your ARB files, which is the template, and where to output the generated Dart code.

```yaml
# l10n.yaml
arb-dir: lib/l10n
template-arb-file: app_en.arb
output-localization-file: app_localizations.dart
output-class: AppLocalizations
output-dir: lib/l10n/generated
synthetic-package: false
nullable-getter: false
```

### Key options

| Option | Purpose |
|---|---|
| `arb-dir` | Directory containing `.arb` files |
| `template-arb-file` | The source-of-truth ARB file (English by convention) |
| `output-localization-file` | Name of the generated Dart entry file |
| `output-class` | The class name you import in widgets |
| `synthetic-package` | `false` places output in your `lib/` tree so it is versioned and visible |
| `nullable-getter` | `false` makes getters non-nullable, so you do not need `!` on every call |
| `output-dir` | Where generated files are written |

---

## MaterialApp Localization Delegates

Wire the generated localizations into `MaterialApp` (or `CupertinoApp`).

```dart
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'l10n/generated/app_localizations.dart';

class MyApp extends StatelessWidget {
  const MyApp({super.key, required this.locale});

  final Locale locale;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      locale: locale,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: const HomePage(),
    );
  }
}
```

### What Each Delegate Does

| Delegate | Responsibility |
|---|---|
| `AppLocalizations.delegate` | Your translated strings |
| `GlobalMaterialLocalizations.delegate` | Material widget labels (e.g., date picker "OK"/"CANCEL") |
| `GlobalWidgetsLocalizations.delegate` | Text direction (LTR/RTL) |
| `GlobalCupertinoLocalizations.delegate` | Cupertino widget labels |

---

## supportedLocales Configuration

Define every locale your app supports. The order matters -- the first locale is the default fallback.

```dart
// This list is auto-generated in AppLocalizations.supportedLocales
// based on the ARB files present in arb-dir. You typically just reference it:
supportedLocales: AppLocalizations.supportedLocales,

// If you need to restrict or reorder, you can provide your own list:
supportedLocales: const [
  Locale('en'),        // English (default fallback)
  Locale('ar'),        // Arabic
  Locale('es'),        // Spanish
  Locale('fr'),        // French
  Locale('ja'),        // Japanese
  Locale('zh', 'CN'),  // Chinese (Simplified)
  Locale('zh', 'TW'),  // Chinese (Traditional)
],
```

---

## flutter gen-l10n Command

Run code generation after adding or modifying ARB files.

```bash
# Generate localization code
flutter gen-l10n

# Or run it as part of the full build
flutter build apk  # gen-l10n runs automatically when flutter.generate is true
```

The generated output includes:

- `app_localizations.dart` -- the `AppLocalizations` class with a static `of(context)` accessor
- `app_localizations_en.dart`, `app_localizations_ar.dart`, etc. -- per-locale implementations
- A `delegate` and `supportedLocales` list

---

## AppLocalizations Usage in Widgets

Access translated strings through the `AppLocalizations` instance from the nearest `BuildContext`.

### Direct usage

```dart
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Scaffold(
      appBar: AppBar(title: Text(l10n.homeTitle)),
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(l10n.welcomeMessage('Ali')),
            const SizedBox(height: 16),
            Text(l10n.itemCount(5)),
          ],
        ),
      ),
    );
  }
}
```

### Extension for cleaner access

Create a convenience extension so you can write `context.l10n` instead of `AppLocalizations.of(context)`.

```dart
import 'package:flutter/widgets.dart';

import 'l10n/generated/app_localizations.dart';

extension AppLocalizationsX on BuildContext {
  AppLocalizations get l10n => AppLocalizations.of(this);
}
```

Usage:

```dart
class ProfilePage extends StatelessWidget {
  const ProfilePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(context.l10n.profileTitle)),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Text(context.l10n.profileDescription),
      ),
    );
  }
}
```

---

## Locale Switching at Runtime

Use a `ValueNotifier<Locale>` (or a state management solution like Riverpod) to allow users to change the locale at runtime.

### With ValueNotifier

```dart
class LocaleController extends ValueNotifier<Locale> {
  LocaleController(super.initialLocale);

  void changeLocale(Locale newLocale) {
    value = newLocale;
  }
}

class MyApp extends StatelessWidget {
  const MyApp({super.key, required this.localeController});

  final LocaleController localeController;

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<Locale>(
      valueListenable: localeController,
      builder: (context, locale, _) {
        return MaterialApp(
          locale: locale,
          localizationsDelegates: const [
            AppLocalizations.delegate,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          supportedLocales: AppLocalizations.supportedLocales,
          home: const HomePage(),
        );
      },
    );
  }
}
```

### Language Selection Widget

```dart
class LanguageSelector extends StatelessWidget {
  const LanguageSelector({super.key, required this.localeController});

  final LocaleController localeController;

  static const _supportedLocales = <Locale, String>{
    Locale('en'): 'English',
    Locale('ar'): 'العربية',
    Locale('es'): 'Español',
    Locale('fr'): 'Français',
    Locale('ja'): '日本語',
  };

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<Locale>(
      valueListenable: localeController,
      builder: (context, currentLocale, _) {
        return DropdownButton<Locale>(
          value: currentLocale,
          onChanged: (locale) {
            if (locale != null) {
              localeController.changeLocale(locale);
            }
          },
          items: [
            for (final entry in _supportedLocales.entries)
              DropdownMenuItem(
                value: entry.key,
                child: Text(entry.value),
              ),
          ],
        );
      },
    );
  }
}
```

### With Riverpod

```dart
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

final localeProvider = StateNotifierProvider<LocaleNotifier, Locale>((ref) {
  return LocaleNotifier();
});

class LocaleNotifier extends StateNotifier<Locale> {
  LocaleNotifier() : super(const Locale('en'));

  static const _key = 'app_locale';

  Future<void> initialize() async {
    final prefs = await SharedPreferences.getInstance();
    final languageCode = prefs.getString(_key);
    if (languageCode != null) {
      state = Locale(languageCode);
    }
  }

  Future<void> changeLocale(Locale locale) async {
    state = locale;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, locale.languageCode);
  }
}

// In main.dart
class MyApp extends ConsumerWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final locale = ref.watch(localeProvider);

    return MaterialApp(
      locale: locale,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: const HomePage(),
    );
  }
}
```

---

## Persisting Locale Preference

Store the user's chosen locale in `SharedPreferences` so it survives app restarts.

```dart
import 'package:shared_preferences/shared_preferences.dart';

class LocaleRepository {
  const LocaleRepository(this._prefs);

  final SharedPreferences _prefs;

  static const _languageCodeKey = 'locale_language_code';
  static const _countryCodeKey = 'locale_country_code';

  Locale? getSavedLocale() {
    final languageCode = _prefs.getString(_languageCodeKey);
    if (languageCode == null) return null;
    final countryCode = _prefs.getString(_countryCodeKey);
    return Locale(languageCode, countryCode);
  }

  Future<void> saveLocale(Locale locale) async {
    await _prefs.setString(_languageCodeKey, locale.languageCode);
    if (locale.countryCode != null) {
      await _prefs.setString(_countryCodeKey, locale.countryCode!);
    } else {
      await _prefs.remove(_countryCodeKey);
    }
  }

  Future<void> clearLocale() async {
    await _prefs.remove(_languageCodeKey);
    await _prefs.remove(_countryCodeKey);
  }
}
```

### Loading saved locale at startup

```dart
Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final prefs = await SharedPreferences.getInstance();
  final localeRepo = LocaleRepository(prefs);
  final savedLocale = localeRepo.getSavedLocale() ?? const Locale('en');

  final localeController = LocaleController(savedLocale);

  runApp(MyApp(localeController: localeController));
}
```

---

## Testing with Specific Locales

### Widget test with a specific locale

```dart
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:my_app/l10n/generated/app_localizations.dart';
import 'package:my_app/pages/home_page.dart';

void main() {
  Widget buildTestableWidget({Locale locale = const Locale('en')}) {
    return MaterialApp(
      locale: locale,
      localizationsDelegates: const [
        AppLocalizations.delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: AppLocalizations.supportedLocales,
      home: const HomePage(),
    );
  }

  testWidgets('displays English title', (tester) async {
    await tester.pumpWidget(buildTestableWidget());
    await tester.pumpAndSettle();

    expect(find.text('Home'), findsOneWidget);
  });

  testWidgets('displays Arabic title in RTL', (tester) async {
    await tester.pumpWidget(buildTestableWidget(locale: const Locale('ar')));
    await tester.pumpAndSettle();

    expect(find.text('الرئيسية'), findsOneWidget);
  });

  testWidgets('displays Spanish title', (tester) async {
    await tester.pumpWidget(buildTestableWidget(locale: const Locale('es')));
    await tester.pumpAndSettle();

    expect(find.text('Inicio'), findsOneWidget);
  });
}
```

### Testing locale switching

```dart
testWidgets('switches locale at runtime', (tester) async {
  final localeController = LocaleController(const Locale('en'));

  await tester.pumpWidget(
    ValueListenableBuilder<Locale>(
      valueListenable: localeController,
      builder: (context, locale, _) {
        return MaterialApp(
          locale: locale,
          localizationsDelegates: const [
            AppLocalizations.delegate,
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          supportedLocales: AppLocalizations.supportedLocales,
          home: const HomePage(),
        );
      },
    ),
  );
  await tester.pumpAndSettle();

  expect(find.text('Home'), findsOneWidget);

  localeController.changeLocale(const Locale('es'));
  await tester.pumpAndSettle();

  expect(find.text('Inicio'), findsOneWidget);
});
```

---

## Fallback Locale Strategy

Flutter resolves the device locale against your supported locales using the following priority:

1. Exact match (`en_US` matches `en_US`)
2. Language match (`en_US` matches `en` if no `en_US` is provided)
3. First locale in `supportedLocales` (the ultimate fallback)

### Custom locale resolution

Override the default resolution logic with `localeResolutionCallback` when you need special behavior.

```dart
MaterialApp(
  localizationsDelegates: const [
    AppLocalizations.delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ],
  supportedLocales: AppLocalizations.supportedLocales,
  localeResolutionCallback: (deviceLocale, supportedLocales) {
    // 1. Check for exact match
    for (final supported in supportedLocales) {
      if (supported.languageCode == deviceLocale?.languageCode &&
          supported.countryCode == deviceLocale?.countryCode) {
        return supported;
      }
    }

    // 2. Check for language-only match
    for (final supported in supportedLocales) {
      if (supported.languageCode == deviceLocale?.languageCode) {
        return supported;
      }
    }

    // 3. Map similar languages (e.g., Norwegian variants)
    final languageFallbacks = <String, String>{
      'nb': 'no', // Norwegian Bokmal -> Norwegian
      'nn': 'no', // Norwegian Nynorsk -> Norwegian
      'pt': 'pt', // Portuguese variants -> Portuguese
    };

    final fallbackCode = languageFallbacks[deviceLocale?.languageCode];
    if (fallbackCode != null) {
      for (final supported in supportedLocales) {
        if (supported.languageCode == fallbackCode) {
          return supported;
        }
      }
    }

    // 4. Return the first supported locale as the ultimate fallback
    return supportedLocales.first;
  },
  home: const HomePage(),
)
```

### Fallback strings in ARB

When adding a new key to the template ARB, make sure all other ARB files get a translation. If you cannot translate immediately, the `gen-l10n` tool will use the template ARB value as the fallback for any locale missing that key. This is only safe if `nullable-getter` is `false` in `l10n.yaml`.
