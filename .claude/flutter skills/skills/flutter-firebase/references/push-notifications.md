# FCM Push Notifications and Crashlytics Reference Guide

## Overview

This reference covers the complete push notification pipeline using Firebase
Cloud Messaging (FCM) -- setup for iOS and Android, permission handling,
foreground and background notification processing, notification channels, token
management, topic subscriptions, local notification integration, and deep
linking. It also covers Firebase Crashlytics setup and error reporting.

---

## Table of Contents

1. [Firebase Messaging Setup](#firebase-messaging-setup)
2. [Requesting Permissions](#requesting-permissions)
3. [Handling Foreground Notifications](#handling-foreground-notifications)
4. [Handling Background and Terminated Notifications](#handling-background-and-terminated-notifications)
5. [Notification Channels (Android)](#notification-channels-android)
6. [Token Management](#token-management)
7. [Topic Subscriptions](#topic-subscriptions)
8. [Local Notifications Integration](#local-notifications-integration)
9. [Deep Linking from Notifications](#deep-linking-from-notifications)
10. [Crashlytics Setup and Error Reporting](#crashlytics-setup-and-error-reporting)

---

## Firebase Messaging Setup

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_core: ^3.8.0
  firebase_messaging: ^15.1.0
  flutter_local_notifications: ^18.0.0
```

### iOS setup

1. Enable **Push Notifications** in Xcode:
   - Open `ios/Runner.xcworkspace`
   - Select Runner target > **Signing & Capabilities**
   - Click **+ Capability** > **Push Notifications**

2. Enable **Background Modes** > **Remote notifications**:
   - Same Capabilities tab > **+ Capability** > **Background Modes**
   - Check **Remote notifications**

3. Upload your APNs authentication key to the Firebase Console:
   - **Project Settings > Cloud Messaging > iOS app configuration**
   - Upload the `.p8` key from Apple Developer portal

4. Add to `ios/Runner/AppDelegate.swift`:

```swift
import UIKit
import Flutter
import FirebaseMessaging

@main
@objc class AppDelegate: FlutterAppDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    GeneratedPluginRegistrant.register(with: self)

    // Required for iOS 10+ foreground notification display.
    UNUserNotificationCenter.current().delegate = self

    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }

  override func application(
    _ application: UIApplication,
    didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data
  ) {
    Messaging.messaging().apnsToken = deviceToken
    super.application(application, didRegisterForRemoteNotificationsWithDeviceToken: deviceToken)
  }
}
```

### Android setup

1. Ensure `google-services.json` is placed in `android/app/`.

2. The `firebase_messaging` plugin handles most Android configuration
   automatically. For custom notification icons and colors, add to
   `android/app/src/main/AndroidManifest.xml` inside `<application>`:

```xml
<meta-data
    android:name="com.google.firebase.messaging.default_notification_icon"
    android:resource="@drawable/ic_notification" />
<meta-data
    android:name="com.google.firebase.messaging.default_notification_channel_id"
    android:value="high_importance_channel" />
<meta-data
    android:name="com.google.firebase.messaging.default_notification_color"
    android:resource="@color/notification_color" />
```

3. Create the notification icon at `android/app/src/main/res/drawable/ic_notification.png`
   (white silhouette on transparent background).

---

## Requesting Permissions

```dart
// lib/services/notification_service.dart
import 'package:firebase_messaging/firebase_messaging.dart';

class NotificationService {
  NotificationService({FirebaseMessaging? messaging})
      : _messaging = messaging ?? FirebaseMessaging.instance;

  final FirebaseMessaging _messaging;

  /// Requests notification permissions. Returns the granted settings.
  Future<NotificationSettings> requestPermission() async {
    final settings = await _messaging.requestPermission(
      alert: true,
      announcement: false,
      badge: true,
      carPlay: false,
      criticalAlert: false,
      provisional: false,
      sound: true,
    );

    return settings;
  }

  /// Returns true if the user has granted notification permission.
  Future<bool> isPermissionGranted() async {
    final settings = await _messaging.getNotificationSettings();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  /// Request provisional permission (iOS only) -- delivers quietly without
  /// prompting. The user can later promote to full authorization.
  Future<NotificationSettings> requestProvisionalPermission() async {
    return _messaging.requestPermission(
      provisional: true,
    );
  }
}
```

### Handling permission states

```dart
Future<void> handlePermissionResult(NotificationSettings settings) async {
  switch (settings.authorizationStatus) {
    case AuthorizationStatus.authorized:
      // Full permission granted.
      break;
    case AuthorizationStatus.provisional:
      // Provisional permission (iOS only, quiet delivery).
      break;
    case AuthorizationStatus.denied:
      // Permission denied. Show an in-app explanation and link to settings.
      break;
    case AuthorizationStatus.notDetermined:
      // Permission not yet requested.
      break;
  }
}
```

---

## Handling Foreground Notifications

By default, FCM does **not** display a notification UI when the app is in the
foreground. You must either show a local notification or handle the message data
directly.

### Option 1: Present as heads-up notification (iOS)

```dart
// Call this once at app startup.
void configureForegroundPresentation() {
  FirebaseMessaging.instance.setForegroundNotificationPresentationOptions(
    alert: true,
    badge: true,
    sound: true,
  );
}
```

### Option 2: Listen and show a local notification (cross-platform)

```dart
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

void setupForegroundMessageHandler(
  FlutterLocalNotificationsPlugin localNotifications,
) {
  FirebaseMessaging.onMessage.listen((RemoteMessage message) {
    final notification = message.notification;
    if (notification == null) return;

    final android = notification.android;
    localNotifications.show(
      notification.hashCode,
      notification.title,
      notification.body,
      NotificationDetails(
        android: AndroidNotificationDetails(
          'high_importance_channel',
          'High Importance Notifications',
          channelDescription: 'Channel for important notifications',
          importance: Importance.high,
          priority: Priority.high,
          icon: android?.smallIcon ?? '@drawable/ic_notification',
        ),
        iOS: const DarwinNotificationDetails(
          presentAlert: true,
          presentBadge: true,
          presentSound: true,
        ),
      ),
      payload: message.data['route'],
    );
  });
}
```

### Option 3: In-app banner (SnackBar / overlay)

```dart
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';

void setupInAppNotificationBanner(GlobalKey<ScaffoldMessengerState> messengerKey) {
  FirebaseMessaging.onMessage.listen((RemoteMessage message) {
    final notification = message.notification;
    if (notification == null) return;

    messengerKey.currentState?.showSnackBar(
      SnackBar(
        content: ListTile(
          title: Text(
            notification.title ?? '',
            style: const TextStyle(color: Colors.white),
          ),
          subtitle: Text(
            notification.body ?? '',
            style: const TextStyle(color: Colors.white70),
          ),
        ),
        behavior: SnackBarBehavior.floating,
        duration: const Duration(seconds: 4),
      ),
    );
  });
}
```

---

## Handling Background and Terminated Notifications

### Background message handler

The handler must be a **top-level function** (not a class method or closure).

```dart
// lib/main.dart
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';

import 'firebase_options.dart';

// MUST be a top-level function.
@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  // Firebase must be initialized in the background isolate.
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);

  // Process the message (e.g., update local DB, schedule local notification).
  // Do NOT update UI from here -- this runs in a separate isolate.
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);

  // Register the background handler.
  FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);

  runApp(const MyApp());
}
```

### Handling notification taps (app opened from notification)

```dart
void setupNotificationTapHandlers() {
  // App was in the background and the user tapped the notification.
  FirebaseMessaging.onMessageOpenedApp.listen((RemoteMessage message) {
    _handleNotificationNavigation(message);
  });

  // App was terminated and launched by tapping the notification.
  _handleInitialMessage();
}

Future<void> _handleInitialMessage() async {
  final initialMessage = await FirebaseMessaging.instance.getInitialMessage();
  if (initialMessage != null) {
    _handleNotificationNavigation(initialMessage);
  }
}

void _handleNotificationNavigation(RemoteMessage message) {
  final route = message.data['route'] as String?;
  final id = message.data['id'] as String?;

  if (route != null) {
    // Navigate using your router (e.g., GoRouter).
    // navigatorKey.currentContext?.go('$route/$id');
  }
}
```

---

## Notification Channels (Android)

Android 8.0+ (API 26+) requires notification channels. Create them at app
startup before showing any notifications.

```dart
// lib/services/notification_channels.dart
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

class NotificationChannels {
  static const highImportance = AndroidNotificationChannel(
    'high_importance_channel',
    'High Importance Notifications',
    description: 'Channel for important notifications like messages and alerts',
    importance: Importance.high,
    playSound: true,
    enableVibration: true,
  );

  static const promotions = AndroidNotificationChannel(
    'promotions_channel',
    'Promotions',
    description: 'Promotional offers and deals',
    importance: Importance.defaultImportance,
    playSound: true,
  );

  static const silent = AndroidNotificationChannel(
    'silent_channel',
    'Silent Updates',
    description: 'Background data sync notifications',
    importance: Importance.low,
    playSound: false,
    enableVibration: false,
  );

  static List<AndroidNotificationChannel> get all => [
        highImportance,
        promotions,
        silent,
      ];
}
```

### Creating channels at startup

```dart
Future<void> createNotificationChannels() async {
  final plugin = FlutterLocalNotificationsPlugin();
  final androidPlugin =
      plugin.resolvePlatformSpecificImplementation<
          AndroidFlutterLocalNotificationsPlugin>();

  if (androidPlugin != null) {
    for (final channel in NotificationChannels.all) {
      await androidPlugin.createNotificationChannel(channel);
    }
  }
}
```

---

## Token Management

### Get the FCM token

```dart
Future<String?> getFcmToken() async {
  final token = await FirebaseMessaging.instance.getToken();
  return token;
}
```

### Save token to Firestore

```dart
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_messaging/firebase_messaging.dart';

Future<void> saveTokenToFirestore() async {
  final user = FirebaseAuth.instance.currentUser;
  if (user == null) return;

  final token = await FirebaseMessaging.instance.getToken();
  if (token == null) return;

  await FirebaseFirestore.instance
      .collection('users')
      .doc(user.uid)
      .collection('tokens')
      .doc(token)
      .set({
    'token': token,
    'platform': _getPlatform(),
    'createdAt': FieldValue.serverTimestamp(),
  });
}

String _getPlatform() {
  // Use dart:io Platform or defaultTargetPlatform.
  return 'unknown';
}
```

### Listen for token refresh

```dart
void listenForTokenRefresh() {
  FirebaseMessaging.instance.onTokenRefresh.listen((newToken) async {
    // Delete the old token and save the new one.
    await saveTokenToFirestore();
  });
}
```

### Remove token on sign-out

```dart
Future<void> removeTokenOnSignOut() async {
  final user = FirebaseAuth.instance.currentUser;
  if (user == null) return;

  final token = await FirebaseMessaging.instance.getToken();
  if (token == null) return;

  await FirebaseFirestore.instance
      .collection('users')
      .doc(user.uid)
      .collection('tokens')
      .doc(token)
      .delete();

  await FirebaseMessaging.instance.deleteToken();
}
```

### Complete token management service

```dart
// lib/services/fcm_token_service.dart
import 'dart:async';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

class FcmTokenService {
  FcmTokenService({
    FirebaseMessaging? messaging,
    FirebaseFirestore? firestore,
    FirebaseAuth? auth,
  })  : _messaging = messaging ?? FirebaseMessaging.instance,
        _firestore = firestore ?? FirebaseFirestore.instance,
        _auth = auth ?? FirebaseAuth.instance;

  final FirebaseMessaging _messaging;
  final FirebaseFirestore _firestore;
  final FirebaseAuth _auth;
  StreamSubscription<String>? _tokenRefreshSubscription;

  Future<void> initialize() async {
    await _saveCurrentToken();

    _tokenRefreshSubscription =
        _messaging.onTokenRefresh.listen((_) async {
      await _saveCurrentToken();
    });
  }

  Future<void> _saveCurrentToken() async {
    final user = _auth.currentUser;
    if (user == null) return;

    final token = await _messaging.getToken();
    if (token == null) return;

    await _firestore
        .collection('users')
        .doc(user.uid)
        .collection('fcmTokens')
        .doc(token)
        .set({
      'token': token,
      'platform': defaultTargetPlatform.name,
      'updatedAt': FieldValue.serverTimestamp(),
    });
  }

  Future<void> removeCurrentToken() async {
    final user = _auth.currentUser;
    if (user == null) return;

    final token = await _messaging.getToken();
    if (token != null) {
      await _firestore
          .collection('users')
          .doc(user.uid)
          .collection('fcmTokens')
          .doc(token)
          .delete();
    }

    await _messaging.deleteToken();
  }

  void dispose() {
    _tokenRefreshSubscription?.cancel();
  }
}
```

---

## Topic Subscriptions

Topics allow you to send notifications to groups of users without managing
individual tokens.

### Subscribe and unsubscribe

```dart
Future<void> subscribeToTopic(String topic) async {
  await FirebaseMessaging.instance.subscribeToTopic(topic);
}

Future<void> unsubscribeFromTopic(String topic) async {
  await FirebaseMessaging.instance.unsubscribeFromTopic(topic);
}
```

### Notification preferences UI

```dart
// lib/screens/notification_preferences_screen.dart
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class NotificationPreferencesScreen extends StatefulWidget {
  const NotificationPreferencesScreen({super.key});

  @override
  State<NotificationPreferencesScreen> createState() =>
      _NotificationPreferencesScreenState();
}

class _NotificationPreferencesScreenState
    extends State<NotificationPreferencesScreen> {
  final _messaging = FirebaseMessaging.instance;
  late final SharedPreferences _prefs;
  bool _loaded = false;

  final _topics = <String, String>{
    'news': 'Breaking News',
    'promotions': 'Promotions & Deals',
    'product_updates': 'Product Updates',
    'weekly_digest': 'Weekly Digest',
  };

  Map<String, bool> _subscriptions = {};

  @override
  void initState() {
    super.initState();
    _loadPreferences();
  }

  Future<void> _loadPreferences() async {
    _prefs = await SharedPreferences.getInstance();
    _subscriptions = {
      for (final topic in _topics.keys)
        topic: _prefs.getBool('topic_$topic') ?? false,
    };
    setState(() => _loaded = true);
  }

  Future<void> _toggleTopic(String topic, bool value) async {
    setState(() => _subscriptions[topic] = value);
    await _prefs.setBool('topic_$topic', value);

    if (value) {
      await _messaging.subscribeToTopic(topic);
    } else {
      await _messaging.unsubscribeFromTopic(topic);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Notification Preferences')),
      body: _loaded
          ? ListView(
              children: _topics.entries.map((entry) {
                return SwitchListTile(
                  title: Text(entry.value),
                  value: _subscriptions[entry.key] ?? false,
                  onChanged: (value) => _toggleTopic(entry.key, value),
                );
              }).toList(),
            )
          : const Center(child: CircularProgressIndicator()),
    );
  }
}
```

### Sending a topic message (Cloud Function)

```typescript
// functions/src/index.ts
import { onDocumentCreated } from 'firebase-functions/v2/firestore';
import { getMessaging } from 'firebase-admin/messaging';

export const sendNewsNotification = onDocumentCreated(
  'news/{articleId}',
  async (event) => {
    const article = event.data?.data();
    if (!article) return;

    await getMessaging().send({
      topic: 'news',
      notification: {
        title: article.title,
        body: article.summary,
      },
      data: {
        route: '/news',
        id: event.params.articleId,
      },
      android: {
        notification: {
          channelId: 'high_importance_channel',
          priority: 'high',
        },
      },
      apns: {
        payload: {
          aps: {
            sound: 'default',
            badge: 1,
          },
        },
      },
    });
  },
);
```

---

## Local Notifications Integration

### Setup flutter_local_notifications

```dart
// lib/services/local_notification_service.dart
import 'dart:async';

import 'package:flutter_local_notifications/flutter_local_notifications.dart';

class LocalNotificationService {
  LocalNotificationService();

  final FlutterLocalNotificationsPlugin _plugin =
      FlutterLocalNotificationsPlugin();

  final StreamController<String?> _notificationTapStream =
      StreamController<String?>.broadcast();

  Stream<String?> get onNotificationTap => _notificationTapStream.stream;

  Future<void> initialize() async {
    const androidSettings =
        AndroidInitializationSettings('@drawable/ic_notification');

    const iosSettings = DarwinInitializationSettings(
      requestAlertPermission: false,
      requestBadgePermission: false,
      requestSoundPermission: false,
    );

    const settings = InitializationSettings(
      android: androidSettings,
      iOS: iosSettings,
    );

    await _plugin.initialize(
      settings,
      onDidReceiveNotificationResponse: (response) {
        _notificationTapStream.add(response.payload);
      },
    );
  }

  Future<void> showNotification({
    required int id,
    required String title,
    required String body,
    String? payload,
    String channelId = 'high_importance_channel',
    String channelName = 'High Importance Notifications',
  }) async {
    final details = NotificationDetails(
      android: AndroidNotificationDetails(
        channelId,
        channelName,
        importance: Importance.high,
        priority: Priority.high,
        icon: '@drawable/ic_notification',
      ),
      iOS: const DarwinNotificationDetails(
        presentAlert: true,
        presentBadge: true,
        presentSound: true,
      ),
    );

    await _plugin.show(id, title, body, details, payload: payload);
  }

  Future<void> showGroupedNotification({
    required int id,
    required String title,
    required String body,
    required String groupKey,
    String? payload,
  }) async {
    // Show the individual notification.
    await _plugin.show(
      id,
      title,
      body,
      NotificationDetails(
        android: AndroidNotificationDetails(
          'high_importance_channel',
          'High Importance Notifications',
          importance: Importance.high,
          priority: Priority.high,
          groupKey: groupKey,
        ),
      ),
      payload: payload,
    );

    // Show the summary notification (Android only).
    await _plugin.show(
      0, // Summary notification always uses id 0 for the group.
      'Messages',
      '',
      NotificationDetails(
        android: AndroidNotificationDetails(
          'high_importance_channel',
          'High Importance Notifications',
          groupKey: groupKey,
          setAsGroupSummary: true,
          importance: Importance.high,
          priority: Priority.high,
        ),
      ),
    );
  }

  Future<void> cancelNotification(int id) async {
    await _plugin.cancel(id);
  }

  Future<void> cancelAllNotifications() async {
    await _plugin.cancelAll();
  }

  void dispose() {
    _notificationTapStream.close();
  }
}
```

### Wiring FCM and local notifications together

```dart
// lib/main.dart
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/material.dart';

import 'firebase_options.dart';
import 'services/local_notification_service.dart';
import 'services/notification_channels.dart';

@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);
}

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform);

  FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);

  // Create Android notification channels.
  await createNotificationChannels();

  // Initialize local notifications.
  final localNotifications = LocalNotificationService();
  await localNotifications.initialize();

  // Request permission.
  await FirebaseMessaging.instance.requestPermission();

  // Configure foreground presentation (iOS).
  await FirebaseMessaging.instance.setForegroundNotificationPresentationOptions(
    alert: true,
    badge: true,
    sound: true,
  );

  // Show local notification for foreground FCM messages.
  FirebaseMessaging.onMessage.listen((RemoteMessage message) {
    final notification = message.notification;
    if (notification == null) return;

    localNotifications.showNotification(
      id: notification.hashCode,
      title: notification.title ?? '',
      body: notification.body ?? '',
      payload: message.data['route'],
    );
  });

  runApp(MyApp(localNotifications: localNotifications));
}

