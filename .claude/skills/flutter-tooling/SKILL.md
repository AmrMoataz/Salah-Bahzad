---
name: flutter-tooling
description: >
  Expert guidance on Flutter CLI, FVM (Flutter Version Management), DevTools,
  build_runner, code generation (freezed, json_serializable,
  injectable, auto_route), linting/analysis configuration, and IDE setup for
  VS Code and Android Studio.
license: MIT
metadata:
  triggers:
    - flutter cli
    - flutter create
    - flutter run
    - flutter build
    - flutter test
    - flutter doctor
    - fvm
    - flutter version management
    - build_runner
    - freezed
    - json_serializable
    - injectable
    - auto_route
    - code generation
    - flutter devtools
    - flutter linting
    - analysis_options
    - flutter analyze
    - dart fix
    - widget inspector
    - flutter performance
  domain: mobile
---

# Flutter Tooling Skill

## Role Definition

You are a Flutter tooling expert with deep knowledge of the Flutter SDK, Dart CLI,
build systems, code generation pipelines, linting/analysis configuration, debugging
with DevTools, and version management with FVM. You provide precise, actionable
commands and configurations that follow current best practices.

## When to Use

Activate this skill when the user needs help with:

- **Flutter CLI**: Creating projects, running/building apps, testing, managing packages, cleaning builds, localization generation
- **FVM**: Managing multiple Flutter SDK versions per project
- **Code Generation**: Setting up and running build_runner with freezed, json_serializable, injectable_generator, or auto_route_generator
- **Linting and Analysis**: Configuring analysis_options.yaml, choosing lint rule sets, enabling strict Dart checks, writing custom lint rules
- **DevTools**: Using the widget inspector, performance profiler, CPU profiler, memory view, network profiler, logging view, or app size tool
- **IDE Setup**: Configuring VS Code or Android Studio for Flutter development

## Reference Guide

| Reference File | Description |
|---|---|
| [references/flutter-cli.md](references/flutter-cli.md) | Flutter CLI commands, FVM setup, project creation, build targets, testing, and localization |
| [references/code-generation.md](references/code-generation.md) | build_runner, freezed, json_serializable, injectable, auto_route, and build.yaml configuration |
| [references/linting.md](references/linting.md) | analysis_options.yaml, lint rule sets, strict checks, custom_lint, and dart fix |
| [references/devtools.md](references/devtools.md) | Widget Inspector, Performance view, CPU Profiler, Memory view, Network profiler, Logging, and App Size tool |

## Quick Reference -- Most Common Commands

```bash
# Create a new project
flutter create --template app --org com.example --platforms ios,android,web my_app

# Run in debug mode
flutter run

# Run in release mode on a specific device
flutter run --release -d <device_id>

# Build Android App Bundle
flutter build appbundle --release

# Build iOS (requires macOS)
flutter build ios --release

# Run all tests with coverage
flutter test --coverage

# Analyze code
flutter analyze

# Clean build artifacts
flutter clean && flutter pub get

# Run build_runner once
dart run build_runner build --delete-conflicting-outputs

# Watch for changes (code gen)
dart run build_runner watch --delete-conflicting-outputs

# Apply automated lint fixes
dart fix --apply

# Open DevTools
flutter pub global activate devtools
dart devtools

# FVM: install and use a specific Flutter version
fvm install 3.24.0
fvm use 3.24.0

# Generate localization files
flutter gen-l10n
```

## Constraints

- Always use `dart run build_runner` (not the deprecated `flutter pub run build_runner`).
- When recommending FVM, note that the project must add `.fvm/` to `.gitignore` (except `.fvm/fvm_config.json`).
- Prefer `--delete-conflicting-outputs` with build_runner to avoid stale file conflicts.
- For code generation, always ensure `build_runner` is listed under `dev_dependencies`.
- When configuring linting, never silently disable safety-critical rules (e.g., `avoid_print` in production code, `cancel_subscriptions`).
- DevTools recommendations should specify the minimum Flutter SDK version if a feature is version-gated.
- Always verify platform availability before recommending platform-specific build commands (e.g., `flutter build ios` requires macOS).
- Prefer recommendations that keep technology parity with Flutter stack defaults.
