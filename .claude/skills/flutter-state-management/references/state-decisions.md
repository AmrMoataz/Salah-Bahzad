# Flutter State Management Decision Framework

## Primary Rule

Use Bloc/Cubit for feature-level shared state and business workflows. Use `setState` or `ValueNotifier` only for local ephemeral UI state.

## Decision Matrix

| Scenario | Default |
|----------|---------|
| Single widget toggle/input/focus | `setState` or `ValueNotifier` |
| Feature flow with async + side effects | `Bloc` |
| Shared state with simple transitions | `Cubit` |
| Legacy module already on Provider | Maintain in place, do not expand as default |

## Ownership Model

1. Local UI state belongs to widget.
2. Feature workflow state belongs to Bloc/Cubit.
3. App session state belongs to dedicated root feature blocs.
4. Persisted state belongs to storage/repository boundaries, not UI.

## Declarative Rendering Contract

1. Widgets read state and render.
2. Widgets emit intent through events/actions.
3. Bloc performs orchestration and emits new immutable state.
4. UI rebuild is a pure consequence of state.

## Flutter-Compatible Example

```dart
class CreateFacilityBloc extends Bloc<CreateFacilityEvent, CreateFacilityState> {
  CreateFacilityBloc(this.repository) : super(const CreateFacilityState()) {
    on<CreateFacilitySubmitted>(_onSubmitted);
  }

  final FacilityRepository repository;

  Future<void> _onSubmitted(
    CreateFacilitySubmitted event,
    Emitter<CreateFacilityState> emit,
  ) async {
    emit(state.copyWith(status: CreateFacilityStatus.loading));
    final result = await repository.createFacility(event.request);
    emit(
      result.when(
        success: (_) => state.copyWith(status: CreateFacilityStatus.success),
        error: (message) => state.copyWith(
          status: CreateFacilityStatus.failure,
          errorMessage: message,
        ),
      ),
    );
  }
}
```

## Anti-Patterns

1. Mutating state objects directly.
2. Performing repository/network work directly in widgets.
3. Treating Provider or Riverpod as equal defaults in Flutter-aligned projects.
4. Triggering navigation or snackbar logic from build methods.
