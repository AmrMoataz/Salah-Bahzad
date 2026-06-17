# Hero and Staggered Animations

---

## Hero Widget Setup

The `Hero` widget animates a shared element between two routes. Both the source and destination must use a `Hero` with the same `tag`.

```dart
import 'package:flutter/material.dart';

/// Source screen with a list of items. Tapping an item navigates to the detail
/// screen with a Hero animation on the image.
class GalleryScreen extends StatelessWidget {
  const GalleryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Gallery')),
      body: GridView.builder(
        padding: const EdgeInsets.all(12),
        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 2,
          mainAxisSpacing: 12,
          crossAxisSpacing: 12,
        ),
        itemCount: 10,
        itemBuilder: (context, index) {
          final tag = 'image-hero-$index';
          return GestureDetector(
            onTap: () {
              Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (context) => DetailScreen(
                    index: index,
                    heroTag: tag,
                  ),
                ),
              );
            },
            child: Hero(
              tag: tag,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: Image.network(
                  'https://picsum.photos/seed/$index/300/300',
                  fit: BoxFit.cover,
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}

/// Destination screen showing the full image with the same Hero tag.
class DetailScreen extends StatelessWidget {
  const DetailScreen({
    super.key,
    required this.index,
    required this.heroTag,
  });

  final int index;
  final String heroTag;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Image $index')),
      body: Center(
        child: Hero(
          tag: heroTag,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(16),
            child: Image.network(
              'https://picsum.photos/seed/$index/600/600',
              fit: BoxFit.contain,
            ),
          ),
        ),
      ),
    );
  }
}
```

### Key Rules for Hero Tags

- Tags must be **unique within each route**. Duplicate tags on the same screen cause errors.
- Tags must match **exactly** between source and destination.
- The `Hero` child widget type does not need to match, but matching produces a smoother visual.

---

## Custom Hero Flight Animation (flightShuttleBuilder)

Override the default cross-fade flight animation with a custom builder.

```dart
import 'package:flutter/material.dart';

class CustomFlightHero extends StatelessWidget {
  const CustomFlightHero({
    super.key,
    required this.tag,
    required this.child,
  });

  final String tag;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Hero(
      tag: tag,
      flightShuttleBuilder: (
        flightContext,
        animation,
        flightDirection,
        fromHeroContext,
        toHeroContext,
      ) {
        final curvedAnimation = CurvedAnimation(
          parent: animation,
          curve: Curves.easeInOut,
        );
        return AnimatedBuilder(
          animation: curvedAnimation,
          builder: (context, child) {
            return Material(
              color: Colors.transparent,
              child: Transform.scale(
                scale: 1.0 + (0.1 * curvedAnimation.value),
                child: Opacity(
                  opacity: 0.5 + (0.5 * curvedAnimation.value),
                  child: child,
                ),
              ),
            );
          },
          child: toHeroContext.widget,
        );
      },
      child: child,
    );
  }
}
```

### Handling Hero with Material Widgets

When the Hero child contains text or other Material widgets, wrap it in `Material` to prevent rendering artifacts during flight:

```dart
Hero(
  tag: 'profile-$userId',
  child: Material(
    color: Colors.transparent,
    child: CircleAvatar(
      radius: 40,
      backgroundImage: NetworkImage(avatarUrl),
    ),
  ),
)
```

---

## Hero with GoRouter

When using `go_router`, Hero animations work the same way, but you define routes declaratively.

