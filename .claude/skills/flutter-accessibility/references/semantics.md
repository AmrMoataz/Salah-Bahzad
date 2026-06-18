# Semantics Comprehensive Guide

## Overview

Flutter builds a **semantic tree** parallel to the widget tree. Assistive
technologies (VoiceOver, TalkBack, switch control) consume this tree to
describe the UI to users. The primary widget for controlling this tree is
`Semantics`.

---

## 1. The Semantics Widget

`Semantics` annotates a subtree with accessibility metadata.

```dart
import 'package:flutter/material.dart';

class WelcomeBanner extends StatelessWidget {
  const WelcomeBanner({super.key});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Welcome to the app',
      header: true,
      child: const Text(
        'Welcome!',
        style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold),
      ),
    );
  }
}
```

### Commonly Used Properties

| Property        | Type       | Purpose                                                                 |
|-----------------|------------|-------------------------------------------------------------------------|
| `label`         | `String?`  | Primary description read by the screen reader.                          |
| `hint`          | `String?`  | Describes the result of an action (e.g., "Double tap to open").         |
| `value`         | `String?`  | Current value (e.g., slider position, text field content).              |
| `increasedValue`| `String?`  | Value after an increase action.                                         |
| `decreasedValue`| `String?`  | Value after a decrease action.                                          |
| `button`        | `bool?`    | Marks the node as a button.                                             |
| `header`        | `bool?`    | Marks the node as a heading.                                            |
| `link`          | `bool?`    | Marks the node as a link.                                               |
| `textField`     | `bool?`    | Marks the node as a text field.                                         |
| `slider`        | `bool?`    | Marks the node as a slider.                                             |
| `image`         | `bool?`    | Marks the node as an image.                                             |
| `readOnly`      | `bool?`    | Indicates the element is not editable.                                  |
| `enabled`       | `bool?`    | Whether the widget is enabled or disabled.                              |
| `checked`       | `bool?`    | Checked state for checkboxes and toggles.                               |
| `toggled`       | `bool?`    | Toggled state for switches.                                             |
| `selected`      | `bool?`    | Selected state (e.g., list item, tab).                                  |
| `focused`       | `bool?`    | Whether the node currently has input focus.                             |
| `hidden`        | `bool`     | Hides the node from the semantic tree (default `false`).                |
| `obscured`      | `bool?`    | Indicates obscured text (e.g., password field).                         |
| `liveRegion`    | `bool`     | Announces content changes automatically (default `false`).              |
| `maxValueLength`| `int?`     | Maximum character count for a text field.                               |
| `currentValueLength` | `int?` | Current character count for a text field.                              |
| `sortKey`       | `SemanticsSortKey?` | Controls traversal order.                                      |
| `onTap`         | `VoidCallback?` | Custom tap handler exposed to assistive technology.                 |
| `onLongPress`   | `VoidCallback?` | Custom long-press handler.                                          |
| `onIncrease`    | `VoidCallback?` | Invoked when the user performs an increase gesture.                  |
| `onDecrease`    | `VoidCallback?` | Invoked when the user performs a decrease gesture.                   |
| `onDismiss`     | `VoidCallback?` | Invoked when the user performs a dismiss gesture.                    |
| `onCopy`        | `VoidCallback?` | Custom copy action.                                                  |
| `onCut`         | `VoidCallback?` | Custom cut action.                                                   |
| `onPaste`       | `VoidCallback?` | Custom paste action.                                                 |

### Complete Property Example

```dart
import 'package:flutter/material.dart';

class AccessibleSlider extends StatefulWidget {
  const AccessibleSlider({super.key});

  @override
  State<AccessibleSlider> createState() => _AccessibleSliderState();
}

class _AccessibleSliderState extends State<AccessibleSlider> {
  double _volume = 50;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Volume',
      value: '${_volume.round()} percent',
      hint: 'Swipe up to increase, swipe down to decrease',
      increasedValue: '${(_volume + 10).clamp(0, 100).round()} percent',
      decreasedValue: '${(_volume - 10).clamp(0, 100).round()} percent',
      slider: true,
      onIncrease: () {
        setState(() => _volume = (_volume + 10).clamp(0, 100));
      },
      onDecrease: () {
        setState(() => _volume = (_volume - 10).clamp(0, 100));
      },
      child: Slider(
        value: _volume,
        min: 0,
        max: 100,
        divisions: 10,
        label: '${_volume.round()}%',
        onChanged: (v) => setState(() => _volume = v),
      ),
    );
  }
}
```

