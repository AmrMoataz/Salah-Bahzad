# Material 3 Theming

## ThemeData with Material 3

Material 3 is enabled by default in Flutter 3.16+. Always construct themes using `ColorScheme` instead of the deprecated `primarySwatch`.

```dart
class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Material 3 App',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ThemeMode.system,
      home: const HomePage(),
    );
  }
}
```

---

## ColorScheme.fromSeed for Dynamic Color

`ColorScheme.fromSeed` generates a complete, harmonious Material 3 color scheme from a single seed color. This is the recommended approach for most apps.

```dart
class AppTheme {
  const AppTheme._();

  static const _seedColor = Color(0xFF1A73E8);

  static final light = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: _seedColor,
      brightness: Brightness.light,
    ),
    textTheme: _textTheme,
    inputDecorationTheme: _inputDecorationTheme,
    elevatedButtonTheme: _elevatedButtonTheme,
    cardTheme: _cardTheme,
  );

  static final dark = ThemeData(
    useMaterial3: true,
    colorScheme: ColorScheme.fromSeed(
      seedColor: _seedColor,
      brightness: Brightness.dark,
    ),
    textTheme: _textTheme,
    inputDecorationTheme: _inputDecorationTheme,
    elevatedButtonTheme: _elevatedButtonTheme,
    cardTheme: _cardTheme,
  );

  static const _textTheme = TextTheme(
    displayLarge: TextStyle(fontWeight: FontWeight.w400, letterSpacing: -0.25),
    headlineLarge: TextStyle(fontWeight: FontWeight.w400),
    titleLarge: TextStyle(fontWeight: FontWeight.w500),
    bodyLarge: TextStyle(fontWeight: FontWeight.w400, letterSpacing: 0.15),
    bodyMedium: TextStyle(fontWeight: FontWeight.w400, letterSpacing: 0.25),
    labelLarge: TextStyle(fontWeight: FontWeight.w500, letterSpacing: 0.1),
  );

  static final _inputDecorationTheme = InputDecorationTheme(
    filled: true,
    border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
  );

  static final _elevatedButtonTheme = ElevatedButtonThemeData(
    style: ElevatedButton.styleFrom(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    ),
  );

  static const _cardTheme = CardTheme(
    elevation: 1,
    margin: EdgeInsets.all(8),
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.all(Radius.circular(12)),
    ),
  );
}
```

---

## Custom ColorScheme

When you need full control over every color role, define the `ColorScheme` explicitly.

```dart
class BrandTheme {
  const BrandTheme._();

  static const _lightScheme = ColorScheme(
    brightness: Brightness.light,
    primary: Color(0xFF0D47A1),
    onPrimary: Color(0xFFFFFFFF),
    primaryContainer: Color(0xFFBBDEFB),
    onPrimaryContainer: Color(0xFF0D47A1),
    secondary: Color(0xFF00695C),
    onSecondary: Color(0xFFFFFFFF),
    secondaryContainer: Color(0xFFB2DFDB),
    onSecondaryContainer: Color(0xFF00695C),
    tertiary: Color(0xFF6A1B9A),
    onTertiary: Color(0xFFFFFFFF),
    tertiaryContainer: Color(0xFFE1BEE7),
    onTertiaryContainer: Color(0xFF6A1B9A),
    error: Color(0xFFB00020),
    onError: Color(0xFFFFFFFF),
    errorContainer: Color(0xFFFCDAD7),
    onErrorContainer: Color(0xFF410002),
    surface: Color(0xFFFFFBFE),
    onSurface: Color(0xFF1C1B1F),
    surfaceContainerHighest: Color(0xFFE7E0EC),
    onSurfaceVariant: Color(0xFF49454F),
    outline: Color(0xFF79747E),
    outlineVariant: Color(0xFFCAC4D0),
    shadow: Color(0xFF000000),
    scrim: Color(0xFF000000),
    inverseSurface: Color(0xFF313033),
    onInverseSurface: Color(0xFFF4EFF4),
    inversePrimary: Color(0xFF90CAF9),
  );

  static const _darkScheme = ColorScheme(
    brightness: Brightness.dark,
    primary: Color(0xFF90CAF9),
    onPrimary: Color(0xFF003C8F),
    primaryContainer: Color(0xFF0D47A1),
    onPrimaryContainer: Color(0xFFBBDEFB),
    secondary: Color(0xFF80CBC4),
    onSecondary: Color(0xFF003731),
    secondaryContainer: Color(0xFF00695C),
    onSecondaryContainer: Color(0xFFB2DFDB),
    tertiary: Color(0xFFCE93D8),
    onTertiary: Color(0xFF38006B),
    tertiaryContainer: Color(0xFF6A1B9A),
    onTertiaryContainer: Color(0xFFE1BEE7),
    error: Color(0xFFCF6679),
    onError: Color(0xFF690005),
    errorContainer: Color(0xFF93000A),
    onErrorContainer: Color(0xFFFCDAD7),
    surface: Color(0xFF1C1B1F),
    onSurface: Color(0xFFE6E1E5),
    surfaceContainerHighest: Color(0xFF49454F),
    onSurfaceVariant: Color(0xFFCAC4D0),
    outline: Color(0xFF938F99),
    outlineVariant: Color(0xFF49454F),
    shadow: Color(0xFF000000),
    scrim: Color(0xFF000000),
    inverseSurface: Color(0xFFE6E1E5),
    onInverseSurface: Color(0xFF313033),
    inversePrimary: Color(0xFF0D47A1),
  );

  static final light = ThemeData(
    useMaterial3: true,
    colorScheme: _lightScheme,
  );

  static final dark = ThemeData(
    useMaterial3: true,
    colorScheme: _darkScheme,
  );
}
```

