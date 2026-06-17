# Hive NoSQL -- Comprehensive Guide

Hive is a lightweight, fast, key-value database written in pure Dart.
It is ideal for storing structured objects without a relational schema,
caching API responses, and persisting UI state.

## Table of Contents

1. [Initialization](#initialization)
2. [TypeAdapters for Custom Objects](#typeadapters-for-custom-objects)
3. [CRUD Operations](#crud-operations)
4. [Lazy Boxes](#lazy-boxes)
5. [Encrypted Boxes](#encrypted-boxes)
6. [Listening to Changes](#listening-to-changes)
7. [Code Generation](#code-generation)
8. [Best Practices and Limitations](#best-practices-and-limitations)
9. [Migration from Hive to Isar](#migration-from-hive-to-isar)

---

## Initialization

### pubspec.yaml

```yaml
dependencies:
  hive: ^4.0.0
  hive_flutter: ^2.0.0
  path_provider: ^2.1.5

dev_dependencies:
  hive_generator: ^3.0.0
  build_runner: ^2.4.14
```

### Initialization in main.dart

```dart
import 'package:flutter/widgets.dart';
import 'package:hive_flutter/hive_flutter.dart';

import 'data/models/user_settings.dart';
import 'data/models/cached_article.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Initialize Hive for Flutter (sets the default path).
  await Hive.initFlutter();

  // Register all TypeAdapters before opening boxes.
  Hive.registerAdapter(UserSettingsAdapter());
  Hive.registerAdapter(CachedArticleAdapter());

  // Open boxes that are needed at startup.
  await Hive.openBox<UserSettings>('userSettings');
  await Hive.openBox<CachedArticle>('articleCache');

  runApp(const MyApp());
}
```

---

## TypeAdapters for Custom Objects

A TypeAdapter tells Hive how to serialize and deserialize a custom Dart class.

### Manual TypeAdapter

```dart
// lib/data/models/user_settings.dart
import 'package:hive/hive.dart';

class UserSettings {
  final String locale;
  final bool darkMode;
  final double fontSize;
  final List<String> favoriteCategories;

  const UserSettings({
    required this.locale,
    required this.darkMode,
    required this.fontSize,
    this.favoriteCategories = const [],
  });

  UserSettings copyWith({
    String? locale,
    bool? darkMode,
    double? fontSize,
    List<String>? favoriteCategories,
  }) {
    return UserSettings(
      locale: locale ?? this.locale,
      darkMode: darkMode ?? this.darkMode,
      fontSize: fontSize ?? this.fontSize,
      favoriteCategories: favoriteCategories ?? this.favoriteCategories,
    );
  }
}

class UserSettingsAdapter extends TypeAdapter<UserSettings> {
  @override
  final int typeId = 0;

  @override
  UserSettings read(BinaryReader reader) {
    final numOfFields = reader.readByte();
    final fields = <int, dynamic>{
      for (var i = 0; i < numOfFields; i++)
        reader.readByte(): reader.read(),
    };
    return UserSettings(
      locale: fields[0] as String? ?? 'en',
      darkMode: fields[1] as bool? ?? false,
      fontSize: fields[2] as double? ?? 16.0,
      favoriteCategories:
          (fields[3] as List?)?.cast<String>() ?? const [],
    );
  }

  @override
  void write(BinaryWriter writer, UserSettings obj) {
    writer
      ..writeByte(4) // number of fields
      ..writeByte(0)
      ..write(obj.locale)
      ..writeByte(1)
      ..write(obj.darkMode)
      ..writeByte(2)
      ..write(obj.fontSize)
      ..writeByte(3)
      ..write(obj.favoriteCategories);
  }
}
```

> **Tip:** Always provide defaults in `read()` for new fields. This makes
> the adapter forward-compatible when you add fields later.

---

## CRUD Operations

```dart
// lib/data/repositories/settings_repository.dart
import 'package:hive/hive.dart';
import '../models/user_settings.dart';

class SettingsRepository {
  static const _boxName = 'userSettings';
  static const _key = 'current';

  Box<UserSettings> get _box => Hive.box<UserSettings>(_boxName);

  // --- Create / Update ---
  Future<void> saveSettings(UserSettings settings) async {
    await _box.put(_key, settings);
  }

  // --- Read ---
  UserSettings getSettings() {
    return _box.get(
      _key,
      defaultValue: const UserSettings(
        locale: 'en',
        darkMode: false,
        fontSize: 16.0,
      ),
    )!;
  }

  // --- Delete ---
  Future<void> clearSettings() async {
    await _box.delete(_key);
  }
}
```

### Working with Indexed Collections

```dart
// lib/data/repositories/article_cache_repository.dart
import 'package:hive/hive.dart';
import '../models/cached_article.dart';

class ArticleCacheRepository {
  static const _boxName = 'articleCache';

  Box<CachedArticle> get _box => Hive.box<CachedArticle>(_boxName);

  // Store by article ID as key.
  Future<void> cacheArticle(CachedArticle article) async {
    await _box.put(article.id, article);
  }

  Future<void> cacheAll(List<CachedArticle> articles) async {
    final map = {for (final a in articles) a.id: a};
    await _box.putAll(map);
  }

  CachedArticle? getById(String id) => _box.get(id);

  List<CachedArticle> getAll() => _box.values.toList();

  /// Returns articles cached within the last [duration].
  List<CachedArticle> getRecent(Duration duration) {
    final cutoff = DateTime.now().subtract(duration);
    return _box.values
        .where((a) => a.cachedAt.isAfter(cutoff))
        .toList();
  }

  Future<void> removeById(String id) async {
    await _box.delete(id);
  }

  Future<void> clearAll() async {
    await _box.clear();
  }

  /// Remove entries older than [maxAge].
  Future<int> evictStale(Duration maxAge) async {
    final cutoff = DateTime.now().subtract(maxAge);
    final staleKeys = _box.keys.where((key) {
      final article = _box.get(key);
      return article != null && article.cachedAt.isBefore(cutoff);
    }).toList();

    for (final key in staleKeys) {
      await _box.delete(key);
    }
    return staleKeys.length;
  }
}
```

---

## Lazy Boxes

Lazy boxes load values from disk on demand instead of keeping them all in memory.
Use them for large datasets or when you only access a few items at a time.

```dart
Future<void> openLazyBox() async {
  final lazyBox = await Hive.openLazyBox<CachedArticle>('largeArticleCache');

  // Read -- returns a Future instead of a synchronous value.
  final article = await lazyBox.get('article-123');

  // Write -- same API as a regular box.
  if (article != null) {
    await lazyBox.put('article-123', article);
  }

  // Iterate keys without loading all values.
  for (final key in lazyBox.keys) {
    final value = await lazyBox.get(key);
    if (value != null && _isExpired(value)) {
      await lazyBox.delete(key);
    }
  }
}

bool _isExpired(CachedArticle article) {
  return DateTime.now().difference(article.cachedAt) >
      const Duration(days: 7);
}
```

---

## Encrypted Boxes

Hive supports AES-256 encryption for sensitive (but non-credential) data.

> **Important:** For truly sensitive data like auth tokens and passwords, use
> `flutter_secure_storage` instead. Encrypted Hive boxes are appropriate for
> data such as locally cached personal notes or health records that need
> at-rest encryption but are not authentication credentials.

```dart
import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';

/// Generates or retrieves a 256-bit encryption key, stored securely.
Future<Uint8List> getEncryptionKey() async {
  const storage = FlutterSecureStorage();
  const storageKey = 'hive_encryption_key';

  final encoded = await storage.read(key: storageKey);
  if (encoded != null) {
    return base64Url.decode(encoded);
  }

  // Generate a new key and persist it in secure storage.
  final key = Hive.generateSecureKey();
  await storage.write(key: storageKey, value: base64UrlEncode(key));
  return Uint8List.fromList(key);
}

Future<Box<T>> openEncryptedBox<T>(String name) async {
  final key = await getEncryptionKey();
  return Hive.openBox<T>(
    name,
    encryptionCipher: HiveAesCipher(key),
  );
}

// Usage:
// final secureBox = await openEncryptedBox<String>('secureNotes');
// await secureBox.put('note1', 'This is encrypted at rest.');
// final note = secureBox.get('note1');
```

---

## Listening to Changes

### watch() on a Box

```dart
void listenToSettingsChanges(Box<UserSettings> box) {
  box.watch().listen((event) {
    final (:key, :value, :deleted) = (
      key: event.key,
      value: event.value as UserSettings?,
      deleted: event.deleted,
    );

    if (deleted) {
      print('Settings key "$key" was deleted.');
    } else {
      print('Settings key "$key" updated: darkMode=${value?.darkMode}');
    }
  });
}

// Watch a specific key.
void watchDarkMode(Box<UserSettings> box) {
  box.watch(key: 'current').listen((event) {
    final settings = event.value as UserSettings?;
    if (settings != null) {
      print('Dark mode is now: ${settings.darkMode}');
    }
  });
}
```

### Hive + ValueListenableBuilder

```dart
import 'package:flutter/material.dart';
import 'package:hive_flutter/hive_flutter.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final box = Hive.box<UserSettings>('userSettings');

    return ValueListenableBuilder<Box<UserSettings>>(
      valueListenable: box.listenable(keys: ['current']),
      builder: (context, box, _) {
        final settings = box.get(
          'current',
          defaultValue: const UserSettings(
            locale: 'en',
            darkMode: false,
            fontSize: 16.0,
          ),
        )!;

        return SwitchListTile(
          title: const Text('Dark Mode'),
          value: settings.darkMode,
          onChanged: (value) {
            box.put('current', settings.copyWith(darkMode: value));
          },
        );
      },
    );
  }
}
```

---

## Code Generation

Using `@HiveType` and `@HiveField` annotations eliminates the need to write
TypeAdapters by hand.

### Model with Annotations

```dart
// lib/data/models/cached_article.dart
import 'package:hive/hive.dart';

part 'cached_article.g.dart';

@HiveType(typeId: 1)
class CachedArticle extends HiveObject {
  @HiveField(0)
  final String id;

  @HiveField(1)
  final String title;

  @HiveField(2)
  final String body;

  @HiveField(3)
  final String? imageUrl;

  @HiveField(4)
  final DateTime cachedAt;

  @HiveField(5, defaultValue: false)
  final bool bookmarked;

  CachedArticle({
    required this.id,
    required this.title,
    required this.body,
    this.imageUrl,
    required this.cachedAt,
    this.bookmarked = false,
  });
}
```

### Enum with Annotations

```dart
// lib/data/models/article_status.dart
import 'package:hive/hive.dart';

part 'article_status.g.dart';

@HiveType(typeId: 2)
enum ArticleStatus {
  @HiveField(0)
  draft,

  @HiveField(1)
  published,

  @HiveField(2)
  archived,
}
```

### Generate Adapters

```bash
dart run build_runner build --delete-conflicting-outputs
```

### Rules for Adding Fields

- **Always append new fields** with the next `@HiveField` index. Never reorder
  or reuse an index that was previously assigned.
- Provide `defaultValue` in the annotation for any new field so that objects
  stored before the field existed can still be deserialized.
- Never remove a `@HiveField` -- mark it `@Deprecated` and stop using it in
  new code.

---

## Best Practices and Limitations

### Best Practices

1. **Register adapters before opening boxes.**
   Call all `Hive.registerAdapter(...)` in a single initialization function
   before any `Hive.openBox(...)`.

2. **Use typed boxes.**
   Prefer `Box<MyType>` over `Box<dynamic>` to catch type errors at compile time.

3. **Use `put()` with explicit keys** for singleton-style data (e.g. settings).
   Use `add()` only when you need auto-incrementing integer keys.

4. **Close boxes on app termination** (optional but recommended):
   ```dart
   await Hive.close();
   ```

5. **Compact boxes periodically** to reclaim disk space:
   ```dart
   if (box.length > 0) {
     await box.compact();
   }
   ```

6. **Never store sensitive credentials in Hive**, even in encrypted boxes.
   Use `flutter_secure_storage` for tokens, passwords, and API keys.

7. **Keep values small.** Hive loads the entire box index into memory. If you
   have millions of entries, use a lazy box or switch to Drift/Isar.

### Limitations

| Limitation                       | Workaround                                   |
|----------------------------------|----------------------------------------------|
| No query language                | Filter in Dart; use Isar/Drift for queries   |
| No relational joins              | Denormalize or use Drift for relational data |
| No built-in full-text search     | Use Isar or SQLite FTS5                      |
| Entire box index loaded to RAM   | Use lazy boxes for large datasets            |
| No multi-isolate concurrent access | Use Isar or Drift for background isolates   |
| No automatic schema migration    | Use `defaultValue` and append-only fields    |

---

## Migration from Hive to Isar

Isar is a fast, fully indexed NoSQL database designed as Hive's successor.
Below is a step-by-step migration guide.

### 1. Add Isar Dependencies

```yaml
dependencies:
  isar: ^4.0.0-dev.14
  isar_flutter_libs: ^4.0.0-dev.14
  path_provider: ^2.1.5

dev_dependencies:
  isar_generator: ^4.0.0-dev.14
  build_runner: ^2.4.14
```

### 2. Define Isar Collections (Equivalent to Hive Types)

```dart
// lib/data/models/article_isar.dart
import 'package:isar/isar.dart';

part 'article_isar.g.dart';

@collection
class Article {
  Id id = Isar.autoIncrement;

  @Index(type: IndexType.value)
  late String remoteId;

  @Index(type: IndexType.value, caseSensitive: false)
  late String title;

  late String body;
  String? imageUrl;

  @Index()
  late DateTime cachedAt;

  late bool bookmarked;
}
```

### 3. Data Migration Script

```dart
import 'package:hive_flutter/hive_flutter.dart';
import 'package:isar/isar.dart';
import 'package:path_provider/path_provider.dart';

import 'models/cached_article.dart'; // Hive model
import 'models/article_isar.dart';   // Isar model

Future<void> migrateHiveToIsar() async {
  // Open source (Hive) and destination (Isar).
  final hiveBox = await Hive.openBox<CachedArticle>('articleCache');
  final dir = await getApplicationDocumentsDirectory();
  final isar = await Isar.open([ArticleSchema], directory: dir.path);

  // Transfer data.
  final hiveArticles = hiveBox.values.toList();
  await isar.writeTxn(() async {
    for (final ha in hiveArticles) {
      final isarArticle = Article()
        ..remoteId = ha.id
        ..title = ha.title
        ..body = ha.body
        ..imageUrl = ha.imageUrl
        ..cachedAt = ha.cachedAt
        ..bookmarked = ha.bookmarked;
      await isar.articles.put(isarArticle);
    }
  });

  // Clean up Hive data after successful migration.
  await hiveBox.deleteFromDisk();
}
```

### 4. One-Time Migration Gate

```dart
import 'package:shared_preferences/shared_preferences.dart';

Future<void> runMigrationIfNeeded() async {
  final prefs = await SharedPreferences.getInstance();
  const migrationKey = 'hive_to_isar_migrated';

  if (prefs.getBool(migrationKey) == true) return;

  await migrateHiveToIsar();
  await prefs.setBool(migrationKey, true);
}
```

### 5. Querying with Isar

```dart
// Read all bookmarked articles, sorted by cache date descending.
Future<List<Article>> bookmarkedArticles(Isar isar) async {
  return isar.articles
      .filter()
      .bookmarkedEqualTo(true)
      .sortByCachedAtDesc()
      .findAll();
}

// Full-text-ish search on title.
Future<List<Article>> searchArticles(Isar isar, String query) async {
  return isar.articles
      .filter()
      .titleContains(query, caseSensitive: false)
      .findAll();
}

// Reactive query -- watch for changes.
Stream<List<Article>> watchRecentArticles(Isar isar) {
  return isar.articles
      .filter()
      .cachedAtGreaterThan(
        DateTime.now().subtract(const Duration(days: 7)),
      )
      .sortByCachedAtDesc()
      .watch(fireImmediately: true);
}
```