```dart
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

final GoRouter router = GoRouter(
  routes: [
    GoRoute(
      path: '/',
      builder: (context, state) => const ItemListScreen(),
    ),
    GoRoute(
      path: '/item/:id',
      pageBuilder: (context, state) {
        final id = state.pathParameters['id']!;
        return CustomTransitionPage<void>(
          key: state.pageKey,
          child: ItemDetailScreen(id: id),
          transitionsBuilder: (context, animation, secondaryAnimation, child) {
            return FadeTransition(opacity: animation, child: child);
          },
        );
      },
    ),
  ],
);

class ItemListScreen extends StatelessWidget {
  const ItemListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Items')),
      body: ListView.builder(
        itemCount: 20,
        itemBuilder: (context, index) {
          final id = 'item-$index';
          return ListTile(
            leading: Hero(
              tag: 'hero-$id',
              child: CircleAvatar(child: Text('$index')),
            ),
            title: Text('Item $index'),
            onTap: () => context.go('/item/$index'),
          );
        },
      ),
    );
  }
}

class ItemDetailScreen extends StatelessWidget {
  const ItemDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Item $id')),
      body: Center(
        child: Hero(
          tag: 'hero-item-$id',
          child: CircleAvatar(
            radius: 80,
            child: Text(id, style: const TextStyle(fontSize: 32)),
          ),
        ),
      ),
    );
  }
}
```

### GoRouter CustomTransitionPage for Hero Support

By default, `GoRouter` uses `MaterialPage`, which supports Hero out of the box. Use `CustomTransitionPage` only when you need a non-standard page transition:

```dart
GoRoute(
  path: '/details/:id',
  pageBuilder: (context, state) {
    return CustomTransitionPage<void>(
      key: state.pageKey,
      child: DetailsScreen(id: state.pathParameters['id']!),
      transitionDuration: const Duration(milliseconds: 400),
      reverseTransitionDuration: const Duration(milliseconds: 300),
      transitionsBuilder: (context, animation, secondaryAnimation, child) {
        return SlideTransition(
          position: Tween<Offset>(
            begin: const Offset(1, 0),
            end: Offset.zero,
          ).animate(
            CurvedAnimation(parent: animation, curve: Curves.easeOutCubic),
          ),
          child: child,
        );
      },
    );
  },
)
```

---

## Staggered Animations with Interval

Staggered animations run multiple animations on a single `AnimationController`, each within a different time `Interval`. The intervals are fractions of the controller's total duration (0.0 to 1.0).

```dart
import 'package:flutter/material.dart';

class StaggeredCardReveal extends StatefulWidget {
  const StaggeredCardReveal({super.key});

  @override
  State<StaggeredCardReveal> createState() => _StaggeredCardRevealState();
}

class _StaggeredCardRevealState extends State<StaggeredCardReveal>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  // Each animation occupies a portion of the total duration.
  late final Animation<double> _iconScale;
  late final Animation<double> _titleOpacity;
  late final Animation<Offset> _subtitleSlide;
  late final Animation<double> _buttonOpacity;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1200),
    );

    _iconScale = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: const Interval(0.0, 0.3, curve: Curves.easeOutBack),
      ),
    );

    _titleOpacity = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: const Interval(0.2, 0.5, curve: Curves.easeIn),
      ),
    );

    _subtitleSlide = Tween<Offset>(
      begin: const Offset(0, 0.3),
      end: Offset.zero,
    ).animate(
      CurvedAnimation(
        parent: _controller,
        curve: const Interval(0.4, 0.7, curve: Curves.easeOutCubic),
      ),
    );

    _buttonOpacity = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: const Interval(0.6, 1.0, curve: Curves.easeIn),
      ),
    );

    _controller.forward();
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
      builder: (context, _) {
        return Card(
          elevation: 4,
          margin: const EdgeInsets.all(24),
          child: Padding(
            padding: const EdgeInsets.all(32),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Transform.scale(
                  scale: _iconScale.value,
                  child: const Icon(
                    Icons.check_circle,
                    size: 64,
                    color: Colors.green,
                  ),
                ),
                const SizedBox(height: 16),
                Opacity(
                  opacity: _titleOpacity.value,
                  child: Text(
                    'Payment Successful',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                ),
                const SizedBox(height: 8),
                FractionalTranslation(
                  translation: _subtitleSlide.value,
                  child: Opacity(
                    opacity: _subtitleSlide.value == Offset.zero ? 1.0 : 0.5,
                    child: Text(
                      'Your order has been confirmed.',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ),
                ),
                const SizedBox(height: 24),
                Opacity(
                  opacity: _buttonOpacity.value,
                  child: FilledButton(
                    onPressed: () {},
                    child: const Text('Continue'),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
```

### Interval Timing Diagram

