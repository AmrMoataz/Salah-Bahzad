# Focus Management and Keyboard Navigation

## Overview

Flutter's focus system controls which widget receives keyboard input. Proper
focus management is essential for keyboard users, switch-control users, and
screen reader navigation. The key classes are `FocusNode`, `FocusScope`,
`FocusTraversalGroup`, and the `Shortcuts` / `Actions` widgets.

---

## 1. FocusNode and FocusScope

### FocusNode Basics

Every focusable widget owns a `FocusNode`. You can create one manually for
fine-grained control.

```dart
import 'package:flutter/material.dart';

class FocusableCard extends StatefulWidget {
  final String title;

  const FocusableCard({super.key, required this.title});

  @override
  State<FocusableCard> createState() => _FocusableCardState();
}

class _FocusableCardState extends State<FocusableCard> {
  final FocusNode _focusNode = FocusNode();
  bool _isFocused = false;

  @override
  void initState() {
    super.initState();
    _focusNode.addListener(() {
      setState(() => _isFocused = _focusNode.hasFocus);
    });
  }

  @override
  void dispose() {
    _focusNode.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Focus(
      focusNode: _focusNode,
      child: GestureDetector(
        onTap: () => _focusNode.requestFocus(),
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          decoration: BoxDecoration(
            border: Border.all(
              color: _isFocused ? Colors.blue : Colors.grey,
              width: _isFocused ? 3 : 1,
            ),
            borderRadius: BorderRadius.circular(8),
          ),
          padding: const EdgeInsets.all(16),
          child: Text(widget.title),
        ),
      ),
    );
  }
}
```

### FocusScope

A `FocusScope` groups focus nodes so that tab traversal stays within a logical
region (e.g., a dialog or a form section).

```dart
import 'package:flutter/material.dart';

class LoginForm extends StatelessWidget {
  const LoginForm({super.key});

  @override
  Widget build(BuildContext context) {
    return FocusScope(
      // Keeps focus cycling within this scope when the scope is active.
      child: Column(
        children: [
          const TextField(
            decoration: InputDecoration(labelText: 'Email'),
            autofocus: true,
          ),
          const SizedBox(height: 16),
          const TextField(
            decoration: InputDecoration(labelText: 'Password'),
            obscureText: true,
          ),
          const SizedBox(height: 24),
          ElevatedButton(
            onPressed: () {},
            child: const Text('Sign In'),
          ),
        ],
      ),
    );
  }
}
```

---

## 2. Focus Traversal Order

Flutter traverses focusable widgets in **reading order** by default (left to
right, top to bottom for LTR locales). You can customize this with traversal
policies.

### ReadingOrderTraversalPolicy (Default)

```dart
import 'package:flutter/material.dart';

class ReadingOrderExample extends StatelessWidget {
  const ReadingOrderExample({super.key});

  @override
  Widget build(BuildContext context) {
    return FocusTraversalGroup(
      policy: ReadingOrderTraversalPolicy(),
      child: const Row(
        children: [
          Expanded(child: TextField(decoration: InputDecoration(labelText: 'First'))),
          SizedBox(width: 16),
          Expanded(child: TextField(decoration: InputDecoration(labelText: 'Second'))),
        ],
      ),
    );
  }
}
```

### OrderedTraversalPolicy

Use `OrderedTraversalPolicy` with `FocusTraversalOrder` to set an explicit
numeric order.

```dart
import 'package:flutter/material.dart';

class ExplicitOrderForm extends StatelessWidget {
  const ExplicitOrderForm({super.key});

  @override
  Widget build(BuildContext context) {
    return FocusTraversalGroup(
      policy: OrderedTraversalPolicy(),
      child: Column(
        children: [
          FocusTraversalOrder(
            order: const NumericFocusOrder(2),
            child: const TextField(
              decoration: InputDecoration(labelText: 'Last Name'),
            ),
          ),
          const SizedBox(height: 16),
          FocusTraversalOrder(
            order: const NumericFocusOrder(1),
            child: const TextField(
              decoration: InputDecoration(labelText: 'First Name'),
            ),
          ),
          const SizedBox(height: 16),
          FocusTraversalOrder(
            order: const NumericFocusOrder(3),
            child: ElevatedButton(
              onPressed: () {},
              child: const Text('Submit'),
            ),
          ),
        ],
      ),
    );
  }
}
```

Tab key now visits First Name -> Last Name -> Submit regardless of visual
layout order.

---

## 3. FocusTraversalGroup

