# Custom Form Fields

## FormField\<T\> Fundamentals

`FormField<T>` is the base class for every field that participates in a `Form`.
`TextFormField` is simply a `FormField<String>` wrapping a `TextField`. To
create a custom field for any type, extend `FormField<T>` or use its constructor
directly.

Key responsibilities of a custom `FormField<T>`:

1. Call `state.didChange(newValue)` whenever the user modifies the value.
2. Read `state.value` for the current value.
3. Read `state.hasError` and `state.errorText` to display validation errors.
4. Support `validator`, `onSaved`, `autovalidateMode`, and `initialValue`.

---

## Custom Dropdown Form Field

A dropdown that integrates with `Form` validation and saving.

```dart
import 'package:flutter/material.dart';

class DropdownFormField<T> extends FormField<T> {
  DropdownFormField({
    super.key,
    required List<DropdownMenuItem<T>> items,
    super.initialValue,
    super.validator,
    super.onSaved,
    super.autovalidateMode,
    InputDecoration decoration = const InputDecoration(),
    ValueChanged<T?>? onChanged,
  }) : super(
          builder: (FormFieldState<T> state) {
            final effectiveDecoration = decoration.copyWith(
              errorText: state.hasError ? state.errorText : null,
            );
            return InputDecorator(
              decoration: effectiveDecoration,
              isEmpty: state.value == null,
              child: DropdownButtonHideUnderline(
                child: DropdownButton<T>(
                  value: state.value,
                  isDense: true,
                  isExpanded: true,
                  items: items,
                  onChanged: (T? value) {
                    state.didChange(value);
                    onChanged?.call(value);
                  },
                ),
              ),
            );
          },
        );
}
```

### Usage

```dart
DropdownFormField<String>(
  initialValue: null,
  decoration: const InputDecoration(labelText: 'Country'),
  items: const [
    DropdownMenuItem(value: 'us', child: Text('United States')),
    DropdownMenuItem(value: 'ca', child: Text('Canada')),
    DropdownMenuItem(value: 'uk', child: Text('United Kingdom')),
  ],
  validator: (value) {
    if (value == null) return 'Please select a country';
    return null;
  },
  onSaved: (value) => debugPrint('Country: $value'),
)
```

---

## Date Picker Form Field

Wraps a date picker dialog inside a `FormField<DateTime>`.

```dart
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

class DatePickerFormField extends FormField<DateTime> {
  DatePickerFormField({
    super.key,
    super.initialValue,
    super.validator,
    super.onSaved,
    super.autovalidateMode,
    InputDecoration decoration = const InputDecoration(),
    required DateTime firstDate,
    required DateTime lastDate,
    DateFormat? displayFormat,
  }) : super(
          builder: (FormFieldState<DateTime> state) {
            final format = displayFormat ?? DateFormat.yMMMd();
            final effectiveDecoration = decoration.copyWith(
              errorText: state.hasError ? state.errorText : null,
            );

            return GestureDetector(
              onTap: () async {
                final picked = await showDatePicker(
                  context: state.context,
                  initialDate: state.value ?? DateTime.now(),
                  firstDate: firstDate,
                  lastDate: lastDate,
                );
                if (picked != null) {
                  state.didChange(picked);
                }
              },
              child: InputDecorator(
                decoration: effectiveDecoration,
                isEmpty: state.value == null,
                child: Text(
                  state.value != null ? format.format(state.value!) : '',
                  style: Theme.of(state.context).textTheme.bodyLarge,
                ),
              ),
            );
          },
        );
}
```

### Usage

```dart
DatePickerFormField(
  decoration: const InputDecoration(
    labelText: 'Date of Birth',
    suffixIcon: Icon(Icons.calendar_today),
  ),
  firstDate: DateTime(1900),
  lastDate: DateTime.now(),
  validator: (value) {
    if (value == null) return 'Date of birth is required';
    final age = DateTime.now().difference(value).inDays ~/ 365;
    if (age < 18) return 'You must be at least 18 years old';
    return null;
  },
  onSaved: (value) => debugPrint('DOB: $value'),
)
```

---

## File Upload Form Field

A `FormField<String>` that stores a file path selected through a picker.

