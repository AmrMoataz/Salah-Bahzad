# Cloud Firestore Reference Guide

## Overview

Cloud Firestore is a flexible, scalable NoSQL cloud database that supports
real-time data synchronization and offline persistence. This reference covers
the full spectrum of Firestore operations in Flutter -- CRUD, real-time
listeners, complex queries, pagination, batch writes, transactions,
subcollections, offline persistence, data modeling patterns, and security rules.

---

## Table of Contents

1. [Setup](#setup)
2. [Collection and Document References](#collection-and-document-references)
3. [CRUD Operations](#crud-operations)
4. [Real-Time Listeners](#real-time-listeners)
5. [Queries](#queries)
6. [Compound Queries and Indexes](#compound-queries-and-indexes)
7. [Batch Writes and Transactions](#batch-writes-and-transactions)
8. [Subcollections](#subcollections)
9. [Pagination with Cursors](#pagination-with-cursors)
10. [Offline Persistence](#offline-persistence)
11. [Data Modeling Patterns](#data-modeling-patterns)
12. [Security Rules Patterns](#security-rules-patterns)

---

## Setup

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_core: ^3.8.0
  cloud_firestore: ^5.5.0
```

### Initialization

Firebase must be initialized before any Firestore calls (see auth.md for the
full `main.dart` setup).

```dart
import 'package:cloud_firestore/cloud_firestore.dart';

final FirebaseFirestore db = FirebaseFirestore.instance;
```

---

## Collection and Document References

```dart
// Collection reference
final usersRef = db.collection('users');

// Document reference (existing document)
final userDocRef = db.collection('users').doc('user123');

// Document reference (auto-generated ID)
final newUserRef = db.collection('users').doc();
// newUserRef.id contains the auto-generated ID before writing.

// Subcollection reference
final postsRef = db.collection('users').doc('user123').collection('posts');

// Typed converter (recommended)
final typedUsersRef = db.collection('users').withConverter<UserModel>(
  fromFirestore: (snapshot, _) => UserModel.fromFirestore(snapshot),
  toFirestore: (user, _) => user.toFirestore(),
);
```

### Type-safe model with converter

```dart
// lib/models/user_model.dart
import 'package:cloud_firestore/cloud_firestore.dart';

class UserModel {
  const UserModel({
    required this.id,
    required this.email,
    required this.displayName,
    required this.createdAt,
    this.photoUrl,
    this.role = 'user',
  });

  factory UserModel.fromFirestore(
    DocumentSnapshot<Map<String, dynamic>> snapshot,
  ) {
    final data = snapshot.data()!;
    return UserModel(
      id: snapshot.id,
      email: data['email'] as String,
      displayName: data['displayName'] as String,
      createdAt: (data['createdAt'] as Timestamp).toDate(),
      photoUrl: data['photoUrl'] as String?,
      role: data['role'] as String? ?? 'user',
    );
  }

  final String id;
  final String email;
  final String displayName;
  final DateTime createdAt;
  final String? photoUrl;
  final String role;

  Map<String, dynamic> toFirestore() {
    return {
      'email': email,
      'displayName': displayName,
      'createdAt': Timestamp.fromDate(createdAt),
      'photoUrl': photoUrl,
      'role': role,
    };
  }

  UserModel copyWith({
    String? email,
    String? displayName,
    DateTime? createdAt,
    String? photoUrl,
    String? role,
  }) {
    return UserModel(
      id: id,
      email: email ?? this.email,
      displayName: displayName ?? this.displayName,
      createdAt: createdAt ?? this.createdAt,
      photoUrl: photoUrl ?? this.photoUrl,
      role: role ?? this.role,
    );
  }
}
```

---

## CRUD Operations

### Create (set)

```dart
// Set with a specific ID (overwrites the entire document).
Future<void> createUser(UserModel user) async {
  await db.collection('users').doc(user.id).set(user.toFirestore());
}

// Set with merge (only updates provided fields, creates if missing).
Future<void> upsertUser(String userId, Map<String, dynamic> data) async {
  await db.collection('users').doc(userId).set(data, SetOptions(merge: true));
}

// Add with auto-generated ID.
Future<String> addPost(Map<String, dynamic> postData) async {
  final docRef = await db.collection('posts').add(postData);
  return docRef.id;
}
```

### Read (get)

```dart
// Get a single document.
Future<UserModel?> getUser(String userId) async {
  final snapshot = await db.collection('users').doc(userId).get();
  if (!snapshot.exists) return null;
  return UserModel.fromFirestore(snapshot);
}

// Get all documents in a collection.
Future<List<UserModel>> getAllUsers() async {
  final snapshot = await db.collection('users').get();
  return snapshot.docs.map(UserModel.fromFirestore).toList();
}

// Using typed converter.
Future<UserModel?> getUserTyped(String userId) async {
  final typedRef = db.collection('users').withConverter<UserModel>(
    fromFirestore: (snapshot, _) => UserModel.fromFirestore(snapshot),
    toFirestore: (user, _) => user.toFirestore(),
  );
  final snapshot = await typedRef.doc(userId).get();
  return snapshot.data();
}
```

### Update

```dart
// Update specific fields (document must exist).
Future<void> updateUserName(String userId, String newName) async {
  await db.collection('users').doc(userId).update({
    'displayName': newName,
    'updatedAt': FieldValue.serverTimestamp(),
  });
}

// Increment a numeric field atomically.
Future<void> incrementLikeCount(String postId) async {
  await db.collection('posts').doc(postId).update({
    'likeCount': FieldValue.increment(1),
  });
}

// Add an element to an array field.
Future<void> addTag(String postId, String tag) async {
  await db.collection('posts').doc(postId).update({
    'tags': FieldValue.arrayUnion([tag]),
  });
}

// Remove an element from an array field.
Future<void> removeTag(String postId, String tag) async {
  await db.collection('posts').doc(postId).update({
    'tags': FieldValue.arrayRemove([tag]),
  });
}

// Delete a field.
Future<void> removePhotoUrl(String userId) async {
  await db.collection('users').doc(userId).update({
    'photoUrl': FieldValue.delete(),
  });
}
```

### Delete

```dart
// Delete a document.
Future<void> deletePost(String postId) async {
  await db.collection('posts').doc(postId).delete();
}

// Delete a document and all its subcollections (requires Cloud Function or
// recursive client logic -- Firestore does not cascade deletes automatically).
```

---

## Real-Time Listeners

### Listen to a single document

```dart
StreamSubscription<DocumentSnapshot<Map<String, dynamic>>> listenToUser(
  String userId,
  void Function(UserModel?) onData,
) {
  return db.collection('users').doc(userId).snapshots().listen((snapshot) {
    if (snapshot.exists) {
      onData(UserModel.fromFirestore(snapshot));
    } else {
      onData(null);
    }
  });
}
```

### Listen to a collection

```dart
Stream<List<UserModel>> watchAllUsers() {
  return db.collection('users').snapshots().map(
        (snapshot) => snapshot.docs.map(UserModel.fromFirestore).toList(),
      );
}
```

### Listen with metadata (hasPendingWrites)

```dart
Stream<List<UserModel>> watchUsersWithMeta() {
  return db
      .collection('users')
      .snapshots(includeMetadataChanges: true)
      .map((snapshot) {
    for (final change in snapshot.docChanges) {
      if (change.doc.metadata.hasPendingWrites) {
        // This document has local changes not yet confirmed by the server.
      }
    }
    return snapshot.docs.map(UserModel.fromFirestore).toList();
  });
}
```

### Listen to document changes (diff)

```dart
void listenToPostChanges() {
  db.collection('posts').snapshots().listen((snapshot) {
    for (final change in snapshot.docChanges) {
      switch (change.type) {
        case DocumentChangeType.added:
          // Handle new document.
          break;
        case DocumentChangeType.modified:
          // Handle updated document.
          break;
        case DocumentChangeType.removed:
          // Handle deleted document.
          break;
      }
    }
  });
}
```

---

## Queries

### Basic where clauses

```dart
// Equality
Future<List<UserModel>> getAdmins() async {
  final snapshot = await db
      .collection('users')
      .where('role', isEqualTo: 'admin')
      .get();
  return snapshot.docs.map(UserModel.fromFirestore).toList();
}

// Inequality
Future<List<Map<String, dynamic>>> getExpensiveProducts(double minPrice) async {
  final snapshot = await db
      .collection('products')
      .where('price', isGreaterThanOrEqualTo: minPrice)
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}

// Array contains
Future<List<Map<String, dynamic>>> getPostsByTag(String tag) async {
  final snapshot = await db
      .collection('posts')
      .where('tags', arrayContains: tag)
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}

// Array contains any (up to 30 values)
Future<List<Map<String, dynamic>>> getPostsByAnyTag(List<String> tags) async {
  final snapshot = await db
      .collection('posts')
      .where('tags', arrayContainsAny: tags)
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}

// In (up to 30 values)
Future<List<UserModel>> getUsersByIds(List<String> userIds) async {
  final snapshot = await db
      .collection('users')
      .where(FieldPath.documentId, whereIn: userIds)
      .get();
  return snapshot.docs.map(UserModel.fromFirestore).toList();
}

// Not in
Future<List<Map<String, dynamic>>> getActiveStatuses() async {
  final snapshot = await db
      .collection('orders')
      .where('status', whereNotIn: ['cancelled', 'refunded'])
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}
```

### orderBy and limit

```dart
Future<List<Map<String, dynamic>>> getRecentPosts({int limit = 20}) async {
  final snapshot = await db
      .collection('posts')
      .orderBy('createdAt', descending: true)
      .limit(limit)
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}
```

---

## Compound Queries and Indexes

### Compound queries

```dart
// Multiple where clauses on the same field.
Future<List<Map<String, dynamic>>> getProductsInPriceRange({
  required double min,
  required double max,
}) async {
  final snapshot = await db
      .collection('products')
      .where('price', isGreaterThanOrEqualTo: min)
      .where('price', isLessThanOrEqualTo: max)
      .orderBy('price')
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}

// Where clause on one field + orderBy on a different field (requires composite index).
Future<List<Map<String, dynamic>>> getActivePostsSorted() async {
  final snapshot = await db
      .collection('posts')
      .where('isPublished', isEqualTo: true)
      .orderBy('createdAt', descending: true)
      .limit(50)
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}
```

### Creating indexes

Firestore creates single-field indexes automatically. Composite indexes must be
created explicitly. When a query requires a missing index, Firestore throws an
error with a direct link to create the index in the Firebase Console.

You can also define indexes in `firestore.indexes.json`:

```json
{
  "indexes": [
    {
      "collectionGroup": "posts",
      "queryScope": "COLLECTION",
      "fields": [
        { "fieldPath": "isPublished", "order": "ASCENDING" },
        { "fieldPath": "createdAt", "order": "DESCENDING" }
      ]
    },
    {
      "collectionGroup": "products",
      "queryScope": "COLLECTION",
      "fields": [
        { "fieldPath": "category", "order": "ASCENDING" },
        { "fieldPath": "price", "order": "ASCENDING" }
      ]
    }
  ]
}
```

Deploy indexes:

```bash
firebase deploy --only firestore:indexes
```

---

## Batch Writes and Transactions

### Batch writes

Batches are atomic -- all operations succeed or all fail. Maximum 500 operations
per batch.

```dart
Future<void> batchCreateUsers(List<UserModel> users) async {
  final batch = db.batch();

  for (final user in users) {
    final docRef = db.collection('users').doc(user.id);
    batch.set(docRef, user.toFirestore());
  }

  await batch.commit();
}

Future<void> batchUpdatePostStatuses(
  List<String> postIds,
  String newStatus,
) async {
  final batch = db.batch();

  for (final postId in postIds) {
    final docRef = db.collection('posts').doc(postId);
    batch.update(docRef, {
      'status': newStatus,
      'updatedAt': FieldValue.serverTimestamp(),
    });
  }

  await batch.commit();
}

Future<void> batchDeletePosts(List<String> postIds) async {
  // Split into chunks of 500 for large lists.
  const batchSize = 500;
  for (var i = 0; i < postIds.length; i += batchSize) {
    final chunk = postIds.sublist(
      i,
      i + batchSize > postIds.length ? postIds.length : i + batchSize,
    );
    final batch = db.batch();
    for (final id in chunk) {
      batch.delete(db.collection('posts').doc(id));
    }
    await batch.commit();
  }
}
```

### Transactions

Transactions read then write atomically. All reads must come before writes.

```dart
// Transfer credits between two users atomically.
Future<void> transferCredits({
  required String fromUserId,
  required String toUserId,
  required int amount,
}) async {
  await db.runTransaction((transaction) async {
    final fromRef = db.collection('users').doc(fromUserId);
    final toRef = db.collection('users').doc(toUserId);

    // All reads first.
    final fromSnapshot = await transaction.get(fromRef);
    final toSnapshot = await transaction.get(toRef);

    if (!fromSnapshot.exists || !toSnapshot.exists) {
      throw Exception('One or both users do not exist');
    }

    final fromCredits = fromSnapshot.data()!['credits'] as int;
    if (fromCredits < amount) {
      throw Exception('Insufficient credits');
    }

    final toCredits = toSnapshot.data()!['credits'] as int;

    // Then all writes.
    transaction.update(fromRef, {'credits': fromCredits - amount});
    transaction.update(toRef, {'credits': toCredits + amount});
  });
}

// Idempotent like toggle (read current state, then toggle).
Future<void> toggleLike({
  required String postId,
  required String userId,
}) async {
  final likeRef = db
      .collection('posts')
      .doc(postId)
      .collection('likes')
      .doc(userId);
  final postRef = db.collection('posts').doc(postId);

  await db.runTransaction((transaction) async {
    final likeSnapshot = await transaction.get(likeRef);

    if (likeSnapshot.exists) {
      transaction.delete(likeRef);
      transaction.update(postRef, {
        'likeCount': FieldValue.increment(-1),
      });
    } else {
      transaction.set(likeRef, {
        'userId': userId,
        'createdAt': FieldValue.serverTimestamp(),
      });
      transaction.update(postRef, {
        'likeCount': FieldValue.increment(1),
      });
    }
  });
}
```

---

## Subcollections

### Defining subcollections

```dart
// users/{userId}/posts/{postId}
final userPostsRef = db
    .collection('users')
    .doc('user123')
    .collection('posts');

// Add a document to a subcollection.
Future<void> addUserPost(String userId, Map<String, dynamic> postData) async {
  await db
      .collection('users')
      .doc(userId)
      .collection('posts')
      .add(postData);
}

// Query across all subcollections with the same name (collection group query).
Future<List<Map<String, dynamic>>> getAllPostsAcrossUsers() async {
  final snapshot = await db.collectionGroup('posts').get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}
```

### Collection group queries

Collection group queries search all collections with the same name, regardless
of parent. Requires a collection group index.

```dart
// Find all posts tagged "flutter" across all users.
Future<List<Map<String, dynamic>>> searchPostsByTag(String tag) async {
  final snapshot = await db
      .collectionGroup('posts')
      .where('tags', arrayContains: tag)
      .orderBy('createdAt', descending: true)
      .limit(50)
      .get();
  return snapshot.docs.map((doc) => {'id': doc.id, ...doc.data()}).toList();
}
```

Collection group index in `firestore.indexes.json`:

```json
{
  "indexes": [
    {
      "collectionGroup": "posts",
      "queryScope": "COLLECTION_GROUP",
      "fields": [
        { "fieldPath": "tags", "arrayConfig": "CONTAINS" },
        { "fieldPath": "createdAt", "order": "DESCENDING" }
      ]
    }
  ]
}
```

---

## Pagination with Cursors

### Forward pagination with startAfterDocument

```dart
// lib/services/firestore_pagination_service.dart
import 'package:cloud_firestore/cloud_firestore.dart';

class PaginatedResult<T> {
  const PaginatedResult({
    required this.items,
    required this.lastDocument,
    required this.hasMore,
  });

  final List<T> items;
  final DocumentSnapshot? lastDocument;
  final bool hasMore;
}

class FirestorePaginationService {
  FirestorePaginationService({FirebaseFirestore? firestore})
      : _db = firestore ?? FirebaseFirestore.instance;

  final FirebaseFirestore _db;

  Future<PaginatedResult<Map<String, dynamic>>> getPosts({
    required int pageSize,
    DocumentSnapshot? startAfter,
  }) async {
    Query<Map<String, dynamic>> query = _db
        .collection('posts')
        .orderBy('createdAt', descending: true)
        .limit(pageSize);

    if (startAfter != null) {
      query = query.startAfterDocument(startAfter);
    }

    final snapshot = await query.get();
    final items = snapshot.docs
        .map((doc) => {'id': doc.id, ...doc.data()})
        .toList();

    return PaginatedResult(
      items: items,
      lastDocument: snapshot.docs.isNotEmpty ? snapshot.docs.last : null,
      hasMore: snapshot.docs.length == pageSize,
    );
  }
}
```

### Riverpod pagination notifier

```dart
// lib/providers/paginated_posts_provider.dart
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'paginated_posts_provider.g.dart';

typedef PostEntry = Map<String, dynamic>;

@riverpod
class PaginatedPosts extends _$PaginatedPosts {
  static const _pageSize = 20;
  DocumentSnapshot? _lastDocument;
  bool _hasMore = true;

  @override
  Future<List<PostEntry>> build() async {
    _lastDocument = null;
    _hasMore = true;
    return _fetchPage();
  }

  Future<List<PostEntry>> _fetchPage() async {
    final db = FirebaseFirestore.instance;
    Query<Map<String, dynamic>> query = db
        .collection('posts')
        .orderBy('createdAt', descending: true)
        .limit(_pageSize);

    if (_lastDocument != null) {
      query = query.startAfterDocument(_lastDocument!);
    }

    final snapshot = await query.get();
    if (snapshot.docs.isNotEmpty) {
      _lastDocument = snapshot.docs.last;
    }
    _hasMore = snapshot.docs.length == _pageSize;

    return snapshot.docs
        .map((doc) => <String, dynamic>{'id': doc.id, ...doc.data()})
        .toList();
  }

  Future<void> loadNextPage() async {
    if (!_hasMore) return;
    final currentItems = state.valueOrNull ?? [];
    final nextItems = await _fetchPage();
    state = AsyncData([...currentItems, ...nextItems]);
  }

  bool get hasMore => _hasMore;
}
```

### Infinite scroll widget

```dart
// lib/screens/posts_list_screen.dart
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/paginated_posts_provider.dart';

class PostsListScreen extends ConsumerWidget {
  const PostsListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final postsAsync = ref.watch(paginatedPostsProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Posts')),
      body: postsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, stack) => Center(child: Text('Error: $error')),
        data: (posts) => NotificationListener<ScrollNotification>(
          onNotification: (notification) {
            if (notification is ScrollEndNotification &&
                notification.metrics.extentAfter < 200) {
              final notifier = ref.read(paginatedPostsProvider.notifier);
              if (notifier.hasMore) {
                notifier.loadNextPage();
              }
            }
            return false;
          },
          child: ListView.builder(
            itemCount: posts.length,
            itemBuilder: (context, index) {
              final post = posts[index];
              return ListTile(
                title: Text(post['title'] as String? ?? 'Untitled'),
                subtitle: Text(post['excerpt'] as String? ?? ''),
              );
            },
          ),
        ),
      ),
    );
  }
}
```

---

## Offline Persistence

### Default behavior

Firestore enables offline persistence by default on iOS and Android. On web,
persistence must be enabled explicitly.

```dart
// Enable persistence on web (call before any Firestore reads/writes).
void configureFirestoreWeb() {
  FirebaseFirestore.instance.settings = const Settings(
    persistenceEnabled: true,
    cacheSizeBytes: Settings.CACHE_SIZE_UNLIMITED,
  );
}
```

### Configuring cache size

```dart
void configureFirestoreCache() {
  FirebaseFirestore.instance.settings = const Settings(
    // Default is 40 MB. Use CACHE_SIZE_UNLIMITED for offline-heavy apps.
    cacheSizeBytes: 100 * 1024 * 1024, // 100 MB
  );
}
```

### Detecting online/offline state with metadata

```dart
Stream<({List<Map<String, dynamic>> items, bool isFromCache})>
    watchPostsWithCacheInfo() {
  return FirebaseFirestore.instance
      .collection('posts')
      .orderBy('createdAt', descending: true)
      .limit(50)
      .snapshots(includeMetadataChanges: true)
      .map((snapshot) {
    final items = snapshot.docs
        .map((doc) => <String, dynamic>{'id': doc.id, ...doc.data()})
        .toList();
    return (items: items, isFromCache: snapshot.metadata.isFromCache);
  });
}
```

### Writing offline

Writes are queued locally and synced automatically when the device comes back
online. Use `FieldValue.serverTimestamp()` so the timestamp is set correctly
upon sync:

```dart
Future<void> addPostOfflineSafe(Map<String, dynamic> data) async {
  await FirebaseFirestore.instance.collection('posts').add({
    ...data,
    'createdAt': FieldValue.serverTimestamp(),
  });
  // Returns immediately even if offline -- the write is queued.
}
```

### Disabling network (force offline mode)

```dart
// Useful for testing offline behavior.
Future<void> goOffline() async {
  await FirebaseFirestore.instance.disableNetwork();
}

Future<void> goOnline() async {
  await FirebaseFirestore.instance.enableNetwork();
}
```

---

## Data Modeling Patterns

### Embedding vs. referencing

| Pattern | Use When | Trade-off |
|---------|----------|-----------|
| **Embedding** (nested map) | Data is always read together, rarely updated independently | Fast reads, but updates require rewriting the parent |
| **Referencing** (ID field) | Data is shared across documents or updated independently | Requires extra reads (joins), but updates are isolated |
| **Subcollection** | Child data can grow unboundedly (e.g., comments on a post) | Scales well, supports independent queries |

### Embedded example (address inside user)

```dart
// Firestore document structure:
// users/user123 -> { name: "Alice", address: { street: "123 Main", city: "NYC" } }

class Address {
  const Address({required this.street, required this.city, required this.zip});

  factory Address.fromMap(Map<String, dynamic> map) {
    return Address(
      street: map['street'] as String,
      city: map['city'] as String,
      zip: map['zip'] as String,
    );
  }

  final String street;
  final String city;
  final String zip;

  Map<String, dynamic> toMap() => {
        'street': street,
        'city': city,
        'zip': zip,
      };
}

class UserWithAddress {
  const UserWithAddress({
    required this.id,
    required this.name,
    required this.address,
  });

  factory UserWithAddress.fromFirestore(
    DocumentSnapshot<Map<String, dynamic>> doc,
  ) {
    final data = doc.data()!;
    return UserWithAddress(
      id: doc.id,
      name: data['name'] as String,
      address: Address.fromMap(data['address'] as Map<String, dynamic>),
    );
  }

  final String id;
  final String name;
  final Address address;

  Map<String, dynamic> toFirestore() => {
        'name': name,
        'address': address.toMap(),
      };
}
```

### Referenced example (author ID on a post)

```dart
// posts/post123 -> { title: "Hello", authorId: "user123", ... }
// Read the author in a separate call.

Future<({Map<String, dynamic> post, UserModel? author})> getPostWithAuthor(
  String postId,
) async {
  final db = FirebaseFirestore.instance;
  final postSnapshot = await db.collection('posts').doc(postId).get();
  final postData = postSnapshot.data();
  if (postData == null) {
    throw Exception('Post not found');
  }

  final authorId = postData['authorId'] as String;
  final authorSnapshot = await db.collection('users').doc(authorId).get();

  return (
    post: {'id': postSnapshot.id, ...postData},
    author: authorSnapshot.exists
        ? UserModel.fromFirestore(authorSnapshot)
        : null,
  );
}
```

### Denormalization (duplicating data for read performance)

```dart
// Store author name directly on each post to avoid extra reads.
// Trade-off: must update all posts when the user changes their name.

Future<void> createPostDenormalized({
  required String authorId,
  required String authorName,
  required String title,
  required String body,
}) async {
  await FirebaseFirestore.instance.collection('posts').add({
    'authorId': authorId,
    'authorName': authorName, // Denormalized for fast reads.
    'title': title,
    'body': body,
    'createdAt': FieldValue.serverTimestamp(),
  });
}

// Update denormalized author name across all their posts.
Future<void> propagateNameChange(String userId, String newName) async {
  final db = FirebaseFirestore.instance;
  final posts = await db
      .collection('posts')
      .where('authorId', isEqualTo: userId)
      .get();

  final batch = db.batch();
  for (final doc in posts.docs) {
    batch.update(doc.reference, {'authorName': newName});
  }
  batch.update(db.collection('users').doc(userId), {'displayName': newName});
  await batch.commit();
}
```

---

## Security Rules Patterns

### Owner-only access

```
match /users/{userId} {
  allow read, write: if request.auth != null && request.auth.uid == userId;
}
```

### Validated writes

```
match /posts/{postId} {
  allow create: if request.auth != null
                && request.resource.data.title is string
                && request.resource.data.title.size() > 0
                && request.resource.data.title.size() <= 200
                && request.resource.data.authorId == request.auth.uid
                && request.resource.data.createdAt == request.time;

  allow update: if request.auth != null
                && resource.data.authorId == request.auth.uid
                && request.resource.data.authorId == resource.data.authorId
                // Prevent changing the author.
                && request.resource.data.createdAt == resource.data.createdAt;
                // Prevent backdating.

  allow delete: if request.auth != null
                && resource.data.authorId == request.auth.uid;
}
```

### Role-based access with custom claims

```
match /admin/{document=**} {
  allow read, write: if request.auth != null
                     && request.auth.token.admin == true;
}

match /reports/{reportId} {
  allow read: if request.auth != null
              && (request.auth.token.admin == true
                  || request.auth.token.moderator == true);
  allow write: if request.auth != null
               && request.auth.token.admin == true;
}
```

### Rate limiting (approximate)

```
match /comments/{commentId} {
  allow create: if request.auth != null
                && request.resource.data.createdAt == request.time
                && !exists(/databases/$(database)/documents/rateLimits/$(request.auth.uid));
}
```

### Collection group rules

```
match /{path=**}/likes/{likeId} {
  allow read: if request.auth != null;
  allow create: if request.auth != null && likeId == request.auth.uid;
  allow delete: if request.auth != null && likeId == request.auth.uid;
}
```