---

## Typography (TextTheme Customization)

### Google Fonts Integration

```dart
class TypographyTheme {
  const TypographyTheme._();

  static TextTheme create() {
    final baseTheme = GoogleFonts.interTextTheme();

    return baseTheme.copyWith(
      displayLarge: GoogleFonts.playfairDisplay(
        fontSize: 57,
        fontWeight: FontWeight.w400,
        letterSpacing: -0.25,
      ),
      displayMedium: GoogleFonts.playfairDisplay(
        fontSize: 45,
        fontWeight: FontWeight.w400,
      ),
      displaySmall: GoogleFonts.playfairDisplay(
        fontSize: 36,
        fontWeight: FontWeight.w400,
      ),
      headlineLarge: GoogleFonts.inter(
        fontSize: 32,
        fontWeight: FontWeight.w600,
      ),
      headlineMedium: GoogleFonts.inter(
        fontSize: 28,
        fontWeight: FontWeight.w600,
      ),
      headlineSmall: GoogleFonts.inter(
        fontSize: 24,
        fontWeight: FontWeight.w600,
      ),
      titleLarge: GoogleFonts.inter(
        fontSize: 22,
        fontWeight: FontWeight.w500,
      ),
      titleMedium: GoogleFonts.inter(
        fontSize: 16,
        fontWeight: FontWeight.w500,
        letterSpacing: 0.15,
      ),
      titleSmall: GoogleFonts.inter(
        fontSize: 14,
        fontWeight: FontWeight.w500,
        letterSpacing: 0.1,
      ),
      bodyLarge: GoogleFonts.inter(
        fontSize: 16,
        fontWeight: FontWeight.w400,
        letterSpacing: 0.5,
      ),
      bodyMedium: GoogleFonts.inter(
        fontSize: 14,
        fontWeight: FontWeight.w400,
        letterSpacing: 0.25,
      ),
      bodySmall: GoogleFonts.inter(
        fontSize: 12,
        fontWeight: FontWeight.w400,
        letterSpacing: 0.4,
      ),
      labelLarge: GoogleFonts.inter(
        fontSize: 14,
        fontWeight: FontWeight.w500,
        letterSpacing: 0.1,
      ),
      labelMedium: GoogleFonts.inter(
        fontSize: 12,
        fontWeight: FontWeight.w500,
        letterSpacing: 0.5,
      ),
      labelSmall: GoogleFonts.inter(
        fontSize: 11,
        fontWeight: FontWeight.w500,
        letterSpacing: 0.5,
      ),
    );
  }
}
```

### Applying the Custom Typography

```dart
static final light = ThemeData(
  useMaterial3: true,
  colorScheme: ColorScheme.fromSeed(
    seedColor: _seedColor,
    brightness: Brightness.light,
  ),
  textTheme: TypographyTheme.create(),
);
```

---

## Component Themes

