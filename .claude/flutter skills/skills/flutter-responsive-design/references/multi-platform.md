# Multi-Platform Adaptation

## Platform Detection

Flutter runs on Android, iOS, macOS, Windows, Linux, and the web. Use these approaches to detect and adapt to the current platform.

```dart
import 'dart:io' show Platform;
import 'package:flutter/foundation.dart' show kIsWeb, defaultTargetPlatform, TargetPlatform;

/// Centralized platform detection utility.
class PlatformInfo {
  const PlatformInfo._();

  /// True when running in a web browser.
  static bool get isWeb => kIsWeb;

  /// True on iOS or macOS (Apple ecosystem).
  static bool get isApple =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.iOS ||
       defaultTargetPlatform == TargetPlatform.macOS);

  /// True on Android.
  static bool get isAndroid =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  /// True on iOS.
  static bool get isIOS =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.iOS;

  /// True on a desktop operating system (macOS, Windows, Linux).
  static bool get isDesktop =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.macOS ||
       defaultTargetPlatform == TargetPlatform.windows ||
       defaultTargetPlatform == TargetPlatform.linux);

  /// True on a mobile operating system (Android, iOS).
  static bool get isMobile =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
       defaultTargetPlatform == TargetPlatform.iOS);

  /// True when the platform supports hover events (desktop or web).
  static bool get supportsHover => isDesktop || isWeb;

  /// True when the platform supports a physical keyboard by default.
  static bool get hasPhysicalKeyboard => isDesktop || isWeb;

  /// Returns the current TargetPlatform.
  static TargetPlatform get current => defaultTargetPlatform;
}
```

---

## Cupertino vs Material Adaptive Widgets

Build widgets that render the appropriate design language for the current platform.

### Adaptive Dialog

```dart
Future<bool?> showAdaptiveConfirmDialog({
  required BuildContext context,
  required String title,
  required String content,
  String confirmLabel = 'Confirm',
  String cancelLabel = 'Cancel',
}) async {
  final isApple = PlatformInfo.isApple;

  if (isApple) {
    return showCupertinoDialog<bool>(
      context: context,
      builder: (context) => CupertinoAlertDialog(
        title: Text(title),
        content: Text(content),
        actions: [
          CupertinoDialogAction(
            isDestructiveAction: true,
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(cancelLabel),
          ),
          CupertinoDialogAction(
            isDefaultAction: true,
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(confirmLabel),
          ),
        ],
      ),
    );
  }

  return showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(title),
      content: Text(content),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(false),
          child: Text(cancelLabel),
        ),
        FilledButton(
          onPressed: () => Navigator.of(context).pop(true),
          child: Text(confirmLabel),
        ),
      ],
    ),
  );
}
```

### Adaptive Switch

```dart
class AdaptiveSwitch extends StatelessWidget {
  const AdaptiveSwitch({
    super.key,
    required this.value,
    required this.onChanged,
  });

  final bool value;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    if (PlatformInfo.isApple) {
      return CupertinoSwitch(value: value, onChanged: onChanged);
    }
    return Switch(value: value, onChanged: onChanged);
  }
}
```

### Adaptive Refresh Indicator

```dart
class AdaptiveRefreshList extends StatelessWidget {
  const AdaptiveRefreshList({
    super.key,
    required this.onRefresh,
    required this.itemCount,
    required this.itemBuilder,
  });

  final Future<void> Function() onRefresh;
  final int itemCount;
  final Widget Function(BuildContext, int) itemBuilder;

  @override
  Widget build(BuildContext context) {
    final listView = ListView.builder(
      itemCount: itemCount,
      itemBuilder: itemBuilder,
    );

    if (PlatformInfo.isApple) {
      return CustomScrollView(
        slivers: [
          CupertinoSliverRefreshControl(onRefresh: onRefresh),
          SliverList(
            delegate: SliverChildBuilderDelegate(
              itemBuilder,
              childCount: itemCount,
            ),
          ),
        ],
      );
    }

    return RefreshIndicator(
      onRefresh: onRefresh,
      child: listView,
    );
  }
}
```

### Adaptive Page Route

```dart
PageRoute<T> adaptivePageRoute<T>({
  required WidgetBuilder builder,
  RouteSettings? settings,
  bool fullscreenDialog = false,
}) {
  if (PlatformInfo.isApple) {
    return CupertinoPageRoute<T>(
      builder: builder,
      settings: settings,
      fullscreenDialog: fullscreenDialog,
    );
  }

  return MaterialPageRoute<T>(
    builder: builder,
    settings: settings,
    fullscreenDialog: fullscreenDialog,
  );
}
```

