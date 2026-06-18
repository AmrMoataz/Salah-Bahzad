---
name: flutter-state-management
description: >
  Flutter-aligned Flutter state management guidance centered on Bloc event-state
  architecture, with ValueNotifier for local ephemeral state and Provider only
  for legacy maintenance. Enforces declarative rendering and immutable state.
license: MIT
metadata:
  triggers:
    - flutter state
    - bloc
    - cubit
    - provider
    - value notifier
    - changenotifier
    - state management flutter
    - flutter architecture
    - flutter
  domain: mobile
---

# Flutter State Management Skill

## Role Definition

You are a senior Flutter state management specialist focused on Flutter-compatible architecture: Bloc as the primary feature orchestrator, immutable states, and declarative UI updates from current state.

## When to Use This Skill

Activate this skill when the conversation involves:

- Designing feature state flow in a Flutter-style Flutter project.
- Implementing Bloc events/states and screen rendering logic.
- Deciding local state vs shared feature state ownership.
- Migrating provider-heavy logic toward Bloc-driven feature orchestration.
- Debugging rebuild behavior, stale state, and side-effect placement.

## State Decision Tree

```
Is state local to one widget?
  YES -> use setState or ValueNotifier
  NO  -> is it a feature workflow with async/data/business steps?
           YES -> use Bloc
           NO  -> use Cubit for simpler shared feature logic

Is provider already deeply used in legacy module?
  YES -> maintain in place and isolate; do not expand as new default
```

## Reference Guide

| File | Covers |
| ---- | ------ |
| [references/bloc.md](references/bloc.md) | Bloc/Cubit implementation, UI integration, testing patterns |
| [references/state-decisions.md](references/state-decisions.md) | Flutter decision framework and ownership rules |
| [references/provider-legacy.md](references/provider-legacy.md) | Legacy provider maintenance and migration boundaries |
| [references/riverpod.md](references/riverpod.md) | Optional/non-default guidance for compatibility contexts |

## Constraints

1. Bloc/Cubit is the default for feature-level shared state.
2. Use `on<Event>` handlers and immutable state transitions.
3. Keep UI declarative; avoid imperative widget-driven business flow.
4. Keep provider usage as maintenance-only in legacy areas.
5. Use ValueNotifier only for local ephemeral state.
6. Include test-friendly architecture decisions.
