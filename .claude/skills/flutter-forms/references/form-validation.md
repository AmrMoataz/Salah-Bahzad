# Form Validation

## The Form Widget and GlobalKey\<FormState\>

Every Flutter form starts with a `Form` widget and a `GlobalKey<FormState>` that
provides access to validation, saving, and resetting.

```dart
import 'package:flutter/material.dart';

class SignUpForm extends StatefulWidget {
  const SignUpForm({super.key});

  @override
  State<SignUpForm> createState() => _SignUpFormState();
}

class _SignUpFormState extends State<SignUpForm> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  void _submit() {
    if (_formKey.currentState!.validate()) {
      _formKey.currentState!.save();
      // All fields are valid -- proceed with saved values
    }
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextFormField(
            controller: _nameController,
            decoration: const InputDecoration(labelText: 'Full Name'),
            validator: (value) {
              if (value == null || value.trim().isEmpty) {
                return 'Name is required';
              }
              return null;
            },
            onSaved: (value) {
              // Called when _formKey.currentState!.save() is invoked
              debugPrint('Saved name: $value');
            },
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _emailController,
            decoration: const InputDecoration(labelText: 'Email'),
            keyboardType: TextInputType.emailAddress,
            validator: (value) {
              if (value == null || value.trim().isEmpty) {
                return 'Email is required';
              }
              final emailRegex = RegExp(r'^[\w\-.]+@([\w\-]+\.)+[\w\-]{2,4}$');
              if (!emailRegex.hasMatch(value.trim())) {
                return 'Enter a valid email address';
              }
              return null;
            },
          ),
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _submit,
            child: const Text('Sign Up'),
          ),
        ],
      ),
    );
  }
}
```

---

## TextFormField In Depth

`TextFormField` wraps a `TextField` inside a `FormField<String>`. Key properties:

| Property | Purpose |
|---|---|
| `controller` | Read/write the text value programmatically |
| `validator` | Returns an error string or `null` if valid |
| `onSaved` | Called when `FormState.save()` is invoked |
| `onChanged` | Called on every keystroke |
| `autovalidateMode` | Controls when validation runs automatically |
| `decoration` | `InputDecoration` for label, hint, prefix/suffix, error styling |
| `keyboardType` | Keyboard layout hint (email, number, phone, etc.) |
| `textInputAction` | Action button on the keyboard (next, done, search) |
| `obscureText` | Mask input for passwords |
| `inputFormatters` | List of `TextInputFormatter` to restrict or transform input |

---

## Built-in Validators and Custom Validator Functions

Keep validators as standalone pure functions so they are unit-testable and reusable.

```dart
typedef Validator = String? Function(String? value);

/// Composes multiple validators. Returns the first error or null.
Validator composeValidators(List<Validator> validators) {
  return (String? value) {
    for (final validator in validators) {
      final error = validator(value);
      if (error != null) return error;
    }
    return null;
  };
}

/// Value must not be null or blank.
Validator requiredValidator({String message = 'This field is required'}) {
  return (String? value) {
    if (value == null || value.trim().isEmpty) return message;
    return null;
  };
}

/// Value must be at least [min] characters.
Validator minLengthValidator(int min, {String? message}) {
  return (String? value) {
    if (value != null && value.trim().length < min) {
      return message ?? 'Must be at least $min characters';
    }
    return null;
  };
}

/// Value must not exceed [max] characters.
Validator maxLengthValidator(int max, {String? message}) {
  return (String? value) {
    if (value != null && value.trim().length > max) {
      return message ?? 'Must be at most $max characters';
    }
    return null;
  };
}

/// Value must match [regex].
Validator patternValidator(RegExp regex, {required String message}) {
  return (String? value) {
    if (value != null && value.isNotEmpty && !regex.hasMatch(value)) {
      return message;
    }
    return null;
  };
}

/// Value must be a valid email address.
Validator emailValidator({String message = 'Enter a valid email address'}) {
  final regex = RegExp(r'^[\w\-.]+@([\w\-]+\.)+[\w\-]{2,4}$');
  return patternValidator(regex, message: message);
}
```