class MyApp extends StatefulWidget {
  const MyApp({required this.localNotifications, super.key});

  final LocalNotificationService localNotifications;

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  @override
  void initState() {
    super.initState();
    _setupNotificationTapHandler();
    _checkInitialNotification();
  }

  void _setupNotificationTapHandler() {
    // Handle tap on local notification.
    widget.localNotifications.onNotificationTap.listen((payload) {
      if (payload != null) {
        // Navigate to the route specified in the payload.
        // context.go(payload);
      }
    });

    // Handle tap on FCM notification (app was in background).
    FirebaseMessaging.onMessageOpenedApp.listen((message) {
      final route = message.data['route'] as String?;
      if (route != null) {
        // context.go(route);
      }
    });
  }

  Future<void> _checkInitialNotification() async {
    // Handle FCM notification that launched the app.
    final initialMessage =
        await FirebaseMessaging.instance.getInitialMessage();
    if (initialMessage != null) {
      final route = initialMessage.data['route'] as String?;
      if (route != null) {
        // context.go(route);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      routerConfig: appRouter,
    );
  }
}
```

---

## Deep Linking from Notifications

### Notification payload structure

Design your notification payloads with a consistent `route` and `id` pattern:

```json
{
  "notification": {
    "title": "New comment on your post",
    "body": "Alice commented: 'Great article!'"
  },
  "data": {
    "route": "/posts",
    "id": "post123",
    "type": "comment"
  }
}
```

### Centralized notification router

```dart
// lib/services/notification_router.dart
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:go_router/go_router.dart';

class NotificationRouter {
  NotificationRouter(this._router);

