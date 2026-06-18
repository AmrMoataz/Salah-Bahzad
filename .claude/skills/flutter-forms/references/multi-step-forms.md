# Multi-Step and Complex Forms

## Stepper Widget for Wizard Flows

The built-in `Stepper` widget manages a sequence of numbered steps. Each step
can contain its own form section with independent validation.

```dart
import 'package:flutter/material.dart';

class RegistrationStepper extends StatefulWidget {
  const RegistrationStepper({super.key});

  @override
  State<RegistrationStepper> createState() => _RegistrationStepperState();
}

class _RegistrationStepperState extends State<RegistrationStepper> {
  var _currentStep = 0;

  // A separate GlobalKey per step so each step validates independently
  final _accountFormKey = GlobalKey<FormState>();
  final _profileFormKey = GlobalKey<FormState>();
  final _preferencesFormKey = GlobalKey<FormState>();

  // Controllers for all fields
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _nameController = TextEditingController();
  final _bioController = TextEditingController();

  // Preferences state
  var _newsletter = false;
  var _theme = 'light';

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _nameController.dispose();
    _bioController.dispose();
    super.dispose();
  }

  GlobalKey<FormState> _keyForStep(int step) {
    return switch (step) {
      0 => _accountFormKey,
      1 => _profileFormKey,
      2 => _preferencesFormKey,
      _ => _accountFormKey,
    };
  }

  bool _validateCurrentStep() {
    return _keyForStep(_currentStep).currentState?.validate() ?? false;
  }

  void _onStepContinue() {
    if (!_validateCurrentStep()) return;

    if (_currentStep < 2) {
      setState(() => _currentStep++);
    } else {
      _submitAll();
    }
  }

  void _onStepCancel() {
    if (_currentStep > 0) {
      setState(() => _currentStep--);
    }
  }

  void _onStepTapped(int step) {
    // Only allow going back, or forward if current step is valid
    if (step < _currentStep || _validateCurrentStep()) {
      setState(() => _currentStep = step);
    }
  }

  void _submitAll() {
    // Collect data from all steps
    final data = {
      'email': _emailController.text,
      'password': _passwordController.text,
      'name': _nameController.text,
      'bio': _bioController.text,
      'newsletter': _newsletter,
      'theme': _theme,
    };
    debugPrint('Registration data: $data');
  }

  @override
  Widget build(BuildContext context) {
    return Stepper(
      currentStep: _currentStep,
      onStepContinue: _onStepContinue,
      onStepCancel: _onStepCancel,
      onStepTapped: _onStepTapped,
      controlsBuilder: (context, details) {
        return Padding(
          padding: const EdgeInsets.only(top: 16),
          child: Row(
            children: [
              FilledButton(
                onPressed: details.onStepContinue,
                child: Text(_currentStep == 2 ? 'Submit' : 'Continue'),
              ),
              if (_currentStep > 0) ...[
                const SizedBox(width: 12),
                OutlinedButton(
                  onPressed: details.onStepCancel,
                  child: const Text('Back'),
                ),
              ],
            ],
          ),
        );
      },
      steps: [
        Step(
          title: const Text('Account'),
          isActive: _currentStep >= 0,
          state: _currentStep > 0 ? StepState.complete : StepState.indexed,
          content: Form(
            key: _accountFormKey,
            child: Column(
              children: [
                TextFormField(
                  controller: _emailController,
                  decoration: const InputDecoration(labelText: 'Email'),
                  keyboardType: TextInputType.emailAddress,
                  validator: (v) {
                    if (v == null || v.trim().isEmpty) return 'Email is required';
                    if (!v.contains('@')) return 'Enter a valid email';
                    return null;
                  },
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _passwordController,
                  decoration: const InputDecoration(labelText: 'Password'),
                  obscureText: true,
                  validator: (v) {
                    if (v == null || v.length < 8) {
                      return 'Password must be at least 8 characters';
                    }
                    return null;
                  },
                ),
              ],
            ),
          ),
        ),
        Step(
          title: const Text('Profile'),
          isActive: _currentStep >= 1,
          state: _currentStep > 1 ? StepState.complete : StepState.indexed,
          content: Form(
            key: _profileFormKey,
            child: Column(
              children: [
                TextFormField(
                  controller: _nameController,
                  decoration: const InputDecoration(labelText: 'Display Name'),
                  validator: (v) {
                    if (v == null || v.trim().isEmpty) return 'Name is required';
                    return null;
                  },
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _bioController,
                  decoration: const InputDecoration(
                    labelText: 'Bio',
                    alignLabelWithHint: true,
                  ),
                  maxLines: 3,
                  maxLength: 200,
                ),
              ],
            ),
          ),
        ),
        Step(
          title: const Text('Preferences'),
          isActive: _currentStep >= 2,
          content: Form(
            key: _preferencesFormKey,
            child: Column(
              children: [
                SwitchListTile(
                  title: const Text('Subscribe to newsletter'),
                  value: _newsletter,
                  onChanged: (v) => setState(() => _newsletter = v),
                ),
                const SizedBox(height: 8),
                DropdownButtonFormField<String>(
                  value: _theme,
                  decoration: const InputDecoration(labelText: 'App Theme'),
                  items: const [
                    DropdownMenuItem(value: 'light', child: Text('Light')),
                    DropdownMenuItem(value: 'dark', child: Text('Dark')),
                    DropdownMenuItem(value: 'system', child: Text('System')),
                  ],
                  onChanged: (v) {
                    if (v != null) setState(() => _theme = v);
                  },
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
```