### Using Composed Validators

```dart
TextFormField(
  decoration: const InputDecoration(labelText: 'Username'),
  validator: composeValidators([
    requiredValidator(message: 'Username is required'),
    minLengthValidator(3, message: 'Username must be at least 3 characters'),
    maxLengthValidator(20, message: 'Username must be at most 20 characters'),
    patternValidator(
      RegExp(r'^[a-zA-Z0-9_]+$'),
      message: 'Only letters, numbers, and underscores allowed',
    ),
  ]),
)
```

---

## Real-time Validation (autovalidateMode)

| Mode | Behavior |
|---|---|
| `AutovalidateMode.disabled` | Validate only when `FormState.validate()` is called (default) |
| `AutovalidateMode.onUserInteraction` | Validate after the user edits or focuses/blurs the field |
| `AutovalidateMode.always` | Validate on every build -- **avoid** in most cases |

### Recommended Pattern: Validate on Interaction After First Submit

```dart
class LoginForm extends StatefulWidget {
  const LoginForm({super.key});

  @override
  State<LoginForm> createState() => _LoginFormState();
}

class _LoginFormState extends State<LoginForm> {
  final _formKey = GlobalKey<FormState>();
  var _autovalidateMode = AutovalidateMode.disabled;

  void _submit() {
    if (_formKey.currentState!.validate()) {
      _formKey.currentState!.save();
      // proceed
    } else {
      // Switch to real-time validation so errors clear as the user fixes them
      setState(() => _autovalidateMode = AutovalidateMode.onUserInteraction);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      autovalidateMode: _autovalidateMode,
      child: Column(
        children: [
          TextFormField(
            decoration: const InputDecoration(labelText: 'Email'),
            keyboardType: TextInputType.emailAddress,
            validator: composeValidators([
              requiredValidator(message: 'Email is required'),
              emailValidator(),
            ]),
          ),
          TextFormField(
            decoration: const InputDecoration(labelText: 'Password'),
            obscureText: true,
            validator: composeValidators([
              requiredValidator(message: 'Password is required'),
              minLengthValidator(8, message: 'Password must be at least 8 characters'),
            ]),
          ),
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _submit,
            child: const Text('Log In'),
          ),
        ],
      ),
    );
  }
}
```

---

## Cross-field Validation

Cross-field validators compare values from two or more fields. Because each
`TextFormField.validator` only receives its own value, you reference sibling
controllers directly.

```dart
class PasswordResetForm extends StatefulWidget {
  const PasswordResetForm({super.key, required this.onSubmit});

  final ValueChanged<String> onSubmit;

  @override
  State<PasswordResetForm> createState() => _PasswordResetFormState();
}

class _PasswordResetFormState extends State<PasswordResetForm> {
  final _formKey = GlobalKey<FormState>();
  final _passwordController = TextEditingController();
  final _confirmController = TextEditingController();

  @override
  void dispose() {
    _passwordController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  void _submit() {
    if (_formKey.currentState!.validate()) {
      widget.onSubmit(_passwordController.text);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      child: Column(
        children: [
          TextFormField(
            controller: _passwordController,
            decoration: const InputDecoration(labelText: 'New Password'),
            obscureText: true,
            validator: composeValidators([
              requiredValidator(message: 'Password is required'),
              minLengthValidator(8),
            ]),
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _confirmController,
            decoration: const InputDecoration(labelText: 'Confirm Password'),
            obscureText: true,
            validator: (value) {
              if (value == null || value.isEmpty) {
                return 'Please confirm your password';
              }
              if (value != _passwordController.text) {
                return 'Passwords do not match';
              }
              return null;
            },
          ),
          const SizedBox(height: 24),
          FilledButton(onPressed: _submit, child: const Text('Reset Password')),
        ],
      ),
    );
  }
}
```

---

## Async Validation (Checking Email Availability)

Async validators cannot return a `Future` from `TextFormField.validator`. The
standard pattern is to validate asynchronously on field change and store the
result in local state, then reference that state inside the synchronous
`validator`.

