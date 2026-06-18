# Composition Patterns

## Widget Composition Over Inheritance

Flutter strongly favors composition over inheritance. Instead of subclassing a widget to add behavior, wrap it with another widget or extract shared logic into a helper widget.

**Anti-pattern -- inheriting to customize:**

```dart
// Bad: extending a concrete widget
class FancyButton extends ElevatedButton {
  // This breaks easily and bypasses Flutter's design
}
```

**Correct -- composing widgets:**

```dart
class FancyButton extends StatelessWidget {
  const FancyButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.icon,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return ElevatedButton(
      onPressed: onPressed,
      style: ElevatedButton.styleFrom(
        backgroundColor: theme.colorScheme.primary,
        foregroundColor: theme.colorScheme.onPrimary,
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 20),
            const SizedBox(width: 8),
          ],
          Text(label),
        ],
      ),
    );
  }
}
```

### Multi-level composition

Build complex UIs by layering small widgets:

```dart
class UserCard extends StatelessWidget {
  const UserCard({super.key, required this.user});

  final User user;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            UserAvatar(imageUrl: user.avatarUrl, size: 48),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  UserName(name: user.displayName),
                  UserBio(bio: user.bio),
                ],
              ),
            ),
            UserFollowButton(userId: user.id),
          ],
        ),
      ),
    );
  }
}

class UserAvatar extends StatelessWidget {
  const UserAvatar({super.key, required this.imageUrl, this.size = 40});

  final String? imageUrl;
  final double size;

  @override
  Widget build(BuildContext context) {
    return CircleAvatar(
      radius: size / 2,
      backgroundImage: imageUrl != null ? NetworkImage(imageUrl!) : null,
      child: imageUrl == null ? Icon(Icons.person, size: size * 0.6) : null,
    );
  }
}

class UserName extends StatelessWidget {
  const UserName({super.key, required this.name});

  final String name;

  @override
  Widget build(BuildContext context) {
    return Text(name, style: Theme.of(context).textTheme.titleMedium);
  }
}

class UserBio extends StatelessWidget {
  const UserBio({super.key, required this.bio});

  final String? bio;

  @override
  Widget build(BuildContext context) {
    if (bio == null || bio!.isEmpty) return const SizedBox.shrink();
    return Text(bio!, maxLines: 2, overflow: TextOverflow.ellipsis);
  }
}
```

---

## Const Widget Optimization Patterns

The `const` keyword lets Flutter skip rebuilding a widget entirely when its parent rebuilds, because the framework detects that the widget instance has not changed.

### Rule: extract static subtrees as const

```dart
// Before -- rebuilt every time the parent rebuilds
class ProductPage extends StatelessWidget {
  const ProductPage({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        // This header never changes but is rebuilt on every build()
        Padding(
          padding: EdgeInsets.all(16),
          child: Text('Product Details', style: TextStyle(fontSize: 24)),
        ),
        ProductDetails(product: product),
      ],
    );
  }
}

// After -- the header is const and never rebuilds
class ProductPage extends StatelessWidget {
  const ProductPage({super.key, required this.product});

  final Product product;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        const _ProductHeader(),
        ProductDetails(product: product),
      ],
    );
  }
}

class _ProductHeader extends StatelessWidget {
  const _ProductHeader();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.all(16),
      child: Text('Product Details', style: TextStyle(fontSize: 24)),
    );
  }
}
```

### Const-friendly patterns

```dart
class AppDivider extends StatelessWidget {
  const AppDivider({super.key});

  @override
  Widget build(BuildContext context) {
    return const Divider(height: 1, thickness: 1);
  }
}

class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.inbox_outlined, size: 64, color: Colors.grey),
          const SizedBox(height: 16),
          Text(message, style: Theme.of(context).textTheme.bodyLarge),
        ],
      ),
    );
  }
}
```

---

## Keys

Keys tell Flutter which widgets correspond to which elements across rebuilds. Without keys, Flutter matches widgets by type and position only.

### ValueKey

Use when you have a unique, stable value (ID, name, enum).

```dart
ListView.builder(
  itemCount: tasks.length,
  itemBuilder: (context, index) {
    final task = tasks[index];
    return TaskTile(
      key: ValueKey(task.id), // Stable identity across reorders
      task: task,
      onToggle: () => onToggle(task.id),
    );
  },
)
```

### ObjectKey

Use when the identity is the object reference itself and you do not have a unique scalar field.

```dart
@override
Widget build(BuildContext context) {
  return Column(
    children: items.map((item) {
      return DismissibleCard(
        key: ObjectKey(item), // Uses object identity
        child: Text(item.toString()),
      );
    }).toList(),
  );
}
```

### UniqueKey

Creates a new identity every time. Use when you want to force a widget to be treated as entirely new (e.g., resetting animation state).

```dart
class ShakeOnError extends StatelessWidget {
  const ShakeOnError({super.key, required this.errorCount, required this.child});

  final int errorCount;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return ShakeAnimation(
      key: ValueKey(errorCount), // New key each error -> restarts animation
      child: child,
    );
  }
}
```