---

## PageView-Based Multi-Step Form

A `PageView` gives full visual control over transitions and layout. Pair it with
`PageController` and a linear progress indicator.

```dart
import 'package:flutter/material.dart';

class PageViewWizard extends StatefulWidget {
  const PageViewWizard({super.key});

  @override
  State<PageViewWizard> createState() => _PageViewWizardState();
}

class _PageViewWizardState extends State<PageViewWizard> {
  final _pageController = PageController();
  final _stepKeys = List.generate(3, (_) => GlobalKey<FormState>());

  // Field controllers
  final _firstNameCtrl = TextEditingController();
  final _lastNameCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  final _streetCtrl = TextEditingController();
  final _cityCtrl = TextEditingController();

  var _currentPage = 0;
  static const _totalPages = 3;

  @override
  void dispose() {
    _pageController.dispose();
    _firstNameCtrl.dispose();
    _lastNameCtrl.dispose();
    _emailCtrl.dispose();
    _phoneCtrl.dispose();
    _streetCtrl.dispose();
    _cityCtrl.dispose();
    super.dispose();
  }

  void _goToPage(int page) {
    _pageController.animateToPage(
      page,
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeInOut,
    );
  }

  void _next() {
    if (!(_stepKeys[_currentPage].currentState?.validate() ?? false)) return;

    if (_currentPage < _totalPages - 1) {
      _goToPage(_currentPage + 1);
    } else {
      _submit();
    }
  }

  void _back() {
    if (_currentPage > 0) _goToPage(_currentPage - 1);
  }

  void _submit() {
    final data = {
      'firstName': _firstNameCtrl.text,
      'lastName': _lastNameCtrl.text,
      'email': _emailCtrl.text,
      'phone': _phoneCtrl.text,
      'street': _streetCtrl.text,
      'city': _cityCtrl.text,
    };
    debugPrint('Wizard data: $data');
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        // Progress indicator
        Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text('Step ${_currentPage + 1} of $_totalPages'),
                  Text('${((_currentPage + 1) / _totalPages * 100).round()}%'),
                ],
              ),
              const SizedBox(height: 8),
              LinearProgressIndicator(
                value: (_currentPage + 1) / _totalPages,
                borderRadius: BorderRadius.circular(4),
              ),
            ],
          ),
        ),

        // Pages
        Expanded(
          child: PageView(
            controller: _pageController,
            physics: const NeverScrollableScrollPhysics(),
            onPageChanged: (page) => setState(() => _currentPage = page),
            children: [
              _PersonalInfoPage(
                formKey: _stepKeys[0],
                firstNameCtrl: _firstNameCtrl,
                lastNameCtrl: _lastNameCtrl,
              ),
              _ContactInfoPage(
                formKey: _stepKeys[1],
                emailCtrl: _emailCtrl,
                phoneCtrl: _phoneCtrl,
              ),
              _AddressPage(
                formKey: _stepKeys[2],
                streetCtrl: _streetCtrl,
                cityCtrl: _cityCtrl,
              ),
            ],
          ),
        ),

        // Navigation buttons
        Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              if (_currentPage > 0)
                OutlinedButton(onPressed: _back, child: const Text('Back')),
              const Spacer(),
              FilledButton(
                onPressed: _next,
                child: Text(_currentPage == _totalPages - 1 ? 'Submit' : 'Next'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _PersonalInfoPage extends StatelessWidget {
  const _PersonalInfoPage({
    required this.formKey,
    required this.firstNameCtrl,
    required this.lastNameCtrl,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController firstNameCtrl;
  final TextEditingController lastNameCtrl;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Personal Information',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 24),
            TextFormField(
              controller: firstNameCtrl,
              decoration: const InputDecoration(labelText: 'First Name'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'First name is required' : null,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: lastNameCtrl,
              decoration: const InputDecoration(labelText: 'Last Name'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Last name is required' : null,
            ),
          ],
        ),
      ),
    );
  }
}

class _ContactInfoPage extends StatelessWidget {
  const _ContactInfoPage({
    required this.formKey,
    required this.emailCtrl,
    required this.phoneCtrl,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController emailCtrl;
  final TextEditingController phoneCtrl;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Contact Information',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 24),
            TextFormField(
              controller: emailCtrl,
              decoration: const InputDecoration(labelText: 'Email'),
              keyboardType: TextInputType.emailAddress,
              validator: (v) {
                if (v == null || v.trim().isEmpty) return 'Email is required';
                if (!v.contains('@')) return 'Enter a valid email';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: phoneCtrl,
              decoration: const InputDecoration(labelText: 'Phone Number'),
              keyboardType: TextInputType.phone,
              validator: (v) {
                if (v == null || v.trim().isEmpty) return 'Phone number is required';
                return null;
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _AddressPage extends StatelessWidget {
  const _AddressPage({
    required this.formKey,
    required this.streetCtrl,
    required this.cityCtrl,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController streetCtrl;
  final TextEditingController cityCtrl;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Address', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 24),
            TextFormField(
              controller: streetCtrl,
              decoration: const InputDecoration(labelText: 'Street Address'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Street is required' : null,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: cityCtrl,
              decoration: const InputDecoration(labelText: 'City'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'City is required' : null,
            ),
          ],
        ),
      ),
    );
  }
}
```

