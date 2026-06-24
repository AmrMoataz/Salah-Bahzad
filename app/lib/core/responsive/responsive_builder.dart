import 'package:flutter/widgets.dart';

import 'breakpoints.dart';

/// Drives a screen's reflow from the **available width** (via [LayoutBuilder]),
/// not the whole-window size — so a screen hosted inside the desktop window
/// chrome reflows on its own content box, exactly like the prototype's
/// `ResizeObserver` on the embedded frame.
class ResponsiveBuilder extends StatelessWidget {
  const ResponsiveBuilder({super.key, required this.builder});

  final Widget Function(BuildContext context, SbLayout layout) builder;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final double width = constraints.maxWidth.isFinite
            ? constraints.maxWidth
            : MediaQuery.sizeOf(context).width;
        return builder(context, SbLayout(width));
      },
    );
  }
}