---

## Platform-Adaptive Navigation

Use different navigation patterns depending on screen size and platform: bottom navigation bar on phones, navigation rail on tablets, full side drawer on desktop.

```dart
class AdaptiveScaffold extends StatelessWidget {
  const AdaptiveScaffold({
    super.key,
    required this.destinations,
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.body,
    this.floatingActionButton,
  });

  final List<AdaptiveDestination> destinations;
  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final Widget body;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final screenSize = Breakpoints.fromWidth(constraints.maxWidth);

        return switch (screenSize) {
          ScreenSize.compact => _CompactScaffold(
              destinations: destinations,
              selectedIndex: selectedIndex,
              onDestinationSelected: onDestinationSelected,
              body: body,
              floatingActionButton: floatingActionButton,
            ),
          ScreenSize.medium => _MediumScaffold(
              destinations: destinations,
              selectedIndex: selectedIndex,
              onDestinationSelected: onDestinationSelected,
              body: body,
              floatingActionButton: floatingActionButton,
            ),
          ScreenSize.expanded || ScreenSize.large => _ExpandedScaffold(
              destinations: destinations,
              selectedIndex: selectedIndex,
              onDestinationSelected: onDestinationSelected,
              body: body,
              floatingActionButton: floatingActionButton,
            ),
        };
      },
    );
  }
}

class AdaptiveDestination {
  const AdaptiveDestination({
    required this.icon,
    required this.selectedIcon,
    required this.label,
  });

  final IconData icon;
  final IconData selectedIcon;
  final String label;
}

/// Phone layout: bottom navigation bar.
class _CompactScaffold extends StatelessWidget {
  const _CompactScaffold({
    required this.destinations,
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.body,
    this.floatingActionButton,
  });

  final List<AdaptiveDestination> destinations;
  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final Widget body;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: body,
      floatingActionButton: floatingActionButton,
      bottomNavigationBar: NavigationBar(
        selectedIndex: selectedIndex,
        onDestinationSelected: onDestinationSelected,
        destinations: [
          for (final dest in destinations)
            NavigationDestination(
              icon: Icon(dest.icon),
              selectedIcon: Icon(dest.selectedIcon),
              label: dest.label,
            ),
        ],
      ),
    );
  }
}

/// Tablet layout: navigation rail on the left.
class _MediumScaffold extends StatelessWidget {
  const _MediumScaffold({
    required this.destinations,
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.body,
    this.floatingActionButton,
  });

  final List<AdaptiveDestination> destinations;
  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final Widget body;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            selectedIndex: selectedIndex,
            onDestinationSelected: onDestinationSelected,
            labelType: NavigationRailLabelType.selected,
            leading: floatingActionButton,
            destinations: [
              for (final dest in destinations)
                NavigationRailDestination(
                  icon: Icon(dest.icon),
                  selectedIcon: Icon(dest.selectedIcon),
                  label: Text(dest.label),
                ),
            ],
          ),
          const VerticalDivider(width: 1, thickness: 1),
          Expanded(child: body),
        ],
      ),
    );
  }
}

/// Desktop layout: expanded navigation drawer.
class _ExpandedScaffold extends StatelessWidget {
  const _ExpandedScaffold({
    required this.destinations,
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.body,
    this.floatingActionButton,
  });

  final List<AdaptiveDestination> destinations;
  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final Widget body;
  final Widget? floatingActionButton;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Row(
        children: [
          NavigationDrawer(
            selectedIndex: selectedIndex,
            onDestinationSelected: onDestinationSelected,
            children: [
              const SizedBox(height: 16),
              if (floatingActionButton != null) ...[
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                  child: floatingActionButton,
                ),
                const SizedBox(height: 16),
              ],
              for (final dest in destinations)
                NavigationDrawerDestination(
                  icon: Icon(dest.icon),
                  selectedIcon: Icon(dest.selectedIcon),
                  label: Text(dest.label),
                ),
            ],
          ),
          Expanded(child: body),
        ],
      ),
    );
  }
}
```

### Usage