`FocusTraversalGroup` creates a boundary so that tab traversal completes the
group before moving to the next group. This is ideal for toolbars, forms, and
side panels.

```dart
import 'package:flutter/material.dart';

class TwoColumnLayout extends StatelessWidget {
  const TwoColumnLayout({super.key});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        // Sidebar -- focus traverses all sidebar items first.
        Expanded(
          flex: 1,
          child: FocusTraversalGroup(
            child: Column(
              children: [
                TextButton(onPressed: () {}, child: const Text('Dashboard')),
                TextButton(onPressed: () {}, child: const Text('Settings')),
                TextButton(onPressed: () {}, child: const Text('Profile')),
              ],
            ),
          ),
        ),
        // Main content -- focus moves here after the sidebar group.
        Expanded(
          flex: 3,
          child: FocusTraversalGroup(
            child: Column(
              children: [
                const TextField(decoration: InputDecoration(labelText: 'Search')),
                const SizedBox(height: 16),
                ElevatedButton(
                  onPressed: () {},
                  child: const Text('Go'),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
```

---

## 4. Keyboard Shortcuts with Shortcuts and Actions

The `Shortcuts` widget maps key combinations to `Intent` objects. The
`Actions` widget maps those intents to callbacks.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

// 1. Define intents.
class SaveIntent extends Intent {
  const SaveIntent();
}

class UndoIntent extends Intent {
  const UndoIntent();
}

// 2. Build the widget tree.
class EditorScreen extends StatelessWidget {
  const EditorScreen({super.key});

  void _save() {
    debugPrint('Document saved');
  }

  void _undo() {
    debugPrint('Undo performed');
  }

  @override
  Widget build(BuildContext context) {
    return Shortcuts(
      shortcuts: <ShortcutActivator, Intent>{
        const SingleActivator(LogicalKeyboardKey.keyS, control: true): const SaveIntent(),
        const SingleActivator(LogicalKeyboardKey.keyZ, control: true): const UndoIntent(),
      },
      child: Actions(
        actions: <Type, Action<Intent>>{
          SaveIntent: CallbackAction<SaveIntent>(onInvoke: (_) => _save()),
          UndoIntent: CallbackAction<UndoIntent>(onInvoke: (_) => _undo()),
        },
        child: Focus(
          autofocus: true,
          child: Scaffold(
            appBar: AppBar(title: const Text('Editor')),
            body: const Center(
              child: Text('Press Ctrl+S to save, Ctrl+Z to undo'),
            ),
          ),
        ),
      ),
    );
  }
}
```

### Conditionally Enabled Shortcuts

```dart
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

class DeleteIntent extends Intent {
  const DeleteIntent();
}

class ConditionalShortcutExample extends StatefulWidget {
  const ConditionalShortcutExample({super.key});

  @override
  State<ConditionalShortcutExample> createState() =>
      _ConditionalShortcutExampleState();
}

class _ConditionalShortcutExampleState
    extends State<ConditionalShortcutExample> {
  bool _hasSelection = false;

  @override
  Widget build(BuildContext context) {
    return Shortcuts(
      shortcuts: const <ShortcutActivator, Intent>{
        SingleActivator(LogicalKeyboardKey.delete): DeleteIntent(),
      },
      child: Actions(
        actions: <Type, Action<Intent>>{
          DeleteIntent: CallbackAction<DeleteIntent>(
            onInvoke: (_) {
              if (_hasSelection) {
                debugPrint('Item deleted');
              }
              return null;
            },
          ),
        },
        child: Focus(
          autofocus: true,
          child: Column(
            children: [
              SwitchListTile(
                title: const Text('Simulate selection'),
                value: _hasSelection,
                onChanged: (v) => setState(() => _hasSelection = v),
              ),
              Text(_hasSelection
                  ? 'Press Delete to remove'
                  : 'Select an item first'),
            ],
          ),
        ),
      ),
    );
  }
}
```

---

## 5. CallbackShortcuts

For simple cases where you do not need intent/action separation, use
`CallbackShortcuts`.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

class QuickShortcutExample extends StatelessWidget {
  const QuickShortcutExample({super.key});

  @override
  Widget build(BuildContext context) {
    return CallbackShortcuts(
      bindings: <ShortcutActivator, VoidCallback>{
        const SingleActivator(LogicalKeyboardKey.escape): () {
          Navigator.of(context).maybePop();
        },
        const SingleActivator(LogicalKeyboardKey.keyN, control: true): () {
          debugPrint('New item created');
        },
      },
      child: Focus(
        autofocus: true,
        child: Scaffold(
          appBar: AppBar(title: const Text('Quick Shortcuts')),
          body: const Center(
            child: Text('Esc to go back, Ctrl+N for new item'),
          ),
        ),
      ),
    );
  }
}
```

