import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DashboardQuery, DashboardSummary } from './dashboard.models';

/**
 * Signal-backed data access for the Dashboard (Phase 5A), wired to the FROZEN contract
 * (`docs/contracts/phase5a-audit-dashboard.md`, endpoint #2 `GET /api/dashboard`). The platform JWT is
 * attached by the shared authInterceptor; the server enforces `DashboardRead` (default-deny) and scopes
 * the recent-activity rows (tenant-filtered, non-sensitive).
 */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  readonly #http = inject(HttpClient);

  readonly #summary = signal<DashboardSummary | null>(null);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly summary = this.#summary.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();

  async load(query: DashboardQuery = {}): Promise<DashboardSummary> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<DashboardSummary>(`${this.#api()}/api/dashboard`, {
          params: this.#buildParams(query),
        }),
      );
      this.#summary.set(result);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  /** `period` OR a `from`/`to` range; empties omitted. */
  #buildParams(query: DashboardQuery): HttpParams {
    let params = new HttpParams();
    if (query.period) params = params.set('period', query.period);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    return params;
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
