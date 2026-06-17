# ARB File Patterns

## ARB File Structure

ARB (Application Resource Bundle) is a JSON-based format used by Flutter's `gen-l10n` tool. Each locale has its own file following the naming convention `app_<languageCode>.arb` (e.g., `app_en.arb`, `app_ar.arb`).

### Template ARB file (app_en.arb)

The template file is the source of truth. It contains every message key, its English value, and `@`-prefixed metadata entries that provide context for translators and configure placeholder types.

```json
{
  "@@locale": "en",
  "@@last_modified": "2025-12-01T10:00:00Z",
  "appTitle": "My Application",
  "@appTitle": {
    "description": "The title shown in the app bar on the home screen"
  }
}
```

### Translated ARB file (app_ar.arb)

Translated files contain the same keys with translated values. The `@`-metadata entries are optional in translated files but the `@@locale` is required.

```json
{
  "@@locale": "ar",
  "appTitle": "تطبيقي"
}
```

### Complete file layout example

```
lib/
  l10n/
    app_en.arb        # Template (source of truth)
    app_ar.arb        # Arabic
    app_es.arb        # Spanish
    app_fr.arb        # French
    app_ja.arb        # Japanese
    app_zh_CN.arb     # Chinese (Simplified) -- note underscore for region
    generated/
      app_localizations.dart
      app_localizations_en.dart
      app_localizations_ar.dart
      ...
```

---

## Simple Strings

Basic key-value pairs for static text.

```json
{
  "@@locale": "en",
  "appTitle": "Task Manager",
  "@appTitle": {
    "description": "Application title displayed in the top app bar"
  },
  "settingsLabel": "Settings",
  "@settingsLabel": {
    "description": "Label for the settings navigation item"
  },
  "logoutButton": "Log Out",
  "@logoutButton": {
    "description": "Text on the log out button in the settings page"
  },
  "emptyTaskList": "No tasks yet. Tap + to create one.",
  "@emptyTaskList": {
    "description": "Placeholder text shown when the task list is empty"
  }
}
```

Arabic translation:

```json
{
  "@@locale": "ar",
  "appTitle": "مدير المهام",
  "settingsLabel": "الإعدادات",
  "logoutButton": "تسجيل الخروج",
  "emptyTaskList": "لا توجد مهام بعد. اضغط + لإنشاء واحدة."
}
```

Generated Dart usage:

```dart
Text(context.l10n.appTitle)
Text(context.l10n.emptyTaskList)
```

---

## Parameterized Messages

Use `{parameterName}` syntax to insert dynamic values into messages.

### Single parameter

```json
{
  "welcomeMessage": "Welcome back, {userName}!",
  "@welcomeMessage": {
    "description": "Greeting shown on the home screen after login",
    "placeholders": {
      "userName": {
        "type": "String",
        "example": "Ali"
      }
    }
  }
}
```

Arabic:

```json
{
  "welcomeMessage": "مرحبًا بعودتك، {userName}!"
}
```

Usage:

```dart
Text(context.l10n.welcomeMessage('Ali'))
```

### Multiple parameters

```json
{
  "taskAssignment": "Task \"{taskTitle}\" assigned to {assigneeName}",
  "@taskAssignment": {
    "description": "Notification text when a task is assigned to someone",
    "placeholders": {
      "taskTitle": {
        "type": "String",
        "example": "Review PR"
      },
      "assigneeName": {
        "type": "String",
        "example": "Sara"
      }
    }
  }
}
```

Usage:

```dart
Text(context.l10n.taskAssignment('Review PR', 'Sara'))
```

### Numeric parameter

```json
{
  "progressPercent": "Progress: {percent}%",
  "@progressPercent": {
    "description": "Progress indicator label",
    "placeholders": {
      "percent": {
        "type": "int",
        "example": "75"
      }
    }
  }
}
```

Usage:

```dart
Text(context.l10n.progressPercent(75))
```

---

