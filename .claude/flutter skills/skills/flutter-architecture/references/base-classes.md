# Base Classes

This reference defines the core base classes required for the Convie architecture. These classes standardize the interaction between UI, State Management, and Dependency Injection.

## Base Event & State

Define abstract base classes for all Bloc events and states.

### `presentation/base/base_event.dart`

```dart
abstract class BaseEvent {}
```

### `presentation/base/base_state.dart`

```dart
abstract class BaseState {}
```

## Base Bloc

A base class for all Blocs to ensure they use the correct event and state types.

### `presentation/base/base_bloc.dart`

```dart
import 'package:flutter_bloc/flutter_bloc.dart';
import 'base_event.dart';
import 'base_state.dart';

abstract class BaseBloc extends Bloc<BaseEvent, BaseState> {
  BaseBloc(BaseState initialState) : super(initialState);
}
```

## Base State Handler

Handles UI state logic during the build phase, separating it from the widget tree. It provides access to screen dimensions and context.

### `ui/base/base_state_handler.dart`

```dart
import 'package:flutter/material.dart';
import 'package:your_app_name/presentation/base/base_state.dart';

abstract class BaseStateHandler<StateType extends BaseState> {
  double height = 0;
  double width = 0;
  late BuildContext mainContext;
  Orientation orientation = Orientation.portrait;

  /// Called within the BlocBuilder to handle state changes or configure the handler.
  handleStates(BuildContext blocContext, StateType state);
}
```

## Base Screen

The fundamental building block for all screens. It handles:
- Dependency Injection (via `getIt`) for Bloc and StateHandler.
- Lifecycle management.
- Scaffold setup (AppBar, FAB, BottomSheet, etc.).
- Responsive layout properties in StateHandler.

### `ui/base/base_screen.dart`

```dart
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:get_it/get_it.dart'; // Or your DI setup
import 'package:your_app_name/presentation/base/base_event.dart';
import 'package:your_app_name/presentation/base/base_state.dart';
import 'package:your_app_name/ui/base/base_state_handler.dart';

// Ensure GetIt instance is accessible
final getIt = GetIt.instance;

abstract class BaseScreen<
    BaseBlocType extends Bloc<BaseEvent, BaseState>, 
    BaseStateHandlerType extends BaseStateHandler> extends StatefulWidget {
  
  BaseScreen({Key? key}) : super(key: key);

  BaseBlocType bloc = getIt.get<BaseBlocType>();
  BaseStateHandlerType stateHandler = getIt.get<BaseStateHandlerType>();

  @override
  State<BaseScreen> createState() =>
      _BaseScreenState<BaseBlocType, BaseStateHandlerType>();

  PreferredSizeWidget? appBar(BuildContext context) {
    return null;
  }

  Widget? bottomNavigationBar() {
    return null;
  }

  Widget content(BuildContext context);

  Widget? fab() {
    return null;
  }

  void init() {}

  Widget? bottomSheet(BuildContext context) {
    return null;
  }

  void onDismiss() {}
}

class _BaseScreenState<
    BaseBlocType extends Bloc<BaseEvent, BaseState>,
    BaseStateHandlerType extends BaseStateHandler> extends State<BaseScreen> {
  
  @override
  void initState() {
    // Re-fetch bloc if needed or use the one from widget
    widget.bloc = getIt.get<BaseBlocType>();
    widget.init();
    super.initState();
  }

  @override
  Widget build(BuildContext context) {
    if (widget.bloc.isClosed) {
      widget.bloc = getIt.get<BaseBlocType>();
    }
    return BlocProvider<BaseBlocType>(
        create: (context) => widget.bloc as BaseBlocType,
        child: BlocBuilder<BaseBlocType, BaseState>(
            bloc: widget.bloc as BaseBlocType,
            builder: (blocContext, state) {
              widget.stateHandler.width = MediaQuery.of(context).size.width;
              widget.stateHandler.height = MediaQuery.of(context).size.height;
              widget.stateHandler.orientation = MediaQuery.of(context).orientation;
              
              widget.stateHandler.handleStates(blocContext, state);
              
              return GestureDetector(
                child: Scaffold(
                  extendBody: true,
                  resizeToAvoidBottomInset: false,
                  backgroundColor: Colors.white, 
                  appBar: widget.appBar(context),
                  body: Builder(
                    builder: (ctx) {
                      widget.stateHandler.mainContext = ctx;
                      return widget.content(widget.stateHandler.mainContext);
                    },
                  ),
                  floatingActionButton: widget.fab(),
                  bottomNavigationBar: widget.bottomNavigationBar(),
                  floatingActionButtonLocation:
                      FloatingActionButtonLocation.centerFloat,
                  bottomSheet: widget.bottomSheet(context),
                ),
                onTap: () {
                  widget.onDismiss();
                  FocusManager.instance.primaryFocus?.unfocus();
                }
              );
            }));
  }

  @override
  void dispose() {
    super.dispose();
  }
}
```