---

## Form State Preservation Across Steps

Both `Stepper` and `PageView` preserve state because:

1. **TextEditingController values persist** -- controllers are owned by the
   parent `State`, not the individual step widgets.
2. **Form keys are stable** -- each `GlobalKey<FormState>` is created once and
   stored as a field.
3. **PageView keeps offscreen children alive** by default when using
   `AutomaticKeepAliveClientMixin`.

### AutomaticKeepAlive for PageView Pages

```dart
class _PersonalInfoPageState extends State<_PersonalInfoPageStateful>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context); // Required by AutomaticKeepAliveClientMixin
    return Form(
      key: widget.formKey,
      child: Column(
        children: [
          TextFormField(
            controller: widget.firstNameCtrl,
            decoration: const InputDecoration(labelText: 'First Name'),
          ),
        ],
      ),
    );
  }
}
```

---

## Step Validation Before Proceeding

A reusable mixin that enforces "validate before advance" across different wizard
implementations.

```dart
mixin StepValidationMixin {
  /// Maps step index to its form key.
  Map<int, GlobalKey<FormState>> get stepFormKeys;

  /// Returns true if the given step's form is valid.
  bool validateStep(int step) {
    final key = stepFormKeys[step];
    if (key == null) return true; // No form for this step
    return key.currentState?.validate() ?? false;
  }

  /// Validates all steps from 0 to [upTo] inclusive.
  bool validateAllSteps(int upTo) {
    for (var i = 0; i <= upTo; i++) {
      if (!validateStep(i)) return false;
    }
    return true;
  }
}
```

---

## Progress Indicator

### Segmented Step Indicator

```dart
class StepProgressIndicator extends StatelessWidget {
  const StepProgressIndicator({
    super.key,
    required this.totalSteps,
    required this.currentStep,
    required this.labels,
  });

  final int totalSteps;
  final int currentStep;
  final List<String> labels;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      children: List.generate(totalSteps * 2 - 1, (index) {
        // Even indices are circles, odd indices are connectors
        if (index.isOdd) {
          final stepBefore = index ~/ 2;
          return Expanded(
            child: Container(
              height: 2,
              color: stepBefore < currentStep
                  ? colorScheme.primary
                  : colorScheme.outlineVariant,
            ),
          );
        }

        final stepIndex = index ~/ 2;
        final isCompleted = stepIndex < currentStep;
        final isCurrent = stepIndex == currentStep;

        return Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircleAvatar(
              radius: 16,
              backgroundColor: isCompleted || isCurrent
                  ? colorScheme.primary
                  : colorScheme.outlineVariant,
              child: isCompleted
                  ? Icon(Icons.check, size: 16, color: colorScheme.onPrimary)
                  : Text(
                      '${stepIndex + 1}',
                      style: TextStyle(
                        color: isCurrent
                            ? colorScheme.onPrimary
                            : colorScheme.onSurfaceVariant,
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                      ),
                    ),
            ),
            const SizedBox(height: 4),
            Text(
              labels[stepIndex],
              style: TextStyle(
                fontSize: 11,
                color: isCurrent
                    ? colorScheme.primary
                    : colorScheme.onSurfaceVariant,
                fontWeight: isCurrent ? FontWeight.bold : FontWeight.normal,
              ),
            ),
          ],
        );
      }),
    );
  }
}
```

