import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuditFeedItem, AuditListQuery, PagedResult } from './audit.models';

/**
 * Signal-backed data access for the Audit-log browser (Phase 5A), wired one-method-per-endpoint to the
 * FROZEN contract (`docs/contracts/phase5a-audit-dashboard.md`, endpoint #1 `GET /api/audit`). The
 * platform JWT is attached by the shared authInterceptor; the server enforces `AuditRead` and scopes
 * out sensitive rows for Assistants (default-deny) — the UI only reflects role.
 *
 * `#items` backs the feed. There is intentionally **no** detail method: 5A exposes no before/after
 * endpoint (contract §1); drill-in navigates to the affected entity.
 */
@Injectable({ providedIn: 'root' })
export class AuditService {
  readonly #http = inject(HttpClient);

  readonly #items = signal<AuditFeedItem[]>([]);
  readonly #total = signal(0);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly items = this.#items.asReadonly();
  readonly total = this.#total.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();
  readonly count = computed(() => this.#items().length);

  // ── Activity feed (#1) ───────────────────────────────────────────────────────
  async list(query: AuditListQuery = {}): Promise<PagedResult<AuditFeedItem>> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<PagedResult<AuditFeedItem>>(`${this.#api()}/api/audit`, {
          params: this.#buildListParams(query),
        }),
      );
      this.#items.set(result.items);
      this.#total.set(result.total);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  /** Builds the query string, omitting every empty filter (so the URL stays clean). */
  #buildListParams(query: AuditListQuery): HttpParams {
    let params = new HttpParams();
    if (query.actorId) params = params.set('actorId', query.actorId);
    if (query.actorType) params = params.set('actorType', query.actorType);
    if (query.category) params = params.set('category', query.category);
    if (query.period) params = params.set('period', query.period);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    if (query.studentId) params = params.set('studentId', query.studentId);
    if (query.sessionId) params = params.set('sessionId', query.sessionId);
    if (query.entityType) params = params.set('entityType', query.entityType);
    if (query.entityId) params = params.set('entityId', query.entityId);
    return params
      .set('page', String(query.page ?? 1))
      .set('pageSize', String(query.pageSize ?? 20));
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
