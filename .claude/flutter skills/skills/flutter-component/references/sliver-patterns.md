# Sliver Patterns

Slivers are the low-level building blocks of scrollable areas in Flutter. Every scrollable widget (`ListView`, `GridView`, `PageView`) is built on slivers internally. Using slivers directly gives you full control over scroll composition.

---

## CustomScrollView with SliverAppBar, SliverList, SliverGrid, SliverToBoxAdapter

`CustomScrollView` lets you compose multiple sliver widgets into a single scrollable area.

```dart
class ProductCatalogPage extends StatelessWidget {
  const ProductCatalogPage({
    super.key,
    required this.featuredProducts,
    required this.categories,
    required this.allProducts,
  });

  final List<Product> featuredProducts;
  final List<Category> categories;
  final List<Product> allProducts;

  @override
  Widget build(BuildContext context) {
    return CustomScrollView(
      slivers: [
        // Collapsible app bar with image
        SliverAppBar(
          expandedHeight: 200,
          floating: false,
          pinned: true,
          flexibleSpace: FlexibleSpaceBar(
            title: const Text('Catalog'),
            background: Image.asset(
              'assets/images/catalog_banner.jpg',
              fit: BoxFit.cover,
            ),
          ),
          actions: [
            IconButton(
              icon: const Icon(Icons.search),
              onPressed: () {},
            ),
          ],
        ),

        // Section header -- use SliverToBoxAdapter for non-sliver widgets
        const SliverToBoxAdapter(
          child: Padding(
            padding: EdgeInsets.fromLTRB(16, 24, 16, 8),
            child: Text(
              'Featured',
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
            ),
          ),
        ),

        // Horizontal list inside a sliver (fixed height)
        SliverToBoxAdapter(
          child: SizedBox(
            height: 180,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              itemCount: featuredProducts.length,
              separatorBuilder: (_, __) => const SizedBox(width: 12),
              itemBuilder: (context, index) {
                return FeaturedProductCard(product: featuredProducts[index]);
              },
            ),
          ),
        ),

        // Category chips
        SliverToBoxAdapter(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Wrap(
              spacing: 8,
              children: categories.map((category) {
                return FilterChip(
                  label: Text(category.name),
                  onSelected: (_) {},
                );
              }).toList(),
            ),
          ),
        ),

        // Section header
        const SliverToBoxAdapter(
          child: Padding(
            padding: EdgeInsets.fromLTRB(16, 8, 16, 8),
            child: Text(
              'All Products',
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
            ),
          ),
        ),

        // Product grid
        SliverPadding(
          padding: const EdgeInsets.symmetric(horizontal: 16),
          sliver: SliverGrid(
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 2,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 0.75,
            ),
            delegate: SliverChildBuilderDelegate(
              (context, index) => ProductGridTile(
                key: ValueKey(allProducts[index].id),
                product: allProducts[index],
              ),
              childCount: allProducts.length,
            ),
          ),
        ),

        // Bottom spacing
        const SliverToBoxAdapter(
          child: SizedBox(height: 24),
        ),
      ],
    );
  }
}
```

### SliverAppBar Modes

| Property | Behavior |
|---|---|
| `pinned: true` | App bar stays visible at its collapsed height when scrolling up |
| `floating: true` | App bar reappears immediately when scrolling down (even mid-list) |
| `snap: true` (requires `floating: true`) | App bar snaps fully open or closed, no partial states |
| `stretch: true` | App bar stretches beyond its height when over-scrolling at the top |

```dart
// Floating + snap for a search bar that reappears quickly
SliverAppBar(
  floating: true,
  snap: true,
  title: const TextField(
    decoration: InputDecoration(
      hintText: 'Search...',
      border: InputBorder.none,
      prefixIcon: Icon(Icons.search),
    ),
  ),
)
```

---

## SliverPersistentHeader for Collapsible Headers

`SliverPersistentHeader` creates a header that can shrink between a minimum and maximum extent as the user scrolls.

```dart
class CollapsibleFilterHeader extends SliverPersistentHeaderDelegate {
  const CollapsibleFilterHeader({
    required this.filters,
    required this.onFilterChanged,
  });

  final List<String> filters;
  final ValueChanged<String> onFilterChanged;

  @override
  double get maxExtent => 120;

  @override
  double get minExtent => 56;

  @override
  bool shouldRebuild(covariant CollapsibleFilterHeader oldDelegate) {
    return filters != oldDelegate.filters;
  }

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) {
    // Progress from 0.0 (fully expanded) to 1.0 (fully collapsed)
    final collapseProgress = (shrinkOffset / (maxExtent - minExtent)).clamp(0.0, 1.0);

    return Container(
      color: Theme.of(context).colorScheme.surface,
      child: Stack(
        fit: StackFit.expand,
        children: [
          // Title -- always visible
          Positioned(
            left: 16,
            top: 8,
            child: Text(
              'Filter',
              style: TextStyle(
                fontSize: 16 + (4 * (1 - collapseProgress)), // Shrinks from 20 to 16
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
          // Filter chips -- fade out as header collapses
          Positioned(
            left: 0,
            right: 0,
            bottom: 8,
            child: Opacity(
              opacity: 1 - collapseProgress,
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Row(
                  children: filters.map((filter) {
                    return Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        label: Text(filter),
                        selected: false,
                        onSelected: (_) => onFilterChanged(filter),
                      ),
                    );
                  }).toList(),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// Usage
CustomScrollView(
  slivers: [
    SliverPersistentHeader(
      pinned: true,
      delegate: CollapsibleFilterHeader(
        filters: const ['All', 'New', 'Popular', 'Sale'],
        onFilterChanged: (filter) {},
      ),
    ),
    // ... other slivers
  ],
)
```