---

## 6. Focus Indicators (Visible Focus Rings)

Sighted keyboard users need a visible indicator of the currently focused
element. Flutter's Material widgets include built-in focus styling, but custom
widgets need explicit handling.

```dart
import 'package:flutter/material.dart';

class AccessibleChip extends StatefulWidget {
  final String label;
  final VoidCallback onPressed;

  const AccessibleChip({
    super.key,
    required this.label,
    required this.onPressed,
  });

  @override
  State<AccessibleChip> createState() => _AccessibleChipState();
}

class _AccessibleChipState extends State<AccessibleChip> {
  bool _isFocused = false;

  @override
  Widget build(BuildContext context) {
    return Focus(
      onFocusChange: (focused) => setState(() => _isFocused = focused),
      child: GestureDetector(
        onTap: widget.onPressed,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          decoration: BoxDecoration(
            color: Colors.blue.shade50,
            borderRadius: BorderRadius.circular(20),
            border: Border.all(
              color: _isFocused ? Colors.blue : Colors.transparent,
              width: 2,
            ),
            boxShadow: _isFocused
                ? [
                    BoxShadow(
                      color: Colors.blue.withValues(alpha: 0.4),
                      blurRadius: 4,
                      spreadRadius: 1,
                    ),
                  ]
                : [],
          ),
          child: Text(widget.label),
        ),
      ),
    );
  }
}
```

### Global Focus Indicator via Theme

```dart
import 'package:flutter/material.dart';

ThemeData accessibleTheme() {
  return ThemeData(
    // Ensure all Material widgets show a clear focus indicator.
    focusColor: Colors.blue.shade700,
    inputDecorationTheme: InputDecorationTheme(
      focusedBorder: OutlineInputBorder(
        borderSide: BorderSide(color: Colors.blue.shade700, width: 2),
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        side: const BorderSide(color: Colors.transparent, width: 2),
      ).copyWith(
        side: WidgetStateProperty.resolveWith((states) {
          if (states.contains(WidgetState.focused)) {
            return BorderSide(color: Colors.blue.shade700, width: 2);
          }
          return const BorderSide(color: Colors.transparent, width: 2);
        }),
      ),
    ),
  );
}
```

---

## 7. Autofocus

Set `autofocus: true` on the first logical input when a screen loads. Only one
widget per route should have autofocus.

```dart
import 'package:flutter/material.dart';

class SearchScreen extends StatelessWidget {
  const SearchScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Search')),
      body: const Padding(
        padding: EdgeInsets.all(16),
        child: TextField(
          autofocus: true, // Keyboard opens immediately on mobile.
          decoration: InputDecoration(
            labelText: 'Search',
            prefixIcon: Icon(Icons.search),
          ),
        ),
      ),
    );
  }
}
```

---

## 8. Request Focus Programmatically

After navigation events, dialog openings, or dynamic content changes, move
focus explicitly so keyboard and screen reader users land in the right place.

```dart
import 'package:flutter/material.dart';

class DynamicFocusExample extends StatefulWidget {
  const DynamicFocusExample({super.key});

  @override
  State<DynamicFocusExample> createState() => _DynamicFocusExampleState();
}

class _DynamicFocusExampleState extends State<DynamicFocusExample> {
  final FocusNode _resultFocusNode = FocusNode();
  bool _showResult = false;

  @override
  void dispose() {
    _resultFocusNode.dispose();
    super.dispose();
  }

  void _performSearch() {
    setState(() => _showResult = true);
    // Move focus to the result area after the frame is painted.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _resultFocusNode.requestFocus();
    });
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        ElevatedButton(
          onPressed: _performSearch,
          child: const Text('Search'),
        ),
        if (_showResult)
          Focus(
            focusNode: _resultFocusNode,
            child: Semantics(
              liveRegion: true,
              child: const Padding(
                padding: EdgeInsets.all(16),
                child: Text('3 results found'),
              ),
            ),
          ),
      ],
    );
  }
}
```

### Focus After Dialog Dismiss