---

## 2. MergeSemantics

`MergeSemantics` collapses child semantic nodes into a single node. Use it
when several widgets form one logical unit.

```dart
import 'package:flutter/material.dart';

class ProductTile extends StatelessWidget {
  final String name;
  final String price;
  final VoidCallback onTap;

  const ProductTile({
    super.key,
    required this.name,
    required this.price,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    // Screen reader announces: "Running Shoes, \$79.99, button, Double tap to
    // view details" as a single element.
    return MergeSemantics(
      child: Semantics(
        button: true,
        hint: 'Double tap to view details',
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Expanded(child: Text(name, style: const TextStyle(fontSize: 18))),
                Text(price, style: const TextStyle(fontSize: 18)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
```

### When to Merge

- List tiles with an icon, title, and subtitle.
- Cards where the whole surface is tappable.
- Toggle rows (label + switch).

### When NOT to Merge

- When children have independent actions (e.g., a row with both a "play"
  button and a "delete" button).

---

## 3. ExcludeSemantics

`ExcludeSemantics` removes its subtree from the semantic tree. Use it for
purely decorative elements that would add noise for screen reader users.

```dart
import 'package:flutter/material.dart';

class DecorativeDivider extends StatelessWidget {
  const DecorativeDivider({super.key});

  @override
  Widget build(BuildContext context) {
    return ExcludeSemantics(
      child: Container(
        height: 1,
        color: Colors.grey.shade300,
      ),
    );
  }
}
```

### Decorative Image Example

```dart
import 'package:flutter/material.dart';

class HeroSection extends StatelessWidget {
  const HeroSection({super.key});

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        // Decorative background -- excluded from the semantic tree.
        ExcludeSemantics(
          child: Image.asset(
            'assets/images/background_pattern.png',
            fit: BoxFit.cover,
            width: double.infinity,
            height: 200,
          ),
        ),
        const Center(
          child: Semantics(
            header: true,
            child: Text(
              'Explore Our Collection',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            ),
          ),
        ),
      ],
    );
  }
}
```

Alternatively, set `excludeFromSemantics: true` directly on `Image` widgets:

```dart
Image.asset(
  'assets/images/decoration.png',
  excludeFromSemantics: true,
)
```

---

## 4. SemanticsProperties

For advanced composition, pass a `SemanticsProperties` object. This is useful
when building custom render objects or when you need to set properties
programmatically.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';

class CustomSemanticWidget extends SingleChildRenderObjectWidget {
  final String accessibilityLabel;

  const CustomSemanticWidget({
    super.key,
    required this.accessibilityLabel,
    super.child,
  });

  @override
  RenderObject createRenderObject(BuildContext context) {
    return RenderCustomSemantic(accessibilityLabel: accessibilityLabel);
  }

  @override
  void updateRenderObject(
    BuildContext context,
    covariant RenderCustomSemantic renderObject,
  ) {
    renderObject.accessibilityLabel = accessibilityLabel;
  }
}

class RenderCustomSemantic extends RenderProxyBox {
  String _accessibilityLabel;

  RenderCustomSemantic({required String accessibilityLabel})
      : _accessibilityLabel = accessibilityLabel;

  set accessibilityLabel(String value) {
    if (_accessibilityLabel == value) return;
    _accessibilityLabel = value;
    markNeedsSemanticsUpdate();
  }

