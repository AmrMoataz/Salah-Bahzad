# Widget Testing in Flutter

## testWidgets() Setup

Widget tests live alongside unit tests in the `test/` directory. Use
`testWidgets()` instead of `test()` to get access to `WidgetTester`.

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('Counter increments when FAB is tapped', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(home: CounterPage()),
    );

    expect(find.text('0'), findsOneWidget);

    await tester.tap(find.byIcon(Icons.add));
    await tester.pump();

    expect(find.text('1'), findsOneWidget);
  });
}
```

---

## WidgetTester Methods

| Method | Purpose |
|--------|---------|
| `pumpWidget(widget)` | Builds the widget tree from scratch. Call once per test. |
| `pump([duration])` | Triggers a single frame rebuild. Pass a `Duration` to advance the clock. |
| `pumpAndSettle()` | Pumps frames until there are no pending frames (animations complete). |
| `pumpFrames(duration)` | Pumps continuously for the given duration. |

### pumpWidget

Always wrap the widget under test in a `MaterialApp` (or `CupertinoApp`) to
provide inherited widgets like `Theme`, `MediaQuery`, and `Navigator`.

```dart
await tester.pumpWidget(
  MaterialApp(
    home: Scaffold(
      body: MyWidget(),
    ),
  ),
);
```

### pump vs pumpAndSettle

Use `pump()` when you want to advance exactly one frame. Use
`pumpAndSettle()` to wait for all animations and scheduled frames to
complete. Avoid `pumpAndSettle()` in tests that have infinite animations
(like an indeterminate progress indicator) -- use `pump()` with a specific
duration instead.

```dart
// Tap a button that triggers an animation.
await tester.tap(find.byType(ElevatedButton));

// Advance one frame to see the immediate state change.
await tester.pump();

// Or wait for the animation to finish.
await tester.pumpAndSettle();
```

---

## Finders

Finders locate widgets in the tree by various criteria.

```dart
// By runtime type
find.byType(ElevatedButton);

// By text content
find.text('Submit');
find.textContaining('Sub');

// By Key
find.byKey(const Key('submit-button'));
find.byKey(const ValueKey<String>('email-field'));

// By icon
find.byIcon(Icons.add);

// By widget predicate
find.byWidgetPredicate(
  (widget) => widget is Text && widget.data!.startsWith('Error'),
);

// By descendant / ancestor
find.descendant(
  of: find.byType(AppBar),
  matching: find.text('Home'),
);
find.ancestor(
  of: find.text('Home'),
  matching: find.byType(AppBar),
);

// By semantic label (accessibility)
find.bySemanticsLabel('Close dialog');
```

---

## Actions

```dart
// Tap
await tester.tap(find.byType(ElevatedButton));

// Tap at a specific location within a widget
await tester.tapAt(const Offset(100, 200));

// Enter text in a TextField
await tester.enterText(find.byType(TextField), 'hello@example.com');

// Long press
await tester.longPress(find.byKey(const Key('item-1')));

// Drag
await tester.drag(find.byType(Dismissible), const Offset(500, 0));

// Fling (fast drag)
await tester.fling(find.byType(ListView), const Offset(0, -300), 1000);

// Scroll until visible
await tester.scrollUntilVisible(
  find.text('Item 50'),
  200.0,
  scrollable: find.byType(Scrollable),
);
```

---

## Expecting Widgets

```dart
// Exactly one widget matches.
expect(find.text('Hello'), findsOneWidget);

// No widgets match.
expect(find.text('Error'), findsNothing);

// Exactly N widgets match.
expect(find.byType(ListTile), findsNWidgets(5));

// At least one widget matches.
expect(find.byType(ListTile), findsWidgets);

// At least N widgets match.
expect(find.byType(ListTile), findsAtLeast(3));

// Verify widget properties.
final textWidget = tester.widget<Text>(find.text('Hello'));
expect(textWidget.style?.color, Colors.red);
```

---

## Testing with Bloc

Flutter-style widget tests focus on Bloc-driven rendering and user interactions.

```dart
import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:my_app/features/counter/bloc/counter_bloc.dart';
import 'package:my_app/features/counter/presentation/counter_page.dart';

class MockCounterBloc extends MockBloc<CounterEvent, CounterState>
    implements CounterBloc {}