## Pluralization

ICU plural syntax handles zero, one, two, few, many, and other categories. Different languages use different subsets.

### Basic plural

```json
{
  "itemCount": "{count, plural, =0{No items} =1{1 item} other{{count} items}}",
  "@itemCount": {
    "description": "Label showing the number of items in a list",
    "placeholders": {
      "count": {
        "type": "int",
        "example": "5"
      }
    }
  }
}
```

Arabic (uses =0, =1, =2, few, many, other):

```json
{
  "itemCount": "{count, plural, =0{لا عناصر} =1{عنصر واحد} =2{عنصران} few{{count} عناصر} many{{count} عنصرًا} other{{count} عنصر}}"
}
```

Usage:

```dart
Text(context.l10n.itemCount(0))   // "No items"
Text(context.l10n.itemCount(1))   // "1 item"
Text(context.l10n.itemCount(42))  // "42 items"
```

### Plural with additional context

```json
{
  "unreadMessages": "{count, plural, =0{No unread messages} =1{1 unread message from {sender}} other{{count} unread messages}}",
  "@unreadMessages": {
    "description": "Badge or label showing unread message count",
    "placeholders": {
      "count": {
        "type": "int",
        "example": "3"
      },
      "sender": {
        "type": "String",
        "example": "John"
      }
    }
  }
}
```

Usage:

```dart
Text(context.l10n.unreadMessages(1, 'John'))  // "1 unread message from John"
Text(context.l10n.unreadMessages(5, ''))       // "5 unread messages"
```

### Compact plural for common patterns

```json
{
  "daysRemaining": "{days, plural, =0{Due today} =1{Due tomorrow} other{Due in {days} days}}",
  "@daysRemaining": {
    "description": "Shows how many days remain until a deadline",
    "placeholders": {
      "days": {
        "type": "int",
        "example": "3"
      }
    }
  }
}
```

---

## Select (Gender / Category)

ICU select syntax chooses between variants based on a string value. Commonly used for gender or category distinctions.

### Gender select

```json
{
  "userGreeting": "{gender, select, male{He joined the team} female{She joined the team} other{They joined the team}}",
  "@userGreeting": {
    "description": "Announcement when a new user joins the team",
    "placeholders": {
      "gender": {
        "type": "String",
        "example": "female"
      }
    }
  }
}
```

Arabic:

```json
{
  "userGreeting": "{gender, select, male{انضم إلى الفريق} female{انضمت إلى الفريق} other{انضم/ت إلى الفريق}}"
}
```

Usage:

```dart
Text(context.l10n.userGreeting('female'))  // "She joined the team"
Text(context.l10n.userGreeting('other'))   // "They joined the team"
```

### Select with a name parameter

```json
{
  "profileUpdate": "{gender, select, male{{name} updated his profile} female{{name} updated her profile} other{{name} updated their profile}}",
  "@profileUpdate": {
    "description": "Activity feed message when someone updates their profile",
    "placeholders": {
      "gender": {
        "type": "String",
        "example": "male"
      },
      "name": {
        "type": "String",
        "example": "Ali"
      }
    }
  }
}
```

Usage:

```dart
Text(context.l10n.profileUpdate('male', 'Ali'))
// "Ali updated his profile"
```

### Category select (non-gender)

```json
{
  "notificationType": "{type, select, message{New message received} task{New task assigned} alert{System alert} other{New notification}}",
  "@notificationType": {
    "description": "Notification title based on notification category",
    "placeholders": {
      "type": {
        "type": "String",
        "example": "task"
      }
    }
  }
}
```

---

## Date and Number Formatting in Messages

Use ICU format specifiers to format dates and numbers inline within translated messages.

### Date parameter

```json
{
  "eventDate": "Event on {date, date, medium}",
  "@eventDate": {
    "description": "Shows the date of a scheduled event",
    "placeholders": {
      "date": {
        "type": "DateTime",
        "format": "yMMMd",
        "example": "2025-06-15"
      }
    }
  }
}
```

