import {
  HttpClient,
  HttpEventType,
  HttpParams,
  HttpRequest,
} from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  EnrollmentDto,
  EnrollmentListDto,
  GradeRef,
  PagedResult,
  QuestionDto,
  QuestionVariationDto,
  QuizSettingDto,
  SaveQuestionRequest,
  SaveSessionRequest,
  SaveVariationRequest,
  SessionActivityDto,
  SessionDetailDto,
  SessionListDto,
  SessionListQuery,
  SessionMaterialDto,
  SessionVideoDto,
  SignedUrlDto,
  SpecializationRef,
  StudentSearchRow,
  SubjectRef,
} from './session.models';

/**
 * Signal-backed data access for the Sessions slice — sessions, videos, materials and the question
 * bank (FR-ADM-SES-*, FR-ADM-QB-*, FR-ADM-QZ-*), wired one-method-per-endpoint to the FROZEN Phase 3
 * contract. The platform JWT is attached by the shared authInterceptor; the server enforces the
 * granular `Sessions*` / `Questions*` permissions (default-deny). `#sessions` backs the list screen;
 * detail/mutation calls return fresh DTOs and let the caller decide what to refresh. Taxonomy is read
 * directly from `/api/taxonomy/*` (not via feature-taxonomy) to stay within the Nx feature boundary.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  readonly #http = inject(HttpClient);

  readonly #sessions = signal<SessionListDto[]>([]);
  readonly #total = signal(0);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly #grades = signal<GradeRef[]>([]);
  readonly #subjects = signal<SubjectRef[]>([]);
  readonly #specializations = signal<SpecializationRef[]>([]);

  readonly sessions = this.#sessions.asReadonly();
  readonly total = this.#total.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();
  readonly count = computed(() => this.#sessions().length);

  readonly grades = this.#grades.asReadonly();
  readonly subjects = this.#subjects.asReadonly();
  readonly specializations = this.#specializations.asReadonly();

  // ── Taxonomy reference (loaded on demand; cached + in-flight-deduped) ────────
  // In-flight guards collapse concurrent callers (e.g. list → form navigation) into a single request.
  #gradesInFlight: Promise<void> | null = null;
  #subjectsInFlight: Promise<void> | null = null;
  #specsInFlight: Promise<void> | null = null;

  loadGrades(): Promise<void> {
    if (this.#grades().length > 0) return Promise.resolve();
    this.#gradesInFlight ??= firstValueFrom(this.#http.get<GradeRef[]>(`${this.#api()}/api/taxonomy/grades`))
      .then((items) => void this.#grades.set(items))
      .finally(() => (this.#gradesInFlight = null));
    return this.#gradesInFlight;
  }

  loadSubjects(): Promise<void> {
    if (this.#subjects().length > 0) return Promise.resolve();
    this.#subjectsInFlight ??= firstValueFrom(this.#http.get<SubjectRef[]>(`${this.#api()}/api/taxonomy/subjects`))
      .then((items) => void this.#subjects.set(items))
      .finally(() => (this.#subjectsInFlight = null));
    return this.#subjectsInFlight;
  }

  loadSpecializations(): Promise<void> {
    if (this.#specializations().length > 0) return Promise.resolve();
    this.#specsInFlight ??= firstValueFrom(
      this.#http.get<SpecializationRef[]>(`${this.#api()}/api/taxonomy/specializations`),
    )
      .then((items) => void this.#specializations.set(items))
      .finally(() => (this.#specsInFlight = null));
    return this.#specsInFlight;
  }

  // ── Sessions list (§2.1) ─────────────────────────────────────────────────────
  async list(query: SessionListQuery = {}): Promise<PagedResult<SessionListDto>> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<PagedResult<SessionListDto>>(`${this.#api()}/api/sessions`, {
          params: this.#buildListParams(query),
        }),
      );
      this.#sessions.set(result.items);
      this.#total.set(result.total);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  /** Non-mutating fetch (doesn't touch the list signals) — used to gather all rows for CSV export. */
  listRaw(query: SessionListQuery = {}): Promise<PagedResult<SessionListDto>> {
    return firstValueFrom(
      this.#http.get<PagedResult<SessionListDto>>(`${this.#api()}/api/sessions`, {
        params: this.#buildListParams(query),
      }),
    );
  }

  #buildListParams(query: SessionListQuery): HttpParams {
    let params = new HttpParams();
    if (query.search) params = params.set('search', query.search);
    if (query.gradeId) params = params.set('gradeId', query.gradeId);
    if (query.subjectId) params = params.set('subjectId', query.subjectId);
    if (query.status) params = params.set('status', query.status);
    params = params.set('page', String(query.page ?? 1));
    params = params.set('pageSize', String(query.pageSize ?? 20));
    return params;
  }

  // ── Session CRUD + lifecycle (§2.2–§2.10) ────────────────────────────────────
  getById(id: string): Promise<SessionDetailDto> {
    return firstValueFrom(this.#http.get<SessionDetailDto>(`${this.#api()}/api/sessions/${id}`));
  }

  create(request: SaveSessionRequest): Promise<SessionDetailDto> {
    return firstValueFrom(this.#http.post<SessionDetailDto>(`${this.#api()}/api/sessions`, request));
  }

  update(id: string, request: SaveSessionRequest): Promise<SessionDetailDto> {
    return firstValueFrom(
      this.#http.put<SessionDetailDto>(`${this.#api()}/api/sessions/${id}`, request),
    );
  }

  uploadThumbnail(id: string, file: File): Promise<SessionDetailDto> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(
      this.#http.put<SessionDetailDto>(`${this.#api()}/api/sessions/${id}/thumbnail`, form),
    );
  }

  /** Set or clear (null) the prerequisite session; 409 on self/cycle (§2.6). */
  setPrerequisite(id: string, prerequisiteSessionId: string | null): Promise<SessionDetailDto> {
    return firstValueFrom(
      this.#http.put<SessionDetailDto>(`${this.#api()}/api/sessions/${id}/prerequisite`, {
        prerequisiteSessionId,
      }),
    );
  }

  updateQuizSettings(id: string, settings: QuizSettingDto): Promise<SessionDetailDto> {
    return firstValueFrom(
      this.#http.put<SessionDetailDto>(`${this.#api()}/api/sessions/${id}/quiz-settings`, settings),
    );
  }

  publish(id: string): Promise<SessionDetailDto> {
    return firstValueFrom(
      this.#http.post<SessionDetailDto>(`${this.#api()}/api/sessions/${id}/publish`, {}),
    );
  }

  archive(id: string): Promise<SessionDetailDto> {
    return firstValueFrom(
      this.#http.post<SessionDetailDto>(`${this.#api()}/api/sessions/${id}/archive`, {}),
    );
  }

  remove(id: string): Promise<void> {
    return firstValueFrom(this.#http.delete<void>(`${this.#api()}/api/sessions/${id}`));
  }

  // ── Videos (§2.11–§2.14) ─────────────────────────────────────────────────────
  /** Upload a source video; reports 0–100% via `onProgress` while the bytes transfer. */
  addVideo(
    id: string,
    file: File,
    title: string,
    accessCount: number,
    onProgress?: (percent: number) => void,
  ): Promise<SessionVideoDto> {
    // Metadata first, file LAST: the backend streams the source straight to R2 with a MultipartReader
    // and needs the title/access fields before it reaches the (multi-GB) file part.
    const form = new FormData();
    form.append('title', title);
    form.append('accessCount', String(accessCount));
    form.append('file', file);
    const req = new HttpRequest('POST', `${this.#api()}/api/sessions/${id}/videos`, form, {
      reportProgress: true,
    });
    return this.#sendWithProgress<SessionVideoDto>(req, onProgress);
  }

  reorderVideos(id: string, orderedVideoIds: string[]): Promise<SessionVideoDto[]> {
    return firstValueFrom(
      this.#http.put<SessionVideoDto[]>(`${this.#api()}/api/sessions/${id}/videos/reorder`, {
        orderedVideoIds,
      }),
    );
  }

  /** Edit video metadata (title / access) and optionally replace the source (contract §2.13). */
  updateVideo(
    id: string,
    videoId: string,
    title: string,
    accessCount: number,
    file?: File,
  ): Promise<SessionVideoDto> {
    const form = new FormData();
    form.append('title', title);
    form.append('accessCount', String(accessCount));
    if (file) form.append('file', file);
    return firstValueFrom(
      this.#http.put<SessionVideoDto>(`${this.#api()}/api/sessions/${id}/videos/${videoId}`, form),
    );
  }

  removeVideo(id: string, videoId: string): Promise<void> {
    return firstValueFrom(
      this.#http.delete<void>(`${this.#api()}/api/sessions/${id}/videos/${videoId}`),
    );
  }

  // ── Materials (§2.15–§2.17) ──────────────────────────────────────────────────
  addMaterial(id: string, file: File): Promise<SessionMaterialDto> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(
      this.#http.post<SessionMaterialDto>(`${this.#api()}/api/sessions/${id}/materials`, form),
    );
  }

  /** On-demand signed URL for an explicit material download (audited server-side). */
  getMaterialUrl(id: string, materialId: string): Promise<SignedUrlDto> {
    return firstValueFrom(
      this.#http.get<SignedUrlDto>(`${this.#api()}/api/sessions/${id}/materials/${materialId}/url`),
    );
  }

  removeMaterial(id: string, materialId: string): Promise<void> {
    return firstValueFrom(
      this.#http.delete<void>(`${this.#api()}/api/sessions/${id}/materials/${materialId}`),
    );
  }

  // ── Activity (per-session audit feed, §2.27) ─────────────────────────────────
  /** Paged audit history for the session — every question/video/material/lifecycle event keyed to it. */
  listActivity(id: string, page = 1, pageSize = 20): Promise<PagedResult<SessionActivityDto>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return firstValueFrom(
      this.#http.get<PagedResult<SessionActivityDto>>(
        `${this.#api()}/api/sessions/${id}/activity`,
        { params },
      ),
    );
  }

  // ── Enrollment (Phase 4 — contract §3) ───────────────────────────────────────
  /** Paged enrolled-students list for the session-detail "Enrolled" tab (#8). */
  listEnrollments(
    id: string,
    page = 1,
    pageSize = 20,
    search?: string,
  ): Promise<PagedResult<EnrollmentListDto>> {
    let params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (search) params = params.set('search', search);
    return firstValueFrom(
      this.#http.get<PagedResult<EnrollmentListDto>>(
        `${this.#api()}/api/sessions/${id}/enrollments`,
        { params },
      ),
    );
  }

  /** Manually unlock the session for a student, bypassing code & price (#9); 409 if already-active. */
  unlock(id: string, studentId: string): Promise<EnrollmentDto> {
    return firstValueFrom(
      this.#http.post<EnrollmentDto>(`${this.#api()}/api/sessions/${id}/unlock`, { studentId }),
    );
  }

  /** Refund + revoke an enrollment (#10); returns the enrollment with status `Refunded`. */
  refund(enrollmentId: string, reason?: string): Promise<EnrollmentDto> {
    return firstValueFrom(
      this.#http.post<EnrollmentDto>(`${this.#api()}/api/enrollments/${enrollmentId}/refund`, {
        reason: reason ?? null,
      }),
    );
  }

  /**
   * Active-student search for the unlock picker — reuses the Phase-2 `/api/students` list filtered to
   * `status=Active`. Called from this slice's own service (no `feature-students` import — Nx boundary).
   */
  async searchActiveStudents(query = ''): Promise<StudentSearchRow[]> {
    let params = new HttpParams().set('status', 'Active').set('page', '1').set('pageSize', '100');
    if (query) params = params.set('search', query);
    const result = await firstValueFrom(
      this.#http.get<PagedResult<{ id: string; fullName: string; phoneNumber: string }>>(
        `${this.#api()}/api/students`,
        { params },
      ),
    );
    return result.items.map((s) => ({ id: s.id, name: s.fullName, phone: s.phoneNumber }));
  }

  // ── Question bank (§2.18–§2.22) ──────────────────────────────────────────────
  listQuestions(id: string, page = 1, pageSize = 20): Promise<PagedResult<QuestionDto>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return firstValueFrom(
      this.#http.get<PagedResult<QuestionDto>>(`${this.#api()}/api/sessions/${id}/questions`, {
        params,
      }),
    );
  }

  /** Create a question. Pass `image` to create an **image-only** question (or any question with an image)
   * in a single call — it is sent inline as base64 (§2.19); the body may then be omitted. */
  async createQuestion(
    id: string,
    request: SaveQuestionRequest,
    image?: File | null,
  ): Promise<QuestionDto> {
    const body: SaveQuestionRequest = { ...request };
    if (image) {
      body.imageBase64 = await this.#toBase64(image);
      body.imageContentType = image.type;
    }
    return firstValueFrom(
      this.#http.post<QuestionDto>(`${this.#api()}/api/sessions/${id}/questions`, body),
    );
  }

  /** Reads a File as bare base64 (strips the `data:<type>;base64,` prefix from the data URL). */
  #toBase64(file: File): Promise<string> {
    return new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onerror = () => reject(reader.error);
      reader.onload = () => {
        const result = reader.result as string;
        resolve(result.slice(result.indexOf(',') + 1));
      };
      reader.readAsDataURL(file);
    });
  }

  updateQuestion(
    id: string,
    questionId: string,
    request: SaveQuestionRequest,
  ): Promise<QuestionDto> {
    return firstValueFrom(
      this.#http.put<QuestionDto>(
        `${this.#api()}/api/sessions/${id}/questions/${questionId}`,
        request,
      ),
    );
  }

  uploadQuestionImage(id: string, questionId: string, file: File): Promise<QuestionDto> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(
      this.#http.put<QuestionDto>(
        `${this.#api()}/api/sessions/${id}/questions/${questionId}/image`,
        form,
      ),
    );
  }

  clearQuestionImage(id: string, questionId: string): Promise<QuestionDto> {
    return firstValueFrom(
      this.#http.delete<QuestionDto>(
        `${this.#api()}/api/sessions/${id}/questions/${questionId}/image`,
      ),
    );
  }

  removeQuestion(id: string, questionId: string): Promise<void> {
    return firstValueFrom(
      this.#http.delete<void>(`${this.#api()}/api/sessions/${id}/questions/${questionId}`),
    );
  }

  // ── Variations (§2.23–§2.26) ─────────────────────────────────────────────────
  /** Add a variation. Pass `image` to create an **image-only** variation (or any with an image) in one
   * call — sent inline as base64 (§2.24); the body may then be omitted. */
  async addVariation(
    id: string,
    questionId: string,
    request: SaveVariationRequest,
    image?: File | null,
  ): Promise<QuestionVariationDto> {
    const body: SaveVariationRequest = { ...request };
    if (image) {
      body.imageBase64 = await this.#toBase64(image);
      body.imageContentType = image.type;
    }
    return firstValueFrom(
      this.#http.post<QuestionVariationDto>(
        `${this.#api()}/api/sessions/${id}/questions/${questionId}/variations`,
        body,
      ),
    );
  }

  updateVariation(
    id: string,
    questionId: string,
    variationId: string,
    request: SaveVariationRequest,
  ): Promise<QuestionVariationDto> {
    return firstValueFrom(
      this.#http.put<QuestionVariationDto>(
        `${this.#api()}/api/sessions/${id}/questions/${questionId}/variations/${variationId}`,
        request,
      ),
    );
  }

  uploadVariationImage(
    id: string,
    questionId: string,
    variationId: string,
    file: File,
  ): Promise<QuestionVariationDto> {
    const form = new FormData();
    form.append('file', file);
    return firstValueFrom(
      this.#http.put<QuestionVariationDto>(
        `${this.#api()}/api/sessions/${id}/questions/${questionId}/variations/${variationId}/image`,
        form,
      ),
    );
  }

  removeVariation(id: string, questionId: string, variationId: string): Promise<void> {
    return firstValueFrom(
      this.#http.delete<void>(
        `${this.#api()}/api/sessions/${id}/questions/${questionId}/variations/${variationId}`,
      ),
    );
  }

  // ── Internals ──────────────────────────────────────────────────────────────
  /** Sends an upload request and resolves with the response body, surfacing byte-level progress. */
  #sendWithProgress<T>(
    req: HttpRequest<FormData>,
    onProgress?: (percent: number) => void,
  ): Promise<T> {
    return new Promise<T>((resolve, reject) => {
      this.#http.request<T>(req).subscribe({
        next: (event) => {
          if (event.type === HttpEventType.UploadProgress && event.total) {
            onProgress?.(Math.round((event.loaded / event.total) * 100));
          } else if (event.type === HttpEventType.Response) {
            resolve(event.body as T);
          }
        },
        error: reject,
      });
    });
  }

  /** Mirrors AuthStore: the API base URL is injected onto window to keep shared libs env-agnostic. */
  #api(): string {
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }

  #message(err: unknown): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const body = (err as { error?: unknown }).error;
      if (body && typeof body === 'object' && 'detail' in body) {
        const detail = (body as { detail?: unknown }).detail;
        if (typeof detail === 'string') return detail;
      }
    }
    return 'Something went wrong. Please try again.';
  }
}