  final GoRouter _router;

  void handleNotificationTap(RemoteMessage message) {
    final route = message.data['route'] as String?;
    final id = message.data['id'] as String?;
    final type = message.data['type'] as String?;

    if (route == null) return;

    switch (type) {
      case 'comment':
      case 'post':
        if (id != null) {
          _router.go('$route/$id');
        } else {
          _router.go(route);
        }
      case 'chat':
        if (id != null) {
          _router.go('/chat/$id');
        }
      case 'profile':
        _router.go('/profile');
      default:
        _router.go(route);
    }
  }

  void handlePayload(String? payload) {
    if (payload == null) return;
    _router.go(payload);
  }
}
```

### Deep link handling with Riverpod

```dart
// lib/providers/notification_providers.dart
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'notification_providers.g.dart';

@Riverpod(keepAlive: true)
Stream<RemoteMessage> onMessageOpenedApp(Ref ref) {
  return FirebaseMessaging.onMessageOpenedApp;
}

@Riverpod(keepAlive: true)
Future<RemoteMessage?> initialMessage(Ref ref) {
  return FirebaseMessaging.instance.getInitialMessage();
}

@Riverpod(keepAlive: true)
Stream<RemoteMessage> onForegroundMessage(Ref ref) {
  return FirebaseMessaging.onMessage;
}
```

Usage in a root widget:

```dart
class AppShell extends ConsumerWidget {
  const AppShell({required this.child, super.key});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // React to notification taps while app is in background.
    ref.listen(onMessageOpenedAppProvider, (_, next) {
      next.whenData((message) {
        final route = message.data['route'] as String?;
        final id = message.data['id'] as String?;
        if (route != null) {
          final fullRoute = id != null ? '$route/$id' : route;
          context.go(fullRoute);
        }
      });
    });