```dart
class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _selectedIndex = 0;

  static const _destinations = [
    AdaptiveDestination(
      icon: Icons.home_outlined,
      selectedIcon: Icons.home,
      label: 'Home',
    ),
    AdaptiveDestination(
      icon: Icons.search_outlined,
      selectedIcon: Icons.search,
      label: 'Search',
    ),
    AdaptiveDestination(
      icon: Icons.favorite_border,
      selectedIcon: Icons.favorite,
      label: 'Favorites',
    ),
    AdaptiveDestination(
      icon: Icons.person_outline,
      selectedIcon: Icons.person,
      label: 'Profile',
    ),
  ];

  static const _pages = [
    HomePage(),
    SearchPage(),
    FavoritesPage(),
    ProfilePage(),
  ];

  @override
  Widget build(BuildContext context) {
    return AdaptiveScaffold(
      destinations: _destinations,
      selectedIndex: _selectedIndex,
      onDestinationSelected: (index) => setState(() => _selectedIndex = index),
      body: _pages[_selectedIndex],
      floatingActionButton: FloatingActionButton(
        onPressed: () {},
        child: const Icon(Icons.add),
      ),
    );
  }
}
```

---

## Mouse and Keyboard Input Handling (Desktop/Web)

### Hover Effects

Use `MouseRegion` or the built-in hover support on Material widgets for desktop and web.

```dart
class HoverCard extends StatefulWidget {
  const HoverCard({super.key, required this.child, required this.onTap});

  final Widget child;
  final VoidCallback onTap;

  @override
  State<HoverCard> createState() => _HoverCardState();
}

class _HoverCardState extends State<HoverCard> {
  bool _isHovered = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _isHovered = true),
      onExit: (_) => setState(() => _isHovered = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 200),
          decoration: BoxDecoration(
            color: _isHovered
                ? colorScheme.surfaceContainerHighest
                : colorScheme.surface,
            borderRadius: BorderRadius.circular(12),
            boxShadow: [
              BoxShadow(
                color: colorScheme.shadow.withValues(alpha: _isHovered ? 0.15 : 0.05),
                blurRadius: _isHovered ? 12 : 4,
                offset: Offset(0, _isHovered ? 4 : 2),
              ),
            ],
          ),
          child: widget.child,
        ),
      ),
    );
  }
}
```

### Keyboard Shortcuts

Register keyboard shortcuts for desktop and web using `Shortcuts` and `Actions`.

```dart
class KeyboardShortcutWrapper extends StatelessWidget {
  const KeyboardShortcutWrapper({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Shortcuts(
      shortcuts: {
        LogicalKeySet(LogicalKeyboardKey.control, LogicalKeyboardKey.keyN):
            const CreateNewItemIntent(),
        LogicalKeySet(LogicalKeyboardKey.control, LogicalKeyboardKey.keyS):
            const SaveIntent(),
        LogicalKeySet(LogicalKeyboardKey.control, LogicalKeyboardKey.keyF):
            const SearchIntent(),
        LogicalKeySet(LogicalKeyboardKey.escape):
            const DismissIntent(),
      },
      child: Actions(
        actions: {
          CreateNewItemIntent: CallbackAction<CreateNewItemIntent>(
            onInvoke: (_) => _handleCreateNew(context),
          ),
          SaveIntent: CallbackAction<SaveIntent>(
            onInvoke: (_) => _handleSave(context),
          ),
          SearchIntent: CallbackAction<SearchIntent>(
            onInvoke: (_) => _handleSearch(context),
          ),
          DismissIntent: CallbackAction<DismissIntent>(
            onInvoke: (_) => Navigator.of(context).maybePop(),
          ),
        },
        child: Focus(
          autofocus: true,
          child: child,
        ),
      ),
    );
  }

  static Object? _handleCreateNew(BuildContext context) {
    // Implement create new item logic
    return null;
  }

  static Object? _handleSave(BuildContext context) {
    // Implement save logic
    return null;
  }

  static Object? _handleSearch(BuildContext context) {
    // Implement search focus logic
    return null;
  }
}

class CreateNewItemIntent extends Intent {
  const CreateNewItemIntent();
}

class SaveIntent extends Intent {
  const SaveIntent();
}

class SearchIntent extends Intent {
  const SearchIntent();
}
```

### Tooltips for Desktop

Always add tooltips to icon buttons and actions for discoverability on desktop.

```dart
class DesktopToolbar extends StatelessWidget {
  const DesktopToolbar({super.key});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Tooltip(
          message: 'New document (Ctrl+N)',
          child: IconButton(
            icon: const Icon(Icons.add),
            onPressed: () {},
          ),
        ),
        Tooltip(
          message: 'Save (Ctrl+S)',
          child: IconButton(
            icon: const Icon(Icons.save),
            onPressed: () {},
          ),
        ),
        Tooltip(
          message: 'Search (Ctrl+F)',
          child: IconButton(
            icon: const Icon(Icons.search),
            onPressed: () {},
          ),
        ),
      ],
    );
  }
}
```

