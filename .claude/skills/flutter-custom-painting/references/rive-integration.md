# Rive Integration Reference

## Table of Contents

1. [RiveAnimation.asset and RiveAnimation.network](#riveanimationasset-and-riveanimationnetwork)
2. [Artboard and StateMachine Controllers](#artboard-and-statemachine-controllers)
3. [SMI Inputs (Trigger, Bool, Number)](#smi-inputs-trigger-bool-number)
4. [Interactive Animations Responding to User Input](#interactive-animations-responding-to-user-input)
5. [Custom Rive Widget with RiveAnimationController](#custom-rive-widget-with-riveanimationcontroller)
6. [Combining Rive with Flutter Widgets](#combining-rive-with-flutter-widgets)
7. [Performance Best Practices](#performance-best-practices)

---

## Prerequisites

Add the `rive` package to your `pubspec.yaml`:

```yaml
dependencies:
  rive: ^0.13.0
```

Import in Dart:

```dart
import 'package:rive/rive.dart';
```

---

## RiveAnimation.asset and RiveAnimation.network

The simplest way to display a Rive animation is with the convenience widgets.

### From an asset

Place the `.riv` file in your assets directory and declare it in `pubspec.yaml`:

```yaml
flutter:
  assets:
    - assets/animations/loading.riv
```

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class LoadingIndicator extends StatelessWidget {
  const LoadingIndicator({super.key});

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      width: 120,
      height: 120,
      child: RiveAnimation.asset(
        'assets/animations/loading.riv',
        fit: BoxFit.contain,
        alignment: Alignment.center,
        // Optionally specify the artboard and animation by name.
        artboard: 'Main',
        animations: ['spin'],
      ),
    );
  }
}
```

### From a network URL

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class NetworkRiveWidget extends StatelessWidget {
  const NetworkRiveWidget({super.key, required this.url});

  final String url;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 200,
      height: 200,
      child: RiveAnimation.network(
        url,
        fit: BoxFit.cover,
        placeHolder: const Center(child: CircularProgressIndicator()),
      ),
    );
  }
}
```

---

## Artboard and StateMachine Controllers

For more control, load the `.riv` file manually and instantiate controllers.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rive/rive.dart';

class RiveStateMachineWidget extends StatefulWidget {
  const RiveStateMachineWidget({super.key});

  @override
  State<RiveStateMachineWidget> createState() =>
      _RiveStateMachineWidgetState();
}

class _RiveStateMachineWidgetState extends State<RiveStateMachineWidget> {
  Artboard? _artboard;
  StateMachineController? _stateMachineController;

  @override
  void initState() {
    super.initState();
    _loadRiveFile();
  }

  Future<void> _loadRiveFile() async {
    final data = await rootBundle.load('assets/animations/interactive.riv');
    final file = RiveFile.import(data);

    // Use the default artboard or specify one by name.
    final artboard = file.mainArtboard.instance();

    // Attach a state machine controller.
    final controller = StateMachineController.fromArtboard(
      artboard,
      'MainStateMachine', // Name of the state machine in the Rive editor.
    );

    if (controller != null) {
      artboard.addController(controller);
    }

    if (mounted) {
      setState(() {
        _artboard = artboard;
        _stateMachineController = controller;
      });
    }
  }

  @override
  void dispose() {
    _stateMachineController?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final artboard = _artboard;
    if (artboard == null) {
      return const SizedBox(width: 300, height: 300);
    }

    return SizedBox(
      width: 300,
      height: 300,
      child: Rive(
        artboard: artboard,
        fit: BoxFit.contain,
      ),
    );
  }
}
```

### Key Concepts

| Class | Purpose |
|---|---|
| `RiveFile` | The parsed binary file. |
| `Artboard` | A single canvas/scene inside the file. A file can have multiple artboards. |
| `StateMachineController` | Drives a state machine on an artboard; provides access to SMI inputs. |
| `SimpleAnimation` | Plays a single timeline animation (no state machine). |

---

## SMI Inputs (Trigger, Bool, Number)

State machines expose inputs that control transitions between states. Access them through the `StateMachineController`.

### SMITrigger

A fire-and-forget input. It has no persistent value; it simply triggers a transition.

```dart
SMITrigger? _triggerJump;

void _setupInputs(StateMachineController controller) {
  _triggerJump = controller.findInput<bool>('jump') as SMITrigger?;
}

void _onJumpPressed() {
  _triggerJump?.fire();
}
```

### SMIBool

A boolean input that toggles between states.

```dart
SMIBool? _isHappy;

void _setupInputs(StateMachineController controller) {
  _isHappy = controller.findInput<bool>('isHappy') as SMIBool?;
}

void _toggleMood() {
  final input = _isHappy;
  if (input != null) {
    input.value = !input.value;
  }
}
```

### SMINumber

A numeric input for continuous or stepped values.

```dart
SMINumber? _healthLevel;

void _setupInputs(StateMachineController controller) {
  _healthLevel = controller.findInput<double>('health') as SMINumber?;
}

void _setHealth(double value) {
  _healthLevel?.value = value;
}
```

### Complete Inputs Example

```dart
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rive/rive.dart';

class CharacterWidget extends StatefulWidget {
  const CharacterWidget({super.key});

  @override
  State<CharacterWidget> createState() => _CharacterWidgetState();
}

class _CharacterWidgetState extends State<CharacterWidget> {
  Artboard? _artboard;
  StateMachineController? _controller;

  SMITrigger? _triggerWave;
  SMIBool? _isWalking;
  SMINumber? _speed;

  @override
  void initState() {
    super.initState();
    _loadRiveFile();
  }

  Future<void> _loadRiveFile() async {
    final data = await rootBundle.load('assets/animations/character.riv');
    final file = RiveFile.import(data);
    final artboard = file.mainArtboard.instance();
    final controller = StateMachineController.fromArtboard(
      artboard,
      'CharacterStateMachine',
    );

    if (controller != null) {
      artboard.addController(controller);
      _triggerWave = controller.findInput<bool>('wave') as SMITrigger?;
      _isWalking = controller.findInput<bool>('isWalking') as SMIBool?;
      _speed = controller.findInput<double>('speed') as SMINumber?;
    }

    if (mounted) {
      setState(() {
        _artboard = artboard;
        _controller = controller;
      });
    }
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final artboard = _artboard;

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          width: 300,
          height: 300,
          child: artboard != null
              ? Rive(artboard: artboard, fit: BoxFit.contain)
              : const Center(child: CircularProgressIndicator()),
        ),
        const SizedBox(height: 16),
        Wrap(
          spacing: 8,
          children: [
            ElevatedButton(
              onPressed: () => _triggerWave?.fire(),
              child: const Text('Wave'),
            ),
            ElevatedButton(
              onPressed: () {
                final walking = _isWalking;
                if (walking != null) {
                  walking.value = !walking.value;
                }
              },
              child: const Text('Toggle Walk'),
            ),
            ElevatedButton(
              onPressed: () => _speed?.value = (_speed?.value ?? 0) + 1,
              child: const Text('Speed +1'),
            ),
          ],
        ),
      ],
    );
  }
}
```

---

## Interactive Animations Responding to User Input

### Pointer-Driven Animation

Rive state machines can respond to pointer events (hover, click). Use `RiveAnimation` with `stateMachines` to get built-in hit testing on Rive listeners.

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class InteractiveButton extends StatelessWidget {
  const InteractiveButton({super.key});

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      width: 200,
      height: 80,
      child: RiveAnimation.asset(
        'assets/animations/button.riv',
        stateMachines: ['ButtonStateMachine'],
        fit: BoxFit.contain,
      ),
    );
  }
}
```

When the Rive file has pointer listeners configured in the Rive editor, the `RiveAnimation` widget automatically forwards pointer events to the state machine. Hover, press, and release events drive state transitions without additional Dart code.

### Slider-Driven Animation

Drive a Rive animation with a Flutter `Slider`.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:rive/rive.dart';

class SliderDrivenRive extends StatefulWidget {
  const SliderDrivenRive({super.key});

  @override
  State<SliderDrivenRive> createState() => _SliderDrivenRiveState();
}

class _SliderDrivenRiveState extends State<SliderDrivenRive> {
  Artboard? _artboard;
  StateMachineController? _controller;
  SMINumber? _progress;
  double _sliderValue = 0;

  @override
  void initState() {
    super.initState();
    _loadRiveFile();
  }

  Future<void> _loadRiveFile() async {
    final data = await rootBundle.load('assets/animations/progress.riv');
    final file = RiveFile.import(data);
    final artboard = file.mainArtboard.instance();
    final controller = StateMachineController.fromArtboard(
      artboard,
      'ProgressMachine',
    );

    if (controller != null) {
      artboard.addController(controller);
      _progress = controller.findInput<double>('progress') as SMINumber?;
    }

    if (mounted) {
      setState(() {
        _artboard = artboard;
        _controller = controller;
      });
    }
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final artboard = _artboard;

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          width: 300,
          height: 200,
          child: artboard != null
              ? Rive(artboard: artboard, fit: BoxFit.contain)
              : const SizedBox.shrink(),
        ),
        Slider(
          value: _sliderValue,
          onChanged: (value) {
            setState(() => _sliderValue = value);
            _progress?.value = value * 100; // Map 0..1 to 0..100.
          },
        ),
      ],
    );
  }
}
```

---

## Custom Rive Widget with RiveAnimationController

For scenarios where you need a simple timeline animation (not a state machine), use `RiveAnimationController` subclasses.

### SimpleAnimation Controller

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class PulseIcon extends StatefulWidget {
  const PulseIcon({super.key});

  @override
  State<PulseIcon> createState() => _PulseIconState();
}

class _PulseIconState extends State<PulseIcon> {
  late final RiveAnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = SimpleAnimation(
      'pulse', // Name of the animation in the Rive file.
      autoplay: true,
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 48,
      height: 48,
      child: RiveAnimation.asset(
        'assets/animations/icon.riv',
        controllers: [_controller],
        fit: BoxFit.contain,
      ),
    );
  }
}
```

