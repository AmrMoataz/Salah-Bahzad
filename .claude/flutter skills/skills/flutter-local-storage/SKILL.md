---
name: flutter-local-storage
description: >
  Flutter-aligned guidance on Flutter local persistence emphasizing
  SharedPreferences and flutter_secure_storage, with optional advanced stores
  when required by feature scope.
license: MIT
metadata:
  triggers:
    - storage
    - SharedPreferences
    - Hive
    - Drift
    - SQLite
    - Isar
    - offline
    - cache
    - secure storage
  domain: mobile
  related-skills:
    - flutter-architecture
    - flutter-networking
---

# Flutter Local Storage Skill

## Role

You are a Flutter local data persistence specialist. You design and implement
robust, type-safe, and performant local storage solutions for Flutter
applications. You select the right storage engine for every use case, architect
offline-first data layers, implement secure credential storage, and build
migration paths that protect user data across app updates while staying aligned
to Flutter stack defaults.

## When to Use

Activate this skill when the conversation involves any of the following:

- Persisting user preferences, settings, or feature flags locally.
- Storing secure auth/session data locally.
- Adding optional relational/NoSQL storage only when feature requirements justify it.
- Securing sensitive data such as auth tokens, API keys, or biometric secrets.
- Designing offline-first architectures with sync queues and conflict resolution.
- Implementing cache layers with TTL, versioning, or invalidation strategies.
- Migrating database schemas between app versions.
- Choosing between storage solutions for a given requirement.

## Storage Decision Tree

Use the following decision tree to recommend the correct storage engine:

```
Is the data sensitive (tokens, credentials)?
 YES --> flutter_secure_storage
 NO  --> Is it simple app preferences/flags?
          YES --> SharedPreferences
          NO  --> Evaluate Drift/Hive only if feature explicitly requires complex local data modeling
```

### Quick Comparison Table

| Feature                  | SharedPreferences | flutter_secure_storage | Hive (optional) | Drift (optional) |
|--------------------------|-------------------|------------------------|-----------------|------------------|
| Data model               | Key-value         | Key-value (encrypted) | NoSQL | Relational |
| Flutter default           | Yes               | Yes        | No             | No         |
| Typical usage            | Locale/theme/flags| tokens/credentials | feature cache | complex offline data |
| Code generation required | No                | No         | Optional       | Yes        |

## Reference Guide

| File                                              | Covers                                                                 |
|---------------------------------------------------|------------------------------------------------------------------------|
| [references/drift-sqlite.md](references/drift-sqlite.md)   | Drift setup, tables, CRUD, joins, streams, migrations, DAOs, testing   |
| [references/hive-nosql.md](references/hive-nosql.md)       | Hive init, TypeAdapters, lazy/encrypted boxes, codegen, Isar migration |
| [references/offline-first.md](references/offline-first.md) | Cache-first, sync queues, conflict resolution, background sync         |
| [references/secure-storage.md](references/secure-storage.md) | Secure storage, biometrics, platform config, SharedPreferences         |

## Constraints

- Use Dart 3+ syntax and sound null safety.
- Keep storage access in repositories/services, never directly in widgets.
- Default to SharedPreferences and flutter_secure_storage for Flutter-aligned projects.
- Never store sensitive data in plain text local storage.
- Introduce Hive/Drift only for explicit feature needs and document migration strategy.
