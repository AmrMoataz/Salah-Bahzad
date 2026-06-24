export { StudentAuthStore } from './lib/auth/student-auth.store';
export { studentAuthInterceptor } from './lib/auth/student-auth.interceptor';
export { authGuard, guestGuard, statusGuard } from './lib/auth/auth.guard';
export { getDeviceFingerprint } from './lib/auth/device-fingerprint';
export type {
  StudentStatus,
  StudentBlockReason,
  BoundDeviceInfo,
  StudentInfo,
  StudentAuthResponse,
  StudentAuthProblem,
  StudentAuthState,
} from './lib/auth/student-auth.models';

export { CatalogueService } from './lib/catalogue/catalogue.service';
export type {
  EnrollmentState,
  CatalogueSession,
  CatalogueFilters,
  Enrollment,
} from './lib/catalogue/catalogue.models';

export { MySessionsService } from './lib/sessions/my-sessions.service';
export type {
  MySessionState,
  MySessionFilter,
  VideoProcessingStatus,
  VideoLockState,
  GateState,
  AssignmentStatus,
  MySession,
  MySessionVideo,
  MySessionMaterial,
  MyAssignmentStatus,
  MyQuizStatus,
  MySessionDetail,
  SignedUrl,
  PlaybackHandoff,
} from './lib/sessions/my-sessions.models';

export { AssignmentService } from './lib/assignments/assignment.service';
export type {
  AssignmentStatus as AssignmentRunStatus,
  StudentAssignment,
  StudentAssignmentQuestion,
  StudentAssignmentOption,
  AssignmentProgress,
  AssignmentEventType,
  AssignmentEventBody,
  StudentAssignmentReview,
  StudentReviewQuestion,
  StudentReviewOption,
} from './lib/assignments/assignment.models';

export { QuizService } from './lib/quizzes/quiz.service';
export type {
  QuizAttemptStatus,
  QuizAttemptFlag,
  FocusEventType,
  QuizSettings,
  StudentQuizAttemptSummary,
  StudentQuiz,
  QuizAttemptOption,
  QuizAttemptQuestion,
  QuizAttempt,
  QuizAttemptResult,
  FocusEventBody,
  StudentQuizReviewOption,
  StudentQuizReviewQuestion,
  StudentQuizAttemptReview,
} from './lib/quizzes/quiz.models';

export { ProfileService } from './lib/profile/profile.service';
export type {
  StudentProfile,
  BoundDevice,
  UpdateMyStudentProfile,
} from './lib/profile/profile.models';

export { PlanService } from './lib/plan/plan.service';
export type {
  MyPlanStepKind,
  MyPlanStepStatus,
  MyPlanDueState,
  MyPlanActionType,
  MyPlanKpis,
  MyPlanFocus,
  MyPlanStep,
  MyPlanRecent,
  MyPlanDto,
} from './lib/plan/plan.models';

export { RegistrationService, registrationConfig } from './lib/registration/registration.service';
export {
  ID_IMAGE_MAX_BYTES,
  ID_IMAGE_ACCEPTED_TYPES,
  ID_IMAGE_ACCEPT_ATTR,
} from './lib/registration/registration.models';
export type {
  GradeRef,
  CityRef,
  RegionRef,
  RegistrationMethod,
  RegisterFormData,
  GoogleProfile,
  RegisterResult,
  RegistrationConfig,
} from './lib/registration/registration.models';