### OneShotAnimation Controller

Plays an animation once and optionally runs a callback on completion.

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class SuccessCheckmark extends StatefulWidget {
  const SuccessCheckmark({super.key, this.onComplete});

  final VoidCallback? onComplete;

  @override
  State<SuccessCheckmark> createState() => _SuccessCheckmarkState();
}

class _SuccessCheckmarkState extends State<SuccessCheckmark> {
  late final RiveAnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = OneShotAnimation(
      'checkmark',
      autoplay: true,
      onStop: () => widget.onComplete?.call(),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 100,
      height: 100,
      child: RiveAnimation.asset(
        'assets/animations/success.riv',
        controllers: [_controller],
        fit: BoxFit.contain,
      ),
    );
  }
}
```

### Controlling Playback Programmatically

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class PlayPauseRive extends StatefulWidget {
  const PlayPauseRive({super.key});

  @override
  State<PlayPauseRive> createState() => _PlayPauseRiveState();
}

class _PlayPauseRiveState extends State<PlayPauseRive> {
  late final SimpleAnimation _controller;
  bool _isPlaying = true;

  @override
  void initState() {
    super.initState();
    _controller = SimpleAnimation('idle', autoplay: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _togglePlayback() {
    setState(() {
      _isPlaying = !_isPlaying;
      _controller.isActive = _isPlaying;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          width: 200,
          height: 200,
          child: RiveAnimation.asset(
            'assets/animations/character.riv',
            controllers: [_controller],
            fit: BoxFit.contain,
          ),
        ),
        IconButton(
          onPressed: _togglePlayback,
          icon: Icon(_isPlaying ? Icons.pause : Icons.play_arrow),
          tooltip: _isPlaying ? 'Pause' : 'Play',
        ),
      ],
    );
  }
}
```