### GlobalKey

Provides access to a widget's `State` or `RenderObject` from anywhere in the tree. Expensive -- use sparingly.

```dart
class FormPage extends StatefulWidget {
  const FormPage({super.key});

  @override
  State<FormPage> createState() => _FormPageState();
}

class _FormPageState extends State<FormPage> {
  // Store as a field, never create inside build()
  final _formKey = GlobalKey<FormState>();

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      child: Column(
        children: [
          TextFormField(
            decoration: const InputDecoration(labelText: 'Email'),
            validator: (value) {
              if (value == null || !value.contains('@')) {
                return 'Enter a valid email';
              }
              return null;
            },
          ),
          const SizedBox(height: 16),
          ElevatedButton(
            onPressed: () {
              if (_formKey.currentState!.validate()) {
                _formKey.currentState!.save();
                // proceed
              }
            },
            child: const Text('Submit'),
          ),
        ],
      ),
    );
  }
}
```

### Key Decision Guide

| Scenario | Key Type |
|---|---|
| List items with a unique ID | `ValueKey(item.id)` |
| Reorderable list | `ValueKey(item.id)` |
| Object without a scalar ID | `ObjectKey(item)` |
| Force-reset widget state | `UniqueKey()` or `ValueKey(changingValue)` |
| Access State/RenderObject externally | `GlobalKey<MyState>()` |
| Static widget that never changes | No key needed |

---

## Builder Pattern Widgets

Builder widgets defer subtree creation to a callback, giving you access to information only available at layout or runtime.

### LayoutBuilder

Adapts UI based on the parent's constraints.

```dart
class ResponsiveGrid extends StatelessWidget {
  const ResponsiveGrid({super.key, required this.items});

  final List<Widget> items;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final crossAxisCount = switch (constraints.maxWidth) {
          < 600 => 2,
          < 900 => 3,
          _ => 4,
        };

        return GridView.count(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          crossAxisCount: crossAxisCount,
          mainAxisSpacing: 8,
          crossAxisSpacing: 8,
          children: items,
        );
      },
    );
  }
}
```

### ValueListenableBuilder

Rebuilds only the subtree that depends on a `ValueNotifier`.

```dart
class ThemeSwitcher extends StatelessWidget {
  const ThemeSwitcher({super.key, required this.isDarkMode});

  final ValueNotifier<bool> isDarkMode;

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<bool>(
      valueListenable: isDarkMode,
      builder: (context, isDark, child) {
        return Switch(
          value: isDark,
          onChanged: (value) => isDarkMode.value = value,
        );
      },
    );
  }
}
```

### AnimatedBuilder

Rebuilds when an animation ticks, without rebuilding children passed via the `child` parameter.

```dart
class SpinningLogo extends StatefulWidget {
  const SpinningLogo({super.key});

  @override
  State<SpinningLogo> createState() => _SpinningLogoState();
}

class _SpinningLogoState extends State<SpinningLogo>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();
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
      // child is built once, not on every tick
      child: const FlutterLogo(size: 64),
      builder: (context, child) {
        return Transform.rotate(
          angle: _controller.value * 2 * 3.14159265,
          child: child,
        );
      },
    );
  }
}
```

### StreamBuilder

Renders the latest value from a Stream.

```dart
class ConnectionStatus extends StatelessWidget {
  const ConnectionStatus({super.key, required this.connectivityStream});

  final Stream<ConnectivityResult> connectivityStream;

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<ConnectivityResult>(
      stream: connectivityStream,
      builder: (context, snapshot) {
        if (!snapshot.hasData) {
          return const SizedBox.shrink();
        }

        final isOffline = snapshot.data == ConnectivityResult.none;

        return AnimatedContainer(
          duration: const Duration(milliseconds: 300),
          height: isOffline ? 32 : 0,
          color: Colors.red,
          child: isOffline
              ? const Center(
                  child: Text(
                    'No internet connection',
                    style: TextStyle(color: Colors.white, fontSize: 12),
                  ),
                )
              : const SizedBox.shrink(),
        );
      },
    );
  }
}
```

### FutureBuilder

Renders the result of a one-shot Future.

```dart
class AppVersionInfo extends StatelessWidget {
  const AppVersionInfo({super.key});

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<PackageInfo>(
      future: PackageInfo.fromPlatform(),
      builder: (context, snapshot) {
        return switch (snapshot) {
          AsyncSnapshot(hasData: true, :final data!) =>
            Text('v${data.version} (${data.buildNumber})'),
          AsyncSnapshot(hasError: true, :final error) =>
            Text('Error: $error'),
          _ => const Text('Loading...'),
        };
      },
    );
  }
}
```

---

## InheritedWidget for Dependency Provision

`InheritedWidget` provides data to the entire subtree without passing it through every constructor. This is the mechanism behind `Theme.of(context)`, `MediaQuery.of(context)`, etc.

