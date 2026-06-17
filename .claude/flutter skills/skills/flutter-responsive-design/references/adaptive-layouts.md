# Adaptive Layouts

## Custom Breakpoint System

Define breakpoints once and reuse everywhere. This avoids magic numbers scattered across your codebase.

```dart
enum ScreenSize { compact, medium, expanded, large }

class Breakpoints {
  const Breakpoints._();

  /// Material 3 canonical breakpoints
  static const double compact = 0;
  static const double medium = 600;
  static const double expanded = 840;
  static const double large = 1200;

  /// Determine the current screen size from a width value.
  static ScreenSize fromWidth(double width) => switch (width) {
        < medium => ScreenSize.compact,
        < expanded => ScreenSize.medium,
        < large => ScreenSize.expanded,
        _ => ScreenSize.large,
      };
}
```

---

## LayoutBuilder for Responsive Breakpoints

`LayoutBuilder` provides the parent's constraints, making it ideal for component-level responsiveness. Use it when the widget's own available space (not the full screen) should drive the layout.

```dart
class ResponsiveCard extends StatelessWidget {
  const ResponsiveCard({super.key, required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final screenSize = Breakpoints.fromWidth(constraints.maxWidth);

        return switch (screenSize) {
          ScreenSize.compact => _CompactCard(title: title, body: body),
          ScreenSize.medium || ScreenSize.expanded => _WideCard(title: title, body: body),
          ScreenSize.large => _DesktopCard(title: title, body: body),
        };
      },
    );
  }
}

class _CompactCard extends StatelessWidget {
  const _CompactCard({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(body, style: Theme.of(context).textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}

class _WideCard extends StatelessWidget {
  const _WideCard({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            Expanded(
              flex: 1,
              child: Text(title, style: Theme.of(context).textTheme.titleLarge),
            ),
            const SizedBox(width: 16),
            Expanded(
              flex: 2,
              child: Text(body, style: Theme.of(context).textTheme.bodyLarge),
            ),
          ],
        ),
      ),
    );
  }
}

class _DesktopCard extends StatelessWidget {
  const _DesktopCard({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 2,
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Row(
          children: [
            Expanded(
              flex: 1,
              child: Text(title, style: Theme.of(context).textTheme.headlineSmall),
            ),
            const SizedBox(width: 24),
            Expanded(
              flex: 3,
              child: Text(body, style: Theme.of(context).textTheme.bodyLarge),
            ),
          ],
        ),
      ),
    );
  }
}
```

---

## MediaQuery

`MediaQuery` provides device-level information: full screen size, text scale factor, orientation, padding (for notches/status bars), and accessibility settings. Use it for decisions that depend on the entire screen, not just the parent widget.

### Size and Orientation

```dart
class AdaptiveHomePage extends StatelessWidget {
  const AdaptiveHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.sizeOf(context);
    final orientation = MediaQuery.orientationOf(context);
    final screenSize = Breakpoints.fromWidth(size.width);

    return Scaffold(
      appBar: AppBar(title: const Text('Home')),
      body: switch ((screenSize, orientation)) {
        (ScreenSize.compact, _) => const _PhoneLayout(),
        (ScreenSize.medium, Orientation.portrait) => const _TabletPortraitLayout(),
        (ScreenSize.medium, Orientation.landscape) => const _TabletLandscapeLayout(),
        (_, _) => const _DesktopLayout(),
      },
    );
  }
}
```

### Text Scale Factor

Always test your UI with enlarged text. `MediaQuery.textScaleFactorOf(context)` tells you the user's preferred text scaling.

```dart
class ScaleAwareLabel extends StatelessWidget {
  const ScaleAwareLabel({super.key, required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    final textScale = MediaQuery.textScaleFactorOf(context);

    // Adjust padding proportionally to text scale to prevent overflow
    final verticalPadding = 8.0 * textScale;
    final horizontalPadding = 16.0 * textScale;

    return Container(
      padding: EdgeInsets.symmetric(
        vertical: verticalPadding,
        horizontal: horizontalPadding,
      ),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.primaryContainer,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        text,
        style: Theme.of(context).textTheme.labelLarge,
        overflow: TextOverflow.ellipsis,
        maxLines: 2,
      ),
    );
  }
}
```

### View Padding (Notches and System UI)

`MediaQuery.paddingOf(context)` returns the areas obscured by system UI (notch, status bar, bottom navigation bar). Use `SafeArea` as a shortcut, or read the values directly for fine-grained control.

