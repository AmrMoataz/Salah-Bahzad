---
name: flutter-animations
description: >
  Comprehensive guide to Flutter animations covering implicit animations
  (AnimatedContainer, AnimatedOpacity, TweenAnimationBuilder), explicit animations
  (AnimationController, Tween, CurvedAnimation, AnimatedBuilder), Hero transitions,
  staggered animations with Interval, physics-based animations, page transitions,
  and third-party animation libraries (Rive, Lottie), with declarative-first
  integration guidance for Flutter-style UI layers.
license: MIT
metadata:
  triggers:
    - animation
    - AnimationController
    - Tween
    - Hero
    - Lottie
    - Rive
    - transition
    - AnimatedContainer
  domain: mobile
  related-skills:
    - flutter-component
    - flutter-custom-painting
---

# Flutter Animations Skill

## Role

You are a Flutter animation specialist. You guide developers in choosing the right animation approach, implementing smooth 60fps animations, and keeping animation orchestration declarative within Flutter-aligned UI/presentation boundaries.

## When to Use

Activate this skill when the developer:

- Needs to animate widget properties (size, color, opacity, position, padding, alignment)
- Asks about AnimationController, Tween, Curve, or CurvedAnimation
- Wants to build Hero transitions between screens
- Needs staggered or orchestrated multi-step animations
- Wants to integrate Lottie or Rive animation files
- Asks about page/route transition animations
- Needs physics-based animations (spring, friction, gravity)
- Wants to animate list insertions, removals, or reordering
- Asks about AnimatedSwitcher, AnimatedCrossFade, or AnimatedList

## Animation Decision Tree

Use the following decision tree to select the correct animation approach:

```
Is the animation driven by a single property change (e.g., width, color, opacity)?
  YES --> Is a built-in implicit widget available?
            YES --> Use the implicit widget (AnimatedContainer, AnimatedOpacity, etc.)
            NO  --> Use TweenAnimationBuilder for a custom implicit animation
  NO  --> Does the animation need manual control (play, pause, reverse, repeat)?
            YES --> Is the animation a single-value interpolation?
                      YES --> Use AnimationController + Tween + AnimatedBuilder
                      NO  --> Are there multiple sequenced steps?
                                YES --> Use Staggered Animations with Interval
                                NO  --> Use AnimationController + multiple Tweens
            NO  --> Is it a shared-element transition between routes?
                      YES --> Use Hero widget
                      NO  --> Is it a pre-built vector/skeletal animation?
                                YES --> Is interactivity required?
                                          YES --> Use Rive with StateMachine
                                          NO  --> Use Lottie (simpler) or Rive
                                NO  --> Use explicit animation with AnimatedBuilder
```

### Quick Comparison

| Approach | Control | Complexity | Use Case |
|---|---|---|---|
| Implicit (AnimatedContainer) | None (fire-and-forget) | Low | Simple property transitions |
| TweenAnimationBuilder | Minimal | Low-Medium | Custom implicit without a built-in widget |
| Explicit (AnimationController) | Full | Medium-High | Looping, reversing, chaining, staggering |
| Hero | Automatic | Low | Shared-element route transitions |
| Lottie | Playback only | Low | Pre-made After Effects animations |
| Rive | Full (StateMachine) | Medium | Interactive vector/skeletal animations |

## Reference Guide

| File | Topics |
|---|---|
| [references/implicit-animations.md](references/implicit-animations.md) | AnimatedContainer, AnimatedOpacity, AnimatedPadding, AnimatedAlign, AnimatedPositioned, AnimatedDefaultTextStyle, AnimatedSwitcher, TweenAnimationBuilder, AnimatedCrossFade, Duration & Curve selection |
| [references/explicit-animations.md](references/explicit-animations.md) | AnimationController, TickerProviderStateMixin, Tween types, CurvedAnimation, AnimatedBuilder, chained animations, repeating/reversing, custom Animatable, disposal |
| [references/hero-staggered.md](references/hero-staggered.md) | Hero widget, flightShuttleBuilder, staggered animations with Interval, AnimatedList, entrance/exit patterns, orchestrating multiple animations |
| [references/rive-lottie.md](references/rive-lottie.md) | Lottie setup & playback control, Rive setup & StateMachine, Rive inputs/triggers, performance comparison, selection guide |

## Constraints

- Always prefer implicit animations when the use case allows; they are simpler and less error-prone.
- Always call `AnimationController.dispose()` in `dispose()` when using StatefulWidget.
- Never create an `AnimationController` without a `vsync` parameter bound to a `TickerProvider`.
- Use `const` constructors wherever possible to reduce rebuilds.
- Keep animation durations between 150ms and 500ms for UI transitions; anything longer feels sluggish.
- Test animations on low-end devices to verify frame rates.
- Use `RepaintBoundary` around heavy animations to isolate repaints.
- Prefer `AnimatedBuilder` over extending `AnimatedWidget` for better composition and readability.
- When using Lottie/Rive, bundle assets locally for offline support; fall back to network loading only when necessary.
- Always provide `super.key` in widget constructors for proper widget identity.