```dart
class AppConfig {
  const AppConfig({
    required this.apiBaseUrl,
    required this.environment,
    required this.featureFlags,
  });

  final String apiBaseUrl;
  final String environment;
  final Set<String> featureFlags;

  bool hasFeature(String flag) => featureFlags.contains(flag);
}

class AppConfigProvider extends InheritedWidget {
  const AppConfigProvider({
    super.key,
    required this.config,
    required super.child,
  });

  final AppConfig config;

  static AppConfig of(BuildContext context) {
    final provider = context.dependOnInheritedWidgetOfExactType<AppConfigProvider>();
    assert(provider != null, 'No AppConfigProvider found in context');
    return provider!.config;
  }

  /// Use maybeOf when the provider is optional
  static AppConfig? maybeOf(BuildContext context) {
    return context
        .dependOnInheritedWidgetOfExactType<AppConfigProvider>()
        ?.config;
  }

  @override
  bool updateShouldNotify(AppConfigProvider oldWidget) {
    return config != oldWidget.config;
  }
}

// Usage at the root
void main() {
  runApp(
    AppConfigProvider(
      config: const AppConfig(
        apiBaseUrl: 'https://api.example.com',
        environment: 'production',
        featureFlags: {'dark_mode', 'new_checkout'},
      ),
      child: const MyApp(),
    ),
  );
}

// Usage anywhere in the tree
class FeatureGate extends StatelessWidget {
  const FeatureGate({
    super.key,
    required this.flag,
    required this.child,
    this.fallback = const SizedBox.shrink(),
  });

  final String flag;
  final Widget child;
  final Widget fallback;

  @override
  Widget build(BuildContext context) {
    final config = AppConfigProvider.of(context);
    return config.hasFeature(flag) ? child : fallback;
  }
}
```

---

## Widget Extraction Patterns for Reducing Rebuilds

When a parent widget rebuilds, all of its children rebuild too (unless they are const or identical instances). Extracting parts of the tree into separate widgets creates new `Element` boundaries that can short-circuit rebuilds.

### Before -- one large build method

```dart
class Dashboard extends StatefulWidget {
  const Dashboard({super.key});

  @override
  State<Dashboard> createState() => _DashboardState();
}

class _DashboardState extends State<Dashboard> {
  int _selectedTab = 0;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        // This header is static but rebuilds when _selectedTab changes
        Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              const Icon(Icons.dashboard, size: 32),
              const SizedBox(width: 8),
              Text('Dashboard', style: Theme.of(context).textTheme.headlineMedium),
            ],
          ),
        ),
        // Tab bar -- triggers rebuilds
        ToggleButtons(
          isSelected: List.generate(3, (i) => i == _selectedTab),
          onPressed: (index) => setState(() => _selectedTab = index),
          children: const [Text('Overview'), Text('Stats'), Text('Settings')],
        ),
        Expanded(child: _buildTabContent()),
      ],
    );
  }

  Widget _buildTabContent() {
    return switch (_selectedTab) {
      0 => const OverviewTab(),
      1 => const StatsTab(),
      _ => const SettingsTab(),
    };
  }
}
```

### After -- extracted widgets

```dart
class Dashboard extends StatefulWidget {
  const Dashboard({super.key});

  @override
  State<Dashboard> createState() => _DashboardState();
}

class _DashboardState extends State<Dashboard> {
  int _selectedTab = 0;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        const _DashboardHeader(), // Const -- never rebuilds
        _DashboardTabBar(
          selectedIndex: _selectedTab,
          onTabChanged: (index) => setState(() => _selectedTab = index),
        ),
        Expanded(
          child: IndexedStack(
            index: _selectedTab,
            children: const [OverviewTab(), StatsTab(), SettingsTab()],
          ),
        ),
      ],
    );
  }
}

class _DashboardHeader extends StatelessWidget {
  const _DashboardHeader();

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Row(
        children: [
          const Icon(Icons.dashboard, size: 32),
          const SizedBox(width: 8),
          Text('Dashboard', style: Theme.of(context).textTheme.headlineMedium),
        ],
      ),
    );
  }
}

class _DashboardTabBar extends StatelessWidget {
  const _DashboardTabBar({
    required this.selectedIndex,
    required this.onTabChanged,
  });

  final int selectedIndex;
  final ValueChanged<int> onTabChanged;

  @override
  Widget build(BuildContext context) {
    return ToggleButtons(
      isSelected: List.generate(3, (i) => i == selectedIndex),
      onPressed: onTabChanged,
      children: const [Text('Overview'), Text('Stats'), Text('Settings')],
    );
  }
}
```

This pattern ensures that:
- `_DashboardHeader` is `const` and never rebuilds.
- `_DashboardTabBar` only rebuilds when `selectedIndex` actually changes.
- `IndexedStack` keeps all three tabs alive and simply switches visibility, avoiding teardown/rebuild.