---

## Right-Click Context Menus

Provide context menus on right-click for desktop and web users, and on long-press for mobile.

```dart
class ContextMenuWrapper extends StatelessWidget {
  const ContextMenuWrapper({
    super.key,
    required this.child,
    required this.menuItems,
  });

  final Widget child;
  final List<ContextMenuItem> menuItems;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onSecondaryTapUp: (details) =>
          _showMenu(context, details.globalPosition),
      onLongPressStart: PlatformInfo.isMobile
          ? (details) => _showMenu(context, details.globalPosition)
          : null,
      child: child,
    );
  }

  void _showMenu(BuildContext context, Offset position) {
    final overlay = Overlay.of(context).context.findRenderObject() as RenderBox;

    showMenu<String>(
      context: context,
      position: RelativeRect.fromRect(
        Rect.fromLTWH(position.dx, position.dy, 0, 0),
        Offset.zero & overlay.size,
      ),
      items: [
        for (final item in menuItems)
          PopupMenuItem<String>(
            value: item.id,
            onTap: item.onTap,
            child: Row(
              children: [
                if (item.icon != null) ...[
                  Icon(item.icon, size: 20),
                  const SizedBox(width: 12),
                ],
                Text(item.label),
                if (item.shortcut != null) ...[
                  const Spacer(),
                  Text(
                    item.shortcut!,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                  ),
                ],
              ],
            ),
          ),
      ],
    );
  }
}

class ContextMenuItem {
  const ContextMenuItem({
    required this.id,
    required this.label,
    required this.onTap,
    this.icon,
    this.shortcut,
  });

  final String id;
  final String label;
  final VoidCallback onTap;
  final IconData? icon;
  final String? shortcut;
}
```

### Usage

```dart
class FileListTile extends StatelessWidget {
  const FileListTile({super.key, required this.fileName});

  final String fileName;

  @override
  Widget build(BuildContext context) {
    return ContextMenuWrapper(
      menuItems: [
        ContextMenuItem(
          id: 'open',
          label: 'Open',
          icon: Icons.open_in_new,
          shortcut: 'Enter',
          onTap: () {},
        ),
        ContextMenuItem(
          id: 'rename',
          label: 'Rename',
          icon: Icons.edit,
          shortcut: 'F2',
          onTap: () {},
        ),
        ContextMenuItem(
          id: 'delete',
          label: 'Delete',
          icon: Icons.delete,
          shortcut: 'Del',
          onTap: () {},
        ),
      ],
      child: ListTile(
        leading: const Icon(Icons.insert_drive_file),
        title: Text(fileName),
      ),
    );
  }
}
```

---

## Window Sizing for Desktop (window_manager)

Use the `window_manager` package to control window size, minimum/maximum constraints, and title on desktop platforms.

### Setup in main.dart

```dart
import 'package:flutter/material.dart';
import 'package:window_manager/window_manager.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  if (PlatformInfo.isDesktop) {
    await windowManager.ensureInitialized();

    const windowOptions = WindowOptions(
      size: Size(1280, 800),
      minimumSize: Size(800, 600),
      center: true,
      backgroundColor: Colors.transparent,
      skipTaskbar: false,
      titleBarStyle: TitleBarStyle.hidden,
      title: 'My Desktop App',
    );

    await windowManager.waitUntilReadyToShow(windowOptions, () async {
      await windowManager.show();
      await windowManager.focus();
    });
  }

  runApp(const MyApp());
}
```

### Responding to Window Resize Events

```dart
class DesktopAwareApp extends StatefulWidget {
  const DesktopAwareApp({super.key});

  @override
  State<DesktopAwareApp> createState() => _DesktopAwareAppState();
}

class _DesktopAwareAppState extends State<DesktopAwareApp> with WindowListener {
  @override
  void initState() {
    super.initState();
    if (PlatformInfo.isDesktop) {
      windowManager.addListener(this);
    }
  }

  @override
  void dispose() {
    if (PlatformInfo.isDesktop) {
      windowManager.removeListener(this);
    }
    super.dispose();
  }

  @override
  void onWindowResize() {
    // React to window resize if needed
  }

  @override
  void onWindowMaximize() {
    // React to window maximize
  }

  @override
  void onWindowUnmaximize() {
    // React to window restore from maximize
  }

  @override
  Widget build(BuildContext context) {
    return const MaterialApp(home: HomePage());
  }
}
```

