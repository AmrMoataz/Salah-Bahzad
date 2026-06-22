import type { CanDeactivateFn } from '@angular/router';
import type { Observable } from 'rxjs';
// Type-only import — the guard is referenced EAGERLY from app.routes.ts, so a value import here would
// pull the whole quiz feature (+ @microsoft/signalr) into the initial bundle, defeating lazy loading.
import type { QuizIntroComponent } from './quiz-intro.component';

/**
 * The `/sessions/:id/quiz` CanDeactivate guard (contract §C, `FR-STU-QZ-004`). On an **in-app**
 * navigation away it delegates to the routed {@link QuizIntroComponent}, which — only while a live
 * sitting is running — raises the runner's **"Leave the quiz?"** confirm modal and resolves with the
 * student's choice (`true` = Leave & forfeit, the hub teardown forfeits the attempt; `false` = Stay).
 * Outside a sitting it allows navigation immediately.
 */
export const quizLeaveGuard: CanDeactivateFn<QuizIntroComponent> = (
  component,
): boolean | Observable<boolean> => component.canLeave();
