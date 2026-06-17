# JSON Serialization

Complete guide to JSON serialization in Dart and Flutter using `json_serializable`, `freezed`, and custom converters. Covers annotations, nested objects, generics, enums, and the `build_runner` workflow.

---

## Table of Contents

1. [json_serializable Setup](#json_serializable-setup)
2. [@JsonSerializable Annotation](#jsonserializable-annotation)
3. [@JsonKey for Field Mapping, Defaults, and Custom Converters](#jsonkey-for-field-mapping-defaults-and-custom-converters)
4. [freezed for Immutable Data Classes](#freezed-for-immutable-data-classes)
5. [Nested Object Serialization](#nested-object-serialization)
6. [List and Map Serialization](#list-and-map-serialization)
7. [Custom JsonConverter](#custom-jsonconverter)
8. [Enum Serialization](#enum-serialization)
9. [Generic Type Serialization](#generic-type-serialization)
10. [build_runner Workflow](#build_runner-workflow)

---

## json_serializable Setup

### Installation

```yaml
# pubspec.yaml
dependencies:
  json_annotation: ^4.9.0

dev_dependencies:
  build_runner: ^2.4.0
  json_serializable: ^6.8.0
```

### Global Build Configuration

```yaml
# build.yaml (optional, place in project root)
targets:
  $default:
    builders:
      json_serializable:
        options:
          # Use snake_case for all fields by default
          field_rename: snake
          # Include fields that are null
          include_if_null: false
          # Generate explicit toJson
          explicit_to_json: true
          # Check for required keys during deserialization
          checked: true
```

---

## @JsonSerializable Annotation

### Basic Model

```dart
import 'package:json_annotation/json_annotation.dart';

part 'user.g.dart';

@JsonSerializable()
final class User {
  const User({
    required this.id,
    required this.name,
    required this.email,
    this.avatarUrl,
  });

  factory User.fromJson(Map<String, Object?> json) => _$UserFromJson(json);

  final int id;
  final String name;
  final String email;
  final String? avatarUrl;

  Map<String, Object?> toJson() => _$UserToJson(this);
}
```

### With Custom Options

```dart
import 'package:json_annotation/json_annotation.dart';

part 'api_response.g.dart';

@JsonSerializable(
  fieldRename: FieldRename.snake,
  includeIfNull: false,
  explicitToJson: true,
  checked: true,
)
final class ApiResponse {
  const ApiResponse({
    required this.statusCode,
    required this.requestId,
    this.errorMessage,
  });

  factory ApiResponse.fromJson(Map<String, Object?> json) =>
      _$ApiResponseFromJson(json);

  final int statusCode;      // maps to "status_code" in JSON
  final String requestId;    // maps to "request_id" in JSON
  final String? errorMessage; // maps to "error_message", omitted if null

  Map<String, Object?> toJson() => _$ApiResponseToJson(this);
}
```

---

## @JsonKey for Field Mapping, Defaults, and Custom Converters

### Field Name Mapping

```dart
import 'package:json_annotation/json_annotation.dart';

part 'product.g.dart';

@JsonSerializable()
final class Product {
  const Product({
    required this.id,
    required this.productName,
    required this.priceInCents,
    required this.isAvailable,
    this.createdAt,
  });

  factory Product.fromJson(Map<String, Object?> json) =>
      _$ProductFromJson(json);

  final int id;

  /// Maps to "product_name" in JSON.
  @JsonKey(name: 'product_name')
  final String productName;

  /// Maps to "price_cents" in JSON.
  @JsonKey(name: 'price_cents')
  final int priceInCents;

  /// Maps to "available" in JSON, defaults to false if missing.
  @JsonKey(name: 'available', defaultValue: false)
  final bool isAvailable;

  /// Included in serialization but ignored during deserialization
  /// if not present.
  @JsonKey(includeIfNull: false)
  final DateTime? createdAt;

  Map<String, Object?> toJson() => _$ProductToJson(this);
}
```

### Computed / Read-Only Fields

```dart
import 'package:json_annotation/json_annotation.dart';

part 'order.g.dart';

@JsonSerializable()
final class Order {
  const Order({
    required this.id,
    required this.items,
    required this.currency,
  });

  factory Order.fromJson(Map<String, Object?> json) => _$OrderFromJson(json);

  final String id;
  final List<OrderItem> items;
  final String currency;

  /// Computed field: not in JSON input but included in output.
  @JsonKey(includeFromJson: false, includeToJson: true)
  int get totalCents => items.fold(0, (sum, item) => sum + item.priceCents);

  Map<String, Object?> toJson() => _$OrderToJson(this);
}

@JsonSerializable()
final class OrderItem {
  const OrderItem({required this.name, required this.priceCents});

  factory OrderItem.fromJson(Map<String, Object?> json) =>
      _$OrderItemFromJson(json);

  final String name;
  @JsonKey(name: 'price_cents')
  final int priceCents;

  Map<String, Object?> toJson() => _$OrderItemToJson(this);
}
```

### @JsonKey with fromJson / toJson Functions

```dart
import 'package:json_annotation/json_annotation.dart';

part 'event.g.dart';

/// The server sends timestamps as Unix epoch milliseconds.
@JsonSerializable()
final class Event {
  const Event({
    required this.id,
    required this.title,
    required this.startsAt,
  });

  factory Event.fromJson(Map<String, Object?> json) => _$EventFromJson(json);

  final String id;
  final String title;

  @JsonKey(
    fromJson: _dateTimeFromEpochMs,
    toJson: _dateTimeToEpochMs,
  )
  final DateTime startsAt;

  Map<String, Object?> toJson() => _$EventToJson(this);
}

DateTime _dateTimeFromEpochMs(int ms) =>
    DateTime.fromMillisecondsSinceEpoch(ms, isUtc: true);

int _dateTimeToEpochMs(DateTime dt) => dt.millisecondsSinceEpoch;
```

---

## freezed for Immutable Data Classes

### Installation

```yaml
# pubspec.yaml
dependencies:
  freezed_annotation: ^2.4.0
  json_annotation: ^4.9.0

dev_dependencies:
  build_runner: ^2.4.0
  freezed: ^2.5.0
  json_serializable: ^6.8.0
```

### Basic freezed Model

```dart
import 'package:freezed_annotation/freezed_annotation.dart';

part 'user_profile.freezed.dart';
part 'user_profile.g.dart';

@freezed
sealed class UserProfile with _$UserProfile {
  const factory UserProfile({
    required int id,
    required String name,
    required String email,
    @Default('') String bio,
    String? avatarUrl,
    @Default([]) List<String> tags,
  }) = _UserProfile;

  factory UserProfile.fromJson(Map<String, Object?> json) =>
      _$UserProfileFromJson(json);
}
```

Usage:

```dart
void freezedExample() {
  const profile = UserProfile(
    id: 1,
    name: 'Alice',
    email: 'alice@example.com',
    bio: 'Dart enthusiast',
  );

  // copyWith for immutable updates
  final updated = profile.copyWith(bio: 'Flutter & Dart enthusiast');

  // Serialization
  final json = updated.toJson();
  final restored = UserProfile.fromJson(json);

  // Equality and hashCode are generated
  assert(profile != updated);
  assert(restored == updated);
}
```

### freezed Union Types (Sealed)

```dart
import 'package:freezed_annotation/freezed_annotation.dart';

part 'payment_method.freezed.dart';
part 'payment_method.g.dart';

@freezed
sealed class PaymentMethod with _$PaymentMethod {
  const factory PaymentMethod.creditCard({
    required String last4,
    required String brand,
    required int expMonth,
    required int expYear,
  }) = CreditCard;

  const factory PaymentMethod.bankAccount({
    required String bankName,
    required String last4,
  }) = BankAccount;

  const factory PaymentMethod.digitalWallet({
    required String provider, // e.g., "apple_pay", "google_pay"
    required String email,
  }) = DigitalWallet;

  factory PaymentMethod.fromJson(Map<String, Object?> json) =>
      _$PaymentMethodFromJson(json);
}
```

Usage with pattern matching:

```dart
String describePayment(PaymentMethod method) {
  return switch (method) {
    CreditCard(:final brand, :final last4) => '$brand ending in $last4',
    BankAccount(:final bankName, :final last4) => '$bankName ****$last4',
    DigitalWallet(:final provider) => provider.replaceAll('_', ' '),
  };
}
```

---

## Nested Object Serialization

```dart
import 'package:freezed_annotation/freezed_annotation.dart';

part 'blog_post.freezed.dart';
part 'blog_post.g.dart';

@freezed
sealed class Author with _$Author {
  const factory Author({
    required int id,
    required String name,
    String? avatarUrl,
  }) = _Author;

  factory Author.fromJson(Map<String, Object?> json) =>
      _$AuthorFromJson(json);
}

@freezed
sealed class Tag with _$Tag {
  const factory Tag({
    required String slug,
    required String label,
  }) = _Tag;

  factory Tag.fromJson(Map<String, Object?> json) => _$TagFromJson(json);
}

@freezed
sealed class BlogPost with _$BlogPost {
  const factory BlogPost({
    required int id,
    required String title,
    required String body,
    required Author author,
    @Default([]) List<Tag> tags,
    required DateTime publishedAt,
  }) = _BlogPost;

  factory BlogPost.fromJson(Map<String, Object?> json) =>
      _$BlogPostFromJson(json);
}
```

Corresponding JSON:

```json
{
  "id": 42,
  "title": "Dart 3 Features",
  "body": "Dart 3 introduces records, patterns, and class modifiers...",
  "author": {
    "id": 1,
    "name": "Alice",
    "avatar_url": "https://example.com/alice.png"
  },
  "tags": [
    { "slug": "dart", "label": "Dart" },
    { "slug": "flutter", "label": "Flutter" }
  ],
  "published_at": "2025-09-15T10:30:00Z"
}
```

---

## List and Map Serialization

### List of Objects

```dart
import 'package:json_annotation/json_annotation.dart';

part 'notification_list.g.dart';

@JsonSerializable()
final class AppNotification {
  const AppNotification({
    required this.id,
    required this.message,
    required this.read,
  });

  factory AppNotification.fromJson(Map<String, Object?> json) =>
      _$AppNotificationFromJson(json);

  final String id;
  final String message;
  final bool read;

  Map<String, Object?> toJson() => _$AppNotificationToJson(this);
}

/// Helper to deserialize a list of notifications from a raw JSON list.
List<AppNotification> parseNotificationList(List<Object?> jsonList) {
  return jsonList
      .whereType<Map<String, Object?>>()
      .map(AppNotification.fromJson)
      .toList();
}
```

### Map with Object Values

```dart
import 'package:json_annotation/json_annotation.dart';

part 'settings.g.dart';

@JsonSerializable(explicitToJson: true)
final class UserSettings {
  const UserSettings({
    required this.preferences,
    required this.featureFlags,
  });

  factory UserSettings.fromJson(Map<String, Object?> json) =>
      _$UserSettingsFromJson(json);

  /// Keys are preference names, values are preference objects.
  final Map<String, Preference> preferences;

  /// Simple key-value map for feature toggles.
  final Map<String, bool> featureFlags;

  Map<String, Object?> toJson() => _$UserSettingsToJson(this);
}

@JsonSerializable()
final class Preference {
  const Preference({required this.value, required this.updatedAt});

  factory Preference.fromJson(Map<String, Object?> json) =>
      _$PreferenceFromJson(json);

  final String value;
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$PreferenceToJson(this);
}
```

---

## Custom JsonConverter

A `JsonConverter` is used when you need full control over how a type is serialized and deserialized, especially when the type is third-party or the JSON shape differs significantly from the Dart shape.

### DateTime as ISO 8601 String

```dart
import 'package:json_annotation/json_annotation.dart';

final class Iso8601DateTimeConverter
    implements JsonConverter<DateTime, String> {
  const Iso8601DateTimeConverter();

  @override
  DateTime fromJson(String json) => DateTime.parse(json);

  @override
  String toJson(DateTime object) => object.toUtc().toIso8601String();
}
```

Apply per-field:

```dart
@Iso8601DateTimeConverter()
final DateTime createdAt;
```

Apply to an entire class:

```dart
@JsonSerializable()
@Iso8601DateTimeConverter()
final class TimestampedEntity {
  // ...
}
```

### Duration as Seconds

```dart
import 'package:json_annotation/json_annotation.dart';

final class DurationSecondsConverter
    implements JsonConverter<Duration, int> {
  const DurationSecondsConverter();

  @override
  Duration fromJson(int json) => Duration(seconds: json);

  @override
  int toJson(Duration object) => object.inSeconds;
}
```

### Color as Hex String

```dart
import 'dart:ui';

import 'package:json_annotation/json_annotation.dart';

final class ColorHexConverter implements JsonConverter<Color, String> {
  const ColorHexConverter();

  @override
  Color fromJson(String json) {
    final hex = json.replaceFirst('#', '');
    final value = int.parse(hex, radix: 16);
    return switch (hex.length) {
      6 => Color(value | 0xFF000000), // Add full opacity
      8 => Color(value),
      _ => throw FormatException('Invalid hex color: $json'),
    };
  }

  @override
  String toJson(Color object) {
    // ignore: deprecated_member_use
    return '#${object.value.toRadixString(16).padLeft(8, '0')}';
  }
}
```

### Nullable Converter Wrapper

```dart
import 'package:json_annotation/json_annotation.dart';

/// Wraps any [JsonConverter] to handle nullable values.
final class NullableConverter<T, S>
    implements JsonConverter<T?, S?> {
  const NullableConverter(this._inner);

  final JsonConverter<T, S> _inner;

  @override
  T? fromJson(S? json) => json == null ? null : _inner.fromJson(json);

  @override
  S? toJson(T? object) => object == null ? null : _inner.toJson(object);
}
```

Usage:

```dart
@NullableConverter(Iso8601DateTimeConverter())
final DateTime? deletedAt;
```

---

## Enum Serialization

### Default (name-based)

By default, `json_serializable` uses the enum member's name.

```dart
import 'package:json_annotation/json_annotation.dart';

part 'task.g.dart';

enum TaskStatus {
  @JsonValue('pending')
  pending,

  @JsonValue('in_progress')
  inProgress,

  @JsonValue('completed')
  completed,

  @JsonValue('cancelled')
  cancelled,
}

@JsonSerializable()
final class Task {
  const Task({
    required this.id,
    required this.title,
    required this.status,
    required this.priority,
  });

  factory Task.fromJson(Map<String, Object?> json) => _$TaskFromJson(json);

  final String id;
  final String title;
  final TaskStatus status;
  final TaskPriority priority;

  Map<String, Object?> toJson() => _$TaskToJson(this);
}
```

### Integer-Valued Enum

```dart
import 'package:json_annotation/json_annotation.dart';

enum TaskPriority {
  @JsonValue(0)
  low,

  @JsonValue(1)
  medium,

  @JsonValue(2)
  high,

  @JsonValue(3)
  critical,
}
```

### Enhanced Enum with Custom Converter

```dart
import 'package:json_annotation/json_annotation.dart';

enum Currency {
  usd('USD', r'$'),
  eur('EUR', '\u20AC'),
  gbp('GBP', '\u00A3'),
  jpy('JPY', '\u00A5');

  const Currency(this.code, this.symbol);
  final String code;
  final String symbol;
}

final class CurrencyConverter implements JsonConverter<Currency, String> {
  const CurrencyConverter();

  @override
  Currency fromJson(String json) {
    return Currency.values.firstWhere(
      (c) => c.code == json,
      orElse: () => throw FormatException('Unknown currency: $json'),
    );
  }

  @override
  String toJson(Currency object) => object.code;
}
```

---

## Generic Type Serialization

### Paginated Response Wrapper

```dart
import 'package:json_annotation/json_annotation.dart';

part 'paginated_response.g.dart';

/// A generic paginated API response.
///
/// The generated code calls `fromJsonT` / `toJsonT` callbacks for the
/// generic items list. You must supply these when calling fromJson/toJson.
@JsonSerializable(genericArgumentFactories: true)
final class PaginatedResponse<T> {
  const PaginatedResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
  });

  factory PaginatedResponse.fromJson(
    Map<String, Object?> json,
    T Function(Object? json) fromJsonT,
  ) =>
      _$PaginatedResponseFromJson(json, fromJsonT);

  final List<T> items;
  final int totalCount;
  final int page;
  final int pageSize;

  bool get hasNextPage => page * pageSize < totalCount;

  Map<String, Object?> toJson(Object? Function(T value) toJsonT) =>
      _$PaginatedResponseToJson(this, toJsonT);
}
```

Usage:

```dart
void genericExample(Map<String, Object?> json) {
  final response = PaginatedResponse<User>.fromJson(
    json,
    (item) => User.fromJson(item! as Map<String, Object?>),
  );

  for (final user in response.items) {
    print(user.name);
  }
}
```

### API Result Wrapper

```dart
import 'package:json_annotation/json_annotation.dart';

part 'api_envelope.g.dart';

/// Generic envelope that the API wraps all responses in:
/// `{ "data": <T>, "meta": { ... } }`
@JsonSerializable(genericArgumentFactories: true)
final class ApiEnvelope<T> {
  const ApiEnvelope({
    required this.data,
    this.meta,
  });

  factory ApiEnvelope.fromJson(
    Map<String, Object?> json,
    T Function(Object? json) fromJsonT,
  ) =>
      _$ApiEnvelopeFromJson(json, fromJsonT);

  final T data;
  final ResponseMeta? meta;

  Map<String, Object?> toJson(Object? Function(T value) toJsonT) =>
      _$ApiEnvelopeToJson(this, toJsonT);
}

@JsonSerializable()
final class ResponseMeta {
  const ResponseMeta({
    this.requestId,
    this.serverTime,
  });

  factory ResponseMeta.fromJson(Map<String, Object?> json) =>
      _$ResponseMetaFromJson(json);

  final String? requestId;
  final DateTime? serverTime;

  Map<String, Object?> toJson() => _$ResponseMetaToJson(this);
}
```

---

## build_runner Workflow

### Common Commands

```bash
# One-time build (CI / pre-commit)
dart run build_runner build --delete-conflicting-outputs

# Watch mode during development
dart run build_runner watch --delete-conflicting-outputs

# Clean generated files
dart run build_runner clean
```

### Filtering by Directory

```bash
# Only generate for files in lib/models/
dart run build_runner build \
  --build-filter="lib/models/**" \
  --delete-conflicting-outputs
```

### Ignoring Generated Files in Version Control

```gitignore
# .gitignore
# Uncomment the line below if you want to regenerate in CI
# and not track generated files:
# *.g.dart
# *.freezed.dart
```

Most teams **commit** generated files to avoid requiring `build_runner` on every checkout. If you choose not to commit them, add a CI step:

```yaml
# .github/workflows/ci.yml (excerpt)
- name: Generate code
  run: dart run build_runner build --delete-conflicting-outputs

- name: Verify no uncommitted generated files
  run: git diff --exit-code
```

### Performance Tips

1. **Use `build_filter`** in watch mode to limit which files trigger regeneration.
2. **Split large models** into separate files so that a change in one model does not regenerate everything.
3. **Avoid circular imports** between files with `part` directives -- they slow the resolver.
4. **Use `explicit_to_json: true`** in `build.yaml` globally rather than per-annotation to keep model files clean.
5. **Pin `build_runner` version** in `pubspec.yaml` to avoid unexpected rebuild behavior across team members.
