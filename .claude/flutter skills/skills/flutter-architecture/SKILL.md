---
name: flutter-architecture
description: >
  Flutter-aligned Flutter architecture guidance for layered app structure
  (app/data/presentation/ui/core), Bloc-first state orchestration, Dio-based data
  access, and dependency injection with get_it + injectable. Promotes
  declarative UI composition and limits imperative logic to boundaries.
license: MIT
metadata:
  author: https://github.com/anthropics
  version: "1.1.0"
  domain: mobile
  triggers: Flutter architecture, flutter style, layered architecture, bloc architecture, get_it, injectable, dio, project structure
  role: specialist
  scope: implementation
  output-format: code
  related-skills: flutter-state-management, flutter-navigation, flutter-networking, flutter-testing
---

# Flutter Architecture

## Role Definition

You are a senior Flutter architecture specialist working in the same technology posture as `flutter_admin_panel_app`: layered structure (`app`, `data`, `presentation`, `ui`, `core`), Bloc-centered presentation flow, `get_it` + `injectable` dependency injection, and Dio-based networking.

## When to Use This Skill

- Starting a new Flutter codebase that must match Flutter architectural conventions.
- Refactoring an existing project to the Flutter layered structure.
- Defining ownership boundaries between `data`, `presentation`, and `ui`.
- Establishing DI registration policy with `get_it` and `injectable`.
- Aligning architecture decisions with declarative UI and reactive state updates.

## Flutter Compatibility Rules

1. **Technology parity first**: keep architecture choices aligned to Flutter stack.
2. **Declarative-first UI**: render from current state, avoid imperative widget orchestration.
3. **Boundary-based imperative logic**: allow imperative code in startup, IO, and platform bridges only.
4. **Single source of truth**: presentation reacts to Bloc state; repositories own data access.
5. **Consistent layering**: keep responsibilities explicit and non-overlapping.

## Core Workflow

1. **Model structure**: organize `app`, `data`, `presentation`, `ui`, `core`.
2. **Define contracts**: repository interfaces in `data/*/repo`, service abstractions in `data/*/service`.
3. **Implement orchestration**: event-driven Bloc in `presentation/*`.
4. **Compose UI declaratively**: `ui/*` renders from Bloc state and screen handlers.
5. **Wire dependencies**: register with `injectable` and consume via `get_it`.

## Reference Guide

| Topic | Reference | Load When |
|-------|-----------|-----------|
| Project Structure | [references/project-structure.md](references/project-structure.md) | Folder boundaries and module ownership |
| Layering Model | [references/clean-architecture.md](references/clean-architecture.md) | Responsibility boundaries and flow |
| Dependency Injection | [references/dependency-injection.md](references/dependency-injection.md) | `get_it` + `injectable` setup and lifetimes |
| Base Classes | [references/base-classes.md](references/base-classes.md) | Standard implementation of BaseScreen, BaseBloc, etc. |
| Patterns | [references/design-patterns.md](references/design-patterns.md) | Flutter-compatible patterns and anti-patterns |

## Layer Rules

| Layer | Responsibility | Must Not Do |
|-------|----------------|-------------|
| app | Bootstrap, routes, global theme/localization | Own business workflows |
| data | API/storage integrations, repositories, services | Render widgets or route UI |
| presentation | Bloc events/states and feature orchestration | Perform direct HTTP/storage calls |
| ui | Stateless/stateful widgets and declarative rendering | Contain business or persistence logic |
| core | Shared cross-cutting types/utilities | Become a dump for feature logic |

## Constraints

1. Use `get_it` + `injectable` for dependency wiring.
2. Use Bloc as primary feature orchestration in presentation layer.
3. Use Dio for network boundary integrations.
4. Keep widgets declarative and state-driven.
5. Keep layer dependencies one-directional and explicit.
6. Do not make Riverpod, go_router, or alternate stacks the default architecture.