```dart
import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';

class FileUploadFormField extends FormField<String> {
  FileUploadFormField({
    super.key,
    super.validator,
    super.onSaved,
    super.autovalidateMode,
    InputDecoration decoration = const InputDecoration(),
    List<String>? allowedExtensions,
    FileType fileType = FileType.any,
  }) : super(
          initialValue: null,
          builder: (FormFieldState<String> state) {
            final effectiveDecoration = decoration.copyWith(
              errorText: state.hasError ? state.errorText : null,
            );
            final fileName = state.value != null
                ? state.value!.split('/').last
                : 'No file selected';

            return GestureDetector(
              onTap: () async {
                final result = await FilePicker.platform.pickFiles(
                  type: fileType,
                  allowedExtensions: allowedExtensions,
                );
                if (result != null && result.files.single.path != null) {
                  state.didChange(result.files.single.path!);
                }
              },
              child: InputDecorator(
                decoration: effectiveDecoration,
                child: Row(
                  children: [
                    const Icon(Icons.attach_file),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        fileName,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    if (state.value != null)
                      IconButton(
                        icon: const Icon(Icons.clear, size: 18),
                        onPressed: () => state.didChange(null),
                        padding: EdgeInsets.zero,
                        constraints: const BoxConstraints(),
                      ),
                  ],
                ),
              ),
            );
          },
        );
}
```

### Usage

```dart
FileUploadFormField(
  decoration: const InputDecoration(labelText: 'Resume (PDF)'),
  fileType: FileType.custom,
  allowedExtensions: ['pdf', 'doc', 'docx'],
  validator: (value) {
    if (value == null) return 'Please upload your resume';
    return null;
  },
  onSaved: (path) => debugPrint('File path: $path'),
)
```

---

## Rating Form Field

A star-rating input backed by `FormField<int>`.

```dart
import 'package:flutter/material.dart';

class RatingFormField extends FormField<int> {
  RatingFormField({
    super.key,
    super.validator,
    super.onSaved,
    super.autovalidateMode,
    int initialValue = 0,
    int maxRating = 5,
    double iconSize = 32,
    Color activeColor = Colors.amber,
    Color inactiveColor = Colors.grey,
  }) : super(
          initialValue: initialValue,
          builder: (FormFieldState<int> state) {
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: List.generate(maxRating, (index) {
                    final starIndex = index + 1;
                    return GestureDetector(
                      onTap: () => state.didChange(starIndex),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 2),
                        child: Icon(
                          starIndex <= (state.value ?? 0)
                              ? Icons.star_rounded
                              : Icons.star_outline_rounded,
                          size: iconSize,
                          color: starIndex <= (state.value ?? 0)
                              ? activeColor
                              : inactiveColor,
                        ),
                      ),
                    );
                  }),
                ),
                if (state.hasError)
                  Padding(
                    padding: const EdgeInsets.only(top: 4, left: 4),
                    child: Text(
                      state.errorText!,
                      style: TextStyle(
                        color: Theme.of(state.context).colorScheme.error,
                        fontSize: 12,
                      ),
                    ),
                  ),
              ],
            );
          },
        );
}
```

### Usage

```dart
RatingFormField(
  validator: (value) {
    if (value == null || value == 0) return 'Please provide a rating';
    return null;
  },
  onSaved: (value) => debugPrint('Rating: $value'),
)
```

---

## Checkbox Group Form Field

A `FormField<Set<T>>` that manages multiple checkboxes.

```dart
import 'package:flutter/material.dart';

class CheckboxOption<T> {
  const CheckboxOption({required this.value, required this.label});

  final T value;
  final String label;
}

class CheckboxGroupFormField<T> extends FormField<Set<T>> {
  CheckboxGroupFormField({
    super.key,
    required List<CheckboxOption<T>> options,
    Set<T>? initialValue,
    super.validator,
    super.onSaved,
    super.autovalidateMode,
    String? label,
  }) : super(
          initialValue: initialValue ?? <T>{},
          builder: (FormFieldState<Set<T>> state) {
            final currentSelection = state.value ?? <T>{};

            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (label != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text(
                      label,
                      style: Theme.of(state.context).textTheme.titleSmall,
                    ),
                  ),
                ...options.map((option) {
                  return CheckboxListTile(
                    title: Text(option.label),
                    value: currentSelection.contains(option.value),
                    contentPadding: EdgeInsets.zero,
                    dense: true,
                    controlAffinity: ListTileControlAffinity.leading,
                    onChanged: (checked) {
                      final updated = Set<T>.of(currentSelection);
                      if (checked == true) {
                        updated.add(option.value);
                      } else {
                        updated.remove(option.value);
                      }
                      state.didChange(updated);
                    },
                  );
                }),
                if (state.hasError)
                  Padding(
                    padding: const EdgeInsets.only(top: 4, left: 4),
                    child: Text(
                      state.errorText!,
                      style: TextStyle(
                        color: Theme.of(state.context).colorScheme.error,
                        fontSize: 12,
                      ),
                    ),
                  ),
              ],
            );
          },
        );
}
```

### Usage

```dart
CheckboxGroupFormField<String>(
  label: 'Interests',
  options: const [
    CheckboxOption(value: 'tech', label: 'Technology'),
    CheckboxOption(value: 'sports', label: 'Sports'),
    CheckboxOption(value: 'music', label: 'Music'),
    CheckboxOption(value: 'travel', label: 'Travel'),
  ],
  validator: (selected) {
    if (selected == null || selected.isEmpty) {
      return 'Select at least one interest';
    }
    return null;
  },
  onSaved: (selected) => debugPrint('Interests: $selected'),
)
```