```
Controller: |---- 0.0 ---- 0.2 ---- 0.4 ---- 0.6 ---- 0.8 ---- 1.0 ----|
Icon:       [===========]
Title:              [===========]
Subtitle:                   [===========]
Button:                              [=================]
```

---

## AnimatedList

`AnimatedList` animates the insertion and removal of list items.

```dart
import 'package:flutter/material.dart';

class AnimatedListExample extends StatefulWidget {
  const AnimatedListExample({super.key});

  @override
  State<AnimatedListExample> createState() => _AnimatedListExampleState();
}

class _AnimatedListExampleState extends State<AnimatedListExample> {
  final _listKey = GlobalKey<AnimatedListState>();
  final _items = <String>[];
  int _counter = 0;

  void _addItem() {
    final index = _items.length;
    _items.add('Item ${_counter++}');
    _listKey.currentState?.insertItem(
      index,
      duration: const Duration(milliseconds: 300),
    );
  }

  void _removeItem(int index) {
    final removedItem = _items.removeAt(index);
    _listKey.currentState?.removeItem(
      index,
      (context, animation) => _buildItem(removedItem, animation),
      duration: const Duration(milliseconds: 250),
    );
  }

  Widget _buildItem(String item, Animation<double> animation) {
    return SizeTransition(
      sizeFactor: animation,
      child: FadeTransition(
        opacity: animation,
        child: Card(
          margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
          child: ListTile(
            title: Text(item),
            trailing: IconButton(
              icon: const Icon(Icons.delete),
              onPressed: () {
                final idx = _items.indexOf(item);
                if (idx != -1) _removeItem(idx);
              },
            ),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('AnimatedList')),
      body: AnimatedList(
        key: _listKey,
        initialItemCount: _items.length,
        itemBuilder: (context, index, animation) {
          return _buildItem(_items[index], animation);
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: _addItem,
        child: const Icon(Icons.add),
      ),
    );
  }
}
```

---

## Entrance / Exit Animations Pattern

A reusable pattern for animating widgets on mount and before disposal.

```dart
import 'package:flutter/material.dart';

class EntranceExitWrapper extends StatefulWidget {
  const EntranceExitWrapper({
    super.key,
    required this.child,
    this.entranceDuration = const Duration(milliseconds: 400),
    this.exitDuration = const Duration(milliseconds: 250),
    this.entranceCurve = Curves.easeOutCubic,
    this.exitCurve = Curves.easeIn,
  });

  final Widget child;
  final Duration entranceDuration;
  final Duration exitDuration;
  final Curve entranceCurve;
  final Curve exitCurve;

  @override
  State<EntranceExitWrapper> createState() => EntranceExitWrapperState();
}

class EntranceExitWrapperState extends State<EntranceExitWrapper>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _fadeAnimation;
  late final Animation<Offset> _slideAnimation;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: widget.entranceDuration,
      reverseDuration: widget.exitDuration,
    );

    _fadeAnimation = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(parent: _controller, curve: widget.entranceCurve),
    );

    _slideAnimation = Tween<Offset>(
      begin: const Offset(0, 0.15),
      end: Offset.zero,
    ).animate(
      CurvedAnimation(parent: _controller, curve: widget.entranceCurve),
    );

    _controller.forward();
  }

  /// Call this before removing the widget to play the exit animation.
  /// Returns a Future that completes when the animation finishes.
  Future<void> exit() async {
    await _controller.reverse();
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
        return FractionalTranslation(
          translation: _slideAnimation.value,
          child: Opacity(
            opacity: _fadeAnimation.value,
            child: child,
          ),
        );
      },
      child: widget.child,
    );
  }
}
```

### Using the Entrance/Exit Pattern

```dart
// Hold a GlobalKey to call exit() before navigation
final _wrapperKey = GlobalKey<EntranceExitWrapperState>();

EntranceExitWrapper(
  key: _wrapperKey,
  child: const MyContentWidget(),
)

// Before navigating away:
Future<void> _navigateAway() async {
  await _wrapperKey.currentState?.exit();
  if (mounted) {
    Navigator.of(context).pop();
  }
}
```