### Sticky section headers

Use `SliverPersistentHeader` with `pinned: true` for sticky section headers in a grouped list.

```dart
class SectionHeaderDelegate extends SliverPersistentHeaderDelegate {
  const SectionHeaderDelegate({required this.title});

  final String title;

  @override
  double get maxExtent => 40;

  @override
  double get minExtent => 40;

  @override
  bool shouldRebuild(covariant SectionHeaderDelegate oldDelegate) {
    return title != oldDelegate.title;
  }

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) {
    return Container(
      color: Theme.of(context).colorScheme.surfaceContainerHighest,
      alignment: Alignment.centerLeft,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Text(
        title,
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
          fontWeight: FontWeight.bold,
        ),
      ),
    );
  }
}

// Build a grouped contacts list
List<Widget> _buildContactSlivers(Map<String, List<Contact>> grouped) {
  return [
    for (final MapEntry(key: letter, value: contacts) in grouped.entries) ...[
      SliverPersistentHeader(
        pinned: true,
        delegate: SectionHeaderDelegate(title: letter),
      ),
      SliverList.builder(
        itemCount: contacts.length,
        itemBuilder: (context, index) => ContactTile(
          key: ValueKey(contacts[index].id),
          contact: contacts[index],
        ),
      ),
    ],
  ];
}
```

---

## SliverChildBuilderDelegate vs SliverChildListDelegate

### SliverChildBuilderDelegate (Lazy)

Creates children on demand. Use for large or infinite lists.

```dart
SliverList(
  delegate: SliverChildBuilderDelegate(
    (context, index) {
      final item = items[index];
      return ListTile(
        key: ValueKey(item.id),
        title: Text(item.title),
        subtitle: Text(item.subtitle),
      );
    },
    childCount: items.length,
    // Add separators
    // findChildIndexCallback helps Flutter find children efficiently during reorders
    findChildIndexCallback: (key) {
      final valueKey = key as ValueKey<String>;
      final index = items.indexWhere((item) => item.id == valueKey.value);
      return index != -1 ? index : null;
    },
  ),
)
```

### SliverChildListDelegate (Eager)

Creates all children upfront. Use only when the list is short and known.

```dart
SliverList(
  delegate: SliverChildListDelegate([
    const SettingsTile(icon: Icons.person, title: 'Account'),
    const SettingsTile(icon: Icons.notifications, title: 'Notifications'),
    const SettingsTile(icon: Icons.lock, title: 'Privacy'),
    const SettingsTile(icon: Icons.info, title: 'About'),
  ]),
)
```

### Convenience constructors (Flutter 3.x+)

```dart
// SliverList.builder -- shorthand for SliverChildBuilderDelegate
SliverList.builder(
  itemCount: items.length,
  itemBuilder: (context, index) => ItemTile(item: items[index]),
)

// SliverList.separated -- adds separators between items
SliverList.separated(
  itemCount: items.length,
  itemBuilder: (context, index) => ItemTile(item: items[index]),
  separatorBuilder: (context, index) => const Divider(height: 1),
)

// SliverGrid.builder
SliverGrid.builder(
  gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
    maxCrossAxisExtent: 200,
    mainAxisSpacing: 8,
    crossAxisSpacing: 8,
  ),
  itemCount: items.length,
  itemBuilder: (context, index) => GridTile(item: items[index]),
)
```

---

## NestedScrollView for Tabs + Scrolling

`NestedScrollView` coordinates scrolling between an outer scroll view (typically containing a `SliverAppBar` with a `TabBar`) and an inner scroll view for each tab's content.