  @override
  void describeSemanticsConfiguration(SemanticsConfiguration config) {
    super.describeSemanticsConfiguration(config);
    config
      ..isSemanticBoundary = true
      ..label = _accessibilityLabel
      ..textDirection = TextDirection.ltr;
  }
}
```

---

## 5. Custom Semantic Actions

Define custom actions so assistive technology users can trigger operations that
have no standard gesture equivalent.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';

class MessageCard extends StatelessWidget {
  final String sender;
  final String body;
  final VoidCallback onArchive;
  final VoidCallback onReply;

  const MessageCard({
    super.key,
    required this.sender,
    required this.body,
    required this.onArchive,
    required this.onReply,
  });

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Message from $sender. $body',
      customSemanticsActions: <CustomSemanticsAction, VoidCallback>{
        const CustomSemanticsAction(label: 'Archive'): onArchive,
        const CustomSemanticsAction(label: 'Reply'): onReply,
      },
      child: Card(
        child: ListTile(
          title: Text(sender),
          subtitle: Text(body),
        ),
      ),
    );
  }
}
```

On iOS, custom actions appear in the VoiceOver Actions rotor. On Android,
TalkBack surfaces them in the local context menu.

---

## 6. Semantic Tree Debugging

### Using debugDumpSemanticsTree

```dart
import 'package:flutter/rendering.dart';

void inspectSemantics() {
  // Call from a tap handler or debugger console:
  debugDumpSemanticsTree(DebugSemanticsDumpOrder.traversalOrder);
}
```

### Enabling the Semantics Debugger Overlay

```dart
import 'package:flutter/material.dart';

void main() {
  runApp(
    MaterialApp(
      showSemanticsDebugger: true, // Renders the semantic tree as an overlay.
      home: const MyHomePage(),
    ),
  );
}

class MyHomePage extends StatelessWidget {
  const MyHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Semantics Debugger')),
      body: const Center(child: Text('Hello, accessibility!')),
    );
  }
}
```

### Flutter DevTools Accessibility Inspector

1. Run `flutter run --debug`.
2. Open DevTools (printed URL in the terminal).
3. Navigate to the **Inspector** tab.
4. Enable **Show Accessibility** to overlay semantic boundaries and labels.

---

## 7. Image Semantics

### Meaningful Images

```dart
Image.asset(
  'assets/images/product_photo.png',
  semanticLabel: 'Red running shoes, side view',
)
```

### Icon Semantics

```dart
IconButton(
  icon: const Icon(Icons.delete),
  tooltip: 'Delete item', // Tooltip doubles as the semantic label.
  onPressed: () {},
)
```

When `tooltip` is set on `IconButton`, Flutter automatically applies it as
the semantic label. If you need a different semantic label from the tooltip:

```dart
Semantics(
  label: 'Remove this item permanently',
  child: IconButton(
    icon: const Icon(Icons.delete),
    tooltip: 'Delete',
    onPressed: () {},
  ),
)
```

### Network Images

```dart
Image.network(
  'https://example.com/avatar.jpg',
  semanticLabel: 'Profile photo of Jane Doe',
  errorBuilder: (context, error, stackTrace) {
    return Semantics(
      label: 'Profile photo unavailable',
      child: const Icon(Icons.broken_image, size: 48),
    );
  },
)
```

---

## 8. List and Table Semantics

### Accessible ListView

```dart
import 'package:flutter/material.dart';

class AccessibleTodoList extends StatelessWidget {
  final List<String> items;

  const AccessibleTodoList({super.key, required this.items});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Todo list, ${items.length} items',
      child: ListView.builder(
        itemCount: items.length,
        itemBuilder: (context, index) {
          return Semantics(
            label: 'Item ${index + 1} of ${items.length}: ${items[index]}',
            child: ListTile(
              leading: Text('${index + 1}'),
              title: Text(items[index]),
            ),
          );
        },
      ),
    );
  }
}
```

### Accessible DataTable

```dart
import 'package:flutter/material.dart';

class AccessibleScoreTable extends StatelessWidget {
  const AccessibleScoreTable({super.key});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Player scores table',
      child: DataTable(
        columns: const [
          DataColumn(label: Text('Player')),
          DataColumn(label: Text('Score'), numeric: true),
        ],
        rows: const [
          DataRow(cells: [
            DataCell(Text('Alice')),
            DataCell(Text('42')),
          ]),
          DataRow(cells: [
            DataCell(Text('Bob')),
            DataCell(Text('37')),
          ]),
        ],
      ),
    );
  }
}
```