---

## Orchestrating Multiple Animations

When building complex UI sequences with many animated elements, keep things maintainable by defining timing constants and grouping related animations.

```dart
import 'package:flutter/material.dart';

/// Timing constants for onboarding animation sequence.
abstract final class OnboardingTiming {
  static const totalDuration = Duration(milliseconds: 1800);

  // Phase 1: Background
  static const backgroundInterval = Interval(0.0, 0.3, curve: Curves.easeOut);

  // Phase 2: Title + Subtitle
  static const titleInterval = Interval(0.15, 0.45, curve: Curves.easeOut);
  static const subtitleInterval = Interval(0.25, 0.55, curve: Curves.easeOut);

  // Phase 3: Illustration
  static const illustrationInterval =
      Interval(0.4, 0.75, curve: Curves.easeOutBack);

  // Phase 4: Buttons
  static const primaryButtonInterval =
      Interval(0.6, 0.85, curve: Curves.easeOut);
  static const secondaryButtonInterval =
      Interval(0.7, 1.0, curve: Curves.easeOut);
}

class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _bgOpacity;
  late final Animation<double> _titleOpacity;
  late final Animation<Offset> _titleSlide;
  late final Animation<double> _subtitleOpacity;
  late final Animation<Offset> _subtitleSlide;
  late final Animation<double> _illustrationScale;
  late final Animation<double> _primaryButtonOpacity;
  late final Animation<double> _secondaryButtonOpacity;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: OnboardingTiming.totalDuration,
    );

    _bgOpacity = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.backgroundInterval,
      ),
    );

    _titleOpacity = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.titleInterval,
      ),
    );
    _titleSlide = Tween<Offset>(
      begin: const Offset(0, 0.2),
      end: Offset.zero,
    ).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.titleInterval,
      ),
    );

    _subtitleOpacity = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.subtitleInterval,
      ),
    );
    _subtitleSlide = Tween<Offset>(
      begin: const Offset(0, 0.2),
      end: Offset.zero,
    ).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.subtitleInterval,
      ),
    );

    _illustrationScale = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.illustrationInterval,
      ),
    );

    _primaryButtonOpacity = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.primaryButtonInterval,
      ),
    );

    _secondaryButtonOpacity = Tween<double>(begin: 0, end: 1).animate(
      CurvedAnimation(
        parent: _controller,
        curve: OnboardingTiming.secondaryButtonInterval,
      ),
    );

    _controller.forward();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: AnimatedBuilder(
        animation: _controller,
        builder: (context, _) {
          return Opacity(
            opacity: _bgOpacity.value,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 32),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Transform.scale(
                    scale: _illustrationScale.value,
                    child: const FlutterLogo(size: 120),
                  ),
                  const SizedBox(height: 48),
                  FractionalTranslation(
                    translation: _titleSlide.value,
                    child: Opacity(
                      opacity: _titleOpacity.value,
                      child: Text(
                        'Welcome',
                        style: Theme.of(context).textTheme.headlineLarge,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  FractionalTranslation(
                    translation: _subtitleSlide.value,
                    child: Opacity(
                      opacity: _subtitleOpacity.value,
                      child: Text(
                        'Discover amazing animations in Flutter.',
                        style: Theme.of(context).textTheme.bodyLarge,
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ),
                  const SizedBox(height: 48),
                  Opacity(
                    opacity: _primaryButtonOpacity.value,
                    child: FilledButton(
                      onPressed: () {},
                      child: const Text('Get Started'),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Opacity(
                    opacity: _secondaryButtonOpacity.value,
                    child: TextButton(
                      onPressed: () {},
                      child: const Text('Skip'),
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}
```

### Tips for Orchestrated Animations

- Define all timing as constants in a dedicated class for easy adjustment.
- Use a single `AnimationController` with `Interval` to keep animations synchronized.
- Group related animations (e.g., opacity + slide for the same element) so they share the same interval.
- Use `AnimatedBuilder` with the single controller to avoid multiple listeners.
- Test on low-end devices to ensure the orchestrated sequence runs at 60fps.
