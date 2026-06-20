import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CodeBatchDto,
  CodeListDto,
  CodeListQuery,
  GenerateCodesRequest,
  PagedResult,
  SessionOption,
} from './code.models';

/**
 * Signal-backed data access for the Codes register & generation (FR-ADM-COD-001..005,
 * FR-PLAT-COD-001..006), wired one-method-per-endpoint to the FROZEN Phase 4 contract
 * (`docs/contracts/phase4-codes-enrollment.md`, endpoints #1–7). The platform JWT is attached by the
 * shared authInterceptor; the server enforces the granular `Codes*` permissions (default-deny).
 *
 * `#codes` backs the register; mutations (#5/#6) return a fresh `CodeListDto`. CSV exports stream from
 * the server (#3 whole filtered set, #4 a single batch) and are audited server-side — so each export
 * issues exactly one request. The bulk "Export selection" is a **client-side** CSV built from rows
 * already in memory (`exportRows`). There is intentionally **no** redeem method here — redemption
 * (#12) is the student-portal path and is never called by the admin portal.
 */
@Injectable({ providedIn: 'root' })
export class CodeService {
  readonly #http = inject(HttpClient);

  readonly #codes = signal<CodeListDto[]>([]);
  readonly #total = signal(0);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);
  readonly #sessions = signal<SessionOption[]>([]);

  readonly codes = this.#codes.asReadonly();
  readonly total = this.#total.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();
  readonly count = computed(() => this.#codes().length);
  /** Session reference for the register filter + generate combos (loaded on demand). */
  readonly sessions = this.#sessions.asReadonly();

  // ── Sessions reference (read via this slice's own service, not feature-sessions) ───────────────
  #sessionsInFlight: Promise<void> | null = null;

  /** Loads sessions once for the combos (cached + in-flight-deduped). Reads `/api/sessions` directly to
   * stay within the Nx feature boundary — no `feature-sessions` TS import. */
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

  /** The current price of a session — pre-fills the generate "Value (EGP)" field (contract §5). The
   * list DTO carries no price, so this reads the Phase-3 detail endpoint for the chosen session. */
  async loadSessionPrice(sessionId: string): Promise<number> {
    const detail = await firstValueFrom(
      this.#http.get<{ price: number }>(`${this.#api()}/api/sessions/${sessionId}`),
    );
    return detail.price;
  }

  // ── Register list (#1) ───────────────────────────────────────────────────────
  async list(query: CodeListQuery = {}): Promise<PagedResult<CodeListDto>> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<PagedResult<CodeListDto>>(`${this.#api()}/api/codes`, {
          params: this.#buildListParams(query),
        }),
      );
      this.#codes.set(result.items);
      this.#total.set(result.total);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  /** The shared filter params (#1 / #3 / #4): search + status + batchId + sessionId. */
  #buildFilterParams(query: CodeListQuery): HttpParams {
    let params = new HttpParams();
    if (query.search) params = params.set('search', query.search);
    if (query.status) params = params.set('status', query.status);
    if (query.batchId) params = params.set('batchId', query.batchId);
    if (query.sessionId) params = params.set('sessionId', query.sessionId);
    return params;
  }

  #buildListParams(query: CodeListQuery): HttpParams {
    return this.#buildFilterParams(query)
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
  }

  // ── Generate a batch (#2) ──────────────────────────────────────────────────────
  generateBatch(request: GenerateCodesRequest): Promise<CodeBatchDto> {
    return firstValueFrom(
      this.#http.post<CodeBatchDto>(`${this.#api()}/api/codes/batches`, request),
    );
  }

  // ── Server CSV exports (#3 / #4) — audited; one request each ───────────────────
  /** Server export of the whole filtered set (#3) → streams a CSV file download. */
  async export(query: CodeListQuery = {}): Promise<void> {
    const res = await firstValueFrom(
      this.#http.get(`${this.#api()}/api/codes/export`, {
        params: this.#buildFilterParams(query),
        responseType: 'blob',
        observe: 'response',
      }),
    );
    this.#saveResponse(res, 'codes.csv');
  }

  /** Re-export a single batch (#4, FR-ADM-COD-005) → streams a CSV file download. */
  async exportBatch(batchId: string): Promise<void> {
    const res = await firstValueFrom(
      this.#http.get(`${this.#api()}/api/codes/batches/${batchId}/export`, {
        responseType: 'blob',
        observe: 'response',
      }),
    );
    this.#saveResponse(res, 'codes-batch.csv');
  }

  // ── Enable / disable / delete (#5 / #6 / #7) ───────────────────────────────────
  disable(id: string): Promise<CodeListDto> {
    return firstValueFrom(
      this.#http.post<CodeListDto>(`${this.#api()}/api/codes/${id}/disable`, {}),
    );
  }

  enable(id: string): Promise<CodeListDto> {
    return firstValueFrom(
      this.#http.post<CodeListDto>(`${this.#api()}/api/codes/${id}/enable`, {}),
    );
  }

  /** Soft-delete a code (#7); 409 if the code is `Used`. */
  remove(id: string): Promise<void> {
    return firstValueFrom(this.#http.delete<void>(`${this.#api()}/api/codes/${id}`));
  }

  // ── Client-side selection CSV (bulk "Export selection") ────────────────────────
  /** Builds the **selection** CSV in-browser from rows already loaded (matches `scrCodes` —
   * columns Serial / Value / Status / Session). No server round-trip. */
  exportRows(rows: readonly CodeListDto[], filename = 'codes-selection.csv'): void {
    const header = ['Serial', 'Value', 'Status', 'Session'];
    const lines = [header.map(this.#csvEscape).join(',')];
    for (const r of rows) {
      lines.push(
        [r.serial, r.value, this.#statusCsvLabel(r.status), r.sessionTitle]
          .map(this.#csvEscape)
          .join(','),
      );
    }
    this.#saveBlob(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' }), filename);
  }

  // ── Internals ──────────────────────────────────────────────────────────────────
  /** UI label for a status in CSV — `Inactive` reads as "Disabled" (matches the register). */
  #statusCsvLabel(status: CodeListDto['status']): string {
    return status === 'Inactive' ? 'Disabled' : status;
  }

  #csvEscape = (value: unknown): string =>
    `"${String(value ?? '').replace(/"/g, '""')}"`;

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
