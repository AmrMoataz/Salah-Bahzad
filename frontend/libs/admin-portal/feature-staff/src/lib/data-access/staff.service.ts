import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CreateStaffRequest,
  PagedResult,
  PasswordResetResponse,
  StaffDetail,
  StaffListItem,
  StaffListQuery,
  UpdateStaffRequest,
} from './staff.models';

/**
 * Signal-backed data access for staff & role management (FR-ADM-STAFF-001..004).
 * The platform JWT is attached automatically by the shared authInterceptor.
 */
@Injectable({ providedIn: 'root' })
export class StaffService {
  readonly #http = inject(HttpClient);

  readonly #staff = signal<StaffListItem[]>([]);
  readonly #total = signal(0);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly staff = this.#staff.asReadonly();
  readonly total = this.#total.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();
  readonly count = computed(() => this.#staff().length);

  async list(query: StaffListQuery = {}): Promise<PagedResult<StaffListItem>> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      let params = new HttpParams();
      if (query.search) params = params.set('search', query.search);
      if (query.role) params = params.set('role', query.role);
      if (query.isActive !== undefined && query.isActive !== null) {
        params = params.set('isActive', String(query.isActive));
      }
      params = params.set('page', String(query.page ?? 1));
      params = params.set('pageSize', String(query.pageSize ?? 20));

      const result = await firstValueFrom(
        this.#http.get<PagedResult<StaffListItem>>(`${this.#apiUrl()}/api/staff`, { params }),
      );
      this.#staff.set(result.items);
      this.#total.set(result.total);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  getById(id: string): Promise<StaffDetail> {
    return firstValueFrom(this.#http.get<StaffDetail>(`${this.#apiUrl()}/api/staff/${id}`));
  }

  async create(request: CreateStaffRequest): Promise<StaffListItem> {
    const created = await firstValueFrom(
      this.#http.post<StaffListItem>(`${this.#apiUrl()}/api/staff`, request),
    );
    this.#staff.update((list) => [created, ...list]);
    this.#total.update((t) => t + 1);
    return created;
  }

  async update(id: string, request: UpdateStaffRequest): Promise<StaffListItem> {
    const updated = await firstValueFrom(
      this.#http.put<StaffListItem>(`${this.#apiUrl()}/api/staff/${id}`, request),
    );
    this.#staff.update((list) => list.map((s) => (s.id === id ? updated : s)));
    return updated;
  }

  async setActive(id: string, isActive: boolean): Promise<StaffListItem> {
    const updated = await firstValueFrom(
      this.#http.post<StaffListItem>(`${this.#apiUrl()}/api/staff/${id}/active`, { isActive }),
    );
    this.#staff.update((list) => list.map((s) => (s.id === id ? updated : s)));
    return updated;
  }

  sendPasswordReset(id: string): Promise<PasswordResetResponse> {
    return firstValueFrom(
      this.#http.post<PasswordResetResponse>(`${this.#apiUrl()}/api/staff/${id}/password-reset`, {}),
    );
  }

  async remove(id: string): Promise<void> {
    await firstValueFrom(this.#http.delete<void>(`${this.#apiUrl()}/api/staff/${id}`));
    this.#staff.update((list) => list.filter((s) => s.id !== id));
    this.#total.update((t) => Math.max(0, t - 1));
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