### Usage

```dart
StepProgressIndicator(
  totalSteps: 3,
  currentStep: _currentPage,
  labels: const ['Personal', 'Contact', 'Address'],
)
```

---

## Form State Management with Riverpod

For complex forms that span multiple screens or need to survive navigation,
manage form data in a Riverpod Notifier.

### Form Data Model

```dart
import 'package:flutter/foundation.dart';

@immutable
class OnboardingFormData {
  const OnboardingFormData({
    this.firstName = '',
    this.lastName = '',
    this.email = '',
    this.phone = '',
    this.street = '',
    this.city = '',
    this.currentStep = 0,
  });

  final String firstName;
  final String lastName;
  final String email;
  final String phone;
  final String street;
  final String city;
  final int currentStep;

  OnboardingFormData copyWith({
    String? firstName,
    String? lastName,
    String? email,
    String? phone,
    String? street,
    String? city,
    int? currentStep,
  }) {
    return OnboardingFormData(
      firstName: firstName ?? this.firstName,
      lastName: lastName ?? this.lastName,
      email: email ?? this.email,
      phone: phone ?? this.phone,
      street: street ?? this.street,
      city: city ?? this.city,
      currentStep: currentStep ?? this.currentStep,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'firstName': firstName,
      'lastName': lastName,
      'email': email,
      'phone': phone,
      'street': street,
      'city': city,
    };
  }
}
```

### Riverpod Notifier

```dart
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

part 'onboarding_form_notifier.g.dart';

@riverpod
class OnboardingFormNotifier extends _$OnboardingFormNotifier {
  @override
  OnboardingFormData build() => const OnboardingFormData();

  void updatePersonalInfo({required String firstName, required String lastName}) {
    state = state.copyWith(firstName: firstName, lastName: lastName);
  }

  void updateContactInfo({required String email, required String phone}) {
    state = state.copyWith(email: email, phone: phone);
  }

  void updateAddress({required String street, required String city}) {
    state = state.copyWith(street: street, city: city);
  }

  void goToStep(int step) {
    state = state.copyWith(currentStep: step);
  }

  void nextStep() {
    if (state.currentStep < 2) {
      state = state.copyWith(currentStep: state.currentStep + 1);
    }
  }

  void previousStep() {
    if (state.currentStep > 0) {
      state = state.copyWith(currentStep: state.currentStep - 1);
    }
  }

  Future<void> submit() async {
    final json = state.toJson();
    // Replace with your API call
    debugPrint('Submitting: $json');
    await Future<void>.delayed(const Duration(seconds: 1));
  }

  void reset() {
    state = const OnboardingFormData();
  }
}
```

### Riverpod Form Widget