    // Check if app was launched from a notification.
    ref.listen(initialMessageProvider, (_, next) {
      next.whenData((message) {
        if (message != null) {
          final route = message.data['route'] as String?;
          if (route != null) {
            context.go(route);
          }
        }
      });
    });

    return child;
  }
}
```

---

## Crashlytics Setup and Error Reporting

### Dependencies

```yaml
# pubspec.yaml
dependencies:
  firebase_crashlytics: ^4.2.0
```

### Initialization

```dart
// lib/main.dart
import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'firebase_options.dart';

Future<void> main() async {
  runZonedGuarded(() async {
    WidgetsFlutterBinding.ensureInitialized();
    await Firebase.initializeApp(
      options: DefaultFirebaseOptions.currentPlatform,
    );

    // Pass all uncaught Flutter errors to Crashlytics.
    FlutterError.onError = (errorDetails) {
      FirebaseCrashlytics.instance.recordFlutterFatalError(errorDetails);
    };

    // Pass all uncaught async errors to Crashlytics.
    PlatformDispatcher.instance.onError = (error, stack) {
      FirebaseCrashlytics.instance.recordError(error, stack, fatal: true);
      return true;
    };

    // Disable Crashlytics in debug mode (optional).
    if (kDebugMode) {
      await FirebaseCrashlytics.instance
          .setCrashlyticsCollectionEnabled(false);
    }

    runApp(const MyApp());
  }, (error, stack) {
    FirebaseCrashlytics.instance.recordError(error, stack, fatal: true);
  });
}
```

### Recording non-fatal errors

```dart
Future<void> recordNonFatalError(
  dynamic error,
  StackTrace stackTrace, {
  String? reason,
}) async {
  await FirebaseCrashlytics.instance.recordError(
    error,
    stackTrace,
    reason: reason ?? 'Non-fatal error',
    fatal: false,
  );
}
```

### Custom keys for debugging context

```dart
Future<void> setCrashlyticsContext({
  required String userId,
  required String userEmail,
  String? screenName,
}) async {
  final crashlytics = FirebaseCrashlytics.instance;

  await crashlytics.setUserIdentifier(userId);
  await crashlytics.setCustomKey('user_email', userEmail);

  if (screenName != null) {
    await crashlytics.setCustomKey('current_screen', screenName);
  }
}

