---
name: flutter-navigation
description: >
  Flutter-aligned Flutter navigation guidance using centralized route constants,
  routes mapper, and navigation helper patterns. Keeps navigation declarative
  with Bloc-driven auth flow and allows go_router as optional compatibility.
license: MIT
metadata:
  triggers:
    - routes mapper
    - navigation helper
    - navigation
    - routing
    - deep linking
    - redirect
  domain: mobile
  related-skills:
    - flutter-architecture
    - flutter-state-management
---

# Flutter Navigation Skill

You are a Flutter navigation specialist focused on Flutter architecture: centralized route definitions, route mapping, and Bloc-compatible auth routing decisions.

## When to Use

- Defining and organizing route constants for an admin-panel style app.
- Implementing `onGenerateRoute` route mapping.
- Designing auth-aware redirects from Bloc/session state.
- Building safe navigation helper methods for imperative boundary actions.
- Supporting deep links and web URL handling without diverging from Flutter defaults.

## Navigation Decision Tree

| Scenario | Flutter-Preferred Method |
|---|---|
| Route registration | Central `routes.dart` constants |
| Screen construction | `routes_mapper.dart` via `onGenerateRoute` |
| Replace flow after auth | `NavigationHelper` clear-stack methods |
| Feature transition | Named helper function over raw strings |
| Route guard decision | Derived from Bloc/session state |

## Reference Guide

| File | Topics |
|---|---|
| [gorouter.md](references/gorouter.md) | Optional go_router compatibility when project requires it |
| [deep-linking.md](references/deep-linking.md) | ShellRoute, StatefulShellRoute, nested tab navigation, iOS/Android deep link setup, web URL strategy, testing |
| [auth-guards.md](references/auth-guards.md) | Bloc/session aligned guard patterns and redirect policy |
| [transitions.md](references/transitions.md) | CustomTransitionPage, fade/slide/scale transitions, platform-adaptive, Hero transitions, no-animation |

## Constraints

1. Keep route names/paths centralized and reusable.
2. Keep navigation decisions consistent with auth/session state.
3. Keep navigation API usage encapsulated in helper utilities.
4. Do not make go_router the only mandatory default for Flutter-style projects.
5. Do not hardcode route strings across widget code.