```dart
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

class RiverpodOnboardingWizard extends ConsumerStatefulWidget {
  const RiverpodOnboardingWizard({super.key});

  @override
  ConsumerState<RiverpodOnboardingWizard> createState() =>
      _RiverpodOnboardingWizardState();
}

class _RiverpodOnboardingWizardState
    extends ConsumerState<RiverpodOnboardingWizard> {
  final _pageController = PageController();
  final _stepKeys = List.generate(3, (_) => GlobalKey<FormState>());

  // Controllers pre-filled from Riverpod state
  late final TextEditingController _firstNameCtrl;
  late final TextEditingController _lastNameCtrl;
  late final TextEditingController _emailCtrl;
  late final TextEditingController _phoneCtrl;
  late final TextEditingController _streetCtrl;
  late final TextEditingController _cityCtrl;

  @override
  void initState() {
    super.initState();
    final data = ref.read(onboardingFormNotifierProvider);
    _firstNameCtrl = TextEditingController(text: data.firstName);
    _lastNameCtrl = TextEditingController(text: data.lastName);
    _emailCtrl = TextEditingController(text: data.email);
    _phoneCtrl = TextEditingController(text: data.phone);
    _streetCtrl = TextEditingController(text: data.street);
    _cityCtrl = TextEditingController(text: data.city);
  }

  @override
  void dispose() {
    _pageController.dispose();
    _firstNameCtrl.dispose();
    _lastNameCtrl.dispose();
    _emailCtrl.dispose();
    _phoneCtrl.dispose();
    _streetCtrl.dispose();
    _cityCtrl.dispose();
    super.dispose();
  }

  void _saveCurrentStep(int step) {
    final notifier = ref.read(onboardingFormNotifierProvider.notifier);
    switch (step) {
      case 0:
        notifier.updatePersonalInfo(
          firstName: _firstNameCtrl.text,
          lastName: _lastNameCtrl.text,
        );
      case 1:
        notifier.updateContactInfo(
          email: _emailCtrl.text,
          phone: _phoneCtrl.text,
        );
      case 2:
        notifier.updateAddress(
          street: _streetCtrl.text,
          city: _cityCtrl.text,
        );
    }
  }

  void _next() {
    final data = ref.read(onboardingFormNotifierProvider);
    if (!(_stepKeys[data.currentStep].currentState?.validate() ?? false)) return;

    _saveCurrentStep(data.currentStep);
    final notifier = ref.read(onboardingFormNotifierProvider.notifier);

    if (data.currentStep < 2) {
      notifier.nextStep();
      _pageController.nextPage(
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeInOut,
      );
    } else {
      notifier.submit();
    }
  }

  void _back() {
    final data = ref.read(onboardingFormNotifierProvider);
    _saveCurrentStep(data.currentStep);
    ref.read(onboardingFormNotifierProvider.notifier).previousStep();
    _pageController.previousPage(
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeInOut,
    );
  }

  @override
  Widget build(BuildContext context) {
    final currentStep = ref.watch(
      onboardingFormNotifierProvider.select((data) => data.currentStep),
    );

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(16),
          child: StepProgressIndicator(
            totalSteps: 3,
            currentStep: currentStep,
            labels: const ['Personal', 'Contact', 'Address'],
          ),
        ),
        Expanded(
          child: PageView(
            controller: _pageController,
            physics: const NeverScrollableScrollPhysics(),
            children: [
              _buildPersonalStep(),
              _buildContactStep(),
              _buildAddressStep(),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              if (currentStep > 0)
                OutlinedButton(onPressed: _back, child: const Text('Back')),
              const Spacer(),
              FilledButton(
                onPressed: _next,
                child: Text(currentStep == 2 ? 'Submit' : 'Next'),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildPersonalStep() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _stepKeys[0],
        child: Column(
          children: [
            TextFormField(
              controller: _firstNameCtrl,
              decoration: const InputDecoration(labelText: 'First Name'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _lastNameCtrl,
              decoration: const InputDecoration(labelText: 'Last Name'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildContactStep() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _stepKeys[1],
        child: Column(
          children: [
            TextFormField(
              controller: _emailCtrl,
              decoration: const InputDecoration(labelText: 'Email'),
              keyboardType: TextInputType.emailAddress,
              validator: (v) {
                if (v == null || v.trim().isEmpty) return 'Required';
                if (!v.contains('@')) return 'Enter a valid email';
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _phoneCtrl,
              decoration: const InputDecoration(labelText: 'Phone'),
              keyboardType: TextInputType.phone,
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildAddressStep() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _stepKeys[2],
        child: Column(
          children: [
            TextFormField(
              controller: _streetCtrl,
              decoration: const InputDecoration(labelText: 'Street'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _cityCtrl,
              decoration: const InputDecoration(labelText: 'City'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Required' : null,
            ),
          ],
        ),
      ),
    );
  }
}
```

---

## Form State Management with Bloc (FormBloc Pattern)

### Events and State (Sealed Classes)

```dart
import 'package:flutter/foundation.dart';

// --- State ---

enum FormSubmissionStatus { initial, submitting, success, failure }

@immutable
class RegistrationFormState {
  const RegistrationFormState({
    this.name = '',
    this.email = '',
    this.password = '',
    this.currentStep = 0,
    this.status = FormSubmissionStatus.initial,
    this.errorMessage,
  });

  final String name;
  final String email;
  final String password;
  final int currentStep;
  final FormSubmissionStatus status;
  final String? errorMessage;

  RegistrationFormState copyWith({
    String? name,
    String? email,
    String? password,
    int? currentStep,
    FormSubmissionStatus? status,
    String? errorMessage,
  }) {
    return RegistrationFormState(
      name: name ?? this.name,
      email: email ?? this.email,
      password: password ?? this.password,
      currentStep: currentStep ?? this.currentStep,
      status: status ?? this.status,
      errorMessage: errorMessage,
    );
  }
}

// --- Events ---

sealed class RegistrationFormEvent {
  const RegistrationFormEvent();
}

final class NameChanged extends RegistrationFormEvent {
  const NameChanged(this.name);
  final String name;
}

final class EmailChanged extends RegistrationFormEvent {
  const EmailChanged(this.email);
  final String email;
}

final class PasswordChanged extends RegistrationFormEvent {
  const PasswordChanged(this.password);
  final String password;
}

final class StepAdvanced extends RegistrationFormEvent {
  const StepAdvanced();
}

final class StepReversed extends RegistrationFormEvent {
  const StepReversed();
}

final class FormSubmitted extends RegistrationFormEvent {
  const FormSubmitted();
}

final class FormReset extends RegistrationFormEvent {
  const FormReset();
}
```