```dart
import 'dart:async';
import 'package:flutter/material.dart';

class EmailAvailabilityField extends StatefulWidget {
  const EmailAvailabilityField({super.key, required this.controller});

  final TextEditingController controller;

  @override
  State<EmailAvailabilityField> createState() => _EmailAvailabilityFieldState();
}

class _EmailAvailabilityFieldState extends State<EmailAvailabilityField> {
  Timer? _debounce;
  bool _isChecking = false;
  String? _availabilityError;

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<void> _checkAvailability(String email) async {
    if (email.isEmpty) return;

    setState(() {
      _isChecking = true;
      _availabilityError = null;
    });

    try {
      // Replace with your actual API call
      final isTaken = await _fakeCheckEmail(email);
      if (!mounted) return;
      setState(() {
        _availabilityError = isTaken ? 'This email is already registered' : null;
        _isChecking = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _availabilityError = 'Could not verify email availability';
        _isChecking = false;
      });
    }
  }

  /// Simulates an API call with a 500ms delay.
  Future<bool> _fakeCheckEmail(String email) async {
    await Future<void>.delayed(const Duration(milliseconds: 500));
    const takenEmails = {'alice@example.com', 'bob@example.com'};
    return takenEmails.contains(email.toLowerCase());
  }

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: widget.controller,
      decoration: InputDecoration(
        labelText: 'Email',
        suffixIcon: _isChecking
            ? const SizedBox(
                width: 20,
                height: 20,
                child: Padding(
                  padding: EdgeInsets.all(12),
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              )
            : null,
      ),
      keyboardType: TextInputType.emailAddress,
      onChanged: (value) {
        _debounce?.cancel();
        _debounce = Timer(const Duration(milliseconds: 600), () {
          _checkAvailability(value.trim());
        });
      },
      validator: (value) {
        if (value == null || value.trim().isEmpty) {
          return 'Email is required';
        }
        final emailRegex = RegExp(r'^[\w\-.]+@([\w\-]+\.)+[\w\-]{2,4}$');
        if (!emailRegex.hasMatch(value.trim())) {
          return 'Enter a valid email address';
        }
        // Include the async result in synchronous validation
        if (_availabilityError != null) {
          return _availabilityError;
        }
        return null;
      },
    );
  }
}
```

---

## Form Submission Pattern with Loading State

Prevent double submissions and show feedback while the request is in flight.

```dart
class ContactForm extends StatefulWidget {
  const ContactForm({super.key});

  @override
  State<ContactForm> createState() => _ContactFormState();
}

class _ContactFormState extends State<ContactForm> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _messageController = TextEditingController();
  var _autovalidateMode = AutovalidateMode.disabled;
  var _isSubmitting = false;
  String? _submitError;

  @override
  void dispose() {
    _nameController.dispose();
    _messageController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    // Prevent double tap
    if (_isSubmitting) return;

    if (!_formKey.currentState!.validate()) {
      setState(() => _autovalidateMode = AutovalidateMode.onUserInteraction);
      return;
    }

    _formKey.currentState!.save();

    setState(() {
      _isSubmitting = true;
      _submitError = null;
    });

    try {
      // Replace with your actual API call
      await Future<void>.delayed(const Duration(seconds: 2));

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Message sent successfully!')),
      );
      _formKey.currentState!.reset();
      _nameController.clear();
      _messageController.clear();
      setState(() => _autovalidateMode = AutovalidateMode.disabled);
    } catch (e) {
      if (!mounted) return;
      setState(() => _submitError = 'Failed to send. Please try again.');
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      autovalidateMode: _autovalidateMode,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextFormField(
            controller: _nameController,
            decoration: const InputDecoration(labelText: 'Your Name'),
            enabled: !_isSubmitting,
            validator: requiredValidator(message: 'Name is required'),
          ),
          const SizedBox(height: 16),
          TextFormField(
            controller: _messageController,
            decoration: const InputDecoration(
              labelText: 'Message',
              alignLabelWithHint: true,
            ),
            maxLines: 5,
            enabled: !_isSubmitting,
            validator: composeValidators([
              requiredValidator(message: 'Message is required'),
              minLengthValidator(10, message: 'Message must be at least 10 characters'),
            ]),
          ),
          if (_submitError != null) ...[
            const SizedBox(height: 8),
            Text(_submitError!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          const SizedBox(height: 24),
          FilledButton(
            onPressed: _isSubmitting ? null : _submit,
            child: _isSubmitting
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                  )
                : const Text('Send Message'),
          ),
        ],
      ),
    );
  }
}
```

