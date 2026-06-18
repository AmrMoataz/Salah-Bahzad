# Linting and Code Quality

## analysis_options.yaml Configuration

The `analysis_options.yaml` file in the project root controls the Dart analyzer and linter.

### Basic Structure

```yaml
# analysis_options.yaml

# Include a predefined rule set
include: package:flutter_lints/flutter.yaml

analyzer:
  # Dart language settings
  language:
    strict-casts: true
    strict-inference: true
    strict-raw-types: true

  # Files to exclude from analysis
  exclude:
    - "**/*.g.dart"
    - "**/*.freezed.dart"
    - "**/*.gr.dart"
    - "**/*.config.dart"
    - "build/**"
    - "lib/generated/**"

  # Treat specific rules as errors or warnings
  errors:
    # Promote info-level issues to warnings
    missing_return: error
    dead_code: warning
    # Downgrade issues (use sparingly)
    todo: ignore

  # Enable experimental features
  plugins:
    - custom_lint

linter:
  rules:
    # Enable additional rules
    prefer_single_quotes: true
    always_use_package_imports: true
    # Disable rules from the included set
    avoid_print: false  # Only in dev-only code
```

---

## Lint Rule Sets

### flutter_lints (Official Flutter Team)

The default lint set for Flutter projects. Included automatically in new projects.

```yaml
# pubspec.yaml
dev_dependencies:
  flutter_lints: ^5.0.0

# analysis_options.yaml
include: package:flutter_lints/flutter.yaml
```

This set includes `package:lints/recommended.yaml` plus Flutter-specific rules. It is a moderate rule set suitable for most projects.

### very_good_analysis (Very Good Ventures)

A stricter, more opinionated rule set used by the Very Good Ventures team.

```bash
flutter pub add --dev very_good_analysis
```

```yaml
# analysis_options.yaml
include: package:very_good_analysis/analysis_options.yaml
```

This enables many additional rules beyond the Flutter defaults, including:
- `public_member_api_docs` -- require documentation for public APIs
- `prefer_const_constructors` -- prefer const where possible
- `sort_constructors_first` -- constructors before other members
- Strict formatting and naming conventions

### Custom Rule Set (Building Your Own)

```yaml
# analysis_options.yaml
include: package:flutter_lints/flutter.yaml

linter:
  rules:
    # --- Style ---
    prefer_single_quotes: true
    always_use_package_imports: true
    directives_ordering: true
    sort_constructors_first: true
    sort_unnamed_constructors_first: true
    unawaited_futures: true
    prefer_final_locals: true
    prefer_final_in_for_each: true
    cascade_invocations: true
    avoid_redundant_argument_values: true
    use_named_constants: true
    noop_primitive_operations: true

    # --- Safety ---
    cancel_subscriptions: true
    close_sinks: true
    literal_only_boolean_expressions: true
    no_adjacent_strings_in_list: true
    test_types_in_equals: true
    throw_in_finally: true
    unnecessary_statements: true
    avoid_slow_async_io: true

    # --- Flutter-specific ---
    use_build_context_synchronously: true
    sized_box_for_whitespace: true
    use_colored_box: true
    use_decorated_box: true
    avoid_unnecessary_containers: true
    prefer_const_constructors: true
    prefer_const_constructors_in_immutables: true
    prefer_const_declarations: true
    prefer_const_literals_to_create_immutables: true
```

---

## Strict Dart Checks

### strict-casts

Disallows implicit casts from `dynamic`. Forces explicit type handling.

```yaml
analyzer:
  language:
    strict-casts: true
```

Before (implicit cast allowed):

```dart
dynamic value = fetchData();
String name = value; // Implicit cast from dynamic -- compiles without strict-casts
```

After (explicit cast required):

```dart
dynamic value = fetchData();
String name = value as String; // Explicit cast required
// or better:
if (value is String) {
  String name = value;
}
```

### strict-raw-types

Disallows raw generic types without type arguments.

```yaml
analyzer:
  language:
    strict-raw-types: true
```

Before:

```dart
List items = [1, 2, 3]; // Raw type, no type argument
Map data = {};           // Raw type
```

After:

```dart
List<int> items = [1, 2, 3];
Map<String, dynamic> data = {};
```

### strict-inference

Disallows type inference that results in `dynamic`.

```yaml
analyzer:
  language:
    strict-inference: true
```

Before:

```dart
var items = []; // Inferred as List<dynamic>
```

After:

```dart
var items = <String>[]; // Explicit type argument
List<String> items = [];
```

### Enabling All Strict Checks

```yaml
analyzer:
  language:
    strict-casts: true
    strict-inference: true
    strict-raw-types: true
```

This is recommended for all production projects. It catches many subtle bugs at analysis time.

---

## Custom Lint Rules with custom_lint

### Setup

```bash
flutter pub add --dev custom_lint
```

Add the plugin to `analysis_options.yaml`:

```yaml
analyzer:
  plugins:
    - custom_lint
```

### Using Existing Custom Lint Packages

**riverpod_lint** -- lint rules for Riverpod:

```bash
flutter pub add --dev riverpod_lint
```

```yaml
analyzer:
  plugins:
    - custom_lint

custom_lint:
  rules:
    # Riverpod-specific rules are auto-enabled
    - riverpod_final_provider
    - riverpod_missing_provider_scope
```

### Writing Your Own Lint Rules

Create a separate package for custom lints:

```
my_custom_lints/
  lib/
    my_custom_lints.dart
    src/
      avoid_print_rule.dart
  pubspec.yaml
```

```yaml
# my_custom_lints/pubspec.yaml
name: my_custom_lints
version: 1.0.0

environment:
  sdk: ">=3.0.0 <4.0.0"

dependencies:
  custom_lint_builder: ^0.7.0
  analyzer: ^6.0.0
```