### Bloc Implementation

```dart
import 'package:flutter_bloc/flutter_bloc.dart';

class RegistrationFormBloc
    extends Bloc<RegistrationFormEvent, RegistrationFormState> {
  RegistrationFormBloc({required this.authRepository})
      : super(const RegistrationFormState()) {
    on<NameChanged>(_onNameChanged);
    on<EmailChanged>(_onEmailChanged);
    on<PasswordChanged>(_onPasswordChanged);
    on<StepAdvanced>(_onStepAdvanced);
    on<StepReversed>(_onStepReversed);
    on<FormSubmitted>(_onFormSubmitted);
    on<FormReset>(_onFormReset);
  }

  final AuthRepository authRepository;

  void _onNameChanged(NameChanged event, Emitter<RegistrationFormState> emit) {
    emit(state.copyWith(name: event.name));
  }

  void _onEmailChanged(EmailChanged event, Emitter<RegistrationFormState> emit) {
    emit(state.copyWith(email: event.email));
  }

  void _onPasswordChanged(
      PasswordChanged event, Emitter<RegistrationFormState> emit) {
    emit(state.copyWith(password: event.password));
  }

  void _onStepAdvanced(StepAdvanced event, Emitter<RegistrationFormState> emit) {
    if (state.currentStep < 2) {
      emit(state.copyWith(currentStep: state.currentStep + 1));
    }
  }

  void _onStepReversed(StepReversed event, Emitter<RegistrationFormState> emit) {
    if (state.currentStep > 0) {
      emit(state.copyWith(currentStep: state.currentStep - 1));
    }
  }

  Future<void> _onFormSubmitted(
      FormSubmitted event, Emitter<RegistrationFormState> emit) async {
    emit(state.copyWith(status: FormSubmissionStatus.submitting));

    try {
      await authRepository.register(
        name: state.name,
        email: state.email,
        password: state.password,
      );
      emit(state.copyWith(status: FormSubmissionStatus.success));
    } catch (e) {
      emit(state.copyWith(
        status: FormSubmissionStatus.failure,
        errorMessage: e.toString(),
      ));
    }
  }

  void _onFormReset(FormReset event, Emitter<RegistrationFormState> emit) {
    emit(const RegistrationFormState());
  }
}

// Placeholder for the repository
abstract class AuthRepository {
  Future<void> register({
    required String name,
    required String email,
    required String password,
  });
}
```

### Bloc Form Widget

