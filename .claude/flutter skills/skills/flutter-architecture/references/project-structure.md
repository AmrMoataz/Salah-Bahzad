# Flutter-Aligned Flutter Project Structure

## Canonical Layout

```
lib/
├── main.dart
├── app/
│   ├── admin_application.dart
│   ├── di_configuration/
│   │   ├── configure.dart
│   │   └── configure.config.dart
│   ├── routes/
│   │   ├── routes.dart
│   │   ├── routes_mapper.dart
│   │   └── navigation_helper.dart
│   └── theme/
├── core/
├── data/
│   └── <feature>/
│       ├── model/
│       ├── repo/
│       ├── service/
│       └── mapper/
├── presentation/
│   ├── base/
│   │   ├── base_bloc.dart
│   │   ├── base_event.dart
│   │   └── base_state.dart
│   └── <feature>/
│       ├── <feature>_bloc.dart
│       ├── <feature>_event.dart
│       ├── <feature>_state.dart
│       └── mapper/
└── ui/
    ├── base/
    │   ├── base_screen.dart
    │   └── base_state_handler.dart
    └── <feature>/
        ├── <feature>_screen.dart
        ├── handler/
        └── widgets/
```

## Ownership Rules

| Directory | Owns | Avoids |
|-----------|------|--------|
| `app/` | app bootstrap, routing, app-wide theme/localization, DI init | feature business logic |
| `data/` | external integrations, repositories, services, models, mappers | direct widget concerns |
| `presentation/` | Bloc event/state orchestration and feature flow | direct networking/storage calls |
| `ui/` | declarative widget composition and rendering | business/data orchestration |
| `core/` | shared abstractions/constants reused across features | feature-specific leakage |

## Declarative Composition Rules

1. Build UI from current state, not from imperative mutation chains.
2. Trigger business intent through Bloc events.
3. Keep side effects at boundaries: bootstrap, repo/service calls, platform APIs.
4. Prefer stateless composition unless local ephemeral state is required.

## Feature Addition Checklist

1. Create `data/<feature>` with repo/service/model/mapper as needed.
2. Create `presentation/<feature>` with Bloc, event, and state.
3. Create `ui/<feature>` with screen, handler, and focused widgets.
4. Register dependencies in `app/di_configuration`.
5. Add route constant and mapping in `app/routes`.

## Naming Conventions

| Concern | Pattern |
|--------|---------|
| Bloc | `<feature>_bloc.dart` |
| Event | `<feature>_event.dart` |
| State | `<feature>_state.dart` |
| Repository Interface | `<feature>_repository.dart` |
| Repository Implementation | `<feature>_repository_impl.dart` |
| Service Interface | `<feature>_service.dart` |
| Service Implementation | `<feature>_service_impl.dart` |
| UI Screen | `<feature>_screen.dart` |
| UI State Handler | `<feature>_state_handler.dart` |