### ElevatedButtonThemeData

```dart
static final _elevatedButtonTheme = ElevatedButtonThemeData(
  style: ElevatedButton.styleFrom(
    foregroundColor: _lightScheme.onPrimary,
    backgroundColor: _lightScheme.primary,
    disabledForegroundColor: _lightScheme.onSurface.withValues(alpha: 0.38),
    disabledBackgroundColor: _lightScheme.onSurface.withValues(alpha: 0.12),
    padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
    minimumSize: const Size(64, 40),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    elevation: 1,
  ),
);
```

### FilledButtonThemeData

```dart
static final _filledButtonTheme = FilledButtonThemeData(
  style: FilledButton.styleFrom(
    padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    minimumSize: const Size(64, 40),
  ),
);
```

### OutlinedButtonThemeData

```dart
static final _outlinedButtonTheme = OutlinedButtonThemeData(
  style: OutlinedButton.styleFrom(
    padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
    side: BorderSide(color: _lightScheme.outline),
    minimumSize: const Size(64, 40),
  ),
);
```

### InputDecorationTheme

```dart
static final _inputDecorationTheme = InputDecorationTheme(
  filled: true,
  fillColor: _lightScheme.surfaceContainerHighest.withValues(alpha: 0.3),
  border: OutlineInputBorder(
    borderRadius: BorderRadius.circular(12),
    borderSide: BorderSide(color: _lightScheme.outline),
  ),
  enabledBorder: OutlineInputBorder(
    borderRadius: BorderRadius.circular(12),
    borderSide: BorderSide(color: _lightScheme.outline),
  ),
  focusedBorder: OutlineInputBorder(
    borderRadius: BorderRadius.circular(12),
    borderSide: BorderSide(color: _lightScheme.primary, width: 2),
  ),
  errorBorder: OutlineInputBorder(
    borderRadius: BorderRadius.circular(12),
    borderSide: BorderSide(color: _lightScheme.error),
  ),
  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
  floatingLabelBehavior: FloatingLabelBehavior.auto,
);
```

### AppBarTheme

```dart
static final _appBarTheme = AppBarTheme(
  centerTitle: false,
  elevation: 0,
  scrolledUnderElevation: 2,
  backgroundColor: _lightScheme.surface,
  foregroundColor: _lightScheme.onSurface,
  surfaceTintColor: _lightScheme.surfaceTint,
  titleTextStyle: TextStyle(
    fontSize: 22,
    fontWeight: FontWeight.w500,
    color: _lightScheme.onSurface,
  ),
);
```

### NavigationBarThemeData

```dart
static final _navigationBarTheme = NavigationBarThemeData(
  height: 80,
  elevation: 2,
  indicatorColor: _lightScheme.secondaryContainer,
  labelBehavior: NavigationDestinationLabelBehavior.onlyShowSelected,
);
```

### CardTheme

```dart
static const _cardTheme = CardTheme(
  elevation: 1,
  clipBehavior: Clip.antiAlias,
  margin: EdgeInsets.all(8),
  shape: RoundedRectangleBorder(
    borderRadius: BorderRadius.all(Radius.circular(12)),
  ),
);
```

### DialogTheme

```dart
static final _dialogTheme = DialogTheme(
  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(28)),
  elevation: 6,
  titleTextStyle: TextStyle(
    fontSize: 24,
    fontWeight: FontWeight.w500,
    color: _lightScheme.onSurface,
  ),
);
```

### ChipThemeData

```dart
static final _chipTheme = ChipThemeData(
  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
  side: BorderSide(color: _lightScheme.outline),
  labelStyle: TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w500,
    color: _lightScheme.onSurfaceVariant,
  ),
);
```

---

## Dark Theme Configuration

Build dark themes in parallel with light themes to ensure every component is covered.