```dart
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class BlocRegistrationForm extends StatelessWidget {
  const BlocRegistrationForm({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocListener<RegistrationFormBloc, RegistrationFormState>(
      listenWhen: (prev, curr) => prev.status != curr.status,
      listener: (context, state) {
        switch (state.status) {
          case FormSubmissionStatus.success:
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Registration successful!')),
            );
          case FormSubmissionStatus.failure:
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(state.errorMessage ?? 'Registration failed')),
            );
          default:
            break;
        }
      },
      child: BlocBuilder<RegistrationFormBloc, RegistrationFormState>(
        builder: (context, state) {
          return Column(
            children: [
              StepProgressIndicator(
                totalSteps: 3,
                currentStep: state.currentStep,
                labels: const ['Name', 'Email', 'Password'],
              ),
              const SizedBox(height: 24),
              Expanded(
                child: switch (state.currentStep) {
                  0 => _NameStep(initialValue: state.name),
                  1 => _EmailStep(initialValue: state.email),
                  2 => _PasswordStep(
                      initialValue: state.password,
                      isSubmitting:
                          state.status == FormSubmissionStatus.submitting,
                    ),
                  _ => const SizedBox.shrink(),
                },
              ),
            ],
          );
        },
      ),
    );
  }
}

class _NameStep extends StatefulWidget {
  const _NameStep({required this.initialValue});

  final String initialValue;

  @override
  State<_NameStep> createState() => _NameStepState();
}

class _NameStepState extends State<_NameStep> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _controller;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.initialValue);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: Column(
          children: [
            TextFormField(
              controller: _controller,
              decoration: const InputDecoration(labelText: 'Full Name'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Name is required' : null,
            ),
            const Spacer(),
            FilledButton(
              onPressed: () {
                if (_formKey.currentState!.validate()) {
                  context
                      .read<RegistrationFormBloc>()
                      .add(NameChanged(_controller.text));
                  context
                      .read<RegistrationFormBloc>()
                      .add(const StepAdvanced());
                }
              },
              child: const Text('Next'),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmailStep extends StatefulWidget {
  const _EmailStep({required this.initialValue});

  final String initialValue;

  @override
  State<_EmailStep> createState() => _EmailStepState();
}

class _EmailStepState extends State<_EmailStep> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _controller;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.initialValue);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: Column(
          children: [
            TextFormField(
              controller: _controller,
              decoration: const InputDecoration(labelText: 'Email'),
              keyboardType: TextInputType.emailAddress,
              validator: (v) {
                if (v == null || v.trim().isEmpty) return 'Email is required';
                if (!v.contains('@')) return 'Enter a valid email';
                return null;
              },
            ),
            const Spacer(),
            Row(
              children: [
                OutlinedButton(
                  onPressed: () {
                    context
                        .read<RegistrationFormBloc>()
                        .add(EmailChanged(_controller.text));
                    context
                        .read<RegistrationFormBloc>()
                        .add(const StepReversed());
                  },
                  child: const Text('Back'),
                ),
                const Spacer(),
                FilledButton(
                  onPressed: () {
                    if (_formKey.currentState!.validate()) {
                      context
                          .read<RegistrationFormBloc>()
                          .add(EmailChanged(_controller.text));
                      context
                          .read<RegistrationFormBloc>()
                          .add(const StepAdvanced());
                    }
                  },
                  child: const Text('Next'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _PasswordStep extends StatefulWidget {
  const _PasswordStep({
    required this.initialValue,
    required this.isSubmitting,
  });

  final String initialValue;
  final bool isSubmitting;

  @override
  State<_PasswordStep> createState() => _PasswordStepState();
}

class _PasswordStepState extends State<_PasswordStep> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _controller;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.initialValue);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Form(
        key: _formKey,
        child: Column(
          children: [
            TextFormField(
              controller: _controller,
              decoration: const InputDecoration(labelText: 'Password'),
              obscureText: true,
              enabled: !widget.isSubmitting,
              validator: (v) {
                if (v == null || v.length < 8) {
                  return 'Password must be at least 8 characters';
                }
                return null;
              },
            ),
            const Spacer(),
            Row(
              children: [
                OutlinedButton(
                  onPressed: widget.isSubmitting
                      ? null
                      : () {
                          context
                              .read<RegistrationFormBloc>()
                              .add(PasswordChanged(_controller.text));
                          context
                              .read<RegistrationFormBloc>()
                              .add(const StepReversed());
                        },
                  child: const Text('Back'),
                ),
                const Spacer(),
                FilledButton(
                  onPressed: widget.isSubmitting
                      ? null
                      : () {
                          if (_formKey.currentState!.validate()) {
                            context
                                .read<RegistrationFormBloc>()
                                .add(PasswordChanged(_controller.text));
                            context
                                .read<RegistrationFormBloc>()
                                .add(const FormSubmitted());
                          }
                        },
                  child: widget.isSubmitting
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Register'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
```

---

## Dynamic Form Fields (Add / Remove Fields)

Allow users to add or remove repeating sections, such as phone numbers or
education entries.

```dart
import 'package:flutter/material.dart';

class DynamicPhoneForm extends StatefulWidget {
  const DynamicPhoneForm({super.key});

  @override
  State<DynamicPhoneForm> createState() => _DynamicPhoneFormState();
}

class _DynamicPhoneFormState extends State<DynamicPhoneForm> {
  final _formKey = GlobalKey<FormState>();
  final _phoneEntries = <_PhoneEntry>[_PhoneEntry()];

  void _addPhone() {
    setState(() => _phoneEntries.add(_PhoneEntry()));
  }

  void _removePhone(int index) {
    if (_phoneEntries.length <= 1) return; // Keep at least one
    setState(() {
      _phoneEntries[index].dispose();
      _phoneEntries.removeAt(index);
    });
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) return;

    final phones = _phoneEntries
        .map((e) => {'label': e.label, 'number': e.controller.text})
        .toList();
    debugPrint('Phones: $phones');
  }

  @override
  void dispose() {
    for (final entry in _phoneEntries) {
      entry.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Phone Numbers', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 8),
          for (var i = 0; i < _phoneEntries.length; i++)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(
                    width: 100,
                    child: DropdownButtonFormField<String>(
                      value: _phoneEntries[i].label,
                      decoration: const InputDecoration(
                        labelText: 'Type',
                        isDense: true,
                      ),
                      items: const [
                        DropdownMenuItem(value: 'mobile', child: Text('Mobile')),
                        DropdownMenuItem(value: 'home', child: Text('Home')),
                        DropdownMenuItem(value: 'work', child: Text('Work')),
                      ],
                      onChanged: (v) {
                        if (v != null) {
                          setState(() => _phoneEntries[i].label = v);
                        }
                      },
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: TextFormField(
                      controller: _phoneEntries[i].controller,
                      decoration: InputDecoration(
                        labelText: 'Phone #${i + 1}',
                      ),
                      keyboardType: TextInputType.phone,
                      validator: (v) =>
                          (v == null || v.trim().isEmpty) ? 'Required' : null,
                    ),
                  ),
                  if (_phoneEntries.length > 1)
                    IconButton(
                      icon: const Icon(Icons.remove_circle_outline),
                      color: Theme.of(context).colorScheme.error,
                      onPressed: () => _removePhone(i),
                    ),
                ],
              ),
            ),
          TextButton.icon(
            onPressed: _addPhone,
            icon: const Icon(Icons.add),
            label: const Text('Add Phone'),
          ),
          const SizedBox(height: 24),
          FilledButton(onPressed: _submit, child: const Text('Save')),
        ],
      ),
    );
  }
}

class _PhoneEntry {
  _PhoneEntry();

  final controller = TextEditingController();
  String label = 'mobile';

  void dispose() {
    controller.dispose();
  }
}
```

