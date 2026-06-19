import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * App-wide count of students awaiting approval, used for the sidebar "Approvals" badge
 * (FR-ADM-STU-003). Lives in shared data-access because the shell (which renders the badge) and the
 * students feature (which changes the count on approve/reject) are sibling features that can't import
 * each other. Authoritative: `refresh()` re-reads the server total — call it on app load, on
 * navigation, and after any approve/reject so the badge stays correct across actors.
 */
@Injectable({ providedIn: 'root' })
export class PendingApprovalsStore {
  readonly #http = inject(HttpClient);

  readonly #count = signal(0);
  /** Number of pending registrations; 0 when none or when the caller can't read students. */
  readonly count = this.#count.asReadonly();

  async refresh(): Promise<void> {
    try {
      const params = new HttpParams().set('status', 'Pending').set('page', '1').set('pageSize', '1');
      const result = await firstValueFrom(
        this.#http.get<{ total: number }>(`${this.#apiUrl()}/api/students`, { params }),
      );
      this.#count.set(result.total ?? 0);
    } catch {
      // The badge is non-critical — keep the last known count (e.g. on a 403 for roles without access).
    }
  }

  /** Mirrors AuthStore: the API base URL is injected onto window to keep shared libs env-agnostic. */
  #apiUrl(): string {
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