Future<void> setCustomKey(String key, Object value) async {
  await FirebaseCrashlytics.instance.setCustomKey(key, value);
}
```

### Breadcrumb logging

```dart
void logBreadcrumb(String message) {
  FirebaseCrashlytics.instance.log(message);
}

// Usage throughout the app:
void onCheckoutTapped() {
  logBreadcrumb('User tapped checkout button');
  // ... proceed with checkout
}

void onPaymentMethodSelected(String method) {
  logBreadcrumb('Payment method selected: $method');
}
```

### Force a test crash

```dart
// Use during development to verify Crashlytics is working.
void forceTestCrash() {
  FirebaseCrashlytics.instance.crash();
}
```

### Crashlytics error boundary widget

```dart
// lib/widgets/crashlytics_error_boundary.dart
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:flutter/material.dart';

class CrashlyticsErrorBoundary extends StatefulWidget {
  const CrashlyticsErrorBoundary({required this.child, super.key});

  final Widget child;

  @override
  State<CrashlyticsErrorBoundary> createState() =>
      _CrashlyticsErrorBoundaryState();
}

class _CrashlyticsErrorBoundaryState extends State<CrashlyticsErrorBoundary> {
  bool _hasError = false;

  @override
  Widget build(BuildContext context) {
    if (_hasError) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.error_outline, size: 64, color: Colors.red),
              const SizedBox(height: 16),
              const Text('Something went wrong'),
              const SizedBox(height: 8),
              FilledButton(
                onPressed: () => setState(() => _hasError = false),
                child: const Text('Try Again'),
              ),
            ],
          ),
        ),
      );
    }

    return widget.child;
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    FlutterError.onError = (details) {
      FirebaseCrashlytics.instance.recordFlutterFatalError(details);
      if (mounted) {
        setState(() => _hasError = true);
      }
    };
  }
}
```

### Riverpod integration for error tracking

```dart
// lib/providers/crashlytics_provider.dart
import 'package:firebase_crashlytics/firebase_crashlytics.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'crashlytics_provider.g.dart';

@Riverpod(keepAlive: true)
FirebaseCrashlytics crashlytics(Ref ref) {
  return FirebaseCrashlytics.instance;
}

/// A Riverpod observer that records provider errors to Crashlytics.
class CrashlyticsProviderObserver extends ProviderObserver {
  @override
  void providerDidFail(
    ProviderBase<Object?> provider,
    Object error,
    StackTrace stackTrace,
    ProviderContainer container,
  ) {
    FirebaseCrashlytics.instance.recordError(
      error,
      stackTrace,
      reason: 'Provider error: ${provider.name ?? provider.runtimeType}',
    );
  }
}
```

Usage:

```dart
void main() async {
  // ... Firebase init ...

  runApp(
    ProviderScope(
      observers: [CrashlyticsProviderObserver()],
      child: const MyApp(),
    ),
  );
}
```