---

## Form Data Serialization

### Data Class with toJson / fromJson

```dart
import 'package:flutter/foundation.dart';

@immutable
class ProfileFormData {
  const ProfileFormData({
    required this.name,
    required this.email,
    required this.dateOfBirth,
    required this.interests,
    this.bio,
  });

  final String name;
  final String email;
  final DateTime dateOfBirth;
  final Set<String> interests;
  final String? bio;

  Map<String, dynamic> toJson() {
    return {
      'name': name,
      'email': email,
      'dateOfBirth': dateOfBirth.toIso8601String(),
      'interests': interests.toList(),
      if (bio != null) 'bio': bio,
    };
  }

  factory ProfileFormData.fromJson(Map<String, dynamic> json) {
    return ProfileFormData(
      name: json['name'] as String,
      email: json['email'] as String,
      dateOfBirth: DateTime.parse(json['dateOfBirth'] as String),
      interests: Set<String>.from(json['interests'] as List),
      bio: json['bio'] as String?,
    );
  }
}
```

### Collecting Data from a Form

```dart
void _submitForm() {
  if (!_formKey.currentState!.validate()) return;
  _formKey.currentState!.save();

  final data = ProfileFormData(
    name: _nameController.text.trim(),
    email: _emailController.text.trim(),
    dateOfBirth: _selectedDate!,
    interests: _selectedInterests,
    bio: _bioController.text.trim().isNotEmpty
        ? _bioController.text.trim()
        : null,
  );

  final json = data.toJson();
  debugPrint('Submitting JSON: $json');
  // Send json to your API
}
```

### Restoring Form from Saved Data

```dart
void _restoreForm(ProfileFormData data) {
  _nameController.text = data.name;
  _emailController.text = data.email;
  setState(() {
    _selectedDate = data.dateOfBirth;
    _selectedInterests = data.interests;
  });
  if (data.bio != null) {
    _bioController.text = data.bio!;
  }
}
```

### Using freezed for Immutable Form Models (Recommended for Large Apps)

```dart
import 'package:freezed_annotation/freezed_annotation.dart';

part 'checkout_form_data.freezed.dart';
part 'checkout_form_data.g.dart';

@freezed
class CheckoutFormData with _$CheckoutFormData {
  const factory CheckoutFormData({
    @Default('') String cardNumber,
    @Default('') String expiryDate,
    @Default('') String cvv,
    @Default('') String billingName,
    @Default('') String billingZip,
    @Default(false) bool saveCard,
  }) = _CheckoutFormData;

  factory CheckoutFormData.fromJson(Map<String, dynamic> json) =>
      _$CheckoutFormDataFromJson(json);
}
```

### Usage with Riverpod

```dart
@riverpod
class CheckoutFormNotifier extends _$CheckoutFormNotifier {
  @override
  CheckoutFormData build() => const CheckoutFormData();

  void updateCardNumber(String value) =>
      state = state.copyWith(cardNumber: value);

  void updateExpiryDate(String value) =>
      state = state.copyWith(expiryDate: value);

  void updateCvv(String value) =>
      state = state.copyWith(cvv: value);

  void updateBillingName(String value) =>
      state = state.copyWith(billingName: value);

  void updateBillingZip(String value) =>
      state = state.copyWith(billingZip: value);

  void toggleSaveCard() =>
      state = state.copyWith(saveCard: !state.saveCard);

  Map<String, dynamic> toJson() => state.toJson();
}
```