```dart
class NotchAwareHeader extends StatelessWidget {
  const NotchAwareHeader({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final padding = MediaQuery.paddingOf(context);

    return Container(
      padding: EdgeInsets.only(
        top: padding.top + 16,
        left: padding.left + 16,
        right: padding.right + 16,
      ),
      color: Theme.of(context).colorScheme.surface,
      child: child,
    );
  }
}
```

---

## Responsive Grid Layout

A reusable responsive grid that adjusts column count based on available width.

```dart
class ResponsiveGrid extends StatelessWidget {
  const ResponsiveGrid({
    super.key,
    required this.children,
    this.minItemWidth = 200,
    this.spacing = 16,
    this.runSpacing = 16,
    this.padding = const EdgeInsets.all(16),
  });

  final List<Widget> children;
  final double minItemWidth;
  final double spacing;
  final double runSpacing;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final availableWidth = constraints.maxWidth - padding.horizontal;
        final columns = (availableWidth / minItemWidth).floor().clamp(1, 6);
        final itemWidth =
            (availableWidth - (columns - 1) * spacing) / columns;

        return SingleChildScrollView(
          padding: padding,
          child: Wrap(
            spacing: spacing,
            runSpacing: runSpacing,
            children: [
              for (final child in children)
                SizedBox(width: itemWidth, child: child),
            ],
          ),
        );
      },
    );
  }
}
```

### Usage

```dart
class ProductGridPage extends StatelessWidget {
  const ProductGridPage({super.key, required this.products});

  final List<Product> products;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Products')),
      body: ResponsiveGrid(
        minItemWidth: 240,
        children: [
          for (final product in products)
            ProductCard(key: ValueKey(product.id), product: product),
        ],
      ),
    );
  }
}
```

---

## Master-Detail Pattern

On compact screens, push a detail page. On wider screens, show a side-by-side layout.

```dart
class MasterDetailLayout<T> extends StatefulWidget {
  const MasterDetailLayout({
    super.key,
    required this.items,
    required this.listItemBuilder,
    required this.detailBuilder,
    required this.emptyDetailBuilder,
  });

  final List<T> items;
  final Widget Function(T item, bool isSelected, VoidCallback onTap) listItemBuilder;
  final Widget Function(T item) detailBuilder;
  final Widget Function() emptyDetailBuilder;

  @override
  State<MasterDetailLayout<T>> createState() => _MasterDetailLayoutState<T>();
}

class _MasterDetailLayoutState<T> extends State<MasterDetailLayout<T>> {
  T? _selectedItem;

  void _selectItem(T item, bool useNavigation) {
    if (useNavigation) {
      Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) => Scaffold(
            appBar: AppBar(),
            body: widget.detailBuilder(item),
          ),
        ),
      );
    } else {
      setState(() => _selectedItem = item);
    }
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final isCompact = Breakpoints.fromWidth(constraints.maxWidth) == ScreenSize.compact;

        if (isCompact) {
          return _MasterList<T>(
            items: widget.items,
            selectedItem: null,
            listItemBuilder: widget.listItemBuilder,
            onItemTap: (item) => _selectItem(item, true),
          );
        }

        return Row(
          children: [
            SizedBox(
              width: 320,
              child: _MasterList<T>(
                items: widget.items,
                selectedItem: _selectedItem,
                listItemBuilder: widget.listItemBuilder,
                onItemTap: (item) => _selectItem(item, false),
              ),
            ),
            const VerticalDivider(width: 1),
            Expanded(
              child: _selectedItem != null
                  ? widget.detailBuilder(_selectedItem as T)
                  : widget.emptyDetailBuilder(),
            ),
          ],
        );
      },
    );
  }
}

class _MasterList<T> extends StatelessWidget {
  const _MasterList({
    required this.items,
    required this.selectedItem,
    required this.listItemBuilder,
    required this.onItemTap,
  });

  final List<T> items;
  final T? selectedItem;
  final Widget Function(T item, bool isSelected, VoidCallback onTap) listItemBuilder;
  final void Function(T item) onItemTap;

  @override
  Widget build(BuildContext context) {
    return ListView.builder(
      itemCount: items.length,
      itemBuilder: (context, index) {
        final item = items[index];
        final isSelected = identical(item, selectedItem);
        return listItemBuilder(item, isSelected, () => onItemTap(item));
      },
    );
  }
}
```

### Usage

