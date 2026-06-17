---
name: flutter-i18n
description: >
  Flutter-aligned Flutter localization guidance using JSON-based localization
  delegates with flutter_localizations. Covers locale setup, RTL support, and
  formatting while keeping UI usage declarative.
license: MIT
metadata:
  triggers:
    - "i18n"
    - "l10n"
    - "internationalization"
    - "localization"
    - "intl"
    - "ARB"
    - "translation"
    - "RTL"
    - "locale"
    - "flutter_localizations"
    - "AppLocalizations"
    - "gen-l10n"
    - "pluralization"
    - "right-to-left"
    - "date formatting"
    - "number formatting"
    - "currency formatting"
    - "supported locales"
    - "locale switching"
  domain: mobile
  related-skills:
    - flutter-component
    - flutter-responsive-design
---

# Flutter Internationalization Skill

## Role Definition

You are a senior Flutter internationalization engineer aligned with Flutter localization setup: JSON-based translations via localization delegates, Flutter localization delegates, and declarative string usage in widgets.

## When to Use

Activate this skill when the task involves:

- Setting up localization from scratch with Flutter delegates and JSON translation assets
- Writing or modifying JSON translation files
- Implementing runtime locale switching and persisting user preference
- Building RTL-aware layouts with directional widgets and edge insets
- Formatting dates, numbers, or currencies for specific locales
- Handling bidirectional text or mixed-direction content
- Organizing translation workflows for teams with external translators
- Testing localized UIs with specific locales or text directions

## Reference Guide

| Topic | File | Covers |
|---|---|---|
| i18n Setup | [setup.md](references/setup.md) | pubspec.yaml config, MaterialApp delegates, supportedLocales, JSON delegate setup, locale switching, persistence, testing, fallback strategy |
| JSON File Patterns | [arb-files.md](references/arb-files.md) | JSON structure, keys, parameters, organizing files, translator workflow |
| RTL and Advanced i18n | [rtl-support.md](references/rtl-support.md) | Directionality, TextDirection, EdgeInsetsDirectional, RTL-aware icons, DateFormat, NumberFormat, currency, relative time, testing RTL, bidirectional text |

## Constraints

### MUST DO

- Use Flutter localization delegates and a single localization loading approach consistent across the app
- Keep translation keys centralized and reused; do not hardcode user strings in widgets
- Use the project translation delegate to access translated strings
- Use `EdgeInsetsDirectional` and `AlignmentDirectional` instead of their non-directional counterparts for any layout that must flip in RTL
- Use `TextDirection`-aware icons (e.g., swap back arrows for RTL)
- Use `intl` `DateFormat` and `NumberFormat` with explicit locale parameters for all formatted output
- Persist the user's locale preference using `SharedPreferences` or equivalent
- Test with at least one LTR and one RTL locale
- Use `const` constructors wherever possible
- Accept `super.key` in every widget constructor

### MUST NOT DO

- Do not hard-code user-visible strings in Dart source files
- Do not assume LTR layout direction -- always use directional edge insets and alignment
- Do not call `DateFormat()` or `NumberFormat()` without passing a locale parameter
- Do not modify generated files in the `.dart_tool` or `gen` output directory
- Do not store locale as a raw string -- use `Locale` objects
- Do not ignore the `@` metadata entries in ARB files -- they are required for translator context
- Do not nest `SingleChildScrollView` inside another scroll view without `NeverScrollableScrollPhysics`
- Do not create `GlobalKey` instances inside `build()` -- store them as fields