### Custom Title Bar for Desktop

```dart
class DesktopTitleBar extends StatelessWidget {
  const DesktopTitleBar({super.key, required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    if (!PlatformInfo.isDesktop) return const SizedBox.shrink();

    final colorScheme = Theme.of(context).colorScheme;

    return GestureDetector(
      onPanStart: (_) => windowManager.startDragging(),
      onDoubleTap: () async {
        if (await windowManager.isMaximized()) {
          await windowManager.unmaximize();
        } else {
          await windowManager.maximize();
        }
      },
      child: Container(
        height: 36,
        color: colorScheme.surface,
        child: Row(
          children: [
            const SizedBox(width: 16),
            Text(
              title,
              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    color: colorScheme.onSurface,
                  ),
            ),
            const Spacer(),
            _WindowButton(
              icon: Icons.minimize,
              tooltip: 'Minimize',
              onPressed: windowManager.minimize,
            ),
            _WindowButton(
              icon: Icons.crop_square,
              tooltip: 'Maximize',
              onPressed: () async {
                if (await windowManager.isMaximized()) {
                  await windowManager.unmaximize();
                } else {
                  await windowManager.maximize();
                }
              },
            ),
            _WindowButton(
              icon: Icons.close,
              tooltip: 'Close',
              onPressed: windowManager.close,
              isClose: true,
            ),
          ],
        ),
      ),
    );
  }
}

class _WindowButton extends StatefulWidget {
  const _WindowButton({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
    this.isClose = false,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onPressed;
  final bool isClose;

  @override
  State<_WindowButton> createState() => _WindowButtonState();
}

class _WindowButtonState extends State<_WindowButton> {
  bool _isHovered = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final hoverColor = widget.isClose
        ? const Color(0xFFE81123)
        : colorScheme.onSurface.withValues(alpha: 0.1);
    final iconColor = widget.isClose && _isHovered
        ? Colors.white
        : colorScheme.onSurface;

    return Tooltip(
      message: widget.tooltip,
      child: MouseRegion(
        onEnter: (_) => setState(() => _isHovered = true),
        onExit: (_) => setState(() => _isHovered = false),
        child: GestureDetector(
          onTap: widget.onPressed,
          child: Container(
            width: 46,
            height: 36,
            color: _isHovered ? hoverColor : Colors.transparent,
            child: Icon(widget.icon, size: 16, color: iconColor),
          ),
        ),
      ),
    );
  }
}
```

---

## Web-Specific Considerations

### URL Strategy

Use path-based URL strategy for clean, SEO-friendly URLs on the web.

```dart
import 'package:flutter_web_plugins/url_strategy.dart';

void main() {
  usePathUrlStrategy();
  runApp(const MyApp());
}
```

### Responsive Images for Web

Provide different image resolutions for web to reduce bandwidth.

```dart
class ResponsiveNetworkImage extends StatelessWidget {
  const ResponsiveNetworkImage({
    super.key,
    required this.baseUrl,
    required this.alt,
    this.fit = BoxFit.cover,
  });

  /// Base URL template with `{w}` placeholder for width.
  /// Example: 'https://cdn.example.com/images/hero-{w}.webp'
  final String baseUrl;
  final String alt;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final devicePixelRatio = MediaQuery.devicePixelRatioOf(context);
        final physicalWidth = (constraints.maxWidth * devicePixelRatio).toInt();

        // Snap to common image widths to improve cache hits
        final imageWidth = switch (physicalWidth) {
          < 400 => 400,
          < 800 => 800,
          < 1200 => 1200,
          < 1600 => 1600,
          _ => 2000,
        };

        final url = baseUrl.replaceAll('{w}', imageWidth.toString());

        return Image.network(
          url,
          fit: fit,
          semanticLabel: alt,
          loadingBuilder: (context, child, loadingProgress) {
            if (loadingProgress == null) return child;
            final progress = loadingProgress.expectedTotalBytes != null
                ? loadingProgress.cumulativeBytesLoaded /
                    loadingProgress.expectedTotalBytes!
                : null;
            return Center(
              child: CircularProgressIndicator(value: progress),
            );
          },
          errorBuilder: (context, error, stackTrace) => Container(
            color: Theme.of(context).colorScheme.surfaceContainerHighest,
            child: const Center(child: Icon(Icons.broken_image, size: 48)),
          ),
        );
      },
    );
  }
}
```