---

## Input Formatters

Input formatters intercept and transform text before it reaches the controller.

### FilteringTextInputFormatter

```dart
// Allow only digits
TextFormField(
  decoration: const InputDecoration(labelText: 'Zip Code'),
  keyboardType: TextInputType.number,
  inputFormatters: [
    FilteringTextInputFormatter.digitsOnly,
    LengthLimitingTextInputFormatter(5),
  ],
)

// Allow only letters and spaces
TextFormField(
  decoration: const InputDecoration(labelText: 'Full Name'),
  inputFormatters: [
    FilteringTextInputFormatter.allow(RegExp(r'[a-zA-Z\s]')),
  ],
)

// Deny specific characters
TextFormField(
  decoration: const InputDecoration(labelText: 'Comment'),
  inputFormatters: [
    FilteringTextInputFormatter.deny(RegExp(r'[<>]')),
  ],
)
```

### Custom TextInputFormatter

```dart
import 'package:flutter/services.dart';

/// Converts input to uppercase as the user types.
class UpperCaseTextFormatter extends TextInputFormatter {
  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    return TextEditingValue(
      text: newValue.text.toUpperCase(),
      selection: newValue.selection,
    );
  }
}
```

### Phone Number Mask Formatter

Formats digits as `(XXX) XXX-XXXX` while the user types.

```dart
import 'package:flutter/services.dart';

class PhoneNumberFormatter extends TextInputFormatter {
  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final digits = newValue.text.replaceAll(RegExp(r'\D'), '');
    final buffer = StringBuffer();

    for (var i = 0; i < digits.length && i < 10; i++) {
      if (i == 0) buffer.write('(');
      if (i == 3) buffer.write(') ');
      if (i == 6) buffer.write('-');
      buffer.write(digits[i]);
    }

    final formatted = buffer.toString();
    return TextEditingValue(
      text: formatted,
      selection: TextSelection.collapsed(offset: formatted.length),
    );
  }
}
```

### Usage

```dart
TextFormField(
  decoration: const InputDecoration(
    labelText: 'Phone Number',
    hintText: '(555) 123-4567',
  ),
  keyboardType: TextInputType.phone,
  inputFormatters: [
    FilteringTextInputFormatter.digitsOnly,
    PhoneNumberFormatter(),
  ],
  validator: (value) {
    final digits = value?.replaceAll(RegExp(r'\D'), '') ?? '';
    if (digits.length != 10) return 'Enter a valid 10-digit phone number';
    return null;
  },
)
```

### Credit Card Number Formatter

Groups digits into blocks of four: `XXXX XXXX XXXX XXXX`.

```dart
import 'package:flutter/services.dart';

class CreditCardFormatter extends TextInputFormatter {
  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    final digits = newValue.text.replaceAll(RegExp(r'\D'), '');
    final buffer = StringBuffer();

    for (var i = 0; i < digits.length && i < 16; i++) {
      if (i > 0 && i % 4 == 0) buffer.write(' ');
      buffer.write(digits[i]);
    }

    final formatted = buffer.toString();
    return TextEditingValue(
      text: formatted,
      selection: TextSelection.collapsed(offset: formatted.length),
    );
  }
}
```

### Currency Formatter

Formats a number with two decimal places and a currency symbol.

```dart
import 'package:flutter/services.dart';

class CurrencyFormatter extends TextInputFormatter {
  CurrencyFormatter({this.symbol = '\$', this.decimalDigits = 2});

  final String symbol;
  final int decimalDigits;

  @override
  TextEditingValue formatEditUpdate(
    TextEditingValue oldValue,
    TextEditingValue newValue,
  ) {
    if (newValue.text.isEmpty) {
      return newValue;
    }

    final digits = newValue.text.replaceAll(RegExp(r'\D'), '');
    if (digits.isEmpty) return newValue.copyWith(text: '');

    final value = int.parse(digits);
    final divisor = _pow(10, decimalDigits);
    final integerPart = value ~/ divisor;
    final decimalPart = (value % divisor).toString().padLeft(decimalDigits, '0');

    // Add thousands separators
    final intStr = integerPart.toString();
    final withCommas = StringBuffer();
    for (var i = 0; i < intStr.length; i++) {
      if (i > 0 && (intStr.length - i) % 3 == 0) withCommas.write(',');
      withCommas.write(intStr[i]);
    }

    final formatted = '$symbol${withCommas.toString()}.$decimalPart';
    return TextEditingValue(
      text: formatted,
      selection: TextSelection.collapsed(offset: formatted.length),
    );
  }

  static int _pow(int base, int exponent) {
    var result = 1;
    for (var i = 0; i < exponent; i++) {
      result *= base;
    }
    return result;
  }
}
```

