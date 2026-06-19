import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { City, Grade, Specialization, Subject } from './taxonomy.models';

/**
 * Signal-backed data access for taxonomy (Grades/Subjects/Specializations) and the read-only
 * location reference data (FR-PLAT-TAX-001/002/003). The platform JWT is attached automatically by
 * the shared authInterceptor; cities are anonymously readable but reuse the same client here.
 */
@Injectable({ providedIn: 'root' })
export class TaxonomyService {
  readonly #http = inject(HttpClient);

  readonly #grades = signal<Grade[]>([]);
  readonly #subjects = signal<Subject[]>([]);
  readonly #specializations = signal<Specialization[]>([]);
  readonly #cities = signal<City[]>([]);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly grades = this.#grades.asReadonly();
  readonly subjects = this.#subjects.asReadonly();
  readonly specializations = this.#specializations.asReadonly();
  readonly cities = this.#cities.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();

  // ── Grades ────────────────────────────────────────────────────────────────
  async loadGrades(): Promise<void> {
    await this.#load(async () => {
      this.#grades.set(await firstValueFrom(this.#http.get<Grade[]>(`${this.#api()}/api/taxonomy/grades`)));
    });
  }

  async createGrade(name: string): Promise<void> {
    const created = await firstValueFrom(
      this.#http.post<Grade>(`${this.#api()}/api/taxonomy/grades`, { name }),
    );
    this.#grades.update((list) => this.#sortByName([created, ...list]));
  }

  async updateGrade(id: string, name: string): Promise<void> {
    const updated = await firstValueFrom(
      this.#http.put<Grade>(`${this.#api()}/api/taxonomy/grades/${id}`, { name }),
    );
    this.#grades.update((list) => this.#sortByName(list.map((g) => (g.id === id ? updated : g))));
  }

  async deleteGrade(id: string): Promise<void> {
    await firstValueFrom(this.#http.delete<void>(`${this.#api()}/api/taxonomy/grades/${id}`));
    this.#grades.update((list) => list.filter((g) => g.id !== id));
  }

  // ── Subjects ──────────────────────────────────────────────────────────────
  async loadSubjects(): Promise<void> {
    await this.#load(async () => {
      this.#subjects.set(await firstValueFrom(this.#http.get<Subject[]>(`${this.#api()}/api/taxonomy/subjects`)));
    });
  }

  async createSubject(name: string): Promise<void> {
    const created = await firstValueFrom(
      this.#http.post<Subject>(`${this.#api()}/api/taxonomy/subjects`, { name }),
    );
    this.#subjects.update((list) => this.#sortByName([created, ...list]));
  }

  async updateSubject(id: string, name: string): Promise<void> {
    const updated = await firstValueFrom(
      this.#http.put<Subject>(`${this.#api()}/api/taxonomy/subjects/${id}`, { name }),
    );
    this.#subjects.update((list) => this.#sortByName(list.map((s) => (s.id === id ? updated : s))));
  }

  /** Throws (HTTP 409) when the subject still has live specializations — the caller surfaces it. */
  async deleteSubject(id: string): Promise<void> {
    await firstValueFrom(this.#http.delete<void>(`${this.#api()}/api/taxonomy/subjects/${id}`));
    this.#subjects.update((list) => list.filter((s) => s.id !== id));
  }

  // ── Specializations ─────────────────────────────────────────────────────────
  async loadSpecializations(subjectId?: string): Promise<void> {
    await this.#load(async () => {
      let params = new HttpParams();
      if (subjectId) params = params.set('subjectId', subjectId);
      this.#specializations.set(
        await firstValueFrom(
          this.#http.get<Specialization[]>(`${this.#api()}/api/taxonomy/specializations`, { params }),
        ),
      );
    });
  }

  async createSpecialization(subjectId: string, name: string): Promise<void> {
    const created = await firstValueFrom(
      this.#http.post<Specialization>(`${this.#api()}/api/taxonomy/specializations`, { subjectId, name }),
    );
    this.#specializations.update((list) => this.#sortByName([created, ...list]));
  }

  async updateSpecialization(id: string, subjectId: string, name: string): Promise<void> {
    const updated = await firstValueFrom(
      this.#http.put<Specialization>(`${this.#api()}/api/taxonomy/specializations/${id}`, { subjectId, name }),
    );
    this.#specializations.update((list) => this.#sortByName(list.map((s) => (s.id === id ? updated : s))));
  }

  async deleteSpecialization(id: string): Promise<void> {
    await firstValueFrom(this.#http.delete<void>(`${this.#api()}/api/taxonomy/specializations/${id}`));
    this.#specializations.update((list) => list.filter((s) => s.id !== id));
  }

  // ── Reference (read-only) ───────────────────────────────────────────────────
  async loadCities(): Promise<void> {
    await this.#load(async () => {
      this.#cities.set(await firstValueFrom(this.#http.get<City[]>(`${this.#api()}/api/reference/cities`)));
    });
  }

  // ── Internals ────────────────────────────────────────────────────────────────
  async #load(run: () => Promise<void>): Promise<void> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      await run();
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  #sortByName<T extends { name: string }>(list: T[]): T[] {
    return [...list].sort((a, b) => a.name.localeCompare(b.name));
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
