# Third-Party Animation: Rive and Lottie

---

## Lottie

Lottie renders After Effects animations exported as JSON via the Bodymovin plugin. The `lottie` package is the standard Flutter implementation.

### Setup

```yaml
# pubspec.yaml
dependencies:
  lottie: ^3.1.0
```

Place your `.json` Lottie files in the assets directory and declare them:

```yaml
# pubspec.yaml
flutter:
  assets:
    - assets/animations/
```

### Loading Lottie from Assets

```dart
import 'package:flutter/material.dart';
import 'package:lottie/lottie.dart';

class LottieAssetExample extends StatelessWidget {
  const LottieAssetExample({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Lottie.asset(
          'assets/animations/loading.json',
          width: 200,
          height: 200,
          fit: BoxFit.contain,
          // Plays automatically and loops by default
        ),
      ),
    );
  }
}
```

### Loading Lottie from Network

```dart
import 'package:flutter/material.dart';
import 'package:lottie/lottie.dart';

class LottieNetworkExample extends StatelessWidget {
  const LottieNetworkExample({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Lottie.network(
          'https://assets.lottiefiles.com/packages/lf20_example.json',
          width: 250,
          height: 250,
          fit: BoxFit.contain,
          errorBuilder: (context, error, stackTrace) {
            return const Icon(
              Icons.error_outline,
              size: 48,
              color: Colors.red,
            );
          },
          frameBuilder: (context, child, composition) {
            // Show a placeholder until the animation is loaded
            if (composition == null) {
              return const SizedBox(
                width: 250,
                height: 250,
                child: Center(child: CircularProgressIndicator()),
              );
            }
            return child;
          },
        ),
      ),
    );
  }
}
```

### Controlling Lottie Playback

For full control over playback (play, pause, seek, loop a specific segment), use an `AnimationController`.

```dart
import 'package:flutter/material.dart';
import 'package:lottie/lottie.dart';

class LottieControlledExample extends StatefulWidget {
  const LottieControlledExample({super.key});

  @override
  State<LottieControlledExample> createState() =>
      _LottieControlledExampleState();
}

class _LottieControlledExampleState extends State<LottieControlledExample>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  bool _isPlaying = false;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _togglePlayPause() {
    setState(() {
      if (_isPlaying) {
        _controller.stop();
      } else {
        _controller.repeat();
      }
      _isPlaying = !_isPlaying;
    });
  }

  void _playOnce() {
    _controller.forward(from: 0).then((_) {
      if (mounted) {
        setState(() => _isPlaying = false);
      }
    });
    setState(() => _isPlaying = true);
  }

  void _seekTo(double value) {
    _controller.value = value;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Lottie.asset(
            'assets/animations/success.json',
            controller: _controller,
            width: 200,
            height: 200,
            onLoaded: (composition) {
              // Set the duration from the Lottie file's own duration
              _controller.duration = composition.duration;
            },
          ),
          const SizedBox(height: 24),
          // Seek slider
          AnimatedBuilder(
            animation: _controller,
            builder: (context, _) {
              return Slider(
                value: _controller.value,
                onChanged: _seekTo,
              );
            },
          ),
          const SizedBox(height: 16),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              IconButton(
                icon: Icon(_isPlaying ? Icons.pause : Icons.play_arrow),
                iconSize: 48,
                onPressed: _togglePlayPause,
              ),
              const SizedBox(width: 16),
              FilledButton(
                onPressed: _playOnce,
                child: const Text('Play Once'),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
```

### Playing a Specific Frame Range

```dart
Lottie.asset(
  'assets/animations/multi_section.json',
  controller: _controller,
  onLoaded: (composition) {
    _controller.duration = composition.duration;
    // Play only frames 0 to 60 out of a total animation
    // Convert frame numbers to progress values:
    // progress = frameNumber / totalFrames
    final totalFrames = composition.endFrame - composition.startFrame;
    _controller.animateTo(
      60 / totalFrames,
      from: 0,
      duration: Duration(
        milliseconds: ((60 / totalFrames) * composition.duration.inMilliseconds).round(),
      ),
    );
  },
)
```

### Lottie Delegates for Dynamic Theming

Replace colors, text, or images at runtime using delegates:

```dart
Lottie.asset(
  'assets/animations/themed.json',
  delegates: LottieDelegates(
    values: [
      // Override the color of a specific layer
      ValueDelegate.color(
        const ['Layer Name', '**'],
        value: Theme.of(context).colorScheme.primary,
      ),
      // Override opacity
      ValueDelegate.opacity(
        const ['Background', '**'],
        value: 50, // 0-100
      ),
    ],
  ),
)
```

---

## Rive

Rive is a real-time animation tool with a custom runtime. Unlike Lottie's frame-by-frame approach, Rive uses skeletal animation and a state machine, enabling interactive, branching animations driven by user input.

### Setup

```yaml
# pubspec.yaml
dependencies:
  rive: ^0.13.0
```

Place `.riv` files in assets:

```yaml
# pubspec.yaml
flutter:
  assets:
    - assets/rive/
```

### Basic Rive Animation

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class RiveBasicExample extends StatelessWidget {
  const RiveBasicExample({super.key});

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(
        child: SizedBox(
          width: 300,
          height: 300,
          child: RiveAnimation.asset(
            'assets/rive/character.riv',
            fit: BoxFit.contain,
            // Plays the default animation automatically
          ),
        ),
      ),
    );
  }
}
```

### Specifying an Artboard and Animation

```dart
RiveAnimation.asset(
  'assets/rive/multi_artboard.riv',
  artboard: 'MainCharacter',
  animations: const ['idle'], // Play a specific animation by name
  fit: BoxFit.contain,
)
```

### Loading Rive from Network

```dart
RiveAnimation.network(
  'https://cdn.rive.app/animations/example.riv',
  fit: BoxFit.contain,
  placeHolder: const Center(child: CircularProgressIndicator()),
)
```

---

## Rive StateMachine for Interactive Animations

The state machine is Rive's most powerful feature. It lets you define states, transitions, and inputs in the Rive editor, then drive them from Dart code.

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class RiveStateMachineExample extends StatefulWidget {
  const RiveStateMachineExample({super.key});

  @override
  State<RiveStateMachineExample> createState() =>
      _RiveStateMachineExampleState();
}

class _RiveStateMachineExampleState extends State<RiveStateMachineExample> {
  StateMachineController? _stateMachineController;
  SMIBool? _isHovered;
  SMITrigger? _clickTrigger;
  SMINumber? _level;

  void _onRiveInit(Artboard artboard) {
    final controller = StateMachineController.fromArtboard(
      artboard,
      'ButtonStateMachine', // Name of the state machine in the Rive file
    );

    if (controller != null) {
      artboard.addController(controller);
      _stateMachineController = controller;

      // Find inputs by their names (defined in the Rive editor)
      _isHovered = controller.findInput<bool>('isHovered') as SMIBool?;
      _clickTrigger = controller.findInput<bool>('click') as SMITrigger?;
      _level = controller.findInput<double>('level') as SMINumber?;
    }
  }

  @override
  void dispose() {
    _stateMachineController?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          MouseRegion(
            onEnter: (_) => _isHovered?.value = true,
            onExit: (_) => _isHovered?.value = false,
            child: GestureDetector(
              onTap: () => _clickTrigger?.fire(),
              child: SizedBox(
                width: 250,
                height: 250,
                child: RiveAnimation.asset(
                  'assets/rive/interactive_button.riv',
                  onInit: _onRiveInit,
                  fit: BoxFit.contain,
                ),
              ),
            ),
          ),
          const SizedBox(height: 24),
          // Control a numeric input with a slider
          Slider(
            value: _level?.value ?? 0,
            min: 0,
            max: 100,
            onChanged: (value) {
              setState(() {
                _level?.value = value;
              });
            },
          ),
        ],
      ),
    );
  }
}
```

---

## Rive Inputs and Triggers

Rive state machines expose three input types:

| Input Type | Dart Type | Use Case |
|---|---|---|
| `SMIBool` | `bool` | Toggle states (on/off, hover, active) |
| `SMINumber` | `double` | Continuous values (progress bars, levels, scores) |
| `SMITrigger` | fire() | One-shot events (click, submit, error) |

### Complete Input Example