---

## Focus Management

### FocusNode Basics

Every form field can accept a `FocusNode` to programmatically request or release
focus.

```dart
class FocusDemoForm extends StatefulWidget {
  const FocusDemoForm({super.key});

  @override
  State<FocusDemoForm> createState() => _FocusDemoFormState();
}

class _FocusDemoFormState extends State<FocusDemoForm> {
  final _firstNameFocus = FocusNode();
  final _lastNameFocus = FocusNode();
  final _emailFocus = FocusNode();

  @override
  void dispose() {
    _firstNameFocus.dispose();
    _lastNameFocus.dispose();
    _emailFocus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        TextFormField(
          focusNode: _firstNameFocus,
          autofocus: true, // Focus this field when the form appears
          decoration: const InputDecoration(labelText: 'First Name'),
          textInputAction: TextInputAction.next,
          onFieldSubmitted: (_) => _lastNameFocus.requestFocus(),
        ),
        const SizedBox(height: 16),
        TextFormField(
          focusNode: _lastNameFocus,
          decoration: const InputDecoration(labelText: 'Last Name'),
          textInputAction: TextInputAction.next,
          onFieldSubmitted: (_) => _emailFocus.requestFocus(),
        ),
        const SizedBox(height: 16),
        TextFormField(
          focusNode: _emailFocus,
          decoration: const InputDecoration(labelText: 'Email'),
          keyboardType: TextInputType.emailAddress,
          textInputAction: TextInputAction.done,
          onFieldSubmitted: (_) => _emailFocus.unfocus(),
        ),
      ],
    );
  }
}
```

### FocusScope: Automatic Next-Field Navigation

`FocusScope` and `FocusTraversalGroup` can simplify navigation without
manually wiring each `FocusNode`.

```dart
class AutoFocusForm extends StatelessWidget {
  const AutoFocusForm({super.key});

  @override
  Widget build(BuildContext context) {
    return FocusTraversalGroup(
      policy: OrderedTraversalPolicy(),
      child: Column(
        children: [
          FocusTraversalOrder(
            order: const NumericFocusOrder(1),
            child: TextFormField(
              decoration: const InputDecoration(labelText: 'First Name'),
              textInputAction: TextInputAction.next,
            ),
          ),
          const SizedBox(height: 16),
          FocusTraversalOrder(
            order: const NumericFocusOrder(2),
            child: TextFormField(
              decoration: const InputDecoration(labelText: 'Last Name'),
              textInputAction: TextInputAction.next,
            ),
          ),
          const SizedBox(height: 16),
          FocusTraversalOrder(
            order: const NumericFocusOrder(3),
            child: TextFormField(
              decoration: const InputDecoration(labelText: 'Email'),
              textInputAction: TextInputAction.done,
            ),
          ),
        ],
      ),
    );
  }
}
```

### Move Focus to the Next Field Programmatically

```dart
// Inside a StatefulWidget:
void _moveToNextField(BuildContext context) {
  FocusScope.of(context).nextFocus();
}

// Usage in a TextFormField:
TextFormField(
  textInputAction: TextInputAction.next,
  onFieldSubmitted: (_) => _moveToNextField(context),
)
```

### Focus Change Listener

Listen for focus changes to trigger side effects (e.g., showing a helper or
running validation on blur).

```dart
class FocusAwareField extends StatefulWidget {
  const FocusAwareField({super.key});

  @override
  State<FocusAwareField> createState() => _FocusAwareFieldState();
}

class _FocusAwareFieldState extends State<FocusAwareField> {
  final _focusNode = FocusNode();
  var _showHelper = false;

  @override
  void initState() {
    super.initState();
    _focusNode.addListener(_onFocusChange);
  }

  void _onFocusChange() {
    setState(() => _showHelper = _focusNode.hasFocus);
  }

  @override
  void dispose() {
    _focusNode.removeListener(_onFocusChange);
    _focusNode.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextFormField(
          focusNode: _focusNode,
          decoration: const InputDecoration(labelText: 'Password'),
          obscureText: true,
        ),
        if (_showHelper)
          const Padding(
            padding: EdgeInsets.only(top: 4, left: 12),
            child: Text(
              'Must include uppercase, lowercase, number, and symbol',
              style: TextStyle(color: Colors.grey, fontSize: 12),
            ),
          ),
      ],
    );
  }
}
```

### Dismiss Keyboard on Tap Outside

```dart
// Wrap your Scaffold body with GestureDetector
GestureDetector(
  onTap: () => FocusScope.of(context).unfocus(),
  child: Form(
    key: _formKey,
    child: /* form content */,
  ),
)
```
