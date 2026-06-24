import 'package:flutter/widgets.dart';

/// Responsive breakpoints (master plan §6). One widget tree per screen, reflowed
/// by width — never a separate phone build. These match the prototype's 402 /
/// 862 / 1180 frames and its `isPhone = width < 560` switch.
enum SbBreakpoint { compact, medium, expanded }

class SbBreakpoints {
  SbBreakpoints._();

  /// Phone — single column, stacked sign-in, 1-col status grid.
  static const double compactMax = 560;

  /// Tablet — row sign-in, 3-col status grid.
  static const double mediumMax = 1024;

  static SbBreakpoint of(double width) {
    if (width < compactMax) return SbBreakpoint.compact;
    if (width < mediumMax) return SbBreakpoint.medium;
    return SbBreakpoint.expanded;
  }
}

/// Layout signals derived from a width — what the screens actually branch on.
/// Mirrors the prototype's `renderVals`: `isPhone`, `signinDir`, `heroDir`,
/// `statusCols`, `heroPadW`.
@immutable
class SbLayout {
  const SbLayout(this.width);

  final double width;

  SbBreakpoint get breakpoint => SbBreakpoints.of(width);

  /// The prototype's single switch: everything stacks below 560.
  bool get isPhone => width < SbBreakpoints.compactMax;
  bool get isCompact => breakpoint == SbBreakpoint.compact;
  bool get isExpanded => breakpoint == SbBreakpoint.expanded;

  /// Sign-in / hero panels: column on phone, row otherwise.
  Axis get splitAxis => isPhone ? Axis.vertical : Axis.horizontal;

  /// Security strip: 1 column on phone, 3 otherwise.
  int get statusColumns => isPhone ? 1 : 3;

  /// Hero padding: 20 on phone, 30 otherwise.
  double get heroPadding => isPhone ? 20 : 30;
}

extension SbResponsiveContext on BuildContext {
  SbLayout get layout => SbLayout(MediaQuery.sizeOf(this).width);
}