### Web-Aware Scrolling

Browsers have native scrollbars. On the web, use `ScrollConfiguration` to show or hide scrollbars as appropriate.

```dart
class WebScrollWrapper extends StatelessWidget {
  const WebScrollWrapper({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    if (!PlatformInfo.isWeb) return child;

    return ScrollConfiguration(
      behavior: const MaterialScrollBehavior().copyWith(
        scrollbars: true,
        // Enable drag scrolling on web (useful for touch-enabled laptops)
        dragDevices: {
          PointerDeviceKind.touch,
          PointerDeviceKind.mouse,
          PointerDeviceKind.trackpad,
        },
      ),
      child: child,
    );
  }
}
```

---

## Scrollbar Behavior per Platform

Different platforms have different scrollbar conventions. Configure scrollbar appearance per platform.

```dart
class AdaptiveScrollbar extends StatelessWidget {
  const AdaptiveScrollbar({
    super.key,
    required this.controller,
    required this.child,
  });

  final ScrollController controller;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    // On desktop and web, always show scrollbar for discoverability
    if (PlatformInfo.isDesktop || PlatformInfo.isWeb) {
      return Scrollbar(
        controller: controller,
        thumbVisibility: true,
        trackVisibility: true,
        child: child,
      );
    }

    // On iOS, use the Cupertino-style thin scrollbar
    if (PlatformInfo.isIOS) {
      return CupertinoScrollbar(
        controller: controller,
        child: child,
      );
    }

    // On Android, use the default thin scrollbar
    return Scrollbar(
      controller: controller,
      child: child,
    );
  }
}
```

### Usage

```dart
class LongContentPage extends StatefulWidget {
  const LongContentPage({super.key});

  @override
  State<LongContentPage> createState() => _LongContentPageState();
}

class _LongContentPageState extends State<LongContentPage> {
  final _scrollController = ScrollController();

  @override
  void dispose() {
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Documentation')),
      body: AdaptiveScrollbar(
        controller: _scrollController,
        child: ListView.builder(
          controller: _scrollController,
          padding: const EdgeInsets.all(16),
          itemCount: 100,
          itemBuilder: (context, index) => ListTile(
            title: Text('Item ${index + 1}'),
          ),
        ),
      ),
    );
  }
}
```

---

## Platform-Adaptive Text Selection

On desktop and web, enable text selection for body content so users can copy text. On mobile, keep the default behavior.

```dart
class SelectableBodyText extends StatelessWidget {
  const SelectableBodyText({super.key, required this.text, this.style});

  final String text;
  final TextStyle? style;

  @override
  Widget build(BuildContext context) {
    if (PlatformInfo.hasPhysicalKeyboard) {
      return SelectableText(
        text,
        style: style ?? Theme.of(context).textTheme.bodyLarge,
      );
    }
    return Text(text, style: style ?? Theme.of(context).textTheme.bodyLarge);
  }
}
```

---

## Complete Multi-Platform App Example

Putting it all together: a root widget that adapts theming, navigation, and input handling for every target platform.

```dart
class MultiPlatformApp extends StatefulWidget {
  const MultiPlatformApp({super.key});

  @override
  State<MultiPlatformApp> createState() => _MultiPlatformAppState();
}

class _MultiPlatformAppState extends State<MultiPlatformApp> {
  int _selectedIndex = 0;

  static const _destinations = [
    AdaptiveDestination(
      icon: Icons.dashboard_outlined,
      selectedIcon: Icons.dashboard,
      label: 'Dashboard',
    ),
    AdaptiveDestination(
      icon: Icons.inventory_2_outlined,
      selectedIcon: Icons.inventory_2,
      label: 'Products',
    ),
    AdaptiveDestination(
      icon: Icons.analytics_outlined,
      selectedIcon: Icons.analytics,
      label: 'Analytics',
    ),
    AdaptiveDestination(
      icon: Icons.settings_outlined,
      selectedIcon: Icons.settings,
      label: 'Settings',
    ),
  ];

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Multi-Platform App',
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: ThemeMode.system,
      home: KeyboardShortcutWrapper(
        child: AdaptiveScaffold(
          destinations: _destinations,
          selectedIndex: _selectedIndex,
          onDestinationSelected: (index) => setState(() => _selectedIndex = index),
          body: SafeArea(
            child: IndexedStack(
              index: _selectedIndex,
              children: const [
                DashboardPage(),
                ProductsPage(),
                AnalyticsPage(),
                SettingsPage(),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
```
