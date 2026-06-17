# Drift (SQLite) -- Comprehensive Guide

Drift is a reactive persistence library for Flutter and Dart built on top of
SQLite. It provides compile-time verified SQL, type-safe queries, and automatic
schema migrations.

## Table of Contents

1. [Project Setup](#project-setup)
2. [Table Definitions](#table-definitions)
3. [Database Class](#database-class)
4. [CRUD Operations](#crud-operations)
5. [Complex Queries](#complex-queries)
6. [Stream-Based Reactive Queries](#stream-based-reactive-queries)
7. [Database Migrations](#database-migrations)
8. [DAOs (Data Access Objects)](#daos-data-access-objects)
9. [Transactions](#transactions)
10. [Custom SQL Expressions](#custom-sql-expressions)
11. [Type Converters](#type-converters)
12. [Testing Databases](#testing-databases)

---

## Project Setup

### pubspec.yaml

```yaml
dependencies:
  drift: ^2.24.0
  sqlite3_flutter_libs: ^0.5.28  # ships SQLite for Android/iOS/macOS
  path_provider: ^2.1.5
  path: ^1.9.0

dev_dependencies:
  drift_dev: ^2.24.0
  build_runner: ^2.4.14
```

### File structure

```
lib/
  data/
    database.dart          # AppDatabase definition
    tables/
      todos.dart           # Table classes
      categories.dart
    daos/
      todo_dao.dart        # DAO classes
    converters/
      priority_converter.dart
```

---

## Table Definitions

```dart
// lib/data/tables/categories.dart
import 'package:drift/drift.dart';

class Categories extends Table {
  IntColumn get id => integer().autoIncrement()();
  TextColumn get name => text().withLength(min: 1, max: 100)();
  TextColumn get color => text().withDefault(const Constant('#FF000000'))();
  DateTimeColumn get createdAt =>
      dateTime().withDefault(currentDateAndTime)();
}
```

```dart
// lib/data/tables/todos.dart
import 'package:drift/drift.dart';

enum Priority { low, medium, high, critical }

class Todos extends Table {
  IntColumn get id => integer().autoIncrement()();
  TextColumn get title => text().withLength(min: 1, max: 200)();
  TextColumn get body => text().nullable()();
  BoolColumn get completed => boolean().withDefault(const Constant(false))();
  IntColumn get priority => intEnum<Priority>()();
  IntColumn get categoryId =>
      integer().nullable().references(Categories, #id)();
  DateTimeColumn get dueDate => dateTime().nullable()();
  DateTimeColumn get createdAt =>
      dateTime().withDefault(currentDateAndTime)();
  DateTimeColumn get updatedAt =>
      dateTime().withDefault(currentDateAndTime)();
}
```

---

## Database Class

```dart
// lib/data/database.dart
import 'dart:io';

import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'tables/categories.dart';
import 'tables/todos.dart';
import 'daos/todo_dao.dart';
import 'daos/category_dao.dart';

part 'database.g.dart';

@DriftDatabase(
  tables: [Todos, Categories],
  daos: [TodoDao, CategoryDao],
)
class AppDatabase extends _$AppDatabase {
  AppDatabase._(super.e);

  /// Production constructor -- opens a file-backed database.
  factory AppDatabase() {
    return AppDatabase._(_openConnection());
  }

  /// Test constructor -- accepts any [QueryExecutor] (e.g. in-memory).
  factory AppDatabase.forTesting(QueryExecutor executor) {
    return AppDatabase._(executor);
  }

  @override
  int get schemaVersion => 3;

  @override
  MigrationStrategy get migration => MigrationStrategy(
        onCreate: (m) async {
          await m.createAll();
        },
        onUpgrade: (m, from, to) async {
          await customStatement('PRAGMA foreign_keys = OFF');
          try {
            if (from < 2) {
              await m.addColumn(todos, todos.dueDate);
            }
            if (from < 3) {
              await m.addColumn(todos, todos.updatedAt);
              await m.addColumn(categories, categories.color);
            }
          } finally {
            await customStatement('PRAGMA foreign_keys = ON');
          }
        },
        beforeOpen: (details) async {
          await customStatement('PRAGMA foreign_keys = ON');
        },
      );
}

LazyDatabase _openConnection() {
  return LazyDatabase(() async {
    final dir = await getApplicationDocumentsDirectory();
    final file = File(p.join(dir.path, 'app_database.sqlite'));
    return NativeDatabase.createInBackground(file, logStatements: true);
  });
}
```

Run code generation after defining tables and database:

```bash
dart run build_runner build --delete-conflicting-outputs
```

---

## CRUD Operations

### Create

```dart
Future<int> insertTodo(TodosCompanion entry) {
  return into(todos).insert(entry);
}

// Usage:
final id = await db.insertTodo(
  TodosCompanion.insert(
    title: 'Buy groceries',
    body: const Value('Milk, eggs, bread'),
    priority: Priority.medium,
    categoryId: const Value(1),
    dueDate: Value(DateTime.now().add(const Duration(days: 1))),
  ),
);
```

### Read

```dart
Future<List<Todo>> getAllTodos() {
  return select(todos).get();
}

Future<Todo> getTodoById(int id) {
  return (select(todos)..where((t) => t.id.equals(id))).getSingle();
}

Future<List<Todo>> getIncompleteTodos() {
  return (select(todos)
        ..where((t) => t.completed.equals(false))
        ..orderBy([
          (t) => OrderingTerm.desc(t.priority),
          (t) => OrderingTerm.asc(t.dueDate),
        ]))
      .get();
}
```

### Update

```dart
Future<bool> updateTodo(Todo todo) {
  return update(todos).replace(todo);
}

Future<int> markCompleted(int id) {
  return (update(todos)..where((t) => t.id.equals(id))).write(
    TodosCompanion(
      completed: const Value(true),
      updatedAt: Value(DateTime.now()),
    ),
  );
}
```

### Delete

```dart
Future<int> deleteTodoById(int id) {
  return (delete(todos)..where((t) => t.id.equals(id))).go();
}

Future<int> deleteCompletedTodos() {
  return (delete(todos)..where((t) => t.completed.equals(true))).go();
}
```

---

## Complex Queries

### Joins

```dart
/// Returns each todo together with its category name.
Future<List<({Todo todo, String? categoryName})>> todosWithCategory() async {
  final query = select(todos).join([
    leftOuterJoin(categories, categories.id.equalsExp(todos.categoryId)),
  ]);

  final rows = await query.get();

  return rows.map((row) {
    final todo = row.readTable(todos);
    final category = row.readTableOrNull(categories);
    return (todo: todo, categoryName: category?.name);
  }).toList();
}
```

### Aggregations

```dart
/// Count of todos per priority.
Future<List<({Priority priority, int count})>> todoCountByPriority() async {
  final priorityCol = todos.priority;
  final countExpr = todos.id.count();

  final query = selectOnly(todos)
    ..addColumns([priorityCol, countExpr])
    ..groupBy([priorityCol]);

  final rows = await query.get();

  return rows.map((row) {
    return (
      priority: Priority.values[row.read(priorityCol)!],
      count: row.read(countExpr)!,
    );
  }).toList();
}
```

### GROUP BY with HAVING

```dart
/// Categories that have more than 5 incomplete todos.
Future<List<({String categoryName, int incompleteCount})>>
    busyCategories() async {
  final countExpr = todos.id.count();

  final query = selectOnly(todos).join([
    innerJoin(categories, categories.id.equalsExp(todos.categoryId)),
  ])
    ..addColumns([categories.name, countExpr])
    ..where(todos.completed.equals(false))
    ..groupBy([categories.name])
    ..having(countExpr.isBiggerThanValue(5));

  final rows = await query.get();

  return rows.map((row) {
    return (
      categoryName: row.read(categories.name)!,
      incompleteCount: row.read(countExpr)!,
    );
  }).toList();
}
```

---

## Stream-Based Reactive Queries

Drift can turn any query into a `Stream` that re-emits whenever the underlying
tables change.

```dart
/// Watches all incomplete todos, sorted by due date.
Stream<List<Todo>> watchIncompleteTodos() {
  return (select(todos)
        ..where((t) => t.completed.equals(false))
        ..orderBy([(t) => OrderingTerm.asc(t.dueDate)]))
      .watch();
}

/// Watches a single todo by id. Emits null if deleted.
Stream<Todo?> watchTodoById(int id) {
  return (select(todos)..where((t) => t.id.equals(id)))
      .watchSingleOrNull();
}

/// Watches a join query -- todos with their category.
Stream<List<({Todo todo, String? categoryName})>>
    watchTodosWithCategory() {
  final query = select(todos).join([
    leftOuterJoin(categories, categories.id.equalsExp(todos.categoryId)),
  ]);

  return query.watch().map((rows) {
    return rows.map((row) {
      final todo = row.readTable(todos);
      final category = row.readTableOrNull(categories);
      return (todo: todo, categoryName: category?.name);
    }).toList();
  });
}
```

### Using Streams in Flutter Widgets

```dart
class TodoListScreen extends StatelessWidget {
  final AppDatabase db;

  const TodoListScreen({super.key, required this.db});

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<List<Todo>>(
      stream: db.todoDao.watchIncompleteTodos(),
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }

        final todos = snapshot.data ?? [];
        if (todos.isEmpty) {
          return const Center(child: Text('All done!'));
        }

        return ListView.builder(
          itemCount: todos.length,
          itemBuilder: (context, index) {
            final todo = todos[index];
            return ListTile(
              title: Text(todo.title),
              subtitle: Text(todo.body ?? ''),
              trailing: Checkbox(
                value: todo.completed,
                onChanged: (_) => db.todoDao.markCompleted(todo.id),
              ),
            );
          },
        );
      },
    );
  }
}
```

---

## Database Migrations

### Step-by-step Migration Pattern

```dart
@override
int get schemaVersion => 5;

@override
MigrationStrategy get migration => MigrationStrategy(
      onCreate: (m) async {
        await m.createAll();
      },
      onUpgrade: (m, from, to) async {
        // Disable foreign keys during migration to allow table recreation.
        await customStatement('PRAGMA foreign_keys = OFF');

        try {
          for (var target = from + 1; target <= to; target++) {
            switch (target) {
              case 2:
                await m.addColumn(todos, todos.dueDate);
              case 3:
                await m.addColumn(todos, todos.updatedAt);
                await m.addColumn(categories, categories.color);
              case 4:
                await m.createTable(tags);
                await m.createTable(todoTags);
              case 5:
                // Rename column: create new, copy data, drop old.
                await m.alterTable(TableMigration(todos));
            }
          }
        } finally {
          await customStatement('PRAGMA foreign_keys = ON');
        }
      },
      beforeOpen: (details) async {
        await customStatement('PRAGMA foreign_keys = ON');

        if (details.wasCreated) {
          // Seed default data on first launch.
          await into(categories).insert(
            CategoriesCompanion.insert(name: 'Personal'),
          );
          await into(categories).insert(
            CategoriesCompanion.insert(name: 'Work'),
          );
        }
      },
    );
```

### Verifying Migrations with Tests

```dart
import 'package:drift_dev/api/migrations.dart';
import 'package:test/test.dart';

import 'generated/schema.dart'; // generated by drift_dev

void main() {
  late SchemaVerifier verifier;

  setUpAll(() {
    verifier = SchemaVerifier(GeneratedHelper());
  });

  test('upgrade from v1 to v5', () async {
    final connection = await verifier.startAt(1);
    final db = AppDatabase.forTesting(connection);
    await verifier.migrateAndValidate(db, 5);
    await db.close();
  });

  for (var from = 1; from < 5; from++) {
    test('upgrade from v$from to v${from + 1}', () async {
      final connection = await verifier.startAt(from);
      final db = AppDatabase.forTesting(connection);
      await verifier.migrateAndValidate(db, from + 1);
      await db.close();
    });
  }
}
```

Generate schema snapshots for migration testing:

```bash
dart run drift_dev schema dump lib/data/database.dart drift_schemas/
dart run drift_dev schema generate drift_schemas/ test/generated/
```

---

## DAOs (Data Access Objects)

DAOs encapsulate table-specific queries so the database class stays lean.

```dart
// lib/data/daos/todo_dao.dart
import 'package:drift/drift.dart';

import '../database.dart';
import '../tables/todos.dart';
import '../tables/categories.dart';

part 'todo_dao.g.dart';

@DriftAccessor(tables: [Todos, Categories])
class TodoDao extends DatabaseAccessor<AppDatabase> with _$TodoDaoMixin {
  TodoDao(super.db);

  // --- Create ---
  Future<int> createTodo(TodosCompanion entry) {
    return into(todos).insert(entry);
  }

  Future<void> createMultiple(List<TodosCompanion> entries) {
    return batch((b) => b.insertAll(todos, entries));
  }

  // --- Read ---
  Future<List<Todo>> allTodos() => select(todos).get();

  Stream<List<Todo>> watchIncompleteTodos() {
    return (select(todos)
          ..where((t) => t.completed.equals(false))
          ..orderBy([(t) => OrderingTerm.asc(t.dueDate)]))
        .watch();
  }

  Future<List<Todo>> todosByCategory(int categoryId) {
    return (select(todos)..where((t) => t.categoryId.equals(categoryId)))
        .get();
  }

  Stream<List<({Todo todo, Category? category})>> watchTodosWithCategory() {
    final query = select(todos).join([
      leftOuterJoin(categories, categories.id.equalsExp(todos.categoryId)),
    ]);

    return query.watch().map((rows) {
      return rows.map((row) {
        return (
          todo: row.readTable(todos),
          category: row.readTableOrNull(categories),
        );
      }).toList();
    });
  }

  // --- Update ---
  Future<int> markCompleted(int id) {
    return (update(todos)..where((t) => t.id.equals(id))).write(
      TodosCompanion(
        completed: const Value(true),
        updatedAt: Value(DateTime.now()),
      ),
    );
  }

  Future<bool> updateTodo(Todo entry) => update(todos).replace(entry);

  // --- Delete ---
  Future<int> deleteTodo(int id) {
    return (delete(todos)..where((t) => t.id.equals(id))).go();
  }

  Future<int> clearCompleted() {
    return (delete(todos)..where((t) => t.completed.equals(true))).go();
  }

  // --- Aggregation ---
  Future<int> incompleteCount() async {
    final countExpr = todos.id.count();
    final query = selectOnly(todos)
      ..addColumns([countExpr])
      ..where(todos.completed.equals(false));
    final row = await query.getSingle();
    return row.read(countExpr)!;
  }
}
```

```dart
// lib/data/daos/category_dao.dart
import 'package:drift/drift.dart';

import '../database.dart';
import '../tables/categories.dart';

part 'category_dao.g.dart';

@DriftAccessor(tables: [Categories])
class CategoryDao extends DatabaseAccessor<AppDatabase>
    with _$CategoryDaoMixin {
  CategoryDao(super.db);

  Future<List<Category>> allCategories() => select(categories).get();

  Stream<List<Category>> watchAllCategories() => select(categories).watch();

  Future<int> createCategory(CategoriesCompanion entry) {
    return into(categories).insert(entry);
  }

  Future<int> deleteCategory(int id) {
    return (delete(categories)..where((c) => c.id.equals(id))).go();
  }
}
```

---

## Transactions

```dart
/// Moves all todos from one category to another, then deletes the source
/// category. Rolls back entirely if any step fails.
Future<void> mergeCategories({
  required int sourceId,
  required int targetId,
}) async {
  await transaction(() async {
    // Move all todos from source to target.
    await (update(todos)..where((t) => t.categoryId.equals(sourceId))).write(
      TodosCompanion(categoryId: Value(targetId)),
    );

    // Delete the now-empty source category.
    await (delete(categories)..where((c) => c.id.equals(sourceId))).go();
  });
}

/// Batch insert with transaction for performance.
Future<void> seedDatabase(List<TodosCompanion> entries) async {
  await batch((b) {
    b.insertAll(todos, entries);
  });
}
```

---

## Custom SQL Expressions

```dart
/// Full-text search on todo title and body using SQLite LIKE.
Future<List<Todo>> searchTodos(String query) {
  final pattern = '%$query%';
  return (select(todos)
        ..where(
          (t) =>
              t.title.like(pattern) | t.body.like(pattern),
        )
        ..orderBy([(t) => OrderingTerm.desc(t.createdAt)]))
      .get();
}

/// Uses a custom expression to compute days until due.
Future<List<({Todo todo, int daysUntilDue})>> todosWithDaysLeft() async {
  final daysLeft = todos.dueDate
      .julianday
      .roundToInt() -
      currentDateAndTime.julianday.roundToInt();

  final query = selectOnly(todos)
    ..addColumns([...todos.$columns, daysLeft])
    ..where(todos.dueDate.isNotNull() & todos.completed.equals(false))
    ..orderBy([OrderingTerm.asc(daysLeft)]);

  final rows = await query.get();

  return rows.map((row) {
    return (
      todo: row.readTable(todos),
      daysUntilDue: row.read(daysLeft) ?? 0,
    );
  }).toList();
}

/// Raw custom statement for operations Drift does not natively support.
Future<void> vacuum() async {
  await customStatement('VACUUM');
}
```

---

## Type Converters

### Enum Converter (built-in)

Drift supports `intEnum<T>()` out of the box (see table definition above).
For text-based enum storage:

```dart
// lib/data/converters/priority_converter.dart
import 'package:drift/drift.dart';
import '../tables/todos.dart';

class PriorityTextConverter extends TypeConverter<Priority, String> {
  const PriorityTextConverter();

  @override
  Priority fromSql(String fromDb) {
    return Priority.values.firstWhere(
      (e) => e.name == fromDb,
      orElse: () => Priority.medium,
    );
  }

  @override
  String toSql(Priority value) => value.name;
}
```

Usage in a table:

```dart
class Todos extends Table {
  // ...
  TextColumn get priorityText =>
      text().map(const PriorityTextConverter()).withDefault(
            Constant(const PriorityTextConverter().toSql(Priority.medium)),
          )();
}
```

### Custom Type Converter -- JSON Map

```dart
import 'dart:convert';
import 'package:drift/drift.dart';

class JsonMapConverter extends TypeConverter<Map<String, dynamic>, String> {
  const JsonMapConverter();

  @override
  Map<String, dynamic> fromSql(String fromDb) {
    return jsonDecode(fromDb) as Map<String, dynamic>;
  }

  @override
  String toSql(Map<String, dynamic> value) => jsonEncode(value);
}
```

Usage:

```dart
class UserProfiles extends Table {
  IntColumn get id => integer().autoIncrement()();
  TextColumn get name => text()();
  TextColumn get metadata =>
      text().map(const JsonMapConverter()).withDefault(const Constant('{}'))();
}
```

### Custom Type Converter -- Duration

```dart
import 'package:drift/drift.dart';

class DurationConverter extends TypeConverter<Duration, int> {
  const DurationConverter();

  @override
  Duration fromSql(int fromDb) => Duration(milliseconds: fromDb);

  @override
  int toSql(Duration value) => value.inMilliseconds;
}
```

---

## Testing Databases

### In-Memory Database for Unit Tests

```dart
import 'package:drift/native.dart';
import 'package:test/test.dart';

import 'package:my_app/data/database.dart';

void main() {
  late AppDatabase db;

  setUp(() {
    db = AppDatabase.forTesting(NativeDatabase.memory());
  });

  tearDown(() async {
    await db.close();
  });

  test('insert and retrieve a todo', () async {
    final id = await db.todoDao.createTodo(
      TodosCompanion.insert(
        title: 'Test todo',
        priority: Priority.high,
      ),
    );

    final todo = await (db.select(db.todos)
          ..where((t) => t.id.equals(id)))
        .getSingle();

    expect(todo.title, 'Test todo');
    expect(todo.priority, Priority.high);
    expect(todo.completed, false);
  });

  test('markCompleted updates the completed flag', () async {
    final id = await db.todoDao.createTodo(
      TodosCompanion.insert(title: 'Buy milk', priority: Priority.low),
    );

    await db.todoDao.markCompleted(id);

    final todo = await (db.select(db.todos)
          ..where((t) => t.id.equals(id)))
        .getSingle();

    expect(todo.completed, true);
  });

  test('clearCompleted removes only completed todos', () async {
    await db.todoDao.createTodo(
      TodosCompanion.insert(title: 'Done', priority: Priority.low),
    );
    await db.todoDao.createTodo(
      TodosCompanion.insert(title: 'Not done', priority: Priority.low),
    );

    // Complete the first one.
    await db.todoDao.markCompleted(1);
    await db.todoDao.clearCompleted();

    final remaining = await db.todoDao.allTodos();
    expect(remaining, hasLength(1));
    expect(remaining.first.title, 'Not done');
  });

  test('watch emits updates reactively', () async {
    final stream = db.todoDao.watchIncompleteTodos();

    // First emission: empty.
    expectLater(
      stream,
      emitsInOrder([
        isEmpty,
        hasLength(1),
        isEmpty,
      ]),
    );

    final id = await db.todoDao.createTodo(
      TodosCompanion.insert(title: 'Reactive', priority: Priority.medium),
    );

    await db.todoDao.markCompleted(id);
  });
}
```

### Testing Migrations

See the [Database Migrations](#database-migrations) section for the
`SchemaVerifier` approach that validates every version-to-version upgrade path.