---

## 9. Live Regions for Dynamic Content

A live region causes the screen reader to announce content changes
automatically, without the user navigating to the element.

```dart
import 'package:flutter/material.dart';

class CartBadge extends StatelessWidget {
  final int itemCount;

  const CartBadge({super.key, required this.itemCount});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      liveRegion: true,
      label: '$itemCount items in cart',
      child: Badge(
        label: Text('$itemCount'),
        child: const Icon(Icons.shopping_cart),
      ),
    );
  }
}
```

### Announcing Snackbar-like Messages

```dart
import 'package:flutter/material.dart';

class StatusAnnouncer extends StatefulWidget {
  const StatusAnnouncer({super.key});

  @override
  State<StatusAnnouncer> createState() => _StatusAnnouncerState();
}

class _StatusAnnouncerState extends State<StatusAnnouncer> {
  String _status = '';

  void _saveDocument() {
    // Perform save ...
    setState(() => _status = 'Document saved successfully');
    // SemanticsService.announce is another option for one-shot messages.
    SemanticsService.announce(_status, TextDirection.ltr);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        ElevatedButton(
          onPressed: _saveDocument,
          child: const Text('Save'),
        ),
        if (_status.isNotEmpty)
          Semantics(
            liveRegion: true,
            child: Text(_status),
          ),
      ],
    );
  }
}
```

---

## 10. Platform-Specific Behavior

### VoiceOver (iOS / macOS)

- Reads `Semantics.label`, then `Semantics.value`, then the trait
  (button, header, etc.), then `Semantics.hint`.
- Custom actions appear in the **Actions** rotor (swipe up/down to cycle).
- Announcement order: label -> value -> trait -> hint.
- Use `SemanticsService.announce` for one-shot announcements.

### TalkBack (Android)

- Reads `Semantics.label`, then the role, then `Semantics.hint`.
- Custom actions appear in the **local context menu** (swipe up then right).
- TalkBack may concatenate adjacent text nodes; use `MergeSemantics` to
  control this explicitly.
- `Semantics.liveRegion` maps to `AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED`.

### Practical Cross-Platform Pattern

```dart
import 'dart:io' show Platform;

import 'package:flutter/material.dart';

class PlatformAwareSemantics extends StatelessWidget {
  const PlatformAwareSemantics({super.key});

  @override
  Widget build(BuildContext context) {
    final isIOS = Theme.of(context).platform == TargetPlatform.iOS;

    return Semantics(
      label: 'Notifications',
      hint: isIOS
          ? 'Double tap to open notifications'
          : 'Tap to open notifications',
      button: true,
      child: IconButton(
        icon: const Icon(Icons.notifications),
        onPressed: () {},
        tooltip: 'Notifications',
      ),
    );
  }
}
```

### Testing Both Platforms in Widget Tests

```dart
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final platform in [TargetPlatform.iOS, TargetPlatform.android]) {
    testWidgets('semantics are correct on $platform', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData(platform: platform),
          home: const Scaffold(
            body: PlatformAwareSemantics(),
          ),
        ),
      );

      final semantics = tester.getSemantics(find.byType(PlatformAwareSemantics));

      expect(semantics.label, 'Notifications');

      if (platform == TargetPlatform.iOS) {
        expect(semantics.hint, 'Double tap to open notifications');
      } else {
        expect(semantics.hint, 'Tap to open notifications');
      }
    });
  }
}

// Stub for compilation -- real implementation in the main source.
class PlatformAwareSemantics extends StatelessWidget {
  const PlatformAwareSemantics({super.key});

  @override
  Widget build(BuildContext context) {
    final isIOS = Theme.of(context).platform == TargetPlatform.iOS;
    return Semantics(
      label: 'Notifications',
      hint: isIOS
          ? 'Double tap to open notifications'
          : 'Tap to open notifications',
      button: true,
      child: IconButton(
        icon: const Icon(Icons.notifications),
        onPressed: () {},
        tooltip: 'Notifications',
      ),
    );
  }
}
```