void main() {
  late MockCounterBloc mockBloc;

  setUp(() {
    mockBloc = MockCounterBloc();
  });

  testWidgets('renders count from bloc state', (tester) async {
    when(() => mockBloc.state).thenReturn(const CounterState(count: 42));

    await tester.pumpWidget(
      MaterialApp(
        home: BlocProvider<CounterBloc>.value(
          value: mockBloc,
          child: const CounterPage(),
        ),
      ),
    );

    expect(find.text('42'), findsOneWidget);
  });

  testWidgets('adds IncrementEvent when FAB is tapped', (tester) async {
    when(() => mockBloc.state).thenReturn(const CounterState(count: 0));

    await tester.pumpWidget(
      MaterialApp(
        home: BlocProvider<CounterBloc>.value(
          value: mockBloc,
          child: const CounterPage(),
        ),
      ),
    );

    await tester.tap(find.byIcon(Icons.add));

    verify(() => mockBloc.add(const IncrementEvent())).called(1);
  });
}
```

---

## Testing Navigation

Prefer testing `Navigator` or project navigation helpers directly. Use go_router tests only when go_router is adopted for a specific project.

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';
import 'package:my_app/features/home/presentation/home_page.dart';

class MockNavigationHelper extends Mock implements NavigationHelper {}

void main() {
  late MockNavigationHelper mockNavigationHelper;

  setUp(() {
    mockNavigationHelper = MockNavigationHelper();
  });

  testWidgets('navigates to settings when gear icon is tapped',
      (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: HomePage(navigationHelper: mockNavigationHelper),
      ),
    );

    await tester.tap(find.byIcon(Icons.settings));

    verify(() => mockNavigationHelper.toSettings()).called(1);
  });
}
```

---

## Testing Forms

```dart
testWidgets('shows validation errors for empty fields', (tester) async {
  await tester.pumpWidget(
    const MaterialApp(home: Scaffold(body: LoginForm())),
  );

  // Tap submit without entering anything.
  await tester.tap(find.byType(ElevatedButton));
  await tester.pump();

  expect(find.text('Email is required'), findsOneWidget);
  expect(find.text('Password is required'), findsOneWidget);
});

testWidgets('submits form with valid data', (tester) async {
  await tester.pumpWidget(
    const MaterialApp(home: Scaffold(body: LoginForm())),
  );

  await tester.enterText(
    find.byKey(const Key('email-field')),
    'user@example.com',
  );
  await tester.enterText(
    find.byKey(const Key('password-field')),
    's3cret!Pass',
  );
  await tester.tap(find.byType(ElevatedButton));
  await tester.pumpAndSettle();

  // Verify navigation or success state.
  expect(find.text('Welcome'), findsOneWidget);
});
```

---

## Rendering with Specific Screen Sizes

Control the virtual screen size in widget tests. This is critical for
testing responsive layouts.

```dart
testWidgets('shows rail navigation on tablet', (tester) async {
  // Set a tablet-sized surface.
  tester.view.physicalSize = const Size(1024, 768);
  tester.view.devicePixelRatio = 1.0;

  addTearDown(() {
    tester.view.resetPhysicalSize();
    tester.view.resetDevicePixelRatio();
  });

  await tester.pumpWidget(
    const MaterialApp(home: ResponsiveScaffold()),
  );

  expect(find.byType(NavigationRail), findsOneWidget);
  expect(find.byType(BottomNavigationBar), findsNothing);
});

testWidgets('shows bottom nav on phone', (tester) async {
  tester.view.physicalSize = const Size(375, 812);
  tester.view.devicePixelRatio = 1.0;

  addTearDown(() {
    tester.view.resetPhysicalSize();
    tester.view.resetDevicePixelRatio();
  });

  await tester.pumpWidget(
    const MaterialApp(home: ResponsiveScaffold()),
  );

  expect(find.byType(BottomNavigationBar), findsOneWidget);
  expect(find.byType(NavigationRail), findsNothing);
});
```

---

## Helper: pumpApp

Create a reusable `pumpApp` helper to reduce boilerplate in every test file.

```dart
// test/helpers/pump_app.dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

extension PumpApp on WidgetTester {
  Future<void> pumpApp(
    Widget widget, {
    ThemeData? theme,
    Size? surfaceSize,
  }) async {
    if (surfaceSize != null) {
      view.physicalSize = surfaceSize;
      view.devicePixelRatio = 1.0;
      addTearDown(() {
        view.resetPhysicalSize();
        view.resetDevicePixelRatio();
      });
    }

    await pumpWidget(
      MaterialApp(
        theme: theme ?? ThemeData.light(),
        home: Scaffold(body: widget),
      ),
    );
  }
}
```

Usage:

```dart
import '../helpers/pump_app.dart';

testWidgets('renders greeting', (tester) async {
  await tester.pumpApp(const GreetingWidget());
  expect(find.text('Hello'), findsOneWidget);
});
```
