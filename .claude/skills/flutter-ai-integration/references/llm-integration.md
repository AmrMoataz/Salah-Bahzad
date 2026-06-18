# LLM API Integration for Flutter

## Table of Contents

- [Gemini API Integration](#gemini-api-integration)
  - [Setup](#gemini-setup)
  - [Text Generation](#text-generation)
  - [Chat Conversations](#chat-conversations)
  - [Multimodal (Image + Text)](#multimodal-image--text)
  - [Streaming Responses](#streaming-responses)
- [OpenAI API Integration](#openai-api-integration)
  - [Setup](#openai-setup)
  - [Chat Completion](#chat-completion)
  - [Streaming Chat Completion](#streaming-chat-completion)
- [Token Counting and Cost Management](#token-counting-and-cost-management)
- [Error Handling and Rate Limiting](#error-handling-and-rate-limiting)
- [Caching AI Responses](#caching-ai-responses)
- [Building a Chat Interface](#building-a-chat-interface)

---

## Gemini API Integration

### Gemini Setup

#### pubspec.yaml

```yaml
dependencies:
  google_generative_ai: ^0.4.0
  flutter_dotenv: ^5.2.0  # For API key management
```

#### API Key Management

Store the key in a `.env` file (add to `.gitignore`):

```
GEMINI_API_KEY=your_key_here
```

Load it at app startup:

```dart
import 'package:flutter_dotenv/flutter_dotenv.dart';

Future<void> main() async {
  await dotenv.load(fileName: '.env');
  runApp(const MyApp());
}
```

#### Gemini Client Singleton

```dart
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'package:google_generative_ai/google_generative_ai.dart';

/// Provides a lazily-initialized, app-wide Gemini model instance.
///
/// Usage:
/// ```dart
/// final model = GeminiClient.instance.model;
/// ```
final class GeminiClient {
  GeminiClient._();
  static final instance = GeminiClient._();

  late final GenerativeModel model = _createModel();
  late final GenerativeModel visionModel = _createVisionModel();

  GenerativeModel _createModel() {
    final apiKey = dotenv.env['GEMINI_API_KEY'];
    if (apiKey == null || apiKey.isEmpty) {
      throw StateError('GEMINI_API_KEY not found in .env file.');
    }

    return GenerativeModel(
      model: 'gemini-1.5-flash',
      apiKey: apiKey,
      generationConfig: GenerationConfig(
        temperature: 0.7,
        topP: 0.95,
        topK: 40,
        maxOutputTokens: 2048,
      ),
      safetySettings: [
        SafetySetting(
          HarmCategory.harassment,
          HarmBlockThreshold.mediumAndAbove,
        ),
        SafetySetting(
          HarmCategory.hateSpeech,
          HarmBlockThreshold.mediumAndAbove,
        ),
      ],
    );
  }

  GenerativeModel _createVisionModel() {
    final apiKey = dotenv.env['GEMINI_API_KEY']!;
    return GenerativeModel(
      model: 'gemini-1.5-flash',
      apiKey: apiKey,
      generationConfig: GenerationConfig(
        temperature: 0.4,
        maxOutputTokens: 1024,
      ),
    );
  }
}
```

### Text Generation

```dart
import 'package:google_generative_ai/google_generative_ai.dart';

/// Simple one-shot text generation with Gemini.
Future<String> generateText(String prompt) async {
  final model = GeminiClient.instance.model;

  final content = [Content.text(prompt)];
  final response = await model.generateContent(content);

  final text = response.text;
  if (text == null || text.isEmpty) {
    throw StateError(
      'Gemini returned an empty response. '
      'Finish reason: ${response.candidates.firstOrNull?.finishReason}',
    );
  }
  return text;
}
```

### Chat Conversations

```dart
import 'package:google_generative_ai/google_generative_ai.dart';

/// Manages a multi-turn chat session with Gemini.
final class GeminiChatSession {
  GeminiChatSession({
    String? systemInstruction,
  }) : _chat = GeminiClient.instance.model.startChat(
          history: [
            if (systemInstruction != null)
              Content.text(systemInstruction),
          ],
        );

  final ChatSession _chat;

  /// Sends a user message and returns the model's reply.
  Future<String> sendMessage(String message) async {
    final response = await _chat.sendMessage(Content.text(message));
    return response.text ?? '';
  }

  /// Returns the full conversation history.
  List<Content> get history => _chat.history;
}
```

### Multimodal (Image + Text)

```dart
import 'dart:typed_data';

import 'package:google_generative_ai/google_generative_ai.dart';

/// Sends an image alongside a text prompt to Gemini's vision model.
///
/// [imageBytes] is the raw image data (JPEG/PNG).
/// [mimeType] must match the image format, e.g. `image/jpeg`.
Future<String> analyzeImage({
  required Uint8List imageBytes,
  required String prompt,
  String mimeType = 'image/jpeg',
}) async {
  final model = GeminiClient.instance.visionModel;

  final content = [
    Content.multi([
      TextPart(prompt),
      DataPart(mimeType, imageBytes),
    ]),
  ];

  final response = await model.generateContent(content);
  return response.text ?? '';
}

/// Analyzes multiple images in a single request.
Future<String> analyzeMultipleImages({
  required List<({Uint8List bytes, String mimeType})> images,
  required String prompt,
}) async {
  final model = GeminiClient.instance.visionModel;

  final parts = <Part>[
    TextPart(prompt),
    for (final image in images) DataPart(image.mimeType, image.bytes),
  ];

  final response = await model.generateContent([Content.multi(parts)]);
  return response.text ?? '';
}
```

### Streaming Responses

```dart
import 'package:google_generative_ai/google_generative_ai.dart';

/// Streams a text response token by token.
///
/// [onToken] is called for each chunk of text as it arrives.
/// Returns the complete concatenated response.
Future<String> streamTextGeneration({
  required String prompt,
  required void Function(String token) onToken,
}) async {
  final model = GeminiClient.instance.model;

  final content = [Content.text(prompt)];
  final stream = model.generateContentStream(content);

  final buffer = StringBuffer();

  await for (final chunk in stream) {
    final text = chunk.text;
    if (text != null) {
      buffer.write(text);
      onToken(text);
    }
  }

  return buffer.toString();
}

/// Streams a chat message response.
Future<String> streamChatMessage({
  required ChatSession chat,
  required String message,
  required void Function(String token) onToken,
}) async {
  final stream = chat.sendMessageStream(Content.text(message));

  final buffer = StringBuffer();

  await for (final chunk in stream) {
    final text = chunk.text;
    if (text != null) {
      buffer.write(text);
      onToken(text);
    }
  }

  return buffer.toString();
}
```

---

## OpenAI API Integration

### OpenAI Setup

#### pubspec.yaml

```yaml
dependencies:
  dio: ^5.7.0
  flutter_dotenv: ^5.2.0
```

Store the key in `.env`:

```
OPENAI_API_KEY=sk-your_key_here
```

#### OpenAI HTTP Client

```dart
import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';

/// A lean wrapper around the OpenAI REST API using Dio.
final class OpenAIClient {
  OpenAIClient._()
      : _dio = Dio(BaseOptions(
          baseUrl: 'https://api.openai.com/v1',
          headers: {
            'Authorization': 'Bearer ${dotenv.env['OPENAI_API_KEY']}',
            'Content-Type': 'application/json',
          },
          connectTimeout: const Duration(seconds: 30),
          receiveTimeout: const Duration(seconds: 120),
        ));

  static final instance = OpenAIClient._();

  final Dio _dio;

  /// Raw POST helper. Returns the decoded JSON body.
  Future<Map<String, dynamic>> post(
    String path,
    Map<String, dynamic> body,
  ) async {
    final response = await _dio.post<Map<String, dynamic>>(path, data: body);
    return response.data!;
  }

  /// Streaming POST helper. Returns a stream of server-sent event payloads.
  Future<Stream<Map<String, dynamic>>> postStream(
    String path,
    Map<String, dynamic> body,
  ) async {
    final response = await _dio.post<ResponseBody>(
      path,
      data: body,
      options: Options(responseType: ResponseType.stream),
    );

    return response.data!.stream
        .transform(utf8.decoder)
        .transform(const LineSplitter())
        .where((line) => line.startsWith('data: ') && line != 'data: [DONE]')
        .map((line) {
      final json = line.substring(6); // Remove 'data: ' prefix.
      return jsonDecode(json) as Map<String, dynamic>;
    });
  }
}
```

### Chat Completion

```dart
/// A single message in an OpenAI chat.
typedef ChatMessage = ({String role, String content});

/// Sends a chat completion request and returns the assistant's reply.
Future<String> openAiChatCompletion({
  required List<ChatMessage> messages,
  String model = 'gpt-4o-mini',
  double temperature = 0.7,
  int maxTokens = 2048,
}) async {
  final body = {
    'model': model,
    'messages': [
      for (final msg in messages) {'role': msg.role, 'content': msg.content},
    ],
    'temperature': temperature,
    'max_tokens': maxTokens,
  };

  final response = await OpenAIClient.instance.post('/chat/completions', body);

  final choices = response['choices'] as List<dynamic>;
  if (choices.isEmpty) {
    throw StateError('OpenAI returned no choices.');
  }

  final firstChoice = choices[0] as Map<String, dynamic>;
  final message = firstChoice['message'] as Map<String, dynamic>;
  return message['content'] as String;
}

/// Convenience: simple single-prompt completion.
Future<String> openAiQuickPrompt(String prompt) async {
  return openAiChatCompletion(
    messages: [(role: 'user', content: prompt)],
  );
}
```

### Streaming Chat Completion

```dart
/// Streams the assistant's reply token-by-token.
///
/// [onToken] fires for each text delta.
/// Returns the full assembled response.
Future<String> openAiStreamingChat({
  required List<ChatMessage> messages,
  required void Function(String token) onToken,
  String model = 'gpt-4o-mini',
  double temperature = 0.7,
  int maxTokens = 2048,
}) async {
  final body = {
    'model': model,
    'messages': [
      for (final msg in messages) {'role': msg.role, 'content': msg.content},
    ],
    'temperature': temperature,
    'max_tokens': maxTokens,
    'stream': true,
  };

  final stream = await OpenAIClient.instance.postStream(
    '/chat/completions',
    body,
  );

  final buffer = StringBuffer();

  await for (final event in stream) {
    final choices = event['choices'] as List<dynamic>?;
    if (choices == null || choices.isEmpty) continue;

    final delta =
        (choices[0] as Map<String, dynamic>)['delta'] as Map<String, dynamic>?;
    final content = delta?['content'] as String?;
    if (content != null) {
      buffer.write(content);
      onToken(content);
    }
  }

  return buffer.toString();
}
```

---

## Token Counting and Cost Management

```dart
/// Approximate token counter based on the GPT-family ~4 chars/token rule.
///
/// For production accuracy, use the `tiktoken` package server-side or the
/// OpenAI tokenizer API.
int estimateTokenCount(String text) {
  // Average English token is ~4 characters.
  return (text.length / 4).ceil();
}

/// Tracks cumulative token usage and estimated cost for a session.
final class TokenUsageTracker {
  int _promptTokens = 0;
  int _completionTokens = 0;

  void record({required int promptTokens, required int completionTokens}) {
    _promptTokens += promptTokens;
    _completionTokens += completionTokens;
  }

  int get totalPromptTokens => _promptTokens;
  int get totalCompletionTokens => _completionTokens;
  int get totalTokens => _promptTokens + _completionTokens;

  /// Estimates the session cost in USD.
  ///
  /// [promptPricePer1M] and [completionPricePer1M] are the per-million-token
  /// prices for the model being used. Defaults are for gpt-4o-mini
  /// (as of late 2024).
  double estimatedCostUsd({
    double promptPricePer1M = 0.15,
    double completionPricePer1M = 0.60,
  }) {
    return (_promptTokens * promptPricePer1M / 1e6) +
        (_completionTokens * completionPricePer1M / 1e6);
  }

  /// Extracts token usage from an OpenAI response and records it.
  void recordFromOpenAiResponse(Map<String, dynamic> response) {
    final usage = response['usage'] as Map<String, dynamic>?;
    if (usage == null) return;

    record(
      promptTokens: usage['prompt_tokens'] as int? ?? 0,
      completionTokens: usage['completion_tokens'] as int? ?? 0,
    );
  }

  void reset() {
    _promptTokens = 0;
    _completionTokens = 0;
  }

  @override
  String toString() =>
      'Tokens(prompt: $_promptTokens, completion: $_completionTokens, '
      'total: $totalTokens, cost: \$${estimatedCostUsd().toStringAsFixed(4)})';
}

/// Enforces a per-session budget by throwing when the limit is exceeded.
final class TokenBudget {
  TokenBudget({required this.maxTokens});

  final int maxTokens;
  final _tracker = TokenUsageTracker();

  TokenUsageTracker get tracker => _tracker;

  /// Call before each API request. Throws [StateError] if the budget would
  /// be exceeded.
  void checkBudget(int estimatedPromptTokens) {
    if (_tracker.totalTokens + estimatedPromptTokens > maxTokens) {
      throw StateError(
        'Token budget exceeded. '
        'Used: ${_tracker.totalTokens}, '
        'limit: $maxTokens, '
        'requested: $estimatedPromptTokens.',
      );
    }
  }

  void record({required int promptTokens, required int completionTokens}) {
    _tracker.record(
      promptTokens: promptTokens,
      completionTokens: completionTokens,
    );
  }
}
```

---

## Error Handling and Rate Limiting

```dart
import 'dart:async';
import 'dart:math' as math;

import 'package:dio/dio.dart';

/// Sealed hierarchy for AI service errors, enabling exhaustive handling.
sealed class AiServiceError {
  const AiServiceError(this.message);
  final String message;
}

final class RateLimitError extends AiServiceError {
  const RateLimitError({
    required super.message,
    required this.retryAfterSeconds,
  });
  final int retryAfterSeconds;
}

final class AuthenticationError extends AiServiceError {
  const AuthenticationError() : super('Invalid or missing API key.');
}

final class QuotaExceededError extends AiServiceError {
  const QuotaExceededError() : super('API quota exceeded.');
}

final class ServerError extends AiServiceError {
  const ServerError(super.message);
}

final class NetworkError extends AiServiceError {
  const NetworkError(super.message);
}

/// Maps a Dio error to a typed [AiServiceError].
AiServiceError classifyError(DioException error) {
  final statusCode = error.response?.statusCode;
  final body = error.response?.data;

  return switch (statusCode) {
    401 => const AuthenticationError(),
    429 => RateLimitError(
        message: 'Rate limited by the API.',
        retryAfterSeconds: _parseRetryAfter(error.response),
      ),
    402 || 403 => const QuotaExceededError(),
    >= 500 => ServerError('Server error: $statusCode'),
    _ when error.type == DioExceptionType.connectionTimeout ||
            error.type == DioExceptionType.receiveTimeout =>
      NetworkError('Network timeout: ${error.message}'),
    _ => NetworkError('Network error: ${error.message}'),
  };
}

int _parseRetryAfter(Response<dynamic>? response) {
  final header = response?.headers.value('retry-after');
  if (header != null) return int.tryParse(header) ?? 60;
  return 60;
}

/// Retries a future-returning [action] with exponential backoff.
///
/// Retries on rate-limit (429) and server (5xx) errors up to [maxRetries].
Future<T> withRetry<T>(
  Future<T> Function() action, {
  int maxRetries = 3,
  Duration initialDelay = const Duration(seconds: 1),
}) async {
  var attempt = 0;
  var delay = initialDelay;

  while (true) {
    try {
      return await action();
    } on DioException catch (e) {
      final error = classifyError(e);

      final shouldRetry = switch (error) {
        RateLimitError() => true,
        ServerError() => true,
        _ => false,
      };

      if (!shouldRetry || attempt >= maxRetries) {
        throw error;
      }

      final waitSeconds = switch (error) {
        RateLimitError(:final retryAfterSeconds) => retryAfterSeconds,
        _ => delay.inSeconds,
      };

      await Future<void>.delayed(Duration(seconds: waitSeconds));
      attempt++;
      delay *= 2;
    }
  }
}

/// Simple in-memory rate limiter for client-side throttling.
///
/// Ensures no more than [maxRequests] are made within [window].
final class RateLimiter {
  RateLimiter({
    required this.maxRequests,
    this.window = const Duration(minutes: 1),
  });

  final int maxRequests;
  final Duration window;
  final _timestamps = <DateTime>[];

  /// Returns `true` if a request can proceed, `false` if throttled.
  bool tryAcquire() {
    final now = DateTime.now();
    _timestamps.removeWhere((t) => now.difference(t) > window);

    if (_timestamps.length >= maxRequests) return false;

    _timestamps.add(now);
    return true;
  }

  /// Waits until a slot is available, then proceeds.
  Future<void> waitForSlot() async {
    while (!tryAcquire()) {
      await Future<void>.delayed(const Duration(milliseconds: 500));
    }
  }
}
```

---

## Caching AI Responses

```dart
import 'dart:convert';

import 'package:crypto/crypto.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// A time-aware cache for AI responses, backed by shared_preferences.
///
/// Caches are keyed by a SHA-256 hash of the prompt so that identical
/// requests return instantly without a network call.
final class AiResponseCache {
  AiResponseCache({this.defaultTtl = const Duration(hours: 24)});

  final Duration defaultTtl;

  /// Returns a cached response for [prompt], or `null` if not found or
  /// expired.
  Future<String?> get(String prompt) async {
    final prefs = await SharedPreferences.getInstance();
    final key = _cacheKey(prompt);
    final raw = prefs.getString(key);
    if (raw == null) return null;

    final entry = jsonDecode(raw) as Map<String, dynamic>;
    final expiresAt = DateTime.parse(entry['expiresAt'] as String);

    if (DateTime.now().isAfter(expiresAt)) {
      await prefs.remove(key);
      return null;
    }

    return entry['response'] as String;
  }

  /// Stores a response for [prompt] with an optional custom [ttl].
  Future<void> set(
    String prompt,
    String response, {
    Duration? ttl,
  }) async {
    final prefs = await SharedPreferences.getInstance();
    final key = _cacheKey(prompt);
    final expiresAt = DateTime.now().add(ttl ?? defaultTtl);

    final entry = jsonEncode({
      'response': response,
      'expiresAt': expiresAt.toIso8601String(),
    });

    await prefs.setString(key, entry);
  }

  /// Clears all cached AI responses.
  Future<void> clearAll() async {
    final prefs = await SharedPreferences.getInstance();
    final keys = prefs.getKeys().where((k) => k.startsWith('ai_cache_'));
    for (final key in keys) {
      await prefs.remove(key);
    }
  }

  String _cacheKey(String prompt) {
    final hash = sha256.convert(utf8.encode(prompt)).toString();
    return 'ai_cache_$hash';
  }
}

/// Wraps an AI call with transparent caching.
///
/// Example:
/// ```dart
/// final response = await cachedAiCall(
///   prompt: 'Translate "hello" to French',
///   cache: AiResponseCache(),
///   fetch: (p) => generateText(p),
/// );
/// ```
Future<String> cachedAiCall({
  required String prompt,
  required AiResponseCache cache,
  required Future<String> Function(String prompt) fetch,
  Duration? ttl,
}) async {
  final cached = await cache.get(prompt);
  if (cached != null) return cached;

  final response = await fetch(prompt);
  await cache.set(prompt, response, ttl: ttl);
  return response;
}
```

---

## Building a Chat Interface

A complete, production-quality chat UI that supports both Gemini and OpenAI,
streaming responses, and a typing indicator.

### Data Model

```dart
import 'package:flutter/foundation.dart';

enum ChatRole { user, assistant, system }

enum AiProvider { gemini, openai }

@immutable
final class ChatMessageModel {
  const ChatMessageModel({
    required this.role,
    required this.content,
    required this.timestamp,
    this.isStreaming = false,
  });

  final ChatRole role;
  final String content;
  final DateTime timestamp;
  final bool isStreaming;

  ChatMessageModel copyWith({
    String? content,
    bool? isStreaming,
  }) {
    return ChatMessageModel(
      role: role,
      content: content ?? this.content,
      timestamp: timestamp,
      isStreaming: isStreaming ?? this.isStreaming,
    );
  }
}
```

### Chat Service

```dart
import 'dart:async';

import 'package:google_generative_ai/google_generative_ai.dart';

/// Provider-agnostic chat service with streaming support.
final class AiChatService {
  AiChatService({
    required this.provider,
    this.systemPrompt,
  });

  final AiProvider provider;
  final String? systemPrompt;

  final _messages = <ChatMessageModel>[];
  ChatSession? _geminiChat;

  List<ChatMessageModel> get messages => List.unmodifiable(_messages);

  /// Initializes the chat session (call once before sending messages).
  void initialize() {
    if (provider == AiProvider.gemini) {
      _geminiChat = GeminiClient.instance.model.startChat(
        history: [
          if (systemPrompt != null) Content.text(systemPrompt!),
        ],
      );
    }

    if (systemPrompt != null) {
      _messages.add(ChatMessageModel(
        role: ChatRole.system,
        content: systemPrompt!,
        timestamp: DateTime.now(),
      ));
    }
  }

  /// Sends a message and streams the response.
  ///
  /// [onUpdate] is called whenever the assistant's partial response changes.
  /// Returns the final complete response.
  Future<String> sendMessage(
    String userMessage, {
    required void Function(ChatMessageModel updatedAssistant) onUpdate,
  }) async {
    // Add user message.
    _messages.add(ChatMessageModel(
      role: ChatRole.user,
      content: userMessage,
      timestamp: DateTime.now(),
    ));

    // Create a placeholder for the assistant's reply.
    var assistantMessage = ChatMessageModel(
      role: ChatRole.assistant,
      content: '',
      timestamp: DateTime.now(),
      isStreaming: true,
    );
    _messages.add(assistantMessage);

    final buffer = StringBuffer();

    switch (provider) {
      case AiProvider.gemini:
        final stream = _geminiChat!.sendMessageStream(
          Content.text(userMessage),
        );
        await for (final chunk in stream) {
          final text = chunk.text ?? '';
          buffer.write(text);
          assistantMessage = assistantMessage.copyWith(
            content: buffer.toString(),
          );
          _messages[_messages.length - 1] = assistantMessage;
          onUpdate(assistantMessage);
        }

      case AiProvider.openai:
        final openAiMessages = [
          if (systemPrompt != null)
            (role: 'system', content: systemPrompt!),
          for (final m in _messages.where((m) => m.role != ChatRole.system))
            (
              role: m.role == ChatRole.user ? 'user' : 'assistant',
              content: m.content,
            ),
        ];

        await openAiStreamingChat(
          messages: openAiMessages,
          onToken: (token) {
            buffer.write(token);
            assistantMessage = assistantMessage.copyWith(
              content: buffer.toString(),
            );
            _messages[_messages.length - 1] = assistantMessage;
            onUpdate(assistantMessage);
          },
        );
    }

    // Finalize streaming.
    assistantMessage = assistantMessage.copyWith(isStreaming: false);
    _messages[_messages.length - 1] = assistantMessage;
    onUpdate(assistantMessage);

    return buffer.toString();
  }
}
```

### Chat Screen Widget

```dart
import 'package:flutter/material.dart';

class AiChatScreen extends StatefulWidget {
  const AiChatScreen({
    super.key,
    this.provider = AiProvider.gemini,
    this.systemPrompt,
  });

  final AiProvider provider;
  final String? systemPrompt;

  @override
  State<AiChatScreen> createState() => _AiChatScreenState();
}

class _AiChatScreenState extends State<AiChatScreen> {
  late final AiChatService _chatService;
  final _controller = TextEditingController();
  final _scrollController = ScrollController();
  bool _isSending = false;

  @override
  void initState() {
    super.initState();
    _chatService = AiChatService(
      provider: widget.provider,
      systemPrompt: widget.systemPrompt,
    )..initialize();
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _isSending) return;

    _controller.clear();
    setState(() => _isSending = true);

    try {
      await _chatService.sendMessage(
        text,
        onUpdate: (_) {
          if (mounted) setState(() {});
          _scrollToBottom();
        },
      );
    } on AiServiceError catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('AI error: ${e.message}')),
        );
      }
    } finally {
      if (mounted) setState(() => _isSending = false);
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeOut,
        );
      }
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final visibleMessages = _chatService.messages
        .where((m) => m.role != ChatRole.system)
        .toList(growable: false);

    return Scaffold(
      appBar: AppBar(
        title: Text('Chat (${widget.provider.name})'),
      ),
      body: Column(
        children: [
          Expanded(
            child: ListView.builder(
              controller: _scrollController,
              padding: const EdgeInsets.all(16),
              itemCount: visibleMessages.length,
              itemBuilder: (context, index) {
                final message = visibleMessages[index];
                return _ChatBubble(message: message);
              },
            ),
          ),
          _ChatInputBar(
            controller: _controller,
            isSending: _isSending,
            onSend: _send,
          ),
        ],
      ),
    );
  }
}
```

### Chat Bubble Widget

```dart
class _ChatBubble extends StatelessWidget {
  const _ChatBubble({required this.message});

  final ChatMessageModel message;

  @override
  Widget build(BuildContext context) {
    final isUser = message.role == ChatRole.user;
    final theme = Theme.of(context);

    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
        constraints: BoxConstraints(
          maxWidth: MediaQuery.sizeOf(context).width * 0.75,
        ),
        decoration: BoxDecoration(
          color: isUser
              ? theme.colorScheme.primary
              : theme.colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(16).copyWith(
            bottomRight: isUser ? Radius.zero : null,
            bottomLeft: isUser ? null : Radius.zero,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SelectableText(
              message.content,
              style: TextStyle(
                color: isUser
                    ? theme.colorScheme.onPrimary
                    : theme.colorScheme.onSurface,
              ),
              semanticsLabel: '${isUser ? "You" : "AI"}: ${message.content}',
            ),
            if (message.isStreaming)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: isUser
                        ? theme.colorScheme.onPrimary
                        : theme.colorScheme.primary,
                    semanticsLabel: 'AI is typing',
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

### Chat Input Bar Widget

```dart
class _ChatInputBar extends StatelessWidget {
  const _ChatInputBar({
    required this.controller,
    required this.isSending,
    required this.onSend,
  });

  final TextEditingController controller;
  final bool isSending;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surface,
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.05),
              blurRadius: 4,
              offset: const Offset(0, -1),
            ),
          ],
        ),
        child: Row(
          children: [
            Expanded(
              child: TextField(
                controller: controller,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => onSend(),
                enabled: !isSending,
                maxLines: 4,
                minLines: 1,
                decoration: const InputDecoration(
                  hintText: 'Type a message...',
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.all(Radius.circular(24)),
                  ),
                  contentPadding: EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 10,
                  ),
                ),
              ),
            ),
            const SizedBox(width: 8),
            IconButton.filled(
              onPressed: isSending ? null : onSend,
              icon: isSending
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        semanticsLabel: 'Sending message',
                      ),
                    )
                  : const Icon(Icons.send),
              tooltip: 'Send message',
            ),
          ],
        ),
      ),
    );
  }
}
```