---

## Combining Rive with Flutter Widgets

### Rive as a Background

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class RiveBackgroundPage extends StatelessWidget {
  const RiveBackgroundPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Stack(
        fit: StackFit.expand,
        children: [
          // Rive animation fills the entire background.
          const Positioned.fill(
            child: IgnorePointer(
              child: RiveAnimation.asset(
                'assets/animations/background.riv',
                fit: BoxFit.cover,
                animations: ['ambient'],
              ),
            ),
          ),
          // Flutter UI layered on top.
          Center(
            child: Card(
              elevation: 8,
              child: Padding(
                padding: const EdgeInsets.all(32),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      'Welcome',
                      style: Theme.of(context).textTheme.headlineMedium,
                    ),
                    const SizedBox(height: 16),
                    ElevatedButton(
                      onPressed: () {},
                      child: const Text('Get Started'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
```

### Rive Inside a Button

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class AnimatedIconButton extends StatefulWidget {
  const AnimatedIconButton({
    super.key,
    required this.riveAsset,
    required this.stateMachineName,
    required this.label,
    required this.onPressed,
  });

  final String riveAsset;
  final String stateMachineName;
  final String label;
  final VoidCallback onPressed;

  @override
  State<AnimatedIconButton> createState() => _AnimatedIconButtonState();
}

class _AnimatedIconButtonState extends State<AnimatedIconButton> {
  SMIBool? _isHovered;

  void _onInit(Artboard artboard) {
    final controller = StateMachineController.fromArtboard(
      artboard,
      widget.stateMachineName,
    );
    if (controller != null) {
      artboard.addController(controller);
      _isHovered = controller.findInput<bool>('isHovered') as SMIBool?;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: widget.label,
      child: MouseRegion(
        onEnter: (_) => _isHovered?.value = true,
        onExit: (_) => _isHovered?.value = false,
        child: GestureDetector(
          onTap: widget.onPressed,
          child: SizedBox(
            width: 56,
            height: 56,
            child: RiveAnimation.asset(
              widget.riveAsset,
              onInit: _onInit,
              fit: BoxFit.contain,
            ),
          ),
        ),
      ),
    );
  }
}
```

### Rive with AnimatedSwitcher

Transition between different Rive artboards with Flutter's built-in transitions.

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class RiveTabSwitcher extends StatefulWidget {
  const RiveTabSwitcher({super.key});

  @override
  State<RiveTabSwitcher> createState() => _RiveTabSwitcherState();
}

class _RiveTabSwitcherState extends State<RiveTabSwitcher> {
  String _currentArtboard = 'Home';

  static const _artboards = ['Home', 'Search', 'Profile'];

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Expanded(
          child: AnimatedSwitcher(
            duration: const Duration(milliseconds: 300),
            child: SizedBox(
              key: ValueKey(_currentArtboard),
              width: double.infinity,
              height: double.infinity,
              child: RiveAnimation.asset(
                'assets/animations/tabs.riv',
                artboard: _currentArtboard,
                animations: ['enter'],
                fit: BoxFit.contain,
              ),
            ),
          ),
        ),
        SegmentedButton<String>(
          segments: [
            for (final name in _artboards)
              ButtonSegment(value: name, label: Text(name)),
          ],
          selected: {_currentArtboard},
          onSelectionChanged: (selected) {
            setState(() => _currentArtboard = selected.first);
          },
        ),
      ],
    );
  }
}
```

---

## Performance Best Practices

### 1. Reuse Artboard Instances

Creating a new `Artboard` instance is cheap, but parsing `RiveFile` is not. Parse the file once, then create instances as needed.

```dart
// Parse once (e.g., during app initialization).
final data = await rootBundle.load('assets/animations/character.riv');
final riveFile = RiveFile.import(data);

// Create instances from the cached file.
Artboard createCharacterArtboard() {
  return riveFile.mainArtboard.instance();
}
```

### 2. Limit Visible Rive Widgets

Each `Rive` widget runs its own animation loop. In list views, ensure off-screen widgets are not animating.

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class LazyRiveItem extends StatefulWidget {
  const LazyRiveItem({super.key, required this.asset});

  final String asset;

  @override
  State<LazyRiveItem> createState() => _LazyRiveItemState();
}

class _LazyRiveItemState extends State<LazyRiveItem>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => false; // Allow disposal when off-screen.

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return SizedBox(
      height: 120,
      child: RiveAnimation.asset(
        widget.asset,
        fit: BoxFit.contain,
      ),
    );
  }
}
```

### 3. Use `RepaintBoundary`

Isolate the Rive widget layer to prevent unnecessary repaints of surrounding Flutter widgets.

```dart
RepaintBoundary(
  child: SizedBox(
    width: 200,
    height: 200,
    child: RiveAnimation.asset('assets/animations/loader.riv'),
  ),
)
```

### 4. Prefer State Machines Over Multiple Timeline Animations

State machines let the Rive runtime blend and transition between animations efficiently on the GPU side. Using many separate `SimpleAnimation` controllers on the same artboard is less efficient and harder to manage.

### 5. Reduce Artboard Complexity

- Flatten unnecessary groups in the Rive editor.
- Merge paths that are always the same color.
- Use clipping sparingly inside the artboard; each clip region adds a render pass.
- Keep mesh vertex counts low for morph/bone deformations.

### 6. Profile with DevTools

Use the Flutter Performance overlay (`WidgetsApp.showPerformanceOverlay`) and the DevTools timeline to identify jank caused by Rive rendering.

```dart
MaterialApp(
  showPerformanceOverlay: true, // Enable during profiling only.
  home: const MyHomePage(),
)
```

### 7. Dispose Controllers

Always dispose `StateMachineController` and `RiveAnimationController` instances in `dispose()` to stop animation tickers and free memory.

```dart
@override
void dispose() {
  _stateMachineController?.dispose();
  super.dispose();
}
```
