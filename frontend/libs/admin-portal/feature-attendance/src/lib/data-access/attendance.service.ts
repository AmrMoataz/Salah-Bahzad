import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  PagedResult,
  SessionAttendanceRow,
  SessionOption,
  StudentAttendanceRow,
  StudentOption,
} from './attendance.models';

/**
 * Signal-backed data access for the Attendance & reporting screen (Phase 5B-1), wired
 * one-method-per-endpoint to the FROZEN contract
 * (`docs/contracts/phase5b1-assignments-attendance.md` §B, endpoints #4–6). The platform JWT is
 * attached by the shared authInterceptor; the server enforces `AttendanceRead` (reads) and
 * `AttendanceExport` (CSV) with default-deny.
 *
 * The two matrices each back their own signal (`#sessionRows` for "By session", `#studentRows` for
 * "By student"). CSV exports stream from the server and are **audited server-side** (the GET bypasses
 * the audit interceptor, exactly like the Phase-4 code export), so each export is a single request and
 * honours the server's `Content-Disposition` filename. The session/student combo reference lists read
 * `/api/sessions` and `/api/students` directly to stay within the Nx feature boundary (no
 * `feature-sessions`/`feature-students` import — mirrors `code.service.ts#loadSessions`).
 */
@Injectable({ providedIn: 'root' })
export class AttendanceService {
  readonly #http = inject(HttpClient);

  readonly #sessionRows = signal<SessionAttendanceRow[]>([]);
  readonly #studentRows = signal<StudentAttendanceRow[]>([]);
  readonly #total = signal(0);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);
  readonly #sessions = signal<SessionOption[]>([]);
  readonly #students = signal<StudentOption[]>([]);

  readonly sessionRows = this.#sessionRows.asReadonly();
  readonly studentRows = this.#studentRows.asReadonly();
  readonly total = this.#total.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();
  /** Session reference for the "By session" combo (loaded on demand). */
  readonly sessions = this.#sessions.asReadonly();
  /** Active-student reference for the "By student" combo (loaded on demand). */
  readonly students = this.#students.asReadonly();

  // ── Cohort matrices (#4 / #5) ──────────────────────────────────────────────────
  /** "By session" cohort matrix (#4, `FR-ADM-ATT-001`). */
  async listBySession(sessionId: string, page = 1): Promise<PagedResult<SessionAttendanceRow>> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<PagedResult<SessionAttendanceRow>>(
          `${this.#api()}/api/attendance/sessions/${sessionId}`,
          { params: this.#pageParams(page) },
        ),
      );
      this.#sessionRows.set(result.items);
      this.#total.set(result.total);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  /** "By student" per-session breakdown (#5, `FR-ADM-ATT-002`). */
  async listByStudent(studentId: string, page = 1): Promise<PagedResult<StudentAttendanceRow>> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<PagedResult<StudentAttendanceRow>>(
          `${this.#api()}/api/attendance/students/${studentId}`,
          { params: this.#pageParams(page) },
        ),
      );
      this.#studentRows.set(result.items);
      this.#total.set(result.total);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  #pageParams(page: number): HttpParams {
    return new HttpParams().set('page', String(page)).set('pageSize', '50');
  }

  // ── Server CSV exports (#6) — audited; one request each ─────────────────────────
  /** Export the cohort matrix for a session (#6, `FR-ADM-ATT-004`) → streams a CSV file download. */
  async exportSession(sessionId: string): Promise<void> {
    const res = await firstValueFrom(
      this.#http.get(`${this.#api()}/api/attendance/sessions/${sessionId}/export`, {
        responseType: 'blob',
        observe: 'response',
      }),
    );
    this.#saveResponse(res, 'attendance-session.csv');
  }

  /** Export the per-session breakdown for a student (#6, `FR-ADM-ATT-004`) → streams a CSV file download. */
  async exportStudent(studentId: string): Promise<void> {
    const res = await firstValueFrom(
      this.#http.get(`${this.#api()}/api/attendance/students/${studentId}/export`, {
        responseType: 'blob',
        observe: 'response',
      }),
    );
    this.#saveResponse(res, 'attendance-student.csv');
  }

  // ── Combo reference lists (read directly, not via feature-sessions/feature-students) ──────────
  #sessionsInFlight: Promise<void> | null = null;
  #studentsInFlight: Promise<void> | null = null;

  /** Loads sessions once for the "By session" combo (cached + in-flight-deduped). */
  loadSessions(): Promise<void> {
    if (this.#sessions().length > 0) return Promise.resolve();
    this.#sessionsInFlight ??= firstValueFrom(
      this.#http.get<PagedResult<SessionOption>>(`${this.#api()}/api/sessions`, {
        params: new HttpParams().set('page', '1').set('pageSize', '200'),
      }),
    )
      .then((result) =>
        this.#sessions.set(result.items.map((s) => ({ id: s.id, title: s.title }))),
      )
      .finally(() => (this.#sessionsInFlight = null));
    return this.#sessionsInFlight;
  }

  /** Loads active students once for the "By student" combo (cached + in-flight-deduped). */
  loadStudents(): Promise<void> {
    if (this.#students().length > 0) return Promise.resolve();
    this.#studentsInFlight ??= firstValueFrom(
      this.#http.get<PagedResult<{ id: string; fullName: string }>>(
        `${this.#api()}/api/students`,
        { params: new HttpParams().set('status', 'Active').set('page', '1').set('pageSize', '200') },
      ),
    )
      .then((result) =>
        this.#students.set(result.items.map((s) => ({ id: s.id, name: s.fullName }))),
      )
      .finally(() => (this.#studentsInFlight = null));
    return this.#studentsInFlight;
  }

  // ── Internals ────────────────────────────────────────────────────────────────────
  /** Saves a streamed CSV response, honouring the server's `Content-Disposition` filename. */
  #saveResponse(res: HttpResponse<Blob>, fallback: string): void {
    if (!res.body) return;
    this.#saveBlob(res.body, this.#filenameFromResponse(res, fallback));
  }

  #filenameFromResponse(res: HttpResponse<Blob>, fallback: string): string {
    const disposition = res.headers.get('content-disposition') ?? '';
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    return match ? decodeURIComponent(match[1]) : fallback;
  }

  /** Triggers a browser download for a blob (no-op outside a DOM, e.g. under jsdom in tests). */
  #saveBlob(blob: Blob, filename: string): void {
    if (typeof URL === 'undefined' || typeof URL.createObjectURL !== 'function') return;
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1500);
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
