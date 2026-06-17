---
name: flutter-networking
description: >
  Flutter-aligned Flutter networking guidance centered on Dio, repository/service
  boundaries, and typed result handling. Treats GraphQL and non-Dio stacks as
  optional integrations rather than defaults.
license: MIT
metadata:
  triggers:
    - Dio
    - API
    - HTTP
    - REST
    - networking
    - serialization
    - flutter
  domain: mobile
  related-skills:
    - flutter-architecture
    - flutter-state-management
---

# Flutter Networking Skill

## Role

You are a Flutter networking specialist working in a Flutter-style stack: Dio at the network boundary, repository/service abstractions in the data layer, and presentation consuming typed results without leaking HTTP concerns into widgets.

## When to Use

Activate this skill when the conversation involves any of the following:

- Setting up or configuring **Dio** (base options, interceptors, adapters).
- Building a **REST API** integration layer (CRUD, pagination, file upload/download).
- Designing **retry**, **caching**, or **offline-first** networking strategies.
- Serializing / deserializing JSON with **json_serializable**, **freezed**, or custom converters.
- Handling **authentication flows** at the network layer (token injection, refresh, logout-on-401).
- Discussing **certificate pinning**, **proxy configuration**, or **TLS** setup in Flutter.

## Reference Guide

| Reference File | Description |
|---|---|
| [references/dio-setup.md](references/dio-setup.md) | Dio configuration, interceptors, cancellation, and Flutter-style typed error handling. |
| [references/graphql.md](references/graphql.md) | Optional GraphQL integration when required by backend. |
| [references/websockets.md](references/websockets.md) | Optional real-time integration patterns when required by feature. |
| [references/serialization.md](references/serialization.md) | JSON serialization with `json_serializable` and `freezed` -- annotations, custom converters, generics, enums, and `build_runner` workflow. |

## Constraints

1. **Dart 3+ only** -- use records, sealed classes, pattern matching, and class modifiers (`final`, `base`, `interface`) where they add clarity.
2. **No `dynamic`** -- every type must be explicit or properly inferred. Use `Object?` or generics instead of `dynamic`.
3. **Dio-first** -- do not introduce alternate HTTP stacks as the default.
4. **Typed results** -- represent failure/success with typed result objects, not raw thrown strings.
5. **Testability** -- every public API must accept its dependencies via constructor injection so it can be tested with mocks/fakes.
6. **Cancellation** -- long-running requests must support cancellation via `CancelToken` (Dio) or `StreamSubscription.cancel()`.
7. **No secrets in source** -- API keys, tokens, and URLs must come from environment configuration (e.g., `--dart-define`, `.env` via `envied`), never hard-coded.
8. **Logging** -- all network traffic logging must be conditional on a debug flag and must redact sensitive headers (`Authorization`, cookies).
9. **Immutability** -- data transfer objects must be immutable.
10. **Platform-aware** -- note platform differences (e.g., certificate pinning on iOS vs Android, WebSocket on web) where relevant.