```dart
class ProfilePage extends StatelessWidget {
  const ProfilePage({super.key, required this.user});

  final User user;

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 3,
      child: NestedScrollView(
        headerSliverBuilder: (context, innerBoxIsScrolled) {
          return [
            SliverOverlapAbsorber(
              handle: NestedScrollView.sliverOverlapAbsorberHandleFor(context),
              sliver: SliverAppBar(
                expandedHeight: 240,
                pinned: true,
                forceElevated: innerBoxIsScrolled,
                flexibleSpace: FlexibleSpaceBar(
                  title: Text(user.displayName),
                  background: UserProfileHeader(user: user),
                ),
                bottom: const TabBar(
                  tabs: [
                    Tab(text: 'Posts'),
                    Tab(text: 'Likes'),
                    Tab(text: 'Media'),
                  ],
                ),
              ),
            ),
          ];
        },
        body: const TabBarView(
          children: [
            _PostsTab(),
            _LikesTab(),
            _MediaTab(),
          ],
        ),
      ),
    );
  }
}

// Each tab must handle SliverOverlapInjector for correct scroll alignment
class _PostsTab extends StatelessWidget {
  const _PostsTab();

  @override
  Widget build(BuildContext context) {
    return Builder(
      builder: (context) {
        return CustomScrollView(
          // Each tab needs its own scroll key for independent scroll positions
          key: const PageStorageKey<String>('posts'),
          slivers: [
            SliverOverlapInjector(
              handle: NestedScrollView.sliverOverlapAbsorberHandleFor(context),
            ),
            SliverList.builder(
              itemCount: 50,
              itemBuilder: (context, index) => ListTile(
                title: Text('Post #$index'),
                subtitle: const Text('This is a sample post.'),
              ),
            ),
          ],
        );
      },
    );
  }
}

class _LikesTab extends StatelessWidget {
  const _LikesTab();

  @override
  Widget build(BuildContext context) {
    return Builder(
      builder: (context) {
        return CustomScrollView(
          key: const PageStorageKey<String>('likes'),
          slivers: [
            SliverOverlapInjector(
              handle: NestedScrollView.sliverOverlapAbsorberHandleFor(context),
            ),
            SliverList.builder(
              itemCount: 30,
              itemBuilder: (context, index) => ListTile(
                leading: const Icon(Icons.favorite, color: Colors.red),
                title: Text('Liked item #$index'),
              ),
            ),
          ],
        );
      },
    );
  }
}

class _MediaTab extends StatelessWidget {
  const _MediaTab();

  @override
  Widget build(BuildContext context) {
    return Builder(
      builder: (context) {
        return CustomScrollView(
          key: const PageStorageKey<String>('media'),
          slivers: [
            SliverOverlapInjector(
              handle: NestedScrollView.sliverOverlapAbsorberHandleFor(context),
            ),
            SliverGrid.builder(
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 3,
                mainAxisSpacing: 2,
                crossAxisSpacing: 2,
              ),
              itemCount: 60,
              itemBuilder: (context, index) => Container(
                color: Colors.grey[300],
                child: Center(child: Text('$index')),
              ),
            ),
          ],
        );
      },
    );
  }
}
```

---

## Performance Tips for Large Lists

### 1. Always use builder delegates

```dart
// Good -- lazy creation
SliverList.builder(
  itemCount: 10000,
  itemBuilder: (context, index) => ItemTile(item: items[index]),
)

// Bad -- creates all 10000 widgets upfront
SliverList(
  delegate: SliverChildListDelegate(
    items.map((item) => ItemTile(item: item)).toList(),
  ),
)
```

### 2. Use `addAutomaticKeepAlives: false` when items are cheap to rebuild

```dart
SliverList(
  delegate: SliverChildBuilderDelegate(
    (context, index) => SimpleTextTile(text: items[index]),
    childCount: items.length,
    addAutomaticKeepAlives: false, // Saves memory for simple items
    addRepaintBoundaries: true,    // Keep this true for paint isolation
  ),
)
```

### 3. Use `cacheExtent` to control off-screen pre-building

```dart
CustomScrollView(
  // Pre-build widgets 500 pixels beyond the visible area (default is 250)
  cacheExtent: 500,
  slivers: [/* ... */],
)
```

### 4. Use `RepaintBoundary` for expensive items

```dart
class ExpensiveListItem extends StatelessWidget {
  const ExpensiveListItem({super.key, required this.data});

  final ItemData data;

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      child: Card(
        child: Column(
          children: [
            Image.network(data.imageUrl, height: 200, fit: BoxFit.cover),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(data.title, style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 4),
                  Text(data.description, maxLines: 3, overflow: TextOverflow.ellipsis),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
```

### 5. Use `itemExtent` or `prototypeItem` when all items are the same height

```dart
// Fixed extent -- Flutter skips measuring each child
SliverFixedExtentList(
  itemExtent: 72,
  delegate: SliverChildBuilderDelegate(
    (context, index) => ContactTile(contact: contacts[index]),
    childCount: contacts.length,
  ),
)

// Prototype item -- Flutter measures one item and assumes all match
ListView.builder(
  prototypeItem: const ContactTile(contact: Contact.placeholder),
  itemCount: contacts.length,
  itemBuilder: (context, index) => ContactTile(contact: contacts[index]),
)
```

### 6. Use `findChildIndexCallback` for efficient updates in keyed lists

When the data source changes (reorders, insertions, deletions), this callback helps Flutter find existing children by their key instead of rebuilding everything.

```dart
SliverList(
  delegate: SliverChildBuilderDelegate(
    (context, index) {
      final message = messages[index];
      return MessageBubble(
        key: ValueKey(message.id),
        message: message,
      );
    },
    childCount: messages.length,
    findChildIndexCallback: (key) {
      final id = (key as ValueKey<String>).value;
      final index = messages.indexWhere((m) => m.id == id);
      return index != -1 ? index : null;
    },
  ),
)
```