```dart
import 'package:flutter/material.dart';
import 'package:rive/rive.dart';

class RiveProgressBar extends StatefulWidget {
  const RiveProgressBar({super.key, required this.progress});

  /// A value between 0.0 and 1.0.
  final double progress;

  @override
  State<RiveProgressBar> createState() => _RiveProgressBarState();
}

class _RiveProgressBarState extends State<RiveProgressBar> {
  SMINumber? _progressInput;
  SMIBool? _isCompleteInput;
  SMITrigger? _celebrateTrigger;

  void _onRiveInit(Artboard artboard) {
    final controller = StateMachineController.fromArtboard(
      artboard,
      'ProgressMachine',
    );

    if (controller != null) {
      artboard.addController(controller);
      _progressInput = controller.findInput<double>('progress') as SMINumber?;
      _isCompleteInput =
          controller.findInput<bool>('isComplete') as SMIBool?;
      _celebrateTrigger =
          controller.findInput<bool>('celebrate') as SMITrigger?;

      // Set the initial progress value
      _updateProgress();
    }
  }

  void _updateProgress() {
    _progressInput?.value = widget.progress * 100; // Rive expects 0-100
    _isCompleteInput?.value = widget.progress >= 1.0;
    if (widget.progress >= 1.0) {
      _celebrateTrigger?.fire();
    }
  }

  @override
  void didUpdateWidget(covariant RiveProgressBar oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.progress != widget.progress) {
      _updateProgress();
    }
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: 60,
      child: RiveAnimation.asset(
        'assets/rive/progress_bar.riv',
        onInit: _onRiveInit,
        fit: BoxFit.fitWidth,
      ),
    );
  }
}
```

### Listening to Rive State Changes

```dart
void _onRiveInit(Artboard artboard) {
  final controller = StateMachineController.fromArtboard(
    artboard,
    'GameStateMachine',
  );

  if (controller != null) {
    artboard.addController(controller);

    // Listen to state machine events
    controller.addEventListener((event) {
      debugPrint('Rive event: ${event.name}');
      if (event.name == 'gameOver') {
        _showGameOverDialog();
      }
    });
  }
}
```

---

## Performance Comparison

| Factor | Lottie | Rive | Custom (AnimationController) |
|---|---|---|---|
| **File size** | Large (JSON) | Small (binary .riv) | Zero (code-only) |
| **Render method** | Canvas draw commands from JSON frames | Skeletal interpolation at runtime | Widget tree transforms |
| **Interactivity** | None (playback only) | Full (state machine, inputs) | Full (manual code) |
| **Designer handoff** | After Effects -> Bodymovin export | Rive editor (web-based) | Requires developer implementation |
| **Runtime CPU** | Medium-High (parsing, frame rendering) | Low (skeletal math) | Low (Flutter optimized) |
| **Memory** | Higher (stores all frame data) | Lower (stores bones + meshes) | Lowest |
| **Animation complexity** | Unlimited (frame-by-frame) | High (bones, meshes, blend shapes) | Depends on implementation |
| **Text rendering** | Limited (pre-rendered) | Native text support | Full Flutter text |
| **Theming at runtime** | Partial (via delegates) | Full (via inputs) | Full |
| **Offline support** | Asset bundling or caching | Asset bundling or caching | Built-in |
| **Learning curve** | Low (drop-in widget) | Medium (state machine concepts) | Medium-High (Flutter animation API) |

### Benchmark Guidelines

- **Lottie**: Aim for files under 100KB. Animations with many layers or effects can drop below 60fps on low-end devices. Use `renderCache: RenderCache.raster` on heavy animations.
- **Rive**: Handles complex animations efficiently. State machines add negligible overhead. Ideal for animations that need to be interactive.
- **Custom**: Most performant for simple animations. Use `RepaintBoundary` to isolate animated regions.

```dart
// Lottie render cache for heavy animations
Lottie.asset(
  'assets/animations/heavy.json',
  renderCache: RenderCache.raster,
  width: 300,
  height: 300,
)
```

---

## When to Use Which

### Use Lottie When:

- A designer delivers an After Effects animation as a JSON export.
- The animation is purely decorative (loading spinners, success checkmarks, empty states).
- No runtime interactivity is needed beyond play/pause/seek.
- You want a quick drop-in with minimal code.
- The animation is relatively simple (few layers, short duration).