```dart
class AppThemeComplete {
  const AppThemeComplete._();

  static const _seedColor = Color(0xFF6750A4);

  static final light = _buildTheme(Brightness.light);
  static final dark = _buildTheme(Brightness.dark);

  static ThemeData _buildTheme(Brightness brightness) {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: _seedColor,
      brightness: brightness,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: colorScheme.surface,
      appBarTheme: AppBarTheme(
        centerTitle: false,
        elevation: 0,
        scrolledUnderElevation: 2,
        backgroundColor: colorScheme.surface,
        foregroundColor: colorScheme.onSurface,
      ),
      cardTheme: const CardTheme(
        elevation: 1,
        clipBehavior: Clip.antiAlias,
        margin: EdgeInsets.all(8),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.all(Radius.circular(12)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        height: 80,
        elevation: 2,
        indicatorColor: colorScheme.secondaryContainer,
        labelBehavior: NavigationDestinationLabelBehavior.onlyShowSelected,
      ),
      dividerTheme: DividerThemeData(
        color: colorScheme.outlineVariant,
        thickness: 1,
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      ),
    );
  }
}
```

---

## Dynamic Theming (User-Selectable Themes)

Allow users to pick a theme color at runtime. Store their preference and rebuild the theme.

```dart
/// A set of predefined theme options the user can choose from.
enum ThemeOption {
  ocean(Color(0xFF1A73E8), 'Ocean'),
  forest(Color(0xFF2E7D32), 'Forest'),
  sunset(Color(0xFFE65100), 'Sunset'),
  lavender(Color(0xFF6750A4), 'Lavender'),
  rose(Color(0xFFC2185B), 'Rose');

  const ThemeOption(this.seedColor, this.label);

  final Color seedColor;
  final String label;
}

/// Manages the current theme state including color and mode.
class ThemeNotifier extends ChangeNotifier {
  ThemeNotifier({
    ThemeOption initialOption = ThemeOption.lavender,
    ThemeMode initialMode = ThemeMode.system,
  })  : _option = initialOption,
        _mode = initialMode;

  ThemeOption _option;
  ThemeMode _mode;

  ThemeOption get option => _option;
  ThemeMode get mode => _mode;

  ThemeData get lightTheme => _buildTheme(Brightness.light);
  ThemeData get darkTheme => _buildTheme(Brightness.dark);

  void setOption(ThemeOption option) {
    if (_option == option) return;
    _option = option;
    notifyListeners();
  }

  void setMode(ThemeMode mode) {
    if (_mode == mode) return;
    _mode = mode;
    notifyListeners();
  }

  ThemeData _buildTheme(Brightness brightness) {
    final colorScheme = ColorScheme.fromSeed(
      seedColor: _option.seedColor,
      brightness: brightness,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: colorScheme,
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        ),
      ),
    );
  }
}

/// Root widget that listens to theme changes.
class DynamicThemeApp extends StatelessWidget {
  const DynamicThemeApp({super.key, required this.themeNotifier});

  final ThemeNotifier themeNotifier;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: themeNotifier,
      builder: (context, _) {
        return MaterialApp(
          title: 'Dynamic Theme',
          theme: themeNotifier.lightTheme,
          darkTheme: themeNotifier.darkTheme,
          themeMode: themeNotifier.mode,
          home: const HomePage(),
        );
      },
    );
  }
}
```

### Theme Picker Widget

```dart
class ThemePicker extends StatelessWidget {
  const ThemePicker({super.key, required this.themeNotifier});

  final ThemeNotifier themeNotifier;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Color', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            for (final option in ThemeOption.values)
              _ColorCircle(
                color: option.seedColor,
                label: option.label,
                isSelected: themeNotifier.option == option,
                onTap: () => themeNotifier.setOption(option),
              ),
          ],
        ),
        const SizedBox(height: 24),
        Text('Mode', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 8),
        SegmentedButton<ThemeMode>(
          segments: const [
            ButtonSegment(value: ThemeMode.system, label: Text('System'), icon: Icon(Icons.brightness_auto)),
            ButtonSegment(value: ThemeMode.light, label: Text('Light'), icon: Icon(Icons.light_mode)),
            ButtonSegment(value: ThemeMode.dark, label: Text('Dark'), icon: Icon(Icons.dark_mode)),
          ],
          selected: {themeNotifier.mode},
          onSelectionChanged: (modes) => themeNotifier.setMode(modes.first),
        ),
      ],
    );
  }
}

class _ColorCircle extends StatelessWidget {
  const _ColorCircle({
    required this.color,
    required this.label,
    required this.isSelected,
    required this.onTap,
  });

  final Color color;
  final String label;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: label,
      child: GestureDetector(
        onTap: onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: color,
            shape: BoxShape.circle,
            border: isSelected
                ? Border.all(color: Theme.of(context).colorScheme.onSurface, width: 3)
                : null,
            boxShadow: isSelected
                ? [BoxShadow(color: color.withValues(alpha: 0.4), blurRadius: 8, spreadRadius: 1)]
                : null,
          ),
          child: isSelected
              ? const Icon(Icons.check, color: Colors.white, size: 20)
              : null,
        ),
      ),
    );
  }
}
```

