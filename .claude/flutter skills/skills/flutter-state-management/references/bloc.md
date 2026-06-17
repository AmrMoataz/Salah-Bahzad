# Bloc/Cubit Reference (Flutter-Aligned)

## Default Position

Bloc is the default for feature workflows. Cubit is acceptable for simpler shared state within the same feature boundary.

## Event-State Contract

1. Events represent user/system intent.
2. Bloc handles async workflow and maps intent to immutable states.
3. UI renders declaratively from current state.

## Bloc Skeleton

```dart
sealed class HomeEvent {}
final class HomeStarted extends HomeEvent {}

enum HomeStatus { initial, loading, ready, failure }

class HomeState {
  const HomeState({this.status = HomeStatus.initial, this.errorMessage});
  final HomeStatus status;
  final String? errorMessage;

  HomeState copyWith({HomeStatus? status, String? errorMessage}) {
    return HomeState(
      status: status ?? this.status,
      errorMessage: errorMessage ?? this.errorMessage,
    );
  }
}

class HomeBloc extends Bloc<HomeEvent, HomeState> {
  HomeBloc(this.repository) : super(const HomeState()) {
    on<HomeStarted>(_onStarted);
  }

  final HomeRepository repository;

  Future<void> _onStarted(HomeStarted event, Emitter<HomeState> emit) async {
    emit(state.copyWith(status: HomeStatus.loading));
    final result = await repository.loadHome();
    emit(
      result.when(
        success: (_) => state.copyWith(status: HomeStatus.ready),
        error: (message) => state.copyWith(
          status: HomeStatus.failure,
          errorMessage: message,
        ),
      ),
    );
  }
}
```

## UI Integration

```dart
BlocConsumer<HomeBloc, HomeState>(
  listener: (context, state) {
    if (state.status == HomeStatus.failure && state.errorMessage != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(state.errorMessage!)),
      );
    }
  },
  builder: (context, state) {
    if (state.status == HomeStatus.loading) {
      return const Center(child: CircularProgressIndicator());
    }
    return HomeContent(
      onRefresh: () => context.read<HomeBloc>().add(HomeStarted()),
    );
  },
)
```

## Concurrency

Use `restartable`, `droppable`, or `sequential` only when event pressure requires explicit policy.

## Testing Baseline

Use `bloc_test` + `mockito`/`mocktail` for deterministic state transition testing.

## Anti-Patterns

1. Emitting mutable state objects.
2. Performing repository/network operations in widgets.
3. Running navigation side effects in builders instead of listeners.
4. Using Bloc for trivial local-only state.