```dart
// my_custom_lints/lib/my_custom_lints.dart

import 'package:custom_lint_builder/custom_lint_builder.dart';
import 'src/avoid_print_rule.dart';

PluginBase createPlugin() => _MyCustomLints();

class _MyCustomLints extends PluginBase {
  @override
  List<LintRule> getLintRules(CustomLintConfigs configs) => [
    AvoidPrintRule(),
  ];
}
```

```dart
// my_custom_lints/lib/src/avoid_print_rule.dart

import 'package:analyzer/error/listener.dart';
import 'package:custom_lint_builder/custom_lint_builder.dart';

class AvoidPrintRule extends DartLintRule {
  const AvoidPrintRule() : super(code: _code);

  static const _code = LintCode(
    name: 'avoid_print_in_production',
    problemMessage: 'Avoid using print() in production code. Use a logging framework instead.',
    correctionMessage: 'Replace print() with Logger.info() or similar.',
    errorSeverity: ErrorSeverity.WARNING,
  );

  @override
  void run(
    CustomLintResolver resolver,
    ErrorReporter reporter,
    CustomLintContext context,
  ) {
    context.registry.addMethodInvocation((node) {
      if (node.methodName.name == 'print') {
        reporter.atNode(node, code);
      }
    });
  }
}
```

Then in the main project:

```yaml
# pubspec.yaml
dev_dependencies:
  my_custom_lints:
    path: packages/my_custom_lints
  custom_lint: ^0.7.0
```

---

## dart fix -- Automated Fixes

### Basic Usage

```bash
# Preview fixes without applying them (dry run)
dart fix --dry-run

# Apply all available fixes
dart fix --apply

# Apply fixes to a specific directory
dart fix --apply lib/

# Apply fixes to a specific file
dart fix --apply lib/src/my_file.dart
```

### What dart fix Can Correct

`dart fix` applies fixes for deprecated APIs and common lint violations:

- Replace deprecated constructors and methods with their replacements
- Add `const` where applicable
- Replace `Container` with `SizedBox` when only size is specified
- Convert to null-aware operators
- Apply other automated lint fixes

### Example Output

```
$ dart fix --dry-run

Computing fixes in lib...

  lib/src/old_api.dart
    deprecated_member_use - 3 fixes
    prefer_const_constructors - 2 fixes
    sized_box_for_whitespace - 1 fix

6 fixes in 1 file.

To apply these fixes, run: dart fix --apply
```

### Using in CI

```bash
# Fail CI if there are unfixed issues
dart fix --dry-run
if [ $? -ne 0 ]; then
  echo "Run 'dart fix --apply' to fix these issues."
  exit 1
fi
```

---

## Common Lint Rules Reference

### Rules to Enable

| Rule | Description |
|---|---|
| `prefer_single_quotes` | Consistent quote style |
| `always_use_package_imports` | Use `package:` imports instead of relative |
| `unawaited_futures` | Warn when Future return value is ignored |
| `cancel_subscriptions` | Warn when StreamSubscription is not cancelled |
| `close_sinks` | Warn when StreamSink is not closed |
| `prefer_final_locals` | Encourage immutable local variables |
| `prefer_const_constructors` | Prefer const where possible |
| `use_build_context_synchronously` | Warn about BuildContext use after async gaps |
| `avoid_slow_async_io` | Prefer sync path operations |
| `no_adjacent_strings_in_list` | Catch missing commas in string lists |
| `sort_constructors_first` | Constructors before other members |
| `cascade_invocations` | Use cascade `..` for chained calls |
| `prefer_final_in_for_each` | Use final in for-each loops |

### Rules to Consider Disabling

| Rule | Reason |
|---|---|
| `avoid_print` | Useful during development; re-enable for production |
| `public_member_api_docs` | Too strict for application code (good for packages) |
| `lines_longer_than_80_chars` | Many teams prefer 100 or 120 |
| `always_specify_types` | Conflicts with `omit_local_variable_types` (pick one style) |
| `prefer_double_quotes` | Conflicts with `prefer_single_quotes` (pick one) |
| `flutter_style_todos` | Only needed if following Flutter framework contribution guidelines |

### Recommended CI Configuration

```yaml
# analysis_options.yaml for CI strictness
include: package:flutter_lints/flutter.yaml

analyzer:
  language:
    strict-casts: true
    strict-inference: true
    strict-raw-types: true
  errors:
    # Fail the build on these
    missing_return: error
    missing_required_param: error
    dead_code: error
    unused_import: error
    unused_local_variable: warning
    deprecated_member_use: warning
    # Ignore in generated files (which are already excluded)
    todo: ignore
  exclude:
    - "**/*.g.dart"
    - "**/*.freezed.dart"
    - "**/*.gr.dart"
    - "**/*.config.dart"
    - "**/*.mocks.dart"
    - "build/**"

linter:
  rules:
    prefer_single_quotes: true
    always_use_package_imports: true
    unawaited_futures: true
    cancel_subscriptions: true
    close_sinks: true
    prefer_final_locals: true
    prefer_const_constructors: true
    prefer_const_constructors_in_immutables: true
    prefer_const_declarations: true
    prefer_const_literals_to_create_immutables: true
    use_build_context_synchronously: true
    sized_box_for_whitespace: true
    use_colored_box: true
    use_decorated_box: true
    avoid_unnecessary_containers: true
    no_adjacent_strings_in_list: true
    sort_constructors_first: true
```

### Running Lint Checks in CI

```bash
#!/bin/bash
set -e

echo "Running Dart analysis..."
flutter analyze --fatal-infos --fatal-warnings

echo "Checking formatting..."
dart format --set-exit-if-changed .

echo "Checking for available fixes..."
dart fix --dry-run

echo "All lint checks passed."
```
