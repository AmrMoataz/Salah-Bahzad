# Offline-First Architecture

This guide covers patterns and production-ready Dart code for building Flutter
applications that work reliably without a network connection and synchronize
seamlessly when connectivity is restored.

## Table of Contents

1. [Cache-First Pattern](#cache-first-pattern)
2. [Repository Pattern with Offline Support](#repository-pattern-with-offline-support)
3. [Sync Queue for Pending Operations](#sync-queue-for-pending-operations)
4. [Conflict Resolution Strategies](#conflict-resolution-strategies)
5. [Connectivity Monitoring](#connectivity-monitoring)
6. [Optimistic UI Updates](#optimistic-ui-updates)
7. [Background Sync](#background-sync)
8. [Cache Invalidation Strategies](#cache-invalidation-strategies)

---

## Cache-First Pattern

The core flow: **check local cache -> return cached data -> fetch remote in
background -> update cache -> notify UI**.

```dart
// lib/data/cache/cache_entry.dart

/// A cached value with metadata for expiration and versioning.
class CacheEntry<T> {
  final T data;
  final DateTime cachedAt;
  final Duration ttl;
  final int version;

  const CacheEntry({
    required this.data,
    required this.cachedAt,
    required this.ttl,
    this.version = 1,
  });

  bool get isExpired =>
      DateTime.now().difference(cachedAt) > ttl;

  bool get isFresh => !isExpired;
}
```

```dart
// lib/data/cache/cache_policy.dart

/// Defines how a cache-first request should behave.
sealed class CachePolicy {
  const CachePolicy();
}

/// Always try cache first. Fetch remote only if cache misses or is expired.
class CacheFirst extends CachePolicy {
  final Duration ttl;
  const CacheFirst({this.ttl = const Duration(minutes: 30)});
}

/// Always fetch remote. Fall back to cache on network failure.
class NetworkFirst extends CachePolicy {
  const NetworkFirst();
}

/// Return cache only; never hit the network.
class CacheOnly extends CachePolicy {
  const CacheOnly();
}

/// Bypass cache entirely; always fetch from network.
class NetworkOnly extends CachePolicy {
  const NetworkOnly();
}

/// Return cache immediately, then fetch remote and emit again if data changed.
class StaleWhileRevalidate extends CachePolicy {
  final Duration ttl;
  const StaleWhileRevalidate({this.ttl = const Duration(hours: 1)});
}
```

---

## Repository Pattern with Offline Support

```dart
// lib/data/repositories/article_repository.dart

import 'dart:async';

import '../cache/cache_entry.dart';
import '../cache/cache_policy.dart';
import '../models/article.dart';
import '../sources/article_local_source.dart';
import '../sources/article_remote_source.dart';
import '../../core/connectivity/connectivity_service.dart';

class ArticleRepository {
  final ArticleLocalSource _local;
  final ArticleRemoteSource _remote;
  final ConnectivityService _connectivity;

  ArticleRepository({
    required ArticleLocalSource local,
    required ArticleRemoteSource remote,
    required ConnectivityService connectivity,
  })  : _local = local,
        _remote = remote,
        _connectivity = connectivity;

  /// Returns a stream that emits cached data first (if available), then
  /// fresh data from the network.
  Stream<List<Article>> getArticles({
    CachePolicy policy = const StaleWhileRevalidate(),
  }) async* {
    switch (policy) {
      case CacheFirst(:final ttl):
        yield* _cacheFirst(ttl);
      case NetworkFirst():
        yield* _networkFirst();
      case CacheOnly():
        yield await _local.getAll();
      case NetworkOnly():
        yield await _fetchAndCache();
      case StaleWhileRevalidate(:final ttl):
        yield* _staleWhileRevalidate(ttl);
    }
  }

  Stream<List<Article>> _cacheFirst(Duration ttl) async* {
    final cached = await _local.getAll();
    final meta = await _local.getCacheMeta('articles');

    if (cached.isNotEmpty && meta != null && !meta.isExpired) {
      yield cached;
      return;
    }

    // Cache miss or expired -- try network.
    if (cached.isNotEmpty) yield cached; // show stale while fetching

    if (await _connectivity.isConnected) {
      yield await _fetchAndCache();
    } else if (cached.isNotEmpty) {
      yield cached;
    }
  }

  Stream<List<Article>> _networkFirst() async* {
    if (await _connectivity.isConnected) {
      try {
        yield await _fetchAndCache();
        return;
      } catch (_) {
        // Fall through to cache.
      }
    }
    yield await _local.getAll();
  }

  Stream<List<Article>> _staleWhileRevalidate(Duration ttl) async* {
    final cached = await _local.getAll();
    if (cached.isNotEmpty) yield cached;

    if (await _connectivity.isConnected) {
      try {
        final fresh = await _fetchAndCache();
        // Only emit again if data actually changed.
        if (_hasChanged(cached, fresh)) {
          yield fresh;
        }
      } catch (_) {
        // Stale data already emitted; swallow the error.
      }
    }
  }

  Future<List<Article>> _fetchAndCache() async {
    final articles = await _remote.fetchAll();
    await _local.replaceAll(articles);
    await _local.setCacheMeta(
      'articles',
      CacheEntry(
        data: null,
        cachedAt: DateTime.now(),
        ttl: const Duration(minutes: 30),
      ),
    );
    return articles;
  }

  bool _hasChanged(List<Article> old, List<Article> current) {
    if (old.length != current.length) return true;
    for (var i = 0; i < old.length; i++) {
      if (old[i].id != current[i].id ||
          old[i].updatedAt != current[i].updatedAt) {
        return true;
      }
    }
    return false;
  }
}
```

### Local Source (Drift-backed)

```dart
// lib/data/sources/article_local_source.dart

import 'package:drift/drift.dart';

import '../database/app_database.dart';
import '../models/article.dart';
import '../cache/cache_entry.dart';

class ArticleLocalSource {
  final AppDatabase _db;

  ArticleLocalSource(this._db);

  Future<List<Article>> getAll() => _db.articleDao.getAllArticles();

  Future<void> replaceAll(List<Article> articles) async {
    await _db.transaction(() async {
      await _db.articleDao.deleteAll();
      await _db.articleDao.insertAll(articles);
    });
  }

  Future<CacheEntry<void>?> getCacheMeta(String key) async {
    final row = await _db.cacheMetaDao.get(key);
    if (row == null) return null;
    return CacheEntry(
      data: null,
      cachedAt: row.cachedAt,
      ttl: Duration(milliseconds: row.ttlMs),
      version: row.version,
    );
  }

  Future<void> setCacheMeta(String key, CacheEntry<void> entry) async {
    await _db.cacheMetaDao.upsert(
      key: key,
      cachedAt: entry.cachedAt,
      ttlMs: entry.ttl.inMilliseconds,
      version: entry.version,
    );
  }
}
```

---

## Sync Queue for Pending Operations

When the user performs a write while offline, enqueue the operation and replay
it when connectivity is restored.

```dart
// lib/data/sync/sync_operation.dart

enum SyncMethod { post, put, patch, delete }

enum SyncStatus { pending, inProgress, failed, completed }

class SyncOperation {
  final String id;
  final String endpoint;
  final SyncMethod method;
  final Map<String, dynamic>? payload;
  final DateTime createdAt;
  final int retryCount;
  final SyncStatus status;
  final String? errorMessage;

  const SyncOperation({
    required this.id,
    required this.endpoint,
    required this.method,
    this.payload,
    required this.createdAt,
    this.retryCount = 0,
    this.status = SyncStatus.pending,
    this.errorMessage,
  });

  SyncOperation copyWith({
    SyncStatus? status,
    int? retryCount,
    String? errorMessage,
  }) {
    return SyncOperation(
      id: id,
      endpoint: endpoint,
      method: method,
      payload: payload,
      createdAt: createdAt,
      retryCount: retryCount ?? this.retryCount,
      status: status ?? this.status,
      errorMessage: errorMessage ?? this.errorMessage,
    );
  }
}
```

```dart
// lib/data/sync/sync_queue.dart

import 'dart:async';
import 'dart:collection';

import 'package:uuid/uuid.dart';

import 'sync_operation.dart';
import 'sync_queue_storage.dart';
import '../../core/connectivity/connectivity_service.dart';
import '../../core/network/api_client.dart';

class SyncQueue {
  final SyncQueueStorage _storage;
  final ApiClient _api;
  final ConnectivityService _connectivity;
  final int _maxRetries;

  final _controller = StreamController<SyncQueueState>.broadcast();

  bool _processing = false;
  StreamSubscription<bool>? _connectivitySub;

  SyncQueue({
    required SyncQueueStorage storage,
    required ApiClient api,
    required ConnectivityService connectivity,
    int maxRetries = 3,
  })  : _storage = storage,
        _api = api,
        _connectivity = connectivity,
        _maxRetries = maxRetries {
    // Automatically start processing when connectivity is restored.
    _connectivitySub = _connectivity.onConnectivityChanged.listen((connected) {
      if (connected) processQueue();
    });
  }

  Stream<SyncQueueState> get stateStream => _controller.stream;

  /// Enqueue a new write operation.
  Future<String> enqueue({
    required String endpoint,
    required SyncMethod method,
    Map<String, dynamic>? payload,
  }) async {
    final op = SyncOperation(
      id: const Uuid().v4(),
      endpoint: endpoint,
      method: method,
      payload: payload,
      createdAt: DateTime.now(),
    );
    await _storage.add(op);
    _emitState();

    // Try to process immediately if online.
    if (await _connectivity.isConnected) {
      unawaited(processQueue());
    }
    return op.id;
  }

  /// Process all pending operations in FIFO order.
  Future<void> processQueue() async {
    if (_processing) return;
    _processing = true;
    _emitState();

    try {
      while (true) {
        final pending = await _storage.getPending();
        if (pending.isEmpty) break;

        for (final op in pending) {
          await _processOperation(op);
        }
      }
    } finally {
      _processing = false;
      _emitState();
    }
  }

  Future<void> _processOperation(SyncOperation op) async {
    await _storage.update(op.copyWith(status: SyncStatus.inProgress));
    _emitState();

    try {
      switch (op.method) {
        case SyncMethod.post:
          await _api.post(op.endpoint, body: op.payload);
        case SyncMethod.put:
          await _api.put(op.endpoint, body: op.payload);
        case SyncMethod.patch:
          await _api.patch(op.endpoint, body: op.payload);
        case SyncMethod.delete:
          await _api.delete(op.endpoint);
      }
      await _storage.update(op.copyWith(status: SyncStatus.completed));
    } catch (e) {
      final nextRetry = op.retryCount + 1;
      if (nextRetry >= _maxRetries) {
        await _storage.update(op.copyWith(
          status: SyncStatus.failed,
          retryCount: nextRetry,
          errorMessage: e.toString(),
        ));
      } else {
        await _storage.update(op.copyWith(
          status: SyncStatus.pending,
          retryCount: nextRetry,
          errorMessage: e.toString(),
        ));
      }
    }
    _emitState();
  }

  Future<void> _emitState() async {
    final pending = await _storage.getPending();
    final failed = await _storage.getFailed();
    _controller.add(SyncQueueState(
      pendingCount: pending.length,
      failedCount: failed.length,
      isProcessing: _processing,
    ));
  }

  void dispose() {
    _connectivitySub?.cancel();
    _controller.close();
  }
}

class SyncQueueState {
  final int pendingCount;
  final int failedCount;
  final bool isProcessing;

  const SyncQueueState({
    required this.pendingCount,
    required this.failedCount,
    required this.isProcessing,
  });
}
```

### Sync Queue Persistent Storage (Drift)

```dart
// lib/data/sync/sync_queue_storage.dart

import 'package:drift/drift.dart';

import '../database/app_database.dart';
import 'sync_operation.dart';

/// Drift table for persisting sync operations.
class SyncOperations extends Table {
  TextColumn get id => text()();
  TextColumn get endpoint => text()();
  IntColumn get method => intEnum<SyncMethod>()();
  TextColumn get payload => text().nullable()();
  DateTimeColumn get createdAt => dateTime()();
  IntColumn get retryCount => integer().withDefault(const Constant(0))();
  IntColumn get status => intEnum<SyncStatus>()();
  TextColumn get errorMessage => text().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

class SyncQueueStorage {
  final AppDatabase _db;

  SyncQueueStorage(this._db);

  Future<void> add(SyncOperation op) async {
    await _db.into(_db.syncOperations).insert(
          SyncOperationsCompanion.insert(
            id: op.id,
            endpoint: op.endpoint,
            method: op.method,
            payload: Value(
              op.payload != null ? _encodeJson(op.payload!) : null,
            ),
            createdAt: op.createdAt,
            status: op.status,
          ),
        );
  }

  Future<void> update(SyncOperation op) async {
    await (_db.update(_db.syncOperations)
          ..where((t) => t.id.equals(op.id)))
        .write(SyncOperationsCompanion(
      status: Value(op.status),
      retryCount: Value(op.retryCount),
      errorMessage: Value(op.errorMessage),
    ));
  }

  Future<List<SyncOperation>> getPending() async {
    final rows = await (_db.select(_db.syncOperations)
          ..where((t) => t.status.equalsValue(SyncStatus.pending))
          ..orderBy([(t) => OrderingTerm.asc(t.createdAt)]))
        .get();
    return rows.map(_fromRow).toList();
  }

  Future<List<SyncOperation>> getFailed() async {
    final rows = await (_db.select(_db.syncOperations)
          ..where((t) => t.status.equalsValue(SyncStatus.failed)))
        .get();
    return rows.map(_fromRow).toList();
  }

  Future<void> removeDone() async {
    await (_db.delete(_db.syncOperations)
          ..where((t) => t.status.equalsValue(SyncStatus.completed)))
        .go();
  }

  SyncOperation _fromRow(SyncOperationData row) {
    return SyncOperation(
      id: row.id,
      endpoint: row.endpoint,
      method: row.method,
      payload: row.payload != null ? _decodeJson(row.payload!) : null,
      createdAt: row.createdAt,
      retryCount: row.retryCount,
      status: row.status,
      errorMessage: row.errorMessage,
    );
  }

  String _encodeJson(Map<String, dynamic> data) {
    // Use dart:convert in real code.
    return data.toString();
  }

  Map<String, dynamic> _decodeJson(String raw) {
    // Use dart:convert in real code.
    return {};
  }
}
```

---

## Conflict Resolution Strategies

### Last-Write-Wins (LWW)

The simplest strategy -- the most recent `updatedAt` timestamp wins.

```dart
// lib/data/sync/conflict_resolver.dart

import '../models/article.dart';

sealed class ConflictResult<T> {
  const ConflictResult();
}

class KeepLocal<T> extends ConflictResult<T> {
  final T data;
  const KeepLocal(this.data);
}

class KeepRemote<T> extends ConflictResult<T> {
  final T data;
  const KeepRemote(this.data);
}

class Merged<T> extends ConflictResult<T> {
  final T data;
  const Merged(this.data);
}

/// Last-write-wins: whichever copy has the later updatedAt wins.
ConflictResult<Article> resolveLastWriteWins({
  required Article local,
  required Article remote,
}) {
  if (local.updatedAt.isAfter(remote.updatedAt)) {
    return KeepLocal(local);
  }
  return KeepRemote(remote);
}
```

### Field-Level Merge

More sophisticated: merge individual fields, preferring the more recently
changed field from each side.

```dart
/// Merges two versions field by field.
/// Requires a common ancestor (`base`) to detect which side changed each field.
ConflictResult<Article> resolveFieldMerge({
  required Article base,
  required Article local,
  required Article remote,
}) {
  final merged = Article(
    id: local.id,
    title: _pick(base.title, local.title, remote.title),
    body: _pick(base.body, local.body, remote.body),
    imageUrl: _pick(base.imageUrl, local.imageUrl, remote.imageUrl),
    updatedAt: local.updatedAt.isAfter(remote.updatedAt)
        ? local.updatedAt
        : remote.updatedAt,
  );
  return Merged(merged);
}

/// Returns the changed version, or remote if both changed.
T _pick<T>(T base, T local, T remote) {
  final localChanged = local != base;
  final remoteChanged = remote != base;

  if (localChanged && !remoteChanged) return local;
  if (remoteChanged && !localChanged) return remote;
  // Both changed -- prefer remote (server authority).
  return remote;
}
```

### Conflict Resolution in the Sync Flow

```dart
Future<void> syncArticle(Article localArticle) async {
  final remoteArticle = await _remote.fetchById(localArticle.id);

  if (remoteArticle == null) {
    // Remote deleted -- decide policy.
    await _local.delete(localArticle.id);
    return;
  }

  if (localArticle.updatedAt == remoteArticle.updatedAt) {
    // No conflict.
    return;
  }

  final result = resolveLastWriteWins(
    local: localArticle,
    remote: remoteArticle,
  );

  switch (result) {
    case KeepLocal(:final data):
      await _remote.update(data);
    case KeepRemote(:final data):
      await _local.upsert(data);
    case Merged(:final data):
      await _local.upsert(data);
      await _remote.update(data);
  }
}
```

---

## Connectivity Monitoring

### Using connectivity_plus

```yaml
dependencies:
  connectivity_plus: ^6.1.1
```

```dart
// lib/core/connectivity/connectivity_service.dart

import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';

class ConnectivityService {
  final Connectivity _connectivity;
  late final StreamController<bool> _controller;
  StreamSubscription<List<ConnectivityResult>>? _sub;
  bool _lastKnown = true;

  ConnectivityService({Connectivity? connectivity})
      : _connectivity = connectivity ?? Connectivity() {
    _controller = StreamController<bool>.broadcast(
      onListen: _startListening,
      onCancel: _stopListening,
    );
  }

  /// Current connectivity state (cached; does not hit platform channel).
  bool get lastKnownConnected => _lastKnown;

  /// Checks the actual platform connectivity status.
  Future<bool> get isConnected async {
    final results = await _connectivity.checkConnectivity();
    _lastKnown = _isOnline(results);
    return _lastKnown;
  }

  /// Stream that emits `true` when online, `false` when offline.
  Stream<bool> get onConnectivityChanged => _controller.stream;

  void _startListening() {
    _sub = _connectivity.onConnectivityChanged.listen((results) {
      final online = _isOnline(results);
      if (online != _lastKnown) {
        _lastKnown = online;
        _controller.add(online);
      }
    });
  }

  void _stopListening() {
    _sub?.cancel();
    _sub = null;
  }

  bool _isOnline(List<ConnectivityResult> results) {
    return results.any((r) =>
        r == ConnectivityResult.wifi ||
        r == ConnectivityResult.mobile ||
        r == ConnectivityResult.ethernet);
  }

  void dispose() {
    _stopListening();
    _controller.close();
  }
}
```

### Connectivity-Aware Widget

```dart
import 'package:flutter/material.dart';

import '../../core/connectivity/connectivity_service.dart';

class ConnectivityBanner extends StatelessWidget {
  final ConnectivityService connectivity;
  final Widget child;

  const ConnectivityBanner({
    super.key,
    required this.connectivity,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        StreamBuilder<bool>(
          stream: connectivity.onConnectivityChanged,
          initialData: connectivity.lastKnownConnected,
          builder: (context, snapshot) {
            final online = snapshot.data ?? true;
            if (online) return const SizedBox.shrink();

            return MaterialBanner(
              content: const Text('You are offline. Changes will sync later.'),
              backgroundColor: Theme.of(context).colorScheme.errorContainer,
              actions: [
                TextButton(
                  onPressed: () {}, // dismiss or retry
                  child: const Text('DISMISS'),
                ),
              ],
            );
          },
        ),
        Expanded(child: child),
      ],
    );
  }
}
```

---

## Optimistic UI Updates

Apply changes to the local store immediately, then sync to the server in the
background. Roll back only if the server rejects the change.

```dart
// lib/features/articles/article_controller.dart

import 'dart:async';

import '../../data/models/article.dart';
import '../../data/repositories/article_repository.dart';
import '../../data/sync/sync_queue.dart';
import '../../data/sync/sync_operation.dart';

class ArticleController {
  final ArticleRepository _repo;
  final SyncQueue _syncQueue;

  final _articlesController = StreamController<List<Article>>.broadcast();
  List<Article> _current = [];

  ArticleController({
    required ArticleRepository repo,
    required SyncQueue syncQueue,
  })  : _repo = repo,
        _syncQueue = syncQueue;

  Stream<List<Article>> get articles => _articlesController.stream;

  /// Optimistically marks an article as bookmarked.
  Future<void> toggleBookmark(Article article) async {
    final updated = article.copyWith(
      bookmarked: !article.bookmarked,
      updatedAt: DateTime.now(),
    );

    // 1. Update local store immediately.
    await _repo.updateLocal(updated);

    // 2. Reflect in the UI stream.
    _current = _current.map((a) => a.id == updated.id ? updated : a).toList();
    _articlesController.add(_current);

    // 3. Enqueue sync operation (will execute when online).
    await _syncQueue.enqueue(
      endpoint: '/articles/${updated.id}',
      method: SyncMethod.patch,
      payload: {
        'bookmarked': updated.bookmarked,
        'updatedAt': updated.updatedAt.toIso8601String(),
      },
    );
  }

  /// Optimistic delete with rollback.
  Future<void> deleteArticle(Article article) async {
    // 1. Remove from local store.
    await _repo.deleteLocal(article.id);
    _current = _current.where((a) => a.id != article.id).toList();
    _articlesController.add(_current);

    // 2. Attempt remote delete.
    try {
      await _syncQueue.enqueue(
        endpoint: '/articles/${article.id}',
        method: SyncMethod.delete,
      );
    } catch (_) {
      // 3. Rollback: re-insert locally.
      await _repo.updateLocal(article);
      _current = [..._current, article];
      _articlesController.add(_current);
    }
  }

  void dispose() {
    _articlesController.close();
  }
}
```

---

## Background Sync

### Using workmanager for Periodic Background Sync

```yaml
dependencies:
  workmanager: ^0.5.2
```

```dart
// lib/core/background/background_sync.dart

import 'package:workmanager/workmanager.dart';

import '../../data/sync/sync_queue.dart';
import '../../service_locator.dart';

const _syncTaskName = 'com.example.app.backgroundSync';

/// Register the background sync task. Call once during app initialization.
Future<void> registerBackgroundSync() async {
  await Workmanager().initialize(callbackDispatcher, isInDebugMode: false);

  await Workmanager().registerPeriodicTask(
    _syncTaskName,
    _syncTaskName,
    frequency: const Duration(minutes: 15),
    constraints: Constraints(
      networkType: NetworkType.connected,
      requiresBatteryNotLow: true,
    ),
    existingWorkPolicy: ExistingWorkPolicy.keep,
  );
}

/// Top-level callback -- must be a top-level or static function.
@pragma('vm:entry-point')
void callbackDispatcher() {
  Workmanager().executeTask((taskName, inputData) async {
    if (taskName == _syncTaskName) {
      try {
        // Initialize dependencies needed for sync.
        await initServiceLocator();
        final syncQueue = getIt<SyncQueue>();
        await syncQueue.processQueue();
        return true;
      } catch (_) {
        return false; // Workmanager will retry.
      }
    }
    return true;
  });
}
```

### Manual Pull-to-Refresh Sync

```dart
class ArticleListScreen extends StatelessWidget {
  final ArticleRepository repo;

  const ArticleListScreen({super.key, required this.repo});

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: () async {
        // Force a network-first fetch.
        await repo
            .getArticles(policy: const NetworkFirst())
            .first;
      },
      child: StreamBuilder<List<Article>>(
        stream: repo.getArticles(),
        builder: (context, snapshot) {
          final articles = snapshot.data ?? [];
          return ListView.builder(
            physics: const AlwaysScrollableScrollPhysics(),
            itemCount: articles.length,
            itemBuilder: (context, index) {
              return ListTile(title: Text(articles[index].title));
            },
          );
        },
      ),
    );
  }
}
```

---

## Cache Invalidation Strategies

### TTL-Based (Time-To-Live)

```dart
/// Wraps any cached value with an expiration check.
class TtlCache<K, V> {
  final Map<K, CacheEntry<V>> _store = {};

  void put(K key, V value, {Duration ttl = const Duration(minutes: 30)}) {
    _store[key] = CacheEntry(
      data: value,
      cachedAt: DateTime.now(),
      ttl: ttl,
    );
  }

  V? get(K key) {
    final entry = _store[key];
    if (entry == null || entry.isExpired) {
      _store.remove(key);
      return null;
    }
    return entry.data;
  }

  void invalidate(K key) => _store.remove(key);
  void invalidateAll() => _store.clear();

  /// Remove all expired entries.
  void evictStale() {
    _store.removeWhere((_, entry) => entry.isExpired);
  }
}
```

### Version-Based

Useful when the server provides an ETag or version number.

```dart
class VersionedCache<K, V> {
  final Map<K, ({V data, int version})> _store = {};

  void put(K key, V value, int version) {
    _store[key] = (data: value, version: version);
  }

  /// Returns the value if the cached version matches; null otherwise.
  V? getIfCurrent(K key, int currentVersion) {
    final entry = _store[key];
    if (entry == null || entry.version != currentVersion) {
      return null;
    }
    return entry.data;
  }

  void invalidate(K key) => _store.remove(key);
}
```

### Event-Based Invalidation

Invalidate cache entries in response to domain events (e.g., after a write).

```dart
// lib/data/cache/cache_invalidator.dart

import 'dart:async';

enum CacheRegion { articles, categories, userProfile }

class CacheInvalidator {
  final _controller = StreamController<Set<CacheRegion>>.broadcast();

  Stream<Set<CacheRegion>> get onInvalidate => _controller.stream;

  /// Called after a successful write to notify caches.
  void invalidate(Set<CacheRegion> regions) {
    _controller.add(regions);
  }

  void dispose() => _controller.close();
}

// Usage in a repository:
class ArticleRepository {
  final CacheInvalidator _invalidator;
  // ... other fields ...

  ArticleRepository({required CacheInvalidator invalidator, /* ... */})
      : _invalidator = invalidator {
    _invalidator.onInvalidate.listen((regions) {
      if (regions.contains(CacheRegion.articles)) {
        _localCache.invalidateAll();
      }
    });
  }

  Future<void> createArticle(Article article) async {
    await _remote.create(article);
    await _local.insert(article);
    _invalidator.invalidate({CacheRegion.articles});
  }
}
```

### LRU (Least Recently Used) Eviction

```dart
import 'dart:collection';

class LruCache<K, V> {
  final int maxSize;
  final LinkedHashMap<K, V> _store = LinkedHashMap();

  LruCache({this.maxSize = 100});

  V? get(K key) {
    final value = _store.remove(key);
    if (value != null) {
      // Move to end (most recently used).
      _store[key] = value;
    }
    return value;
  }

  void put(K key, V value) {
    _store.remove(key); // Remove old position if exists.
    _store[key] = value;
    while (_store.length > maxSize) {
      _store.remove(_store.keys.first); // Evict least recently used.
    }
  }

  void invalidate(K key) => _store.remove(key);
  void clear() => _store.clear();
  int get length => _store.length;
}
```