```dart
class EmailInboxPage extends StatelessWidget {
  const EmailInboxPage({super.key, required this.emails});

  final List<Email> emails;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Inbox')),
      body: MasterDetailLayout<Email>(
        items: emails,
        listItemBuilder: (email, isSelected, onTap) => ListTile(
          title: Text(email.subject),
          subtitle: Text(email.sender),
          selected: isSelected,
          selectedTileColor: Theme.of(context).colorScheme.primaryContainer,
          onTap: onTap,
        ),
        detailBuilder: (email) => Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(email.subject, style: Theme.of(context).textTheme.headlineSmall),
              const SizedBox(height: 8),
              Text('From: ${email.sender}', style: Theme.of(context).textTheme.bodyMedium),
              const SizedBox(height: 16),
              Expanded(child: SingleChildScrollView(child: Text(email.body))),
            ],
          ),
        ),
        emptyDetailBuilder: () => const Center(child: Text('Select an email to read')),
      ),
    );
  }
}
```

---

## Flex, Wrap, and Flow Layouts

### Wrap for Tag Chips

`Wrap` automatically moves children to the next line when they exceed the available width.

```dart
class TagChipGroup extends StatelessWidget {
  const TagChipGroup({super.key, required this.tags, required this.onTagTap});

  final List<String> tags;
  final ValueChanged<String> onTagTap;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 4,
      children: [
        for (final tag in tags)
          ActionChip(
            label: Text(tag),
            onPressed: () => onTagTap(tag),
          ),
      ],
    );
  }
}
```

### Responsive Row-to-Column

Switch between horizontal and vertical layout based on available width.

```dart
class ResponsiveRowColumn extends StatelessWidget {
  const ResponsiveRowColumn({
    super.key,
    required this.children,
    this.breakpoint = Breakpoints.medium,
    this.spacing = 16,
  });

  final List<Widget> children;
  final double breakpoint;
  final double spacing;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final isWide = constraints.maxWidth >= breakpoint;

        if (isWide) {
          return Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              for (final (index, child) in children.indexed) ...[
                if (index > 0) SizedBox(width: spacing),
                Expanded(child: child),
              ],
            ],
          );
        }

        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final (index, child) in children.indexed) ...[
              if (index > 0) SizedBox(height: spacing),
              child,
            ],
          ],
        );
      },
    );
  }
}
```

---

## SafeArea and Padding for Notches/System UI

`SafeArea` automatically insets its child to avoid system UI overlaps (status bar, notch, bottom nav bar, rounded corners).

```dart
class SafeScaffoldBody extends StatelessWidget {
  const SafeScaffoldBody({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      // Keep the bottom false if you have a BottomNavigationBar
      // (Scaffold already handles bottom insets for its own bars)
      bottom: false,
      child: child,
    );
  }
}
```

### Selective SafeArea

Use when you only need to respect certain edges:

```dart
class TopSafeContent extends StatelessWidget {
  const TopSafeContent({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      left: false,
      right: false,
      bottom: false,
      child: child,
    );
  }
}
```

---

## OrientationBuilder

Rebuild only when orientation changes. Useful when you want different layouts for portrait vs landscape within a specific widget subtree.

```dart
class ImageGallery extends StatelessWidget {
  const ImageGallery({super.key, required this.imageUrls});

  final List<String> imageUrls;

  @override
  Widget build(BuildContext context) {
    return OrientationBuilder(
      builder: (context, orientation) {
        final crossAxisCount = switch (orientation) {
          Orientation.portrait => 2,
          Orientation.landscape => 4,
        };

        return GridView.builder(
          padding: const EdgeInsets.all(8),
          gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: crossAxisCount,
            crossAxisSpacing: 8,
            mainAxisSpacing: 8,
          ),
          itemCount: imageUrls.length,
          itemBuilder: (context, index) => ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: Image.network(
              imageUrls[index],
              fit: BoxFit.cover,
              semanticLabel: 'Gallery image ${index + 1}',
            ),
          ),
        );
      },
    );
  }
}
```

---

## AspectRatio and FractionallySizedBox

### AspectRatio

Forces a child to maintain a specific width-to-height ratio.

```dart
class VideoThumbnail extends StatelessWidget {
  const VideoThumbnail({super.key, required this.imageUrl, required this.onPlay});

  final String imageUrl;
  final VoidCallback onPlay;

  @override
  Widget build(BuildContext context) {
    return AspectRatio(
      aspectRatio: 16 / 9,
      child: Stack(
        alignment: Alignment.center,
        children: [
          Positioned.fill(
            child: Image.network(
              imageUrl,
              fit: BoxFit.cover,
              semanticLabel: 'Video thumbnail',
            ),
          ),
          IconButton.filled(
            icon: const Icon(Icons.play_arrow, size: 48),
            onPressed: onPlay,
            tooltip: 'Play video',
          ),
        ],
      ),
    );
  }
}
```

