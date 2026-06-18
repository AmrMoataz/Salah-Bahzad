# WebSockets and Real-Time Communication

Complete guide to WebSocket, Socket.IO, and Server-Sent Events in Flutter. Covers channel setup, reconnection strategies, message serialization, stream-based architecture, and UI state management.

---

## Table of Contents

1. [WebSocketChannel Setup](#websocketchannel-setup)
2. [Reconnection Strategy](#reconnection-strategy)
3. [Message Serialization](#message-serialization)
4. [Stream-Based Architecture](#stream-based-architecture)
5. [Socket.IO Integration](#socketio-integration)
6. [Server-Sent Events](#server-sent-events)
7. [Handling Connection States in UI](#handling-connection-states-in-ui)

---

## WebSocketChannel Setup

### Installation

```yaml
# pubspec.yaml
dependencies:
  web_socket_channel: ^3.0.0
```

### Basic Usage

```dart
import 'dart:async';

import 'package:web_socket_channel/web_socket_channel.dart';

/// Opens a WebSocket connection and listens for messages.
Future<void> basicWebSocketExample() async {
  final channel = WebSocketChannel.connect(
    Uri.parse('wss://echo.websocket.events'),
  );

  // Wait for the connection to be established
  await channel.ready;

  // Send a message
  channel.sink.add('Hello, WebSocket!');

  // Listen for incoming messages
  final subscription = channel.stream.listen(
    (message) {
      // `message` is typically a String or List<int> (binary)
      if (message is String) {
        print('Received: $message');
      }
    },
    onError: (Object error) {
      print('WebSocket error: $error');
    },
    onDone: () {
      print('WebSocket closed: ${channel.closeCode} ${channel.closeReason}');
    },
  );

  // Clean up
  await Future<void>.delayed(const Duration(seconds: 5));
  await subscription.cancel();
  await channel.sink.close(1000, 'Normal closure');
}
```

### Authenticated WebSocket

```dart
import 'package:web_socket_channel/web_socket_channel.dart';

WebSocketChannel connectWithAuth({
  required String url,
  required String token,
}) {
  return WebSocketChannel.connect(
    Uri.parse(url),
    protocols: ['graphql-transport-ws'],
  );
  // Note: Many servers accept the token as a query parameter
  // or in the first message payload rather than via HTTP headers,
  // because the browser WebSocket API does not support custom headers.
}

/// Alternative: pass token as a query parameter.
WebSocketChannel connectWithTokenParam({
  required String baseUrl,
  required String token,
}) {
  final uri = Uri.parse(baseUrl).replace(
    queryParameters: {'token': token},
  );
  return WebSocketChannel.connect(uri);
}
```

---

## Reconnection Strategy

A production WebSocket client must handle disconnections gracefully and reconnect with exponential backoff.

```dart
import 'dart:async';
import 'dart:math';

import 'package:web_socket_channel/web_socket_channel.dart';

/// Connection state reported to listeners.
enum WsConnectionState {
  connecting,
  connected,
  reconnecting,
  disconnected,
}

/// A WebSocket client with automatic reconnection using exponential backoff.
final class ReconnectingWebSocket {
  ReconnectingWebSocket({
    required this.uri,
    this.maxRetries = 10,
    this.baseDelay = const Duration(seconds: 1),
    this.maxDelay = const Duration(seconds: 30),
    this.protocols,
  });

  final Uri uri;
  final int maxRetries;
  final Duration baseDelay;
  final Duration maxDelay;
  final Iterable<String>? protocols;

  WebSocketChannel? _channel;
  StreamSubscription<Object?>? _subscription;
  int _retryCount = 0;
  Timer? _reconnectTimer;
  bool _disposed = false;

  final _messageController = StreamController<Object?>.broadcast();
  final _stateController = StreamController<WsConnectionState>.broadcast();

  /// Stream of incoming messages from the server.
  Stream<Object?> get messages => _messageController.stream;

  /// Stream of connection state changes.
  Stream<WsConnectionState> get connectionState => _stateController.stream;

  /// Initiates the WebSocket connection.
  Future<void> connect() async {
    if (_disposed) return;
    _stateController.add(WsConnectionState.connecting);

    try {
      _channel = WebSocketChannel.connect(uri, protocols: protocols);
      await _channel!.ready;
      _retryCount = 0;
      _stateController.add(WsConnectionState.connected);

      _subscription = _channel!.stream.listen(
        _messageController.add,
        onError: (Object error) {
          _messageController.addError(error);
          _scheduleReconnect();
        },
        onDone: _scheduleReconnect,
      );
    } on Object catch (error) {
      _messageController.addError(error);
      _scheduleReconnect();
    }
  }

  /// Sends a message through the WebSocket.
  void send(Object? message) {
    _channel?.sink.add(message);
  }

  void _scheduleReconnect() {
    if (_disposed) return;
    if (_retryCount >= maxRetries) {
      _stateController.add(WsConnectionState.disconnected);
      return;
    }

    _stateController.add(WsConnectionState.reconnecting);

    // Exponential backoff with jitter
    final delay = _calculateDelay(_retryCount);
    _retryCount++;

    _reconnectTimer?.cancel();
    _reconnectTimer = Timer(delay, () async {
      await _subscription?.cancel();
      await connect();
    });
  }

  Duration _calculateDelay(int attempt) {
    final exponential = baseDelay * (1 << attempt);
    final capped = exponential > maxDelay ? maxDelay : exponential;
    // Add random jitter: 0% to 25% of the delay
    final jitter = Random().nextDouble() * 0.25 * capped.inMilliseconds;
    return Duration(milliseconds: capped.inMilliseconds + jitter.toInt());
  }

  /// Cleanly shuts down the connection and all streams.
  Future<void> dispose() async {
    _disposed = true;
    _reconnectTimer?.cancel();
    await _subscription?.cancel();
    await _channel?.sink.close(1000, 'Client disposed');
    await _messageController.close();
    await _stateController.close();
  }
}
```

---

## Message Serialization

Define a typed message protocol for communication over WebSocket.

```dart
import 'dart:convert';

/// Sealed class representing all possible WebSocket messages.
sealed class WsMessage {
  const WsMessage();

  /// Serializes the message to a JSON string for sending.
  String toJsonString();
}

/// A chat message sent or received.
final class ChatMessage extends WsMessage {
  const ChatMessage({
    required this.channelId,
    required this.text,
    this.senderId,
    this.timestamp,
  });

  factory ChatMessage.fromJson(Map<String, Object?> json) {
    return ChatMessage(
      channelId: json['channelId'] as String,
      text: json['text'] as String,
      senderId: json['senderId'] as String?,
      timestamp: json['timestamp'] != null
          ? DateTime.parse(json['timestamp'] as String)
          : null,
    );
  }

  final String channelId;
  final String text;
  final String? senderId;
  final DateTime? timestamp;

  @override
  String toJsonString() {
    return jsonEncode({
      'type': 'chat_message',
      'channelId': channelId,
      'text': text,
      if (senderId != null) 'senderId': senderId,
      if (timestamp != null) 'timestamp': timestamp!.toIso8601String(),
    });
  }
}

/// A presence update (user joined/left).
final class PresenceUpdate extends WsMessage {
  const PresenceUpdate({
    required this.userId,
    required this.status,
  });

  factory PresenceUpdate.fromJson(Map<String, Object?> json) {
    return PresenceUpdate(
      userId: json['userId'] as String,
      status: PresenceStatus.values.byName(json['status'] as String),
    );
  }

  final String userId;
  final PresenceStatus status;

  @override
  String toJsonString() {
    return jsonEncode({
      'type': 'presence',
      'userId': userId,
      'status': status.name,
    });
  }
}

enum PresenceStatus { online, offline, typing }

/// A ping/pong for keep-alive.
final class Ping extends WsMessage {
  const Ping();

  @override
  String toJsonString() => jsonEncode({'type': 'ping'});
}

final class Pong extends WsMessage {
  const Pong();

  @override
  String toJsonString() => jsonEncode({'type': 'pong'});
}

/// Parses a raw JSON string into a typed [WsMessage].
WsMessage parseWsMessage(String raw) {
  final json = jsonDecode(raw) as Map<String, Object?>;
  final type = json['type'] as String;

  return switch (type) {
    'chat_message' => ChatMessage.fromJson(json),
    'presence' => PresenceUpdate.fromJson(json),
    'ping' => const Ping(),
    'pong' => const Pong(),
    _ => throw FormatException('Unknown message type: $type'),
  };
}
```

---

## Stream-Based Architecture

Wrap the reconnecting WebSocket and typed messages into a service that the rest of the app consumes through streams.

```dart
import 'dart:async';

/// A high-level real-time service that exposes typed message streams.
final class RealTimeService {
  RealTimeService({required ReconnectingWebSocket socket}) : _socket = socket;

  final ReconnectingWebSocket _socket;

  /// Stream of parsed, typed WebSocket messages.
  late final Stream<WsMessage> _typedMessages = _socket.messages
      .where((raw) => raw is String)
      .map((raw) => parseWsMessage(raw as String))
      .handleError(
        (Object error) {
          // Log parse errors but do not kill the stream
        },
      )
      .asBroadcastStream();

  /// Stream of only [ChatMessage] events.
  Stream<ChatMessage> chatMessages({String? channelId}) {
    return _typedMessages.whereType<ChatMessage>().where(
          (msg) => channelId == null || msg.channelId == channelId,
        );
  }

  /// Stream of [PresenceUpdate] events.
  Stream<PresenceUpdate> presenceUpdates() {
    return _typedMessages.whereType<PresenceUpdate>();
  }

  /// Stream of connection state changes, forwarded from the socket.
  Stream<WsConnectionState> get connectionState => _socket.connectionState;

  /// Sends a chat message.
  void sendChatMessage({
    required String channelId,
    required String text,
  }) {
    final msg = ChatMessage(channelId: channelId, text: text);
    _socket.send(msg.toJsonString());
  }

  /// Sends a presence update.
  void sendPresence(PresenceStatus status, {required String userId}) {
    final msg = PresenceUpdate(userId: userId, status: status);
    _socket.send(msg.toJsonString());
  }

  /// Starts a periodic ping to keep the connection alive.
  Timer startPingTimer({Duration interval = const Duration(seconds: 25)}) {
    return Timer.periodic(interval, (_) {
      _socket.send(const Ping().toJsonString());
    });
  }

  Future<void> dispose() => _socket.dispose();
}
```

---

## Socket.IO Integration

### Installation

```yaml
# pubspec.yaml
dependencies:
  socket_io_client: ^3.0.1
```

### Setup and Usage

```dart
import 'dart:async';

import 'package:socket_io_client/socket_io_client.dart' as io;

/// Connection state for the Socket.IO client.
enum SioConnectionState { connected, disconnected, reconnecting }

/// A wrapper around `socket_io_client` that provides typed streams.
final class SocketIoService {
  SocketIoService({
    required String url,
    String? token,
  }) : _socket = io.io(
          url,
          io.OptionBuilder()
              .setTransports(['websocket'])
              .enableAutoConnect()
              .enableReconnection()
              .setReconnectionAttempts(10)
              .setReconnectionDelay(1000)
              .setReconnectionDelayMax(30000)
              .setAuth(token != null ? {'token': token} : {})
              .build(),
        ) {
    _initListeners();
  }

  final io.Socket _socket;

  final _stateController = StreamController<SioConnectionState>.broadcast();
  final _eventController = StreamController<({String event, Object? data})>.broadcast();

  /// Stream of connection state changes.
  Stream<SioConnectionState> get connectionState => _stateController.stream;

  void _initListeners() {
    _socket
      ..onConnect((_) {
        _stateController.add(SioConnectionState.connected);
      })
      ..onDisconnect((_) {
        _stateController.add(SioConnectionState.disconnected);
      })
      ..onReconnecting((_) {
        _stateController.add(SioConnectionState.reconnecting);
      })
      ..onReconnect((_) {
        _stateController.add(SioConnectionState.connected);
      })
      ..onConnectError((error) {
        _stateController.addError(error as Object);
      });
  }

  /// Registers a listener for a specific server event and returns a stream.
  Stream<T> on<T>(String event) {
    final controller = StreamController<T>.broadcast(
      onCancel: () => _socket.off(event),
    );

    _socket.on(event, (data) {
      if (data is T) {
        controller.add(data);
      }
    });

    return controller.stream;
  }

  /// Emits an event to the server.
  void emit(String event, [Object? data]) {
    _socket.emit(event, data);
  }

  /// Emits an event and waits for an acknowledgment.
  Future<T> emitWithAck<T>(String event, Object? data) {
    final completer = Completer<T>();

    _socket.emitWithAck(event, data, ack: (response) {
      if (response is T) {
        completer.complete(response);
      } else {
        completer.completeError(
          FormatException('Unexpected ack type: ${response.runtimeType}'),
        );
      }
    });

    return completer.future;
  }

  /// Joins a Socket.IO room (server-side concept, client sends a join event).
  void joinRoom(String room) {
    _socket.emit('join', room);
  }

  /// Leaves a Socket.IO room.
  void leaveRoom(String room) {
    _socket.emit('leave', room);
  }

  /// Disconnects and cleans up.
  Future<void> dispose() async {
    _socket
      ..clearListeners()
      ..disconnect()
      ..dispose();
    await _stateController.close();
    await _eventController.close();
  }
}
```

### Usage Example

```dart
void socketIoExample() {
  final service = SocketIoService(
    url: 'https://api.example.com',
    token: 'my-jwt-token',
  );

  // Listen for typed events
  service.on<Map<String, Object?>>('new_message').listen((data) {
    final text = data['text'] as String?;
    print('New message: $text');
  });

  // Listen for connection state
  service.connectionState.listen((state) {
    print('Socket.IO state: $state');
  });

  // Send a message
  service.emit('send_message', {
    'channelId': 'general',
    'text': 'Hello from Flutter!',
  });

  // Join a room
  service.joinRoom('general');
}
```

---

## Server-Sent Events

Server-Sent Events (SSE) provide a simple, unidirectional, text-based protocol for server-to-client streaming over HTTP.

### Installation

```yaml
# pubspec.yaml
dependencies:
  dio: ^5.4.0       # for SSE over HTTP
  # or use the dedicated package:
  # flutter_client_sse: ^2.0.0
```

### Dio-Based SSE Client

```dart
import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';

/// A single SSE event parsed from the stream.
final class SseEvent {
  const SseEvent({
    this.id,
    this.event,
    required this.data,
  });

  final String? id;
  final String? event;
  final String data;
}

/// Connects to an SSE endpoint and yields parsed [SseEvent]s.
///
/// The returned stream will complete if the server closes the connection.
/// Use [CancelToken] to abort from the client side.
Stream<SseEvent> connectSse(
  Dio dio, {
  required String url,
  Map<String, Object?>? queryParameters,
  CancelToken? cancelToken,
}) async* {
  final response = await dio.get<ResponseBody>(
    url,
    queryParameters: queryParameters,
    options: Options(
      responseType: ResponseType.stream,
      headers: {
        'Accept': 'text/event-stream',
        'Cache-Control': 'no-cache',
      },
    ),
    cancelToken: cancelToken,
  );

  final stream = response.data?.stream;
  if (stream == null) return;

  String? currentId;
  String? currentEvent;
  final dataBuffer = StringBuffer();

  await for (final chunk in stream.transform(utf8.decoder)) {
    final lines = chunk.split('\n');

    for (final line in lines) {
      if (line.startsWith('id:')) {
        currentId = line.substring(3).trim();
      } else if (line.startsWith('event:')) {
        currentEvent = line.substring(6).trim();
      } else if (line.startsWith('data:')) {
        if (dataBuffer.isNotEmpty) dataBuffer.write('\n');
        dataBuffer.write(line.substring(5).trim());
      } else if (line.isEmpty && dataBuffer.isNotEmpty) {
        // Empty line signals end of an event
        yield SseEvent(
          id: currentId,
          event: currentEvent,
          data: dataBuffer.toString(),
        );
        currentId = null;
        currentEvent = null;
        dataBuffer.clear();
      }
    }
  }
}
```

### SSE with Reconnection

```dart
import 'dart:async';

import 'package:dio/dio.dart';

/// Wraps the SSE stream with automatic reconnection.
///
/// Sends `Last-Event-ID` on reconnect so the server can resume
/// from where the client left off.
Stream<SseEvent> connectSseWithReconnect(
  Dio dio, {
  required String url,
  int maxRetries = 10,
  Duration retryDelay = const Duration(seconds: 3),
}) async* {
  String? lastEventId;
  var attempts = 0;

  while (attempts < maxRetries) {
    try {
      final dioWithLastId = Dio(dio.options);
      if (lastEventId != null) {
        dioWithLastId.options.headers['Last-Event-ID'] = lastEventId;
      }

      await for (final event in connectSse(dioWithLastId, url: url)) {
        attempts = 0; // Reset on successful event
        if (event.id != null) lastEventId = event.id;
        yield event;
      }

      // Stream completed normally (server closed)
      break;
    } on Object {
      attempts++;
      if (attempts >= maxRetries) rethrow;
      await Future<void>.delayed(retryDelay * attempts);
    }
  }
}
```

---

## Handling Connection States in UI

### Connection-Aware Widget

```dart
import 'dart:async';

import 'package:flutter/material.dart';

/// A widget that displays a banner when the real-time connection is lost
/// and automatically hides it upon reconnection.
class ConnectionStatusBanner extends StatefulWidget {
  const ConnectionStatusBanner({
    required this.connectionState,
    required this.child,
    super.key,
  });

  final Stream<WsConnectionState> connectionState;
  final Widget child;

  @override
  State<ConnectionStatusBanner> createState() =>
      _ConnectionStatusBannerState();
}

class _ConnectionStatusBannerState extends State<ConnectionStatusBanner> {
  late StreamSubscription<WsConnectionState> _subscription;
  WsConnectionState _state = WsConnectionState.connecting;

  @override
  void initState() {
    super.initState();
    _subscription = widget.connectionState.listen((state) {
      setState(() => _state = state);
    });
  }

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        if (_state != WsConnectionState.connected)
          MaterialBanner(
            content: Text(_bannerText),
            backgroundColor: _bannerColor,
            leading: Icon(_bannerIcon, color: Colors.white),
            actions: [
              if (_state == WsConnectionState.disconnected)
                TextButton(
                  onPressed: () {
                    // Trigger manual reconnection via your service
                  },
                  child: const Text(
                    'Retry',
                    style: TextStyle(color: Colors.white),
                  ),
                )
              else
                const SizedBox.shrink(),
            ],
          ),
        Expanded(child: widget.child),
      ],
    );
  }

  String get _bannerText => switch (_state) {
        WsConnectionState.connecting => 'Connecting...',
        WsConnectionState.reconnecting => 'Reconnecting...',
        WsConnectionState.disconnected => 'Connection lost',
        WsConnectionState.connected => '',
      };

  Color get _bannerColor => switch (_state) {
        WsConnectionState.connecting => Colors.orange,
        WsConnectionState.reconnecting => Colors.orange,
        WsConnectionState.disconnected => Colors.red,
        WsConnectionState.connected => Colors.green,
      };

  IconData get _bannerIcon => switch (_state) {
        WsConnectionState.connecting => Icons.cloud_queue,
        WsConnectionState.reconnecting => Icons.cloud_sync,
        WsConnectionState.disconnected => Icons.cloud_off,
        WsConnectionState.connected => Icons.cloud_done,
      };
}
```

### Stream-Based Chat Screen

```dart
import 'dart:async';

import 'package:flutter/material.dart';

/// A full chat screen that listens to typed message streams.
class StreamChatScreen extends StatefulWidget {
  const StreamChatScreen({
    required this.realTimeService,
    required this.channelId,
    required this.currentUserId,
    super.key,
  });

  final RealTimeService realTimeService;
  final String channelId;
  final String currentUserId;

  @override
  State<StreamChatScreen> createState() => _StreamChatScreenState();
}

class _StreamChatScreenState extends State<StreamChatScreen> {
  final _messages = <ChatMessage>[];
  final _controller = TextEditingController();
  final _scrollController = ScrollController();
  late StreamSubscription<ChatMessage> _messageSub;
  late Timer _pingTimer;

  @override
  void initState() {
    super.initState();

    _messageSub = widget.realTimeService
        .chatMessages(channelId: widget.channelId)
        .listen((message) {
      setState(() => _messages.insert(0, message));
      _scrollToBottom();
    });

    _pingTimer = widget.realTimeService.startPingTimer();
  }

  @override
  void dispose() {
    _messageSub.cancel();
    _pingTimer.cancel();
    _controller.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _sendMessage() {
    final text = _controller.text.trim();
    if (text.isEmpty) return;

    widget.realTimeService.sendChatMessage(
      channelId: widget.channelId,
      text: text,
    );
    _controller.clear();
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          0,
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeOut,
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return ConnectionStatusBanner(
      connectionState: widget.realTimeService.connectionState,
      child: Column(
        children: [
          Expanded(
            child: ListView.builder(
              controller: _scrollController,
              reverse: true,
              itemCount: _messages.length,
              itemBuilder: (context, index) {
                final msg = _messages[index];
                final isMe = msg.senderId == widget.currentUserId;

                return Align(
                  alignment:
                      isMe ? Alignment.centerRight : Alignment.centerLeft,
                  child: Container(
                    margin: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 4,
                    ),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: isMe
                          ? Theme.of(context).colorScheme.primary
                          : Theme.of(context).colorScheme.surfaceContainerHighest,
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Text(
                      msg.text,
                      style: TextStyle(
                        color: isMe
                            ? Theme.of(context).colorScheme.onPrimary
                            : Theme.of(context).colorScheme.onSurface,
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(8),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _controller,
                    decoration: const InputDecoration(
                      hintText: 'Type a message',
                      border: OutlineInputBorder(),
                    ),
                    onSubmitted: (_) => _sendMessage(),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.filled(
                  onPressed: _sendMessage,
                  icon: const Icon(Icons.send),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
```