Usage:

```dart
Text(context.l10n.eventDate(DateTime(2025, 6, 15)))
// English: "Event on Jun 15, 2025"
```

### Number parameter with formatting

```json
{
  "accountBalance": "Balance: {amount, number, decimalPattern}",
  "@accountBalance": {
    "description": "Displays the user's account balance",
    "placeholders": {
      "amount": {
        "type": "double",
        "format": "decimalPattern",
        "example": "1234.56"
      }
    }
  }
}
```

Usage:

```dart
Text(context.l10n.accountBalance(1234.56))
// English: "Balance: 1,234.56"
// Arabic:  "الرصيد: ١٬٢٣٤٫٥٦"
```

### Currency parameter

```json
{
  "productPrice": "Price: {price, number, currency}",
  "@productPrice": {
    "description": "Shows a product price in the local currency format",
    "placeholders": {
      "price": {
        "type": "double",
        "format": "currency",
        "optionalParameters": {
          "symbol": "$",
          "decimalDigits": 2
        },
        "example": "29.99"
      }
    }
  }
}
```

### Available number format specifiers

| Format | Example output (en_US) |
|---|---|
| `compact` | "1.2K" |
| `compactLong` | "1.2 thousand" |
| `decimalPattern` | "1,234.56" |
| `decimalPercentPattern` | "12%" |
| `currency` | "$1,234.56" |
| `simpleCurrency` | "$1,235" |
| `scientificPattern` | "1E3" |

### Available date format specifiers

| Format | Example output (en_US) |
|---|---|
| `yMd` | "6/15/2025" |
| `yMMMd` | "Jun 15, 2025" |
| `yMMMMd` | "June 15, 2025" |
| `yMMMEd` | "Sun, Jun 15, 2025" |
| `Hm` | "14:30" |
| `jm` | "2:30 PM" |
| `yMd` + `jm` | "6/15/2025 2:30 PM" |

---

## Nested ICU Messages

Combine plural and select in a single message for complex requirements.

### Plural inside select

```json
{
  "activitySummary": "{gender, select, male{He completed {count, plural, =1{1 task} other{{count} tasks}}} female{She completed {count, plural, =1{1 task} other{{count} tasks}}} other{They completed {count, plural, =1{1 task} other{{count} tasks}}}}",
  "@activitySummary": {
    "description": "Summary of completed tasks with gendered pronoun",
    "placeholders": {
      "gender": {
        "type": "String",
        "example": "female"
      },
      "count": {
        "type": "int",
        "example": "5"
      }
    }
  }
}
```

Usage:

```dart
Text(context.l10n.activitySummary('female', 3))
// "She completed 3 tasks"
```

### Select inside plural

```json
{
  "invitationCount": "{count, plural, =0{No invitations} =1{{gender, select, male{He has 1 invitation} female{She has 1 invitation} other{They have 1 invitation}}} other{{gender, select, male{He has {count} invitations} female{She has {count} invitations} other{They have {count} invitations}}}}",
  "@invitationCount": {
    "description": "Shows number of pending invitations with gendered pronoun",
    "placeholders": {
      "count": {
        "type": "int",
        "example": "3"
      },
      "gender": {
        "type": "String",
        "example": "other"
      }
    }
  }
}
```

---

## Description and Placeholders Metadata

Every message in the template ARB file should have an `@`-prefixed metadata entry. This metadata is essential for translators who may not have access to the app's UI.

### Complete metadata example

```json
{
  "orderConfirmation": "Order #{orderId} confirmed. {itemCount, plural, =1{1 item} other{{itemCount} items}} will arrive by {deliveryDate, date, yMMMd}.",
  "@orderConfirmation": {
    "description": "Confirmation message shown after a successful purchase. Displayed in a dialog with a green checkmark icon.",
    "placeholders": {
      "orderId": {
        "type": "String",
        "description": "The unique order identifier (e.g., ORD-12345)",
        "example": "ORD-12345"
      },
      "itemCount": {
        "type": "int",
        "description": "Total number of items in the order",
        "example": "3"
      },
      "deliveryDate": {
        "type": "DateTime",
        "format": "yMMMd",
        "description": "Expected delivery date",
        "example": "2025-07-20"
      }
    }
  }
}
```

