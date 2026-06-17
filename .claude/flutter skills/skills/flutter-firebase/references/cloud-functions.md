# Cloud Functions and Remote Services Reference Guide

## Overview

This reference covers calling Cloud Functions from Flutter, writing Firestore
triggers, Firebase Storage operations (upload, download, delete with progress),
Remote Config for feature flags, and Firebase Analytics for custom events.

---

## Table of Contents

1. [Calling Callable Functions from Flutter](#calling-callable-functions-from-flutter)
2. [HTTP Triggers](#http-triggers)
3. [Firestore Triggers](#firestore-triggers)
4. [Firebase Storage](#firebase-storage)
5. [Remote Config](#remote-config)
6. [Analytics](#analytics)

---

## Calling Callable Functions from Flutter

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  cloud_functions: ^5.1.0
```

### Basic callable function invocation

```dart
// lib/services/cloud_functions_service.dart
import 'package:cloud_functions/cloud_functions.dart';

class CloudFunctionsService {
  CloudFunctionsService({FirebaseFunctions? functions})
      : _functions = functions ?? FirebaseFunctions.instance;

  final FirebaseFunctions _functions;

  /// Calls a callable function and returns the decoded response.
  Future<Map<String, dynamic>> callFunction({
    required String name,
    Map<String, dynamic>? parameters,
  }) async {
    final callable = _functions.httpsCallable(
      name,
      options: HttpsCallableOptions(
        timeout: const Duration(seconds: 30),
      ),
    );

    final result = await callable.call<Map<String, dynamic>>(parameters);
    return result.data;
  }
}
```

### Typed callable example: generate invite code

```dart
// Cloud Function (TypeScript)
// functions/src/index.ts
import { onCall, HttpsError } from 'firebase-functions/v2/https';
import { getFirestore } from 'firebase-admin/firestore';
import { initializeApp } from 'firebase-admin/app';
import { randomBytes } from 'crypto';

initializeApp();

export const generateInviteCode = onCall(async (request) => {
  if (!request.auth) {
    throw new HttpsError('unauthenticated', 'Must be signed in.');
  }

  const code = randomBytes(6).toString('hex').toUpperCase();
  const db = getFirestore();

  await db.collection('inviteCodes').doc(code).set({
    createdBy: request.auth.uid,
    createdAt: new Date(),
    used: false,
  });

  return { code };
});
```

```dart
// Flutter client
Future<String> generateInviteCode() async {
  final service = CloudFunctionsService();
  final result = await service.callFunction(name: 'generateInviteCode');
  return result['code'] as String;
}
```

### Callable function with region

```dart
// If your function is deployed to a non-default region:
final functions = FirebaseFunctions.instanceFor(region: 'europe-west1');
final callable = functions.httpsCallable('myFunction');
final result = await callable.call<Map<String, dynamic>>({'key': 'value'});
```

### Error handling for callable functions

```dart
Future<Map<String, dynamic>> safeCallFunction({
  required String name,
  Map<String, dynamic>? parameters,
}) async {
  try {
    final callable = FirebaseFunctions.instance.httpsCallable(name);
    final result = await callable.call<Map<String, dynamic>>(parameters);
    return result.data;
  } on FirebaseFunctionsException catch (e) {
    switch (e.code) {
      case 'unauthenticated':
        throw AuthException('You must be signed in.');
      case 'permission-denied':
        throw AuthException('You do not have permission.');
      case 'not-found':
        throw NotFoundException('The requested resource was not found.');
      case 'already-exists':
        throw ConflictException('The resource already exists.');
      default:
        throw AppException('Function error: ${e.message}');
    }
  }
}

// Application-specific exception types.
class AuthException implements Exception {
  AuthException(this.message);
  final String message;
}

class NotFoundException implements Exception {
  NotFoundException(this.message);
  final String message;
}

class ConflictException implements Exception {
  ConflictException(this.message);
  final String message;
}

class AppException implements Exception {
  AppException(this.message);
  final String message;
}
```

---

## HTTP Triggers

HTTP triggers expose a standard REST endpoint. Call them with any HTTP client
(e.g., `http` or `dio`).

### Cloud Function (TypeScript)

```typescript
// functions/src/index.ts
import { onRequest } from 'firebase-functions/v2/https';
import { getFirestore } from 'firebase-admin/firestore';

export const getPublicPosts = onRequest(async (req, res) => {
  if (req.method !== 'GET') {
    res.status(405).send('Method not allowed');
    return;
  }

  const db = getFirestore();
  const snapshot = await db
    .collection('posts')
    .where('isPublished', '==', true)
    .orderBy('createdAt', 'desc')
    .limit(20)
    .get();

  const posts = snapshot.docs.map((doc) => ({
    id: doc.id,
    ...doc.data(),
  }));

  res.json({ posts });
});
```

### Calling from Flutter

```dart
import 'dart:convert';

import 'package:http/http.dart' as http;

Future<List<Map<String, dynamic>>> fetchPublicPosts() async {
  final uri = Uri.parse(
    'https://us-central1-YOUR_PROJECT.cloudfunctions.net/getPublicPosts',
  );

  final response = await http.get(uri);
  if (response.statusCode != 200) {
    throw Exception('Failed to fetch posts: ${response.statusCode}');
  }

  final body = jsonDecode(response.body) as Map<String, dynamic>;
  final posts = (body['posts'] as List<dynamic>)
      .cast<Map<String, dynamic>>();
  return posts;
}
```

---

## Firestore Triggers

Firestore triggers run server-side Cloud Functions in response to document
changes. They do not require any Flutter client code -- they react
automatically.

### onCreate -- new document

```typescript
// functions/src/index.ts
import { onDocumentCreated } from 'firebase-functions/v2/firestore';
import { getFirestore } from 'firebase-admin/firestore';

export const onUserCreated = onDocumentCreated(
  'users/{userId}',
  async (event) => {
    const snapshot = event.data;
    if (!snapshot) return;

    const userData = snapshot.data();
    const userId = event.params.userId;

    // Create a welcome notification document.
    const db = getFirestore();
    await db.collection('notifications').add({
      userId,
      title: 'Welcome!',
      body: `Hello, ${userData.displayName ?? 'there'}! Welcome to the app.`,
      read: false,
      createdAt: new Date(),
    });

    // Initialize user statistics.
    await db.doc(`userStats/${userId}`).set({
      postCount: 0,
      followerCount: 0,
      followingCount: 0,
      createdAt: new Date(),
    });
  },
);
```

### onUpdate -- modified document

```typescript
import { onDocumentUpdated } from 'firebase-functions/v2/firestore';
import { getFirestore } from 'firebase-admin/firestore';

export const onPostUpdated = onDocumentUpdated(
  'posts/{postId}',
  async (event) => {
    const before = event.data?.before.data();
    const after = event.data?.after.data();
    if (!before || !after) return;

    // Detect publish status change.
    if (!before.isPublished && after.isPublished) {
      const db = getFirestore();
      // Notify followers of the author.
      const followers = await db
        .collection('followers')
        .where('followingId', '==', after.authorId)
        .get();

      const batch = db.batch();
      for (const followerDoc of followers.docs) {
        const notifRef = db.collection('notifications').doc();
        batch.set(notifRef, {
          userId: followerDoc.data().followerId,
          title: 'New Post',
          body: `${after.authorName} published "${after.title}"`,
          postId: event.params.postId,
          read: false,
          createdAt: new Date(),
        });
      }
      await batch.commit();
    }
  },
);
```

### onDelete -- removed document

```typescript
import { onDocumentDeleted } from 'firebase-functions/v2/firestore';
import { getFirestore } from 'firebase-admin/firestore';
import { getStorage } from 'firebase-admin/storage';

export const onPostDeleted = onDocumentDeleted(
  'posts/{postId}',
  async (event) => {
    const postData = event.data?.data();
    if (!postData) return;

    const db = getFirestore();
    const postId = event.params.postId;

    // Delete all comments subcollection.
    const comments = await db
      .collection(`posts/${postId}/comments`)
      .listDocuments();
    const batch = db.batch();
    for (const commentRef of comments) {
      batch.delete(commentRef);
    }
    await batch.commit();

    // Delete associated images from Storage.
    if (postData.imageUrls && postData.imageUrls.length > 0) {
      const bucket = getStorage().bucket();
      for (const url of postData.imageUrls) {
        const filePath = decodeURIComponent(
          url.split('/o/')[1].split('?')[0],
        );
        await bucket.file(filePath).delete().catch(() => {
          // File may already be deleted.
        });
      }
    }

    // Decrement author post count.
    await db.doc(`userStats/${postData.authorId}`).update({
      postCount: FieldValue.increment(-1),
    });
  },
);
```

---

## Firebase Storage

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_storage: ^12.3.0
  image_picker: ^1.1.0  # For selecting files on mobile
```

### Upload a file with progress

```dart
// lib/services/storage_service.dart
import 'dart:io';

import 'package:firebase_storage/firebase_storage.dart';

class StorageService {
  StorageService({FirebaseStorage? storage})
      : _storage = storage ?? FirebaseStorage.instance;

  final FirebaseStorage _storage;

  /// Uploads a file and returns the download URL.
  /// [onProgress] receives a value between 0.0 and 1.0.
  Future<String> uploadFile({
    required File file,
    required String storagePath,
    void Function(double progress)? onProgress,
    Map<String, String>? metadata,
  }) async {
    final ref = _storage.ref(storagePath);

    final settableMetadata = metadata != null
        ? SettableMetadata(customMetadata: metadata)
        : null;

    final uploadTask = ref.putFile(file, settableMetadata);

    if (onProgress != null) {
      uploadTask.snapshotEvents.listen((event) {
        final progress = event.bytesTransferred / event.totalBytes;
        onProgress(progress);
      });
    }

    final snapshot = await uploadTask;
    return snapshot.ref.getDownloadURL();
  }

  /// Uploads raw bytes (e.g., from web or memory).
  Future<String> uploadBytes({
    required List<int> data,
    required String storagePath,
    required String contentType,
    void Function(double progress)? onProgress,
  }) async {
    final ref = _storage.ref(storagePath);
    final metadata = SettableMetadata(contentType: contentType);

    final uploadTask = ref.putData(
      Uint8List.fromList(data),
      metadata,
    );

    if (onProgress != null) {
      uploadTask.snapshotEvents.listen((event) {
        final progress = event.bytesTransferred / event.totalBytes;
        onProgress(progress);
      });
    }

    final snapshot = await uploadTask;
    return snapshot.ref.getDownloadURL();
  }
}
```

### Download a file

```dart
Future<File> downloadFile({
  required String storagePath,
  required String localPath,
}) async {
  final ref = FirebaseStorage.instance.ref(storagePath);
  final file = File(localPath);
  await ref.writeToFile(file);
  return file;
}

// Get download URL without downloading the file.
Future<String> getDownloadUrl(String storagePath) async {
  return FirebaseStorage.instance.ref(storagePath).getDownloadURL();
}
```

### Delete a file

```dart
Future<void> deleteFile(String storagePath) async {
  await FirebaseStorage.instance.ref(storagePath).delete();
}

// Delete all files under a path prefix.
Future<void> deleteFolder(String folderPath) async {
  final ref = FirebaseStorage.instance.ref(folderPath);
  final result = await ref.listAll();

  await Future.wait([
    ...result.items.map((item) => item.delete()),
    ...result.prefixes.map((prefix) => deleteFolder(prefix.fullPath)),
  ]);
}
```

### List files in a directory

```dart
Future<List<Reference>> listFiles(String folderPath) async {
  final ref = FirebaseStorage.instance.ref(folderPath);
  final result = await ref.listAll();
  return result.items;
}

// Paginated listing.
Future<({List<Reference> items, String? pageToken})> listFilesPaginated({
  required String folderPath,
  int maxResults = 100,
  String? pageToken,
}) async {
  final ref = FirebaseStorage.instance.ref(folderPath);
  final result = await ref.list(
    ListOptions(
      maxResults: maxResults,
      pageToken: pageToken,
    ),
  );
  return (items: result.items, pageToken: result.nextPageToken);
}
```

### Complete upload widget with progress indicator

```dart
// lib/widgets/file_upload_widget.dart
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../services/storage_service.dart';

class FileUploadWidget extends StatefulWidget {
  const FileUploadWidget({
    required this.storagePath,
    required this.onUploadComplete,
    super.key,
  });

  final String storagePath;
  final void Function(String downloadUrl) onUploadComplete;

  @override
  State<FileUploadWidget> createState() => _FileUploadWidgetState();
}

class _FileUploadWidgetState extends State<FileUploadWidget> {
  final _storageService = StorageService();
  final _picker = ImagePicker();
  double _progress = 0;
  bool _uploading = false;

  Future<void> _pickAndUpload() async {
    final pickedFile = await _picker.pickImage(
      source: ImageSource.gallery,
      maxWidth: 1920,
      maxHeight: 1080,
      imageQuality: 85,
    );
    if (pickedFile == null) return;

    setState(() {
      _uploading = true;
      _progress = 0;
    });

    try {
      final file = File(pickedFile.path);
      final fileName =
          '${DateTime.now().millisecondsSinceEpoch}_${pickedFile.name}';
      final downloadUrl = await _storageService.uploadFile(
        file: file,
        storagePath: '${widget.storagePath}/$fileName',
        onProgress: (progress) {
          setState(() => _progress = progress);
        },
      );
      widget.onUploadComplete(downloadUrl);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Upload failed: $e')),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _uploading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (_uploading) ...[
          LinearProgressIndicator(value: _progress),
          const SizedBox(height: 8),
          Text('${(_progress * 100).toStringAsFixed(0)}%'),
        ] else
          FilledButton.icon(
            onPressed: _pickAndUpload,
            icon: const Icon(Icons.upload),
            label: const Text('Upload Image'),
          ),
      ],
    );
  }
}
```

---

## Remote Config

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_remote_config: ^5.1.0
```

### Setup and initialization

```dart
// lib/services/remote_config_service.dart
import 'package:firebase_remote_config/firebase_remote_config.dart';

class RemoteConfigService {
  RemoteConfigService({FirebaseRemoteConfig? remoteConfig})
      : _remoteConfig = remoteConfig ?? FirebaseRemoteConfig.instance;

  final FirebaseRemoteConfig _remoteConfig;

  Future<void> initialize() async {
    // Set defaults that are used when no server value exists.
    await _remoteConfig.setDefaults(const {
      'welcome_message': 'Welcome to our app!',
      'show_new_feature': false,
      'max_items_per_page': 20,
      'maintenance_mode': false,
      'minimum_app_version': '1.0.0',
      'api_base_url': 'https://api.example.com',
    });

    // Configure fetch settings.
    await _remoteConfig.setConfigSettings(
      RemoteConfigSettings(
        fetchTimeout: const Duration(seconds: 10),
        // In production, use a longer interval (e.g., 12 hours).
        // Use Duration.zero for development/debugging.
        minimumFetchInterval: const Duration(hours: 12),
      ),
    );

    // Fetch and activate in one call.
    await _remoteConfig.fetchAndActivate();
  }

  String getString(String key) => _remoteConfig.getString(key);
  bool getBool(String key) => _remoteConfig.getBool(key);
  int getInt(String key) => _remoteConfig.getInt(key);
  double getDouble(String key) => _remoteConfig.getDouble(key);
}
```

### Using Remote Config values

```dart
// lib/main.dart
Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);

  final remoteConfig = RemoteConfigService();
  await remoteConfig.initialize();

  runApp(MyApp(remoteConfig: remoteConfig));
}
```

```dart
// Checking a feature flag.
class HomeScreen extends StatelessWidget {
  const HomeScreen({required this.remoteConfig, super.key});

  final RemoteConfigService remoteConfig;

  @override
  Widget build(BuildContext context) {
    final showNewFeature = remoteConfig.getBool('show_new_feature');
    final welcomeMessage = remoteConfig.getString('welcome_message');

    return Scaffold(
      appBar: AppBar(title: const Text('Home')),
      body: Column(
        children: [
          Text(welcomeMessage),
          if (showNewFeature) const NewFeatureWidget(),
        ],
      ),
    );
  }
}
```

### Real-time Remote Config listener

```dart
void listenToRemoteConfigChanges() {
  FirebaseRemoteConfig.instance.onConfigUpdated.listen((event) async {
    await FirebaseRemoteConfig.instance.activate();
    // React to changed keys.
    for (final key in event.updatedKeys) {
      switch (key) {
        case 'maintenance_mode':
          final isMaintenanceMode =
              FirebaseRemoteConfig.instance.getBool('maintenance_mode');
          if (isMaintenanceMode) {
            // Navigate to maintenance screen.
          }
        case 'minimum_app_version':
          // Check if user needs to update.
          break;
      }
    }
  });
}
```

### Remote Config with Riverpod

```dart
// lib/providers/remote_config_providers.dart
import 'package:firebase_remote_config/firebase_remote_config.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'remote_config_providers.g.dart';

@Riverpod(keepAlive: true)
class RemoteConfigNotifier extends _$RemoteConfigNotifier {
  @override
  Future<FirebaseRemoteConfig> build() async {
    final rc = FirebaseRemoteConfig.instance;

    await rc.setDefaults(const {
      'show_new_feature': false,
      'max_items_per_page': 20,
      'maintenance_mode': false,
    });

    await rc.setConfigSettings(
      RemoteConfigSettings(
        fetchTimeout: const Duration(seconds: 10),
        minimumFetchInterval: const Duration(hours: 12),
      ),
    );

    await rc.fetchAndActivate();

    // Listen for real-time updates.
    rc.onConfigUpdated.listen((event) async {
      await rc.activate();
      ref.invalidateSelf();
    });

    return rc;
  }
}

@riverpod
bool featureFlag(Ref ref, String key) {
  final rcAsync = ref.watch(remoteConfigNotifierProvider);
  return rcAsync.whenOrNull(data: (rc) => rc.getBool(key)) ?? false;
}
```

### Force update check using Remote Config

```dart
import 'package:package_info_plus/package_info_plus.dart';

Future<bool> isUpdateRequired(RemoteConfigService config) async {
  final minimumVersion = config.getString('minimum_app_version');
  final packageInfo = await PackageInfo.fromPlatform();
  final currentVersion = packageInfo.version;

  return _compareVersions(currentVersion, minimumVersion) < 0;
}

/// Returns negative if a < b, 0 if equal, positive if a > b.
int _compareVersions(String a, String b) {
  final aParts = a.split('.').map(int.parse).toList();
  final bParts = b.split('.').map(int.parse).toList();

  for (var i = 0; i < 3; i++) {
    final aVal = i < aParts.length ? aParts[i] : 0;
    final bVal = i < bParts.length ? bParts[i] : 0;
    if (aVal != bVal) return aVal - bVal;
  }
  return 0;
}
```

---

## Analytics

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_analytics: ^11.3.0
```

### Setup

```dart
// lib/services/analytics_service.dart
import 'package:firebase_analytics/firebase_analytics.dart';

class AnalyticsService {
  AnalyticsService({FirebaseAnalytics? analytics})
      : _analytics = analytics ?? FirebaseAnalytics.instance;

  final FirebaseAnalytics _analytics;

  FirebaseAnalyticsObserver get observer =>
      FirebaseAnalyticsObserver(analytics: _analytics);
}
```

### Screen tracking with GoRouter

```dart
// lib/router/app_router.dart
import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:go_router/go_router.dart';

GoRouter createRouter(FirebaseAnalytics analytics) {
  return GoRouter(
    observers: [
      FirebaseAnalyticsObserver(analytics: analytics),
    ],
    routes: [
      GoRoute(
        path: '/',
        name: 'home',
        builder: (context, state) => const HomeScreen(),
      ),
    ],
  );
}
```

### Custom events

```dart
extension AnalyticsEvents on AnalyticsService {
  Future<void> logSignUp({required String method}) async {
    await _analytics.logSignUp(signUpMethod: method);
  }

  Future<void> logLogin({required String method}) async {
    await _analytics.logLogin(loginMethod: method);
  }

  Future<void> logPurchase({
    required String itemId,
    required String itemName,
    required double price,
    required String currency,
  }) async {
    await _analytics.logPurchase(
      currency: currency,
      value: price,
      items: [
        AnalyticsEventItem(
          itemId: itemId,
          itemName: itemName,
          price: price,
        ),
      ],
    );
  }

  Future<void> logSearch({required String query}) async {
    await _analytics.logSearch(searchTerm: query);
  }

  Future<void> logShare({
    required String contentType,
    required String itemId,
    required String method,
  }) async {
    await _analytics.logShare(
      contentType: contentType,
      itemId: itemId,
      method: method,
    );
  }

  /// Log a fully custom event with arbitrary parameters.
  Future<void> logCustomEvent({
    required String name,
    Map<String, Object>? parameters,
  }) async {
    await _analytics.logEvent(
      name: name,
      parameters: parameters,
    );
  }

  Future<void> logFeatureUsed({
    required String featureName,
    Map<String, Object>? details,
  }) async {
    await _analytics.logEvent(
      name: 'feature_used',
      parameters: {
        'feature_name': featureName,
        ...?details,
      },
    );
  }

  Future<void> logOnboardingStep({
    required int step,
    required String stepName,
    required bool completed,
  }) async {
    await _analytics.logEvent(
      name: 'onboarding_step',
      parameters: {
        'step_number': step,
        'step_name': stepName,
        'completed': completed,
      },
    );
  }
}
```

### User properties

```dart
extension AnalyticsUserProperties on AnalyticsService {
  Future<void> setUserRole(String role) async {
    await _analytics.setUserProperty(name: 'user_role', value: role);
  }

  Future<void> setSubscriptionTier(String tier) async {
    await _analytics.setUserProperty(name: 'subscription_tier', value: tier);
  }

  Future<void> setUserId(String? userId) async {
    await _analytics.setUserId(id: userId);
  }
}
```

### Analytics with Riverpod

```dart
// lib/providers/analytics_provider.dart
import 'package:firebase_analytics/firebase_analytics.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../services/analytics_service.dart';

part 'analytics_provider.g.dart';

@Riverpod(keepAlive: true)
AnalyticsService analyticsService(Ref ref) {
  return AnalyticsService();
}

@Riverpod(keepAlive: true)
FirebaseAnalyticsObserver analyticsObserver(Ref ref) {
  return ref.watch(analyticsServiceProvider).observer;
}
```

Usage in a widget:

```dart
class ProductDetailScreen extends ConsumerWidget {
  const ProductDetailScreen({required this.productId, super.key});

  final String productId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final analytics = ref.read(analyticsServiceProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Product')),
      body: Center(
        child: FilledButton(
          onPressed: () {
            analytics.logCustomEvent(
              name: 'add_to_cart',
              parameters: {'product_id': productId},
            );
          },
          child: const Text('Add to Cart'),
        ),
      ),
    );
  }
}
```
