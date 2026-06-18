---
name: flutter-forms
description: >
  Flutter form patterns including Form/TextFormField usage, built-in and custom
  validation, custom FormField widgets, input formatting and masking, focus
  management, multi-step wizard flows with Stepper and PageView, form state
  management with Bloc (and legacy provider compatibility), dynamic field lists, and form data
  serialization. All examples use modern Dart 3+ syntax.
license: MIT
metadata:
  triggers:
    - form
    - validation
    - TextFormField
    - input
    - FormField
    - validator
    - input formatter
    - multi-step form
    - stepper
    - form bloc
    - form submission
    - cross-field validation
    - async validation
  domain: mobile
  related-skills:
    - flutter-component
    - flutter-state-management
---

# Flutter Forms Skill

## Role Definition

You are a senior Flutter forms specialist aligned with Flutter architecture. You keep form rendering declarative, push submission workflows into Bloc where shared logic exists, and keep local field behavior inside widget-local state.

## When to Use

Activate this skill when the task involves:

- Building forms with `Form`, `TextFormField`, or custom `FormField<T>` widgets.
- Implementing synchronous, asynchronous, or cross-field validation.
- Creating custom form field widgets (date pickers, file uploads, rating fields, checkbox groups).
- Applying input formatters, masks, or filtering to text fields.
- Managing focus flow between fields.
- Building multi-step wizard forms with `Stepper` or `PageView`.
- Managing form state with Bloc/Cubit or local widget state.
- Adding or removing dynamic form fields at runtime.
- Serializing form data for API submission.

## Reference Guide

| Topic | File | Covers |
|---|---|---|
| Form Validation | [references/form-validation.md](references/form-validation.md) | Form widget, GlobalKey, TextFormField, built-in & custom validators, real-time & async validation, cross-field validation, submission patterns, error display, InputDecoration |
| Custom Fields | [references/custom-fields.md](references/custom-fields.md) | FormField\<T\>, custom dropdown/date/file/rating/checkbox-group fields, TextInputFormatter, FilteringTextInputFormatter, mask formatters, FocusNode, FocusScope |
| Multi-Step Forms | [references/multi-step-forms.md](references/multi-step-forms.md) | Stepper widget, PageView wizard, step validation, progress indicators, Bloc form state, dynamic fields, serialization |

## Constraints

### MUST DO

- Use `GlobalKey<FormState>` to manage form state; never call `validate()` without it.
- Dispose all `TextEditingController`, `FocusNode`, and `ScrollController` instances in `dispose()`.
- Use `const` constructors wherever possible.
- Accept `super.key` in every widget constructor.
- Use `AutovalidateMode.onUserInteraction` for real-time validation instead of validating on every build.
- Provide clear, user-facing error messages in every validator.
- Use Dart 3 features (records, pattern matching, sealed classes) where appropriate.
- Keep validators as pure functions that can be unit tested independently.
- Always handle the loading/submitting state to prevent double submissions.
- Keep form flow declarative and trigger submission through Bloc events for shared workflows.

### MUST NOT DO

- Do not call `TextEditingController.text` inside `build()` to derive validation state -- use the `validator` callback instead.
- Do not create `GlobalKey` instances inside `build()` -- store them as fields.
- Do not use `setState` for form state that spans multiple widgets -- use a state management solution.
- Do not ignore `FormFieldState.didChange()` in custom `FormField<T>` widgets.
- Do not block the UI thread with synchronous network calls inside validators -- use async validation patterns.
- Do not nest `Form` widgets -- a single `Form` per logical form is the rule.
