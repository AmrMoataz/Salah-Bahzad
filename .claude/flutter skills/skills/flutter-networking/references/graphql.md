# GraphQL with Flutter

Complete guide to integrating GraphQL into a Flutter application using `graphql_flutter`, including client configuration, queries, mutations, subscriptions, caching, fragments, and code generation.

---

## Table of Contents

1. [Installation](#installation)
2. [GraphQLClient Configuration](#graphqlclient-configuration)
3. [Query Widget and useQuery Hook](#query-widget-and-usequery-hook)
4. [Mutation Widget and useMutation Hook](#mutation-widget-and-usemutation-hook)
5. [Subscriptions (WebSocket)](#subscriptions-websocket)
6. [Cache Policies](#cache-policies)
7. [Fragments and Variables](#fragments-and-variables)
8. [Error Handling](#error-handling)
9. [Code Generation with graphql_codegen](#code-generation-with-graphql_codegen)

---

## Installation

```yaml
# pubspec.yaml
dependencies:
  graphql_flutter: ^5.2.0
  connectivity_plus: ^6.0.0   # optional, for network-aware cache

dev_dependencies:
  graphql_codegen: ^0.14.0     # optional, type-safe codegen
  build_runner: ^2.4.0
```

---

## GraphQLClient Configuration

### Basic Setup

```dart
import 'package:flutter/material.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Initialize Hive for caching (graphql_flutter uses Hive internally)
  await initHiveForFlutter();

  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    final client = ValueNotifier<GraphQLClient>(
      createGraphQLClient(
        httpUri: 'https://api.example.com/graphql',
        wsUri: 'wss://api.example.com/graphql',
      ),
    );

    return GraphQLProvider(
      client: client,
      child: const MaterialApp(home: HomeScreen()),
    );
  }
}
```

### Client Factory with Auth and Link Composition

```dart
import 'package:graphql_flutter/graphql_flutter.dart';

/// Creates a [GraphQLClient] with HTTP for queries/mutations and
/// WebSocket for subscriptions.
///
/// [getToken] is a callback that returns the current bearer token.
GraphQLClient createGraphQLClient({
  required String httpUri,
  required String wsUri,
  Future<String?> Function()? getToken,
}) {
  // ── Auth link ───────────────────────────────────────────────────────
  final authLink = AuthLink(getToken: () async {
    final token = await getToken?.call();
    return token != null ? 'Bearer $token' : null;
  });

  // ── HTTP link ───────────────────────────────────────────────────────
  final httpLink = HttpLink(httpUri);

  // ── WebSocket link ──────────────────────────────────────────────────
  final wsLink = WebSocketLink(
    wsUri,
    config: SocketClientConfig(
      autoReconnect: true,
      inactivityTimeout: const Duration(seconds: 30),
      initialPayload: () async {
        final token = await getToken?.call();
        return token != null ? {'Authorization': 'Bearer $token'} : null;
      },
    ),
    subProtocol: GraphQLProtocol.graphqlTransportWs,
  );

  // ── Split link: subscriptions go over WS, everything else over HTTP ─
  final link = Link.split(
    (request) => request.isSubscription,
    wsLink,
    authLink.concat(httpLink),
  );

  // ── Cache ───────────────────────────────────────────────────────────
  final cache = GraphQLCache(
    store: HiveStore(),
    typePolicies: {
      'User': TypePolicy(
        keyFields: {'id': true},
      ),
      'Post': TypePolicy(
        keyFields: {'id': true},
      ),
    },
  );

  return GraphQLClient(link: link, cache: cache);
}
```

---

## Query Widget and useQuery Hook

### Widget-Based Approach

```dart
import 'package:flutter/material.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

const fetchUsersQuery = r'''
  query FetchUsers($first: Int!, $after: String) {
    users(first: $first, after: $after) {
      edges {
        node {
          id
          name
          email
          avatarUrl
        }
      }
      pageInfo {
        hasNextPage
        endCursor
      }
    }
  }
''';

class UserListScreen extends StatelessWidget {
  const UserListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Query(
      options: QueryOptions(
        document: gql(fetchUsersQuery),
        variables: const {'first': 20},
        fetchPolicy: FetchPolicy.cacheAndNetwork,
        pollInterval: const Duration(minutes: 5),
      ),
      builder: (
        QueryResult result, {
        VoidCallback? refetch,
        FetchMore? fetchMore,
      }) {
        if (result.isLoading && result.data == null) {
          return const Center(child: CircularProgressIndicator());
        }

        if (result.hasException) {
          return Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('Error: ${result.exception.toString()}'),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: refetch,
                  child: const Text('Retry'),
                ),
              ],
            ),
          );
        }

        final edges =
            (result.data?['users']?['edges'] as List<Object?>?) ?? [];
        final pageInfo =
            result.data?['users']?['pageInfo'] as Map<String, Object?>?;

        return RefreshIndicator(
          onRefresh: () async => refetch?.call(),
          child: ListView.builder(
            itemCount: edges.length + 1,
            itemBuilder: (context, index) {
              if (index == edges.length) {
                final hasNextPage =
                    pageInfo?['hasNextPage'] as bool? ?? false;
                if (!hasNextPage) return const SizedBox.shrink();

                return Center(
                  child: TextButton(
                    onPressed: () {
                      fetchMore?.call(
                        FetchMoreOptions(
                          variables: {
                            'after': pageInfo?['endCursor'],
                          },
                          updateQuery: (previous, fetchMoreResult) {
                            if (fetchMoreResult == null) return previous;
                            final prevEdges = (previous?['users']?['edges']
                                    as List<Object?>?) ??
                                [];
                            final newEdges =
                                (fetchMoreResult['users']?['edges']
                                        as List<Object?>?) ??
                                    [];
                            return {
                              'users': {
                                'edges': [...prevEdges, ...newEdges],
                                'pageInfo':
                                    fetchMoreResult['users']?['pageInfo'],
                              },
                            };
                          },
                        ),
                      );
                    },
                    child: const Text('Load more'),
                  ),
                );
              }

              final node =
                  (edges[index] as Map<String, Object?>?)?['node']
                      as Map<String, Object?>?;
              return ListTile(
                title: Text(node?['name'] as String? ?? ''),
                subtitle: Text(node?['email'] as String? ?? ''),
              );
            },
          ),
        );
      },
    );
  }
}
```

### Hook-Based Approach (with `flutter_hooks`)

```dart
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

class UserListHookScreen extends HookWidget {
  const UserListHookScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final result = useQuery(
      QueryOptions(
        document: gql(fetchUsersQuery),
        variables: const {'first': 20},
        fetchPolicy: FetchPolicy.cacheAndNetwork,
      ),
    );

    final queryResult = result.result;

    if (queryResult.isLoading && queryResult.data == null) {
      return const Center(child: CircularProgressIndicator());
    }

    if (queryResult.hasException) {
      return Center(
        child: Text('Error: ${queryResult.exception}'),
      );
    }

    final edges =
        (queryResult.data?['users']?['edges'] as List<Object?>?) ?? [];

    return ListView.builder(
      itemCount: edges.length,
      itemBuilder: (context, index) {
        final node =
            (edges[index] as Map<String, Object?>?)?['node']
                as Map<String, Object?>?;
        return ListTile(
          title: Text(node?['name'] as String? ?? ''),
          subtitle: Text(node?['email'] as String? ?? ''),
        );
      },
    );
  }
}
```

---

## Mutation Widget and useMutation Hook

### Widget-Based Mutation

```dart
import 'package:flutter/material.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

const createUserMutation = r'''
  mutation CreateUser($input: CreateUserInput!) {
    createUser(input: $input) {
      id
      name
      email
    }
  }
''';

class CreateUserForm extends StatefulWidget {
  const CreateUserForm({super.key});

  @override
  State<CreateUserForm> createState() => _CreateUserFormState();
}

class _CreateUserFormState extends State<CreateUserForm> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Mutation(
      options: MutationOptions(
        document: gql(createUserMutation),
        onCompleted: (data) {
          if (data != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('User created')),
            );
            Navigator.of(context).pop();
          }
        },
        onError: (error) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Error: $error')),
          );
        },
        // Update the cache after mutation
        update: (cache, result) {
          if (result?.data == null) return;

          final newUser = result!.data!['createUser'] as Map<String, Object?>;

          // Read the existing users query from cache
          final request = QueryOptions(
            document: gql(fetchUsersQuery),
            variables: const {'first': 20},
          ).asRequest;

          final existing = cache.readQuery(request);
          if (existing == null) return;

          final existingEdges =
              (existing['users']?['edges'] as List<Object?>?) ?? [];

          cache.writeQuery(
            request,
            data: {
              'users': {
                ...existing['users'] as Map<String, Object?>,
                'edges': [
                  {'node': newUser, '__typename': 'UserEdge'},
                  ...existingEdges,
                ],
              },
            },
          );
        },
      ),
      builder: (runMutation, result) {
        final isLoading = result?.isLoading ?? false;

        return Form(
          key: _formKey,
          child: Column(
            children: [
              TextFormField(
                controller: _nameController,
                decoration: const InputDecoration(labelText: 'Name'),
                validator: (v) =>
                    (v == null || v.isEmpty) ? 'Name is required' : null,
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _emailController,
                decoration: const InputDecoration(labelText: 'Email'),
                validator: (v) =>
                    (v == null || v.isEmpty) ? 'Email is required' : null,
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: isLoading
                    ? null
                    : () {
                        if (_formKey.currentState!.validate()) {
                          runMutation({
                            'input': {
                              'name': _nameController.text,
                              'email': _emailController.text,
                            },
                          });
                        }
                      },
                child: isLoading
                    ? const SizedBox(
                        width: 20,
                        height: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Text('Create User'),
              ),
            ],
          ),
        );
      },
    );
  }
}
```

### Hook-Based Mutation

```dart
import 'package:flutter/material.dart';
import 'package:flutter_hooks/flutter_hooks.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

class CreateUserHookForm extends HookWidget {
  const CreateUserHookForm({super.key});

  @override
  Widget build(BuildContext context) {
    final nameController = useTextEditingController();
    final emailController = useTextEditingController();

    final mutation = useMutation(
      MutationOptions(document: gql(createUserMutation)),
    );

    return Column(
      children: [
        TextField(
          controller: nameController,
          decoration: const InputDecoration(labelText: 'Name'),
        ),
        const SizedBox(height: 16),
        TextField(
          controller: emailController,
          decoration: const InputDecoration(labelText: 'Email'),
        ),
        const SizedBox(height: 24),
        FilledButton(
          onPressed: mutation.result.isLoading
              ? null
              : () {
                  mutation.runMutation({
                    'input': {
                      'name': nameController.text,
                      'email': emailController.text,
                    },
                  });
                },
          child: mutation.result.isLoading
              ? const SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Text('Create User'),
        ),
      ],
    );
  }
}
```

---

## Subscriptions (WebSocket)

```dart
import 'package:flutter/material.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

const onMessageSubscription = r'''
  subscription OnMessage($channelId: ID!) {
    messageAdded(channelId: $channelId) {
      id
      text
      createdAt
      author {
        id
        name
      }
    }
  }
''';

class ChatScreen extends StatelessWidget {
  const ChatScreen({required this.channelId, super.key});

  final String channelId;

  @override
  Widget build(BuildContext context) {
    return Subscription(
      options: SubscriptionOptions(
        document: gql(onMessageSubscription),
        variables: {'channelId': channelId},
      ),
      builder: (result) {
        if (result.isLoading) {
          return const Center(child: Text('Connecting...'));
        }

        if (result.hasException) {
          return Center(
            child: Text('Subscription error: ${result.exception}'),
          );
        }

        final message = result.data?['messageAdded'] as Map<String, Object?>?;
        if (message == null) {
          return const Center(child: Text('Waiting for messages...'));
        }

        final author = message['author'] as Map<String, Object?>?;

        return ListTile(
          title: Text(author?['name'] as String? ?? 'Unknown'),
          subtitle: Text(message['text'] as String? ?? ''),
          trailing: Text(message['createdAt'] as String? ?? ''),
        );
      },
    );
  }
}
```

### Collecting Subscription Events into a List

```dart
import 'package:flutter/material.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

class ChatMessageList extends StatefulWidget {
  const ChatMessageList({required this.channelId, super.key});

  final String channelId;

  @override
  State<ChatMessageList> createState() => _ChatMessageListState();
}

class _ChatMessageListState extends State<ChatMessageList> {
  final List<Map<String, Object?>> _messages = [];

  @override
  Widget build(BuildContext context) {
    return Subscription(
      options: SubscriptionOptions(
        document: gql(onMessageSubscription),
        variables: {'channelId': widget.channelId},
      ),
      builder: (result) {
        if (result.data != null) {
          final message =
              result.data!['messageAdded'] as Map<String, Object?>;
          // Avoid duplicates on re-render
          final id = message['id'] as String?;
          if (_messages.every((m) => m['id'] != id)) {
            _messages.insert(0, message);
          }
        }

        if (_messages.isEmpty) {
          return const Center(child: Text('No messages yet'));
        }

        return ListView.builder(
          reverse: true,
          itemCount: _messages.length,
          itemBuilder: (context, index) {
            final msg = _messages[index];
            final author = msg['author'] as Map<String, Object?>?;
            return ListTile(
              title: Text(author?['name'] as String? ?? 'Unknown'),
              subtitle: Text(msg['text'] as String? ?? ''),
            );
          },
        );
      },
    );
  }
}
```

---

## Cache Policies

`graphql_flutter` supports several `FetchPolicy` values that control how queries interact with the cache.

| Policy | Behavior |
|---|---|
| `FetchPolicy.cacheFirst` | Return cached data if available, otherwise fetch from network. Best for data that rarely changes. |
| `FetchPolicy.cacheAndNetwork` | Return cached data immediately, then update in background from network. Best for most UI screens. |
| `FetchPolicy.networkOnly` | Always fetch from network, then write to cache. Skips reading cache. |
| `FetchPolicy.cacheOnly` | Only read from cache, never fetch. Useful for offline mode. |
| `FetchPolicy.noCache` | Fetch from network, do not read or write cache. For sensitive data. |

### Per-Query Cache Policy

```dart
QueryOptions(
  document: gql(fetchUsersQuery),
  variables: const {'first': 20},
  fetchPolicy: FetchPolicy.cacheAndNetwork,
)
```

### Programmatic Cache Reads and Writes

```dart
import 'package:graphql_flutter/graphql_flutter.dart';

/// Reads a single user from the normalized cache by ID.
Map<String, Object?>? readUserFromCache(
  GraphQLClient client, {
  required String userId,
}) {
  final fragment = gql(r'''
    fragment UserFields on User {
      id
      name
      email
      avatarUrl
    }
  ''');

  return client.cache.readFragment(
    FragmentRequest(
      fragment: Fragment(document: fragment),
      idFields: {'id': userId, '__typename': 'User'},
    ),
  );
}

/// Evicts a single entity from the normalized cache.
void evictUser(GraphQLClient client, {required String userId}) {
  client.cache.evict('User:$userId');
  client.cache.gc(); // Garbage-collect unreachable refs
}
```

---

## Fragments and Variables

### Defining Reusable Fragments

```dart
const userFieldsFragment = r'''
  fragment UserFields on User {
    id
    name
    email
    avatarUrl
    createdAt
  }
''';

const postFieldsFragment = r'''
  fragment PostFields on Post {
    id
    title
    body
    publishedAt
    author {
      ...UserFields
    }
  }
''';

const fetchPostsQuery = '''
  $userFieldsFragment
  $postFieldsFragment

  query FetchPosts(\$first: Int!, \$category: Category) {
    posts(first: \$first, category: \$category) {
      edges {
        node {
          ...PostFields
        }
      }
    }
  }
''';
```

### Using Variables

```dart
import 'package:graphql_flutter/graphql_flutter.dart';

QueryOptions buildPostsQuery({
  int first = 10,
  String? category,
}) {
  return QueryOptions(
    document: gql(fetchPostsQuery),
    variables: {
      'first': first,
      if (category != null) 'category': category,
    },
    fetchPolicy: FetchPolicy.cacheAndNetwork,
  );
}
```

---

## Error Handling

### Typed Error Extraction

```dart
import 'package:graphql_flutter/graphql_flutter.dart';

/// Sealed hierarchy for GraphQL operation failures.
sealed class GqlFailure {
  const GqlFailure({required this.message});
  final String message;
}

final class GqlNetworkFailure extends GqlFailure {
  const GqlNetworkFailure({required super.message});
}

final class GqlServerErrors extends GqlFailure {
  const GqlServerErrors({
    required super.message,
    required this.errors,
  });
  final List<GraphQLError> errors;
}

final class GqlUnknownFailure extends GqlFailure {
  const GqlUnknownFailure({required super.message});
}

/// Extracts a typed failure from a [QueryResult].
GqlFailure? extractFailure(QueryResult result) {
  final exception = result.exception;
  if (exception == null) return null;

  final linkException = exception.linkException;
  if (linkException is NetworkException) {
    return GqlNetworkFailure(
      message: linkException.message ?? 'Network error',
    );
  }

  final graphqlErrors = exception.graphqlErrors;
  if (graphqlErrors.isNotEmpty) {
    return GqlServerErrors(
      message: graphqlErrors.map((e) => e.message).join(', '),
      errors: graphqlErrors,
    );
  }

  return GqlUnknownFailure(message: exception.toString());
}
```

### Handling Specific Error Codes

```dart
import 'package:graphql_flutter/graphql_flutter.dart';

/// Checks whether a GraphQL result contains an UNAUTHENTICATED error code.
bool isUnauthenticated(QueryResult result) {
  final errors = result.exception?.graphqlErrors ?? [];
  return errors.any((e) {
    final code = e.extensions?['code'];
    return code == 'UNAUTHENTICATED';
  });
}

/// Checks for a specific field-level validation error.
String? fieldValidationError(QueryResult result, String fieldName) {
  final errors = result.exception?.graphqlErrors ?? [];
  for (final error in errors) {
    final validationErrors =
        error.extensions?['validationErrors'] as Map<String, Object?>?;
    if (validationErrors != null && validationErrors.containsKey(fieldName)) {
      return validationErrors[fieldName] as String?;
    }
  }
  return null;
}
```

---

## Code Generation with graphql_codegen

`graphql_codegen` generates type-safe Dart classes from `.graphql` schema and operation files. This eliminates stringly-typed access to query results.

### Setup

```yaml
# pubspec.yaml
dev_dependencies:
  build_runner: ^2.4.0
  graphql_codegen: ^0.14.0

# build.yaml
targets:
  $default:
    builders:
      graphql_codegen:
        options:
          clients:
            - graphql_flutter
          scalarMapping:
            DateTime:
              type: DateTime
              fromJsonFunctionName: DateTime.parse
              toJsonFunctionName: toIso8601String
            JSON:
              type: Map<String, dynamic>
```

### Schema File

```graphql
# lib/graphql/schema.graphql
type User {
  id: ID!
  name: String!
  email: String!
  avatarUrl: String
}

type Post {
  id: ID!
  title: String!
  body: String!
  publishedAt: DateTime!
  author: User!
}

type Query {
  users(first: Int!, after: String): UserConnection!
  post(id: ID!): Post
}

type Mutation {
  createUser(input: CreateUserInput!): User!
}

input CreateUserInput {
  name: String!
  email: String!
}

type UserConnection {
  edges: [UserEdge!]!
  pageInfo: PageInfo!
}

type UserEdge {
  node: User!
}

type PageInfo {
  hasNextPage: Boolean!
  endCursor: String
}
```

### Operation File

```graphql
# lib/graphql/fetch_users.graphql
query FetchUsers($first: Int!, $after: String) {
  users(first: $first, after: $after) {
    edges {
      node {
        id
        name
        email
        avatarUrl
      }
    }
    pageInfo {
      hasNextPage
      endCursor
    }
  }
}
```

### Running Code Generation

```bash
dart run build_runner build --delete-conflicting-outputs
```

### Using Generated Code

```dart
import 'package:flutter/material.dart';
import 'package:graphql_flutter/graphql_flutter.dart';

// Generated import (path depends on your structure)
import 'graphql/fetch_users.graphql.dart';

class TypeSafeUserList extends StatelessWidget {
  const TypeSafeUserList({super.key});

  @override
  Widget build(BuildContext context) {
    return Query(
      options: QueryOptions(
        document: documentNodeQueryFetchUsers,
        variables: const FetchUsersArguments(first: 20).toJson(),
      ),
      builder: (result, {refetch, fetchMore}) {
        if (result.isLoading && result.data == null) {
          return const Center(child: CircularProgressIndicator());
        }

        if (result.hasException) {
          return Center(child: Text('Error: ${result.exception}'));
        }

        final parsed = FetchUsers$Query.fromJson(result.data!);
        final users = parsed.users.edges.map((e) => e.node).toList();

        return ListView.builder(
          itemCount: users.length,
          itemBuilder: (context, index) {
            final user = users[index];
            return ListTile(
              title: Text(user.name),
              subtitle: Text(user.email),
            );
          },
        );
      },
    );
  }
}
```
