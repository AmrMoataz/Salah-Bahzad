# Flutter-Compatible Design Patterns

## Preferred Patterns

### Repository + Service

```dart
abstract class FacilityRepository {
  Future<DataResult<List<FacilityModel>>> getFacilities();
}

class FacilityRepositoryImpl implements FacilityRepository {
  FacilityRepositoryImpl(this.service);
  final FacilityService service;

  @override
  Future<DataResult<List<FacilityModel>>> getFacilities() {
    return service.getFacilities();
  }
}
```

### Bloc Event-State Flow

```dart
class HomeBloc extends Bloc<HomeEvent, HomeState> {
  HomeBloc(this.repository) : super(const HomeState()) {
    on<HomeStarted>(_onStarted);
  }

  final HomeRepository repository;
}
```

### Mapper Pattern

```dart
class FacilityMapper {
  FacilityUiModel toUi(FacilityModel model) {
    return FacilityUiModel(id: model.id, title: model.name);
  }
}
```

## Controlled Patterns

### Singleton via DI

Prefer `@singleton` or `@lazySingleton` through `injectable` registrations instead of ad-hoc static singleton classes.

### Strategy for Validation/Formatting

Use when rules vary by context, but keep usage close to forms or data mapping boundaries.

## Patterns to Avoid as Defaults

1. Global event bus as a primary feature communication pattern.
2. Imperative observer chains for UI updates that bypass Bloc state.
3. Over-engineered pattern stacking for small features.

## Selection Guide

| Problem | Recommended Pattern |
|---------|---------------------|
| API/data orchestration | Repository + Service |
| Feature workflow/state transitions | Bloc event/state |
| API/UI model transformation | Mapper |
| Cross-app infrastructure object lifetime | DI singleton/lazy singleton |
| Dynamic business rule variation | Strategy |

## Declarative Rule

No pattern should bypass declarative rendering. UI must still be derived from current state and rebuilt reactively.