### Use Rive When:

- The animation needs to react to user input in real time (hover, tap, drag).
- You need branching logic (different animations based on state).
- File size is a concern (Rive's binary format is significantly smaller).
- Performance is critical (Rive's skeletal approach is more efficient for complex animations).
- You want a unified design and development workflow (the Rive editor is free).
- The animation needs text that responds to localization.

### Use Custom AnimationController When:

- The animation is a simple UI transition (fade, slide, scale, color change).
- The animation is tightly coupled to widget state or gestures.
- You do not have a designer producing animation files.
- You need the absolute smallest footprint (no third-party dependency).
- The animation is part of a reusable design system component.

### Decision Matrix

| Requirement | Lottie | Rive | Custom |
|---|---|---|---|
| Designer provides animation file | Best | Best | N/A |
| Interactive (responds to user) | No | Best | Good |
| Minimal dependencies | No | No | Best |
| Complex vector animation | Good | Best | Hard |
| Simple UI transition | Overkill | Overkill | Best |
| Runtime theming | Partial | Best | Best |
| Tiny bundle size | No | Good | Best |
| Offline-first | Good | Good | Best |

---

## Complete Example: Lottie + Rive Side by Side

```dart
import 'package:flutter/material.dart';
import 'package:lottie/lottie.dart';
import 'package:rive/rive.dart';

class AnimationShowcase extends StatefulWidget {
  const AnimationShowcase({super.key});

  @override
  State<AnimationShowcase> createState() => _AnimationShowcaseState();
}

class _AnimationShowcaseState extends State<AnimationShowcase>
    with SingleTickerProviderStateMixin {
  late final AnimationController _lottieController;
  SMIBool? _riveToggle;

  @override
  void initState() {
    super.initState();
    _lottieController = AnimationController(vsync: this);
  }

  @override
  void dispose() {
    _lottieController.dispose();
    super.dispose();
  }

  void _onRiveInit(Artboard artboard) {
    final controller = StateMachineController.fromArtboard(
      artboard,
      'Toggle',
    );
    if (controller != null) {
      artboard.addController(controller);
      _riveToggle = controller.findInput<bool>('isOn') as SMIBool?;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Animation Showcase')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Lottie Section
            Text(
              'Lottie (Decorative)',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            SizedBox(
              height: 150,
              child: GestureDetector(
                onTap: () {
                  if (_lottieController.isAnimating) {
                    _lottieController.stop();
                  } else {
                    _lottieController.repeat();
                  }
                },
                child: Lottie.asset(
                  'assets/animations/loading.json',
                  controller: _lottieController,
                  onLoaded: (composition) {
                    _lottieController.duration = composition.duration;
                    _lottieController.repeat();
                  },
                ),
              ),
            ),
            const SizedBox(height: 32),

            // Rive Section
            Text(
              'Rive (Interactive)',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            SizedBox(
              height: 150,
              child: GestureDetector(
                onTap: () {
                  if (_riveToggle != null) {
                    _riveToggle!.value = !_riveToggle!.value;
                  }
                },
                child: RiveAnimation.asset(
                  'assets/rive/toggle_switch.riv',
                  onInit: _onRiveInit,
                  fit: BoxFit.contain,
                ),
              ),
            ),
            const SizedBox(height: 32),

            // Custom Section
            Text(
              'Custom (Code-only)',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            const _CustomPulse(),
          ],
        ),
      ),
    );
  }
}

class _CustomPulse extends StatefulWidget {
  const _CustomPulse();

  @override
  State<_CustomPulse> createState() => _CustomPulseState();
}

class _CustomPulseState extends State<_CustomPulse>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 800),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, child) {
        return Transform.scale(
          scale: 0.85 + (_controller.value * 0.15),
          child: Opacity(
            opacity: 0.6 + (_controller.value * 0.4),
            child: child,
          ),
        );
      },
      child: Container(
        height: 100,
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.primaryContainer,
          borderRadius: BorderRadius.circular(16),
        ),
        alignment: Alignment.center,
        child: Text(
          'Pure Flutter Animation',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: Theme.of(context).colorScheme.onPrimaryContainer,
              ),
        ),
      ),
    );
  }
}
```
