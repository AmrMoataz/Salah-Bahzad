import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  GradeRef,
  PagedResult,
  StudentAuditEntry,
  StudentDetail,
  StudentEnrollmentDto,
  StudentIdImageUrl,
  StudentListItem,
  StudentListQuery,
  UpdateStudentContactRequest,
} from './student.models';

/**
 * Signal-backed data access for student management & device/history (FR-ADM-STU-001..010,
 * FR-PLAT-DEV-004/006). The platform JWT is attached automatically by the shared authInterceptor;
 * the server enforces the granular `Students*` permissions (default-deny). The `#students` signals
 * back the list/approvals screens; lifecycle mutations return the refreshed detail and let the
 * caller decide whether to reload the list (the list-row and detail shapes differ).
 */
@Injectable({ providedIn: 'root' })
export class StudentService {
  readonly #http = inject(HttpClient);

  readonly #students = signal<StudentListItem[]>([]);
  readonly #total = signal(0);
  readonly #grades = signal<GradeRef[]>([]);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly students = this.#students.asReadonly();
  readonly total = this.#total.asReadonly();
  /** Grade reference data (for the list filter + contact picker), loaded on demand. */
  readonly grades = this.#grades.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();
  readonly count = computed(() => this.#students().length);

  /** Loads grade reference data once (no-op if already loaded). */
  async loadGrades(): Promise<void> {
    if (this.#grades().length > 0) return;
    const grades = await firstValueFrom(
      this.#http.get<GradeRef[]>(`${this.#apiUrl()}/api/taxonomy/grades`),
    );
    this.#grades.set(grades);
  }

  // ── List ──────────────────────────────────────────────────────────────────
  async list(query: StudentListQuery = {}): Promise<PagedResult<StudentListItem>> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<PagedResult<StudentListItem>>(`${this.#apiUrl()}/api/students`, {
          params: this.#buildListParams(query),
        }),
      );
      this.#students.set(result.items);
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
  listRaw(query: StudentListQuery = {}): Promise<PagedResult<StudentListItem>> {
    return firstValueFrom(
      this.#http.get<PagedResult<StudentListItem>>(`${this.#apiUrl()}/api/students`, {
        params: this.#buildListParams(query),
      }),
    );
  }

  #buildListParams(query: StudentListQuery): HttpParams {
    let params = new HttpParams();
    if (query.search) params = params.set('search', query.search);
    if (query.status) params = params.set('status', query.status);
    if (query.gradeId) params = params.set('gradeId', query.gradeId);
    params = params.set('page', String(query.page ?? 1));
    params = params.set('pageSize', String(query.pageSize ?? 20));
    return params;
  }

  // ── Detail / image / history ────────────────────────────────────────────────
  getById(id: string): Promise<StudentDetail> {
    return firstValueFrom(this.#http.get<StudentDetail>(`${this.#apiUrl()}/api/students/${id}`));
  }

  /** Issues a short-lived signed URL for the ID image; the access is audited server-side (FR-PLAT-AST-003). */
  getIdImageUrl(id: string): Promise<StudentIdImageUrl> {
    return firstValueFrom(
      this.#http.get<StudentIdImageUrl>(`${this.#apiUrl()}/api/students/${id}/id-image`),
    );
  }

  listLoginHistory(id: string, page = 1, pageSize = 20): Promise<PagedResult<StudentAuditEntry>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return firstValueFrom(
      this.#http.get<PagedResult<StudentAuditEntry>>(
        `${this.#apiUrl()}/api/students/${id}/login-history`,
        { params },
      ),
    );
  }

  listActivity(id: string, page = 1, pageSize = 20): Promise<PagedResult<StudentAuditEntry>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return firstValueFrom(
      this.#http.get<PagedResult<StudentAuditEntry>>(
        `${this.#apiUrl()}/api/students/${id}/activity`,
        { params },
      ),
    );
  }

  /** Paged enrolments & transactions for the detail's "Enrollments" tab (Phase 4, contract #11). */
  listEnrollments(id: string, page = 1, pageSize = 20): Promise<PagedResult<StudentEnrollmentDto>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return firstValueFrom(
      this.#http.get<PagedResult<StudentEnrollmentDto>>(
        `${this.#apiUrl()}/api/students/${id}/enrollments`,
        { params },
      ),
    );
  }

  // ── Lifecycle mutations (audited server-side) ───────────────────────────────
  approve(id: string): Promise<StudentDetail> {
    return firstValueFrom(
      this.#http.post<StudentDetail>(`${this.#apiUrl()}/api/students/${id}/approve`, {}),
    );
  }

  /** Reject a pending student — reason is mandatory (server validates, FR-ADM-STU-004). */
  reject(id: string, reason: string): Promise<StudentDetail> {
    return firstValueFrom(
      this.#http.post<StudentDetail>(`${this.#apiUrl()}/api/students/${id}/reject`, { reason }),
    );
  }

  setActive(id: string, isActive: boolean): Promise<StudentDetail> {
    return firstValueFrom(
      this.#http.post<StudentDetail>(`${this.#apiUrl()}/api/students/${id}/active`, { isActive }),
    );
  }

  updateContact(id: string, request: UpdateStudentContactRequest): Promise<StudentDetail> {
    return firstValueFrom(
      this.#http.put<StudentDetail>(`${this.#apiUrl()}/api/students/${id}/contact`, request),
    );
  }

  /** Clear the active bound device — reason is mandatory + audited (FR-PLAT-DEV-004). */
  clearDevice(id: string, reason: string): Promise<StudentDetail> {
    return firstValueFrom(
      this.#http.post<StudentDetail>(`${this.#apiUrl()}/api/students/${id}/clear-device`, { reason }),
    );
  }

  /** Mirrors AuthStore: the API base URL is injected onto window to keep shared libs env-agnostic. */
  #apiUrl(): string {
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