---

## Theme Extensions for Custom Properties

Theme extensions let you add app-specific design tokens to the theme system. They participate in theme lerping for smooth transitions.

```dart
/// Custom spacing tokens attached to the theme.
class SpacingThemeExtension extends ThemeExtension<SpacingThemeExtension> {
  const SpacingThemeExtension({
    required this.small,
    required this.medium,
    required this.large,
    required this.cardPadding,
    required this.pagePadding,
  });

  final double small;
  final double medium;
  final double large;
  final EdgeInsets cardPadding;
  final EdgeInsets pagePadding;

  @override
  SpacingThemeExtension copyWith({
    double? small,
    double? medium,
    double? large,
    EdgeInsets? cardPadding,
    EdgeInsets? pagePadding,
  }) {
    return SpacingThemeExtension(
      small: small ?? this.small,
      medium: medium ?? this.medium,
      large: large ?? this.large,
      cardPadding: cardPadding ?? this.cardPadding,
      pagePadding: pagePadding ?? this.pagePadding,
    );
  }

  @override
  SpacingThemeExtension lerp(covariant SpacingThemeExtension? other, double t) {
    if (other == null) return this;
    return SpacingThemeExtension(
      small: lerpDouble(small, other.small, t) ?? small,
      medium: lerpDouble(medium, other.medium, t) ?? medium,
      large: lerpDouble(large, other.large, t) ?? large,
      cardPadding: EdgeInsets.lerp(cardPadding, other.cardPadding, t) ?? cardPadding,
      pagePadding: EdgeInsets.lerp(pagePadding, other.pagePadding, t) ?? pagePadding,
    );
  }

  static const standard = SpacingThemeExtension(
    small: 4,
    medium: 8,
    large: 16,
    cardPadding: EdgeInsets.all(16),
    pagePadding: EdgeInsets.symmetric(horizontal: 16, vertical: 12),
  );

  static const comfortable = SpacingThemeExtension(
    small: 8,
    medium: 16,
    large: 24,
    cardPadding: EdgeInsets.all(24),
    pagePadding: EdgeInsets.symmetric(horizontal: 24, vertical: 16),
  );
}

/// Custom status colors for business-specific states.
class StatusColorsExtension extends ThemeExtension<StatusColorsExtension> {
  const StatusColorsExtension({
    required this.success,
    required this.onSuccess,
    required this.warning,
    required this.onWarning,
    required this.info,
    required this.onInfo,
  });

  final Color success;
  final Color onSuccess;
  final Color warning;
  final Color onWarning;
  final Color info;
  final Color onInfo;

  @override
  StatusColorsExtension copyWith({
    Color? success,
    Color? onSuccess,
    Color? warning,
    Color? onWarning,
    Color? info,
    Color? onInfo,
  }) {
    return StatusColorsExtension(
      success: success ?? this.success,
      onSuccess: onSuccess ?? this.onSuccess,
      warning: warning ?? this.warning,
      onWarning: onWarning ?? this.onWarning,
      info: info ?? this.info,
      onInfo: onInfo ?? this.onInfo,
    );
  }

  @override
  StatusColorsExtension lerp(covariant StatusColorsExtension? other, double t) {
    if (other == null) return this;
    return StatusColorsExtension(
      success: Color.lerp(success, other.success, t) ?? success,
      onSuccess: Color.lerp(onSuccess, other.onSuccess, t) ?? onSuccess,
      warning: Color.lerp(warning, other.warning, t) ?? warning,
      onWarning: Color.lerp(onWarning, other.onWarning, t) ?? onWarning,
      info: Color.lerp(info, other.info, t) ?? info,
      onInfo: Color.lerp(onInfo, other.onInfo, t) ?? onInfo,
    );
  }

  static const light = StatusColorsExtension(
    success: Color(0xFF2E7D32),
    onSuccess: Color(0xFFFFFFFF),
    warning: Color(0xFFF57F17),
    onWarning: Color(0xFFFFFFFF),
    info: Color(0xFF0277BD),
    onInfo: Color(0xFFFFFFFF),
  );

  static const dark = StatusColorsExtension(
    success: Color(0xFF81C784),
    onSuccess: Color(0xFF1B5E20),
    warning: Color(0xFFFFD54F),
    onWarning: Color(0xFF4E342E),
    info: Color(0xFF4FC3F7),
    onInfo: Color(0xFF01579B),
  );
}
```