### Metadata fields reference

| Field | Required | Purpose |
|---|---|---|
| `description` | Yes | Context for translators -- where the string appears, what it means |
| `placeholders` | Only if message has `{param}` | Defines type, description, example, and formatting for each placeholder |
| `type` | Yes (in placeholder) | Dart type: `String`, `int`, `double`, `num`, `DateTime` |
| `format` | For DateTime/num | ICU format specifier (`yMMMd`, `decimalPattern`, `currency`, etc.) |
| `optionalParameters` | For currency | Additional options like `symbol`, `decimalDigits`, `customPattern` |
| `description` (in placeholder) | Recommended | Tells translators what value will appear |
| `example` | Recommended | Concrete example so translators understand the expected length and format |

---

## Organizing Large ARB Files

As your app grows, ARB files can become unwieldy. Use a consistent ordering and naming convention.

### Naming convention by feature

Prefix keys with a feature or screen name to group related strings.

```json
{
  "@@locale": "en",

  "homeTitle": "Home",
  "@homeTitle": { "description": "Home screen title" },
  "homeWelcome": "Welcome back!",
  "@homeWelcome": { "description": "Home screen welcome banner" },
  "homeRecentActivity": "Recent Activity",
  "@homeRecentActivity": { "description": "Section header for recent activity on home screen" },

  "settingsTitle": "Settings",
  "@settingsTitle": { "description": "Settings screen title" },
  "settingsLanguage": "Language",
  "@settingsLanguage": { "description": "Language selection label in settings" },
  "settingsTheme": "Theme",
  "@settingsTheme": { "description": "Theme selection label in settings" },
  "settingsDarkMode": "Dark Mode",
  "@settingsDarkMode": { "description": "Toggle label for dark mode in settings" },

  "profileTitle": "Profile",
  "@profileTitle": { "description": "Profile screen title" },
  "profileEditButton": "Edit Profile",
  "@profileEditButton": { "description": "Button to open profile editing" },
  "profileBio": "Bio",
  "@profileBio": { "description": "Label for the bio field on profile screen" },

  "errorGeneric": "Something went wrong. Please try again.",
  "@errorGeneric": { "description": "Generic error message shown in a snackbar" },
  "errorNetwork": "No internet connection. Check your network settings.",
  "@errorNetwork": { "description": "Error when the device is offline" },
  "errorTimeout": "Request timed out. Please try again later.",
  "@errorTimeout": { "description": "Error when a network request exceeds the timeout" }
}
```

### Recommended key prefixes

| Prefix | Use for |
|---|---|
| `home*` | Home screen strings |
| `settings*` | Settings screen strings |
| `profile*` | Profile screen strings |
| `auth*` | Login, signup, password reset strings |
| `error*` | Error messages |
| `action*` | Common action verbs (save, cancel, delete, submit) |
| `label*` | Reusable labels (name, email, phone) |
| `dialog*` | Dialog titles and body text |
| `nav*` | Navigation labels (tab bar, drawer) |

### Splitting into multiple ARB directories (large monorepos)

For very large apps, you can run multiple `gen-l10n` passes with separate `l10n.yaml` files per feature module:

```
lib/
  features/
    auth/
      l10n/
        l10n.yaml
        auth_en.arb
        auth_ar.arb
      generated/
        auth_localizations.dart
    tasks/
      l10n/
        l10n.yaml
        tasks_en.arb
        tasks_ar.arb
      generated/
        tasks_localizations.dart
```

Each `l10n.yaml` specifies its own `output-class`, `arb-dir`, and `output-dir`.

