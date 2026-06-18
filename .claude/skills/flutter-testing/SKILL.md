---
name: flutter-testing
description: >
  Flutter-aligned Flutter testing guidance covering unit, widget, and integration
  tests with flutter_test, bloc_test, and mockito/mocktail. Prioritizes
  Bloc-driven feature tests and deterministic behavior.
license: MIT
metadata:
  triggers:
    - test
    - unit test
    - widget test
    - integration test
    - golden test
    - mock
    - TDD
    - coverage
    - bloc_test
  domain: mobile
  related-skills:
    - flutter-state-management
    - flutter-component
---

# Flutter Testing Skill

## Role

You are a Flutter testing specialist for Flutter-style architecture. You validate Bloc presentation flows, repository/service boundaries, and declarative UI rendering with deterministic test suites.

## When to Use

Activate this skill when the user asks to:

- Write or review unit, widget, integration, or golden tests.
- Set up mocking with mocktail or Mockito.
- Test BLoC and Cubit state transitions.
- Configure code coverage or CI test pipelines.
- Apply TDD or BDD practices in a Flutter project.
- Debug flaky or failing tests.
- Create test fixtures, factories, or helpers.

## Test Pyramid

Follow the testing pyramid to balance confidence and speed:

```
         /  Integration  \        ← Few, slow, high confidence
        /  Widget Tests    \      ← Moderate count, medium speed
       /   Unit Tests        \    ← Many, fast, isolated
      ──────────────────────────
```

| Layer       | Scope                        | Speed   | Isolation |
|-------------|------------------------------|---------|-----------|
| Unit        | Single function / class      | Fast    | Full      |
| Widget      | Single widget or small tree  | Medium  | High      |
| Integration | Full app or multi-screen flow| Slow    | Low       |
| Golden      | Visual snapshot comparison   | Medium  | High      |

Aim for roughly **70% unit / 20% widget / 10% integration** by count.

## Reference Guide

| File | Covers |
|------|--------|
| [references/unit-tests.md](references/unit-tests.md) | Test structure, matchers, async testing, fixtures, service/repository tests |
| [references/widget-tests.md](references/widget-tests.md) | WidgetTester, BLoC wiring, navigation, forms, responsive screen testing |
| [references/integration-tests.md](references/integration-tests.md) | Full-app flows, real vs mock services, screenshots, CI, performance profiling |
| [references/golden-tests.md](references/golden-tests.md) | matchesGoldenFile, font loading, platform goldens, Alchemist, CI strategy |
| [references/mocking.md](references/mocking.md) | mockito/mocktail, stubbing, verification, Dio and bloc_test patterns |

## Constraints

1. Prefer `bloc_test` for feature-state behavior.
2. Keep tests deterministic and isolated from real network/time by default.
3. Mock data boundaries, not widget internals.
4. Keep assertions focused on observable behavior and rendered output.
5. Preserve CI parity and deterministic reproducibility.