### Registering Extensions in ThemeData

```dart
static final light = ThemeData(
  useMaterial3: true,
  colorScheme: ColorScheme.fromSeed(
    seedColor: _seedColor,
    brightness: Brightness.light,
  ),
  extensions: const [
    SpacingThemeExtension.standard,
    StatusColorsExtension.light,
  ],
);

static final dark = ThemeData(
  useMaterial3: true,
  colorScheme: ColorScheme.fromSeed(
    seedColor: _seedColor,
    brightness: Brightness.dark,
  ),
  extensions: const [
    SpacingThemeExtension.standard,
    StatusColorsExtension.dark,
  ],
);
```

### Consuming Extensions in Widgets

```dart
class StatusBadge extends StatelessWidget {
  const StatusBadge({super.key, required this.label, required this.status});

  final String label;
  final StatusType status;

  @override
  Widget build(BuildContext context) {
    final statusColors = Theme.of(context).extension<StatusColorsExtension>()!;
    final spacing = Theme.of(context).extension<SpacingThemeExtension>()!;

    final (backgroundColor, foregroundColor) = switch (status) {
      StatusType.success => (statusColors.success, statusColors.onSuccess),
      StatusType.warning => (statusColors.warning, statusColors.onWarning),
      StatusType.info => (statusColors.info, statusColors.onInfo),
    };

    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: spacing.medium,
        vertical: spacing.small,
      ),
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: BorderRadius.circular(spacing.medium),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: foregroundColor,
            ),
      ),
    );
  }
}

enum StatusType { success, warning, info }
```

---

## ThemeMode Switching (Light/Dark/System)

A complete pattern for persisting and applying theme mode preferences.

```dart
class ThemeModeController extends ChangeNotifier {
  ThemeModeController({required SharedPreferences prefs}) : _prefs = prefs {
    final stored = _prefs.getString(_key);
    _mode = switch (stored) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      _ => ThemeMode.system,
    };
  }

  static const _key = 'theme_mode';
  final SharedPreferences _prefs;
  late ThemeMode _mode;

  ThemeMode get mode => _mode;

  Future<void> setMode(ThemeMode mode) async {
    if (_mode == mode) return;
    _mode = mode;
    notifyListeners();

    final value = switch (mode) {
      ThemeMode.light => 'light',
      ThemeMode.dark => 'dark',
      ThemeMode.system => 'system',
    };
    await _prefs.setString(_key, value);
  }

  /// Cycle through system -> light -> dark -> system
  Future<void> cycle() async {
    final next = switch (_mode) {
      ThemeMode.system => ThemeMode.light,
      ThemeMode.light => ThemeMode.dark,
      ThemeMode.dark => ThemeMode.system,
    };
    await setMode(next);
  }
}
```

### Usage in MaterialApp

```dart
class MyApp extends StatelessWidget {
  const MyApp({super.key, required this.themeModeController});

  final ThemeModeController themeModeController;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: themeModeController,
      builder: (context, _) {
        return MaterialApp(
          theme: AppTheme.light,
          darkTheme: AppTheme.dark,
          themeMode: themeModeController.mode,
          home: const HomePage(),
        );
      },
    );
  }
}
```

### Theme Toggle Button

```dart
class ThemeToggleButton extends StatelessWidget {
  const ThemeToggleButton({super.key, required this.controller});

  final ThemeModeController controller;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: controller,
      builder: (context, _) {
        final (icon, tooltip) = switch (controller.mode) {
          ThemeMode.system => (Icons.brightness_auto, 'System theme'),
          ThemeMode.light => (Icons.light_mode, 'Light theme'),
          ThemeMode.dark => (Icons.dark_mode, 'Dark theme'),
        };

        return IconButton(
          icon: Icon(icon),
          tooltip: tooltip,
          onPressed: controller.cycle,
        );
      },
    );
  }
}
```