---

## Error Display Patterns

### Inline Error Below the Field (Default)

The default `TextFormField` renders the error returned by `validator` below the
input using `InputDecoration.errorText`. This happens automatically.

### Custom Error Widget

```dart
TextFormField(
  decoration: const InputDecoration(
    labelText: 'Email',
    // Provide an empty error style to hide the default error text
    errorStyle: TextStyle(height: 0, fontSize: 0),
  ),
  validator: emailValidator(),
  builder: (FormFieldState<String> state) {
    // This approach lets you render errors anywhere
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextField(
          decoration: InputDecoration(
            labelText: 'Email',
            border: OutlineInputBorder(
              borderSide: BorderSide(
                color: state.hasError ? Colors.red : Colors.grey,
              ),
            ),
          ),
          onChanged: (value) => state.didChange(value),
        ),
        if (state.hasError)
          Padding(
            padding: const EdgeInsets.only(top: 4, left: 12),
            child: Row(
              children: [
                const Icon(Icons.error_outline, size: 16, color: Colors.red),
                const SizedBox(width: 4),
                Flexible(
                  child: Text(
                    state.errorText!,
                    style: const TextStyle(color: Colors.red, fontSize: 12),
                  ),
                ),
              ],
            ),
          ),
      ],
    );
  },
)
```

### Form-Level Error Summary

Display all errors at the top of the form after a failed submit.

```dart
class FormErrorSummary extends StatelessWidget {
  const FormErrorSummary({super.key, required this.errors});

  final List<String> errors;

  @override
  Widget build(BuildContext context) {
    if (errors.isEmpty) return const SizedBox.shrink();

    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Please fix the following errors:',
            style: TextStyle(
              fontWeight: FontWeight.bold,
              color: colorScheme.onErrorContainer,
            ),
          ),
          const SizedBox(height: 8),
          for (final error in errors)
            Padding(
              padding: const EdgeInsets.only(bottom: 4),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.error_outline, size: 16, color: colorScheme.onErrorContainer),
                  const SizedBox(width: 8),
                  Flexible(
                    child: Text(error, style: TextStyle(color: colorScheme.onErrorContainer)),
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

---

## InputDecoration Best Practices

### Consistent Decoration via Theme

Define input styling once in your app theme so every `TextFormField` inherits it.

```dart
MaterialApp(
  theme: ThemeData(
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.grey.shade50,
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: Colors.grey),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: BorderSide(color: Colors.grey.shade300),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: Colors.blue, width: 2),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: Colors.red),
      ),
      focusedErrorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: Colors.red, width: 2),
      ),
      labelStyle: const TextStyle(fontSize: 14),
      errorStyle: const TextStyle(fontSize: 12),
      floatingLabelBehavior: FloatingLabelBehavior.auto,
    ),
  ),
  home: const Scaffold(body: SignUpForm()),
)
```

### Helpful Decoration Properties

```dart
TextFormField(
  decoration: InputDecoration(
    labelText: 'Phone Number',             // Floating label
    hintText: '(555) 123-4567',            // Placeholder when empty
    helperText: 'We will never share it',  // Permanent hint below
    prefixIcon: const Icon(Icons.phone),   // Icon inside the field start
    suffixIcon: IconButton(                // Interactive icon at end
      icon: const Icon(Icons.clear),
      onPressed: () => _phoneController.clear(),
    ),
    counterText: '',                       // Hides the character counter
    errorMaxLines: 2,                      // Allow multi-line errors
  ),
  keyboardType: TextInputType.phone,
  maxLength: 14,
)
```