```dart
import 'package:flutter/material.dart';

class DialogFocusExample extends StatefulWidget {
  const DialogFocusExample({super.key});

  @override
  State<DialogFocusExample> createState() => _DialogFocusExampleState();
}

class _DialogFocusExampleState extends State<DialogFocusExample> {
  final FocusNode _triggerFocusNode = FocusNode();

  @override
  void dispose() {
    _triggerFocusNode.dispose();
    super.dispose();
  }

  Future<void> _showConfirmation() async {
    await showDialog<void>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: const Text('Confirm'),
          content: const Text('Are you sure?'),
          actions: [
            TextButton(
              autofocus: true, // Focus lands on Cancel when dialog opens.
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('OK'),
            ),
          ],
        );
      },
    );
    // Return focus to the button that opened the dialog.
    _triggerFocusNode.requestFocus();
  }

  @override
  Widget build(BuildContext context) {
    return ElevatedButton(
      focusNode: _triggerFocusNode,
      onPressed: _showConfirmation,
      child: const Text('Delete Item'),
    );
  }
}
```

---

## 9. Skip Navigation Patterns

On desktop and web, provide a mechanism to skip repetitive navigation and jump
to the main content.

```dart
import 'package:flutter/material.dart';

class SkipToContentApp extends StatelessWidget {
  const SkipToContentApp({super.key});

  @override
  Widget build(BuildContext context) {
    final GlobalKey mainContentKey = GlobalKey();
    final FocusNode mainContentFocus = FocusNode();

    return MaterialApp(
      home: Scaffold(
        body: Column(
          children: [
            // Skip link -- first focusable element on the page.
            Focus(
              child: Builder(
                builder: (context) {
                  return InkWell(
                    onTap: () => mainContentFocus.requestFocus(),
                    child: Container(
                      padding: const EdgeInsets.all(8),
                      color: Colors.blue,
                      child: const Text(
                        'Skip to main content',
                        style: TextStyle(color: Colors.white),
                      ),
                    ),
                  );
                },
              ),
            ),
            // Navigation bar with many links.
            Row(
              children: List.generate(
                5,
                (i) => TextButton(
                  onPressed: () {},
                  child: Text('Nav $i'),
                ),
              ),
            ),
            // Main content area.
            Expanded(
              child: Focus(
                focusNode: mainContentFocus,
                key: mainContentKey,
                child: Semantics(
                  label: 'Main content',
                  child: const Center(
                    child: Text('Page content goes here.'),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
```

A more sophisticated approach hides the skip link off-screen and reveals it
only when focused:

```dart
import 'package:flutter/material.dart';

class HiddenSkipLink extends StatefulWidget {
  final FocusNode targetFocusNode;

  const HiddenSkipLink({super.key, required this.targetFocusNode});

  @override
  State<HiddenSkipLink> createState() => _HiddenSkipLinkState();
}

class _HiddenSkipLinkState extends State<HiddenSkipLink> {
  bool _isVisible = false;

  @override
  Widget build(BuildContext context) {
    return Focus(
      onFocusChange: (focused) => setState(() => _isVisible = focused),
      child: Opacity(
        opacity: _isVisible ? 1.0 : 0.0,
        child: InkWell(
          onTap: () => widget.targetFocusNode.requestFocus(),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            color: Colors.black87,
            child: const Text(
              'Skip to main content',
              style: TextStyle(color: Colors.white),
            ),
          ),
        ),
      ),
    );
  }
}
```

---

## 10. Tab Order Best Practices

| Guideline                                           | Rationale                                                              |
|-----------------------------------------------------|------------------------------------------------------------------------|
| Keep visual order and focus order aligned.           | Prevents confusion for sighted keyboard users.                         |
| Use `FocusTraversalGroup` to scope navigation.       | Users complete one logical section before moving to the next.           |
| Avoid positive `tabIndex`-style hacks.               | Flutter does not have `tabIndex`; use `OrderedTraversalPolicy` instead. |
| Set `autofocus` on the primary action of each route. | Reduces keystrokes to start interacting.                               |
| Restore focus after closing overlays.                | Keyboard users lose context if focus disappears.                       |
| Test with Tab and Shift+Tab on desktop.              | Ensures forward and backward traversal both work correctly.            |
| Test with D-pad / remote on Android TV.              | D-pad navigation uses the same focus system.                           |
| Exclude non-interactive decorations from focus.      | Use `Focus(canRequestFocus: false)` or `ExcludeSemantics`.             |

### Excluding Non-Interactive Widgets from Tab Order

```dart
import 'package:flutter/material.dart';

class NonFocusableDecoration extends StatelessWidget {
  const NonFocusableDecoration({super.key});

  @override
  Widget build(BuildContext context) {
    return Focus(
      canRequestFocus: false, // Tab key skips this widget.
      child: Container(
        height: 2,
        color: Colors.grey.shade300,
      ),
    );
  }
}
```