---

## Translation Workflow

### Extracting strings for translators

1. Developers add new keys to the template ARB file (`app_en.arb`) with complete metadata.
2. Run `flutter gen-l10n` to validate the ARB syntax.
3. Export the template ARB to your translation management system (TMS) or send it directly to translators.

### Sending to translators

ARB files are JSON, so most translation tools support them directly:

| Tool | ARB Support |
|---|---|
| Crowdin | Native import/export |
| Lokalise | Native import/export |
| Phrase (formerly Memsource) | Native import/export |
| Google Translator Toolkit | ARB is the native format |
| POEditor | Native import/export |

For tools that do not support ARB, convert to XLIFF or PO format and back. The `intl_translation` package can help:

```bash
# Extract to ARB (useful if you used Intl.message() directly)
dart run intl_translation:extract_to_arb --output-dir=lib/l10n lib/l10n/messages.dart

# Generate from translated ARB
dart run intl_translation:generate_from_arb --output-dir=lib/l10n/generated lib/l10n/messages.dart lib/l10n/app_*.arb
```

### Receiving translations

1. Translators return completed ARB files (`app_ar.arb`, `app_es.arb`, etc.).
2. Place them in the `arb-dir` directory specified in `l10n.yaml`.
3. Run `flutter gen-l10n` to regenerate the Dart localization code.
4. Run the app in each locale to visually verify.

### CI validation

Add ARB validation to your CI pipeline to catch issues early.

```dart
// test/l10n_test.dart
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  test('all ARB files have the same keys as the template', () {
    final arbDir = Directory('lib/l10n');
    final templateFile = File('${arbDir.path}/app_en.arb');
    final templateJson =
        jsonDecode(templateFile.readAsStringSync()) as Map<String, dynamic>;

    final templateKeys = templateJson.keys
        .where((key) => !key.startsWith('@'))
        .toSet();

    final arbFiles = arbDir
        .listSync()
        .whereType<File>()
        .where((f) => f.path.endsWith('.arb') && !f.path.endsWith('app_en.arb'));

    for (final file in arbFiles) {
      final json = jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
      final keys = json.keys.where((key) => !key.startsWith('@')).toSet();

      final missingKeys = templateKeys.difference(keys);
      final extraKeys = keys.difference(templateKeys);

      expect(
        missingKeys,
        isEmpty,
        reason: '${file.path} is missing keys: $missingKeys',
      );

      expect(
        extraKeys,
        isEmpty,
        reason: '${file.path} has extra keys not in template: $extraKeys',
      );
    }
  });

  test('all ARB files are valid JSON', () {
    final arbDir = Directory('lib/l10n');
    final arbFiles = arbDir
        .listSync()
        .whereType<File>()
        .where((f) => f.path.endsWith('.arb'));

    for (final file in arbFiles) {
      expect(
        () => jsonDecode(file.readAsStringSync()),
        returnsNormally,
        reason: '${file.path} is not valid JSON',
      );
    }
  });

  test('template ARB has metadata for every key', () {
    final templateFile = File('lib/l10n/app_en.arb');
    final templateJson =
        jsonDecode(templateFile.readAsStringSync()) as Map<String, dynamic>;

    final messageKeys = templateJson.keys
        .where((key) => !key.startsWith('@'))
        .toList();

    for (final key in messageKeys) {
      expect(
        templateJson.containsKey('@$key'),
        isTrue,
        reason: 'Template ARB is missing @$key metadata entry',
      );
    }
  });
}
```

### Git workflow for translations

1. Developers create a feature branch and add new ARB keys to `app_en.arb`.
2. On merge to `main`, CI exports the template to the TMS.
3. Translators work in the TMS and submit translated ARB files.
4. A bot or translator opens a PR with updated ARB files.
5. CI validates all ARB files (same keys, valid JSON, metadata present).
6. After merge, `flutter gen-l10n` runs as part of the build.