### FractionallySizedBox

Sizes a child relative to its parent. Useful for responsive widths without hard-coded values.

```dart
class CenteredContentColumn extends StatelessWidget {
  const CenteredContentColumn({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        // On small screens take full width; on large screens, cap content width
        final widthFraction = switch (Breakpoints.fromWidth(constraints.maxWidth)) {
          ScreenSize.compact => 1.0,
          ScreenSize.medium => 0.85,
          ScreenSize.expanded => 0.7,
          ScreenSize.large => 0.55,
        };

        return Center(
          child: FractionallySizedBox(
            widthFactor: widthFraction,
            child: child,
          ),
        );
      },
    );
  }
}
```

---

## Responsive Text Sizing

Scale text sizes based on screen width to maintain readability across form factors.

```dart
class ResponsiveTextTheme {
  const ResponsiveTextTheme._();

  /// Returns a TextTheme scaled for the current screen width.
  static TextTheme scaled(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    final scaleFactor = switch (Breakpoints.fromWidth(width)) {
      ScreenSize.compact => 1.0,
      ScreenSize.medium => 1.1,
      ScreenSize.expanded => 1.15,
      ScreenSize.large => 1.2,
    };

    final base = Theme.of(context).textTheme;

    return base.copyWith(
      displayLarge: base.displayLarge?.copyWith(
        fontSize: (base.displayLarge?.fontSize ?? 57) * scaleFactor,
      ),
      headlineLarge: base.headlineLarge?.copyWith(
        fontSize: (base.headlineLarge?.fontSize ?? 32) * scaleFactor,
      ),
      headlineMedium: base.headlineMedium?.copyWith(
        fontSize: (base.headlineMedium?.fontSize ?? 28) * scaleFactor,
      ),
      titleLarge: base.titleLarge?.copyWith(
        fontSize: (base.titleLarge?.fontSize ?? 22) * scaleFactor,
      ),
      bodyLarge: base.bodyLarge?.copyWith(
        fontSize: (base.bodyLarge?.fontSize ?? 16) * scaleFactor,
      ),
      bodyMedium: base.bodyMedium?.copyWith(
        fontSize: (base.bodyMedium?.fontSize ?? 14) * scaleFactor,
      ),
      labelLarge: base.labelLarge?.copyWith(
        fontSize: (base.labelLarge?.fontSize ?? 14) * scaleFactor,
      ),
    );
  }
}
```

### Usage in a Widget

```dart
class ArticlePage extends StatelessWidget {
  const ArticlePage({super.key, required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final textTheme = ResponsiveTextTheme.scaled(context);

    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: SafeArea(
        child: CenteredContentColumn(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: textTheme.headlineLarge),
                const SizedBox(height: 16),
                Text(body, style: textTheme.bodyLarge),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
```

---

## Responsive Spacing Utility

Define adaptive spacing that scales with breakpoints to keep layouts proportional.

```dart
class ResponsiveSpacing {
  const ResponsiveSpacing._();

  static double horizontal(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    return switch (Breakpoints.fromWidth(width)) {
      ScreenSize.compact => 16,
      ScreenSize.medium => 24,
      ScreenSize.expanded => 32,
      ScreenSize.large => 48,
    };
  }

  static double vertical(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    return switch (Breakpoints.fromWidth(width)) {
      ScreenSize.compact => 12,
      ScreenSize.medium => 16,
      ScreenSize.expanded => 24,
      ScreenSize.large => 32,
    };
  }

  static EdgeInsets page(BuildContext context) {
    final h = horizontal(context);
    final v = vertical(context);
    return EdgeInsets.symmetric(horizontal: h, vertical: v);
  }
}
```

### Usage

```dart
class SettingsPage extends StatelessWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final spacing = ResponsiveSpacing.page(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: SafeArea(
        child: ListView(
          padding: spacing,
          children: [
            Text('Account', style: Theme.of(context).textTheme.titleLarge),
            SizedBox(height: ResponsiveSpacing.vertical(context)),
            const ListTile(title: Text('Profile'), leading: Icon(Icons.person)),
            const ListTile(title: Text('Security'), leading: Icon(Icons.lock)),
            SizedBox(height: ResponsiveSpacing.vertical(context) * 2),
            Text('Preferences', style: Theme.of(context).textTheme.titleLarge),
            SizedBox(height: ResponsiveSpacing.vertical(context)),
            const ListTile(title: Text('Theme'), leading: Icon(Icons.palette)),
            const ListTile(title: Text('Language'), leading: Icon(Icons.language)),
          ],
        ),
      ),
    );
  }
}
```
