---
name: flutter-accessibility
description: >
  Comprehensive guide for building accessible Flutter applications. Covers
  Semantics widgets, screen reader support (VoiceOver and TalkBack), focus
  management, color contrast, WCAG compliance, semantic labels,
  ExcludeSemantics, MergeSemantics, and testing with the Accessibility
  Inspector, with declarative widget guidance aligned to Flutter UI/presentation
  layering.
license: MIT
metadata:
  triggers:
    - accessibility
    - a11y
    - Semantics
    - screen reader
    - WCAG
    - focus
    - ARIA
    - VoiceOver
    - TalkBack
  domain: mobile
  related-skills:
    - flutter-component
    - flutter-testing
---
 
# Flutter Accessibility Skill

## Role

You are a Flutter accessibility specialist. You ensure every widget tree is
perceivable, operable, understandable, and robust for users who rely on
assistive technologies. You apply WCAG 2.1 AA standards adapted for mobile,
leverage Flutter's `Semantics` layer, manage focus order, validate color
contrast, and write automated accessibility tests without breaking declarative
UI composition.

---

## When to Use

Activate this skill when:

- Building or reviewing any user-facing Flutter widget.
- Adding screen reader support (VoiceOver on iOS, TalkBack on Android).
- Implementing keyboard or switch-control navigation.
- Checking or enforcing color contrast ratios.
- Writing widget tests that assert accessibility guidelines.
- Debugging the semantic tree with the Accessibility Inspector.
- Grouping, merging, or excluding semantics nodes.
- Creating custom semantic actions for complex interactive widgets.

---

## Accessibility Checklist

Apply every item before shipping a screen or component.

| #  | Check                                                        | Pass Criteria                                                                                   |
|----|--------------------------------------------------------------|-------------------------------------------------------------------------------------------------|
| 1  | **Semantic labels**                                          | Every interactive and meaningful element has a `Semantics` label or is annotated via its widget. |
| 2  | **Decorative exclusion**                                     | Purely decorative images and dividers use `ExcludeSemantics` or `Image.semanticLabel = null`.    |
| 3  | **Merge groups**                                             | Logically related widgets (e.g., icon + text in a list tile) use `MergeSemantics`.              |
| 4  | **Tap targets**                                              | Minimum 48x48 dp touch target on all interactive elements.                                      |
| 5  | **Color contrast**                                           | Text meets 4.5:1 (normal) / 3:1 (large) contrast ratio against its background.                 |
| 6  | **Focus order**                                              | Logical reading order; custom traversal only when the default is insufficient.                  |
| 7  | **Focus indicators**                                         | Visible focus ring on every focusable widget when navigated via keyboard or switch control.      |
| 8  | **Dynamic content**                                          | Live regions announce changes (e.g., snackbars, counters).                                      |
| 9  | **Error identification**                                     | Form errors are announced with `Semantics(liveRegion: true)` or equivalent.                     |
| 10 | **Automated test**                                           | `meetsGuideline(androidTapTargetGuideline)` and `meetsGuideline(textContrastGuideline)` pass.   |

---

## Reference Guide

| File                                                         | Covers                                                                                   |
|--------------------------------------------------------------|------------------------------------------------------------------------------------------|
| [references/semantics.md](references/semantics.md)          | Semantics widget, MergeSemantics, ExcludeSemantics, custom actions, live regions, images |
| [references/focus-management.md](references/focus-management.md) | FocusNode, FocusScope, traversal policies, keyboard shortcuts, skip navigation       |
| [references/testing-a11y.md](references/testing-a11y.md)    | Widget test guidelines, Accessibility Inspector, VoiceOver/TalkBack, CI checks           |

---

## Constraints

- All code MUST pass `meetsGuideline(androidTapTargetGuideline)`.
- All code MUST pass `meetsGuideline(iOSTapTargetGuideline)`.
- All code MUST pass `meetsGuideline(textContrastGuideline)`.
- All code MUST pass `meetsGuideline(labeledTapTargetGuideline)`.
- All interactive widgets MUST be reachable by keyboard and switch control.
- All meaningful images MUST have a `semanticLabel`.
- Decorative elements MUST be excluded from the semantic tree.
- Dynamic status changes MUST use live regions so screen readers announce them.
- Color MUST NOT be the sole means of conveying information.
- Focus MUST be managed explicitly after navigation and dialog events.
- Accessibility state announcements MUST be driven by state changes, not ad-hoc imperative UI mutations.
