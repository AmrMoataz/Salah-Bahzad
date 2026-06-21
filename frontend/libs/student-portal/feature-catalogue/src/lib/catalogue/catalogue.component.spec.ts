import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  CatalogueService: class CatalogueService {},
}));

import { CatalogueComponent } from './catalogue.component';
import { CatalogueService, CatalogueSession } from '@sb/student-portal/data-access';

function makeSession(over: Partial<CatalogueSession> = {}): CatalogueSession {
  return {
    id: 's1',
    title: 'Algebra Basics',
    description: 'Start here.',
    price: 150,
    thumbnailUrl: null,
    gradeId: 'g1',
    gradeName: 'Grade 1',
    subjectId: 'sub1',
    subjectName: 'Maths',
    specializationId: 'spec1',
    specializationName: 'Pure Maths',
    videoCount: 4,
    materialCount: 2,
    validityDays: 30,
    hasQuiz: true,
    hasAssignment: true,
    prerequisiteSessionId: null,
    prerequisiteTitle: null,
    prerequisiteSatisfied: true,
    enrollmentState: 'NotEnrolled',
    enrolledExpiresAtUtc: null,
    ...over,
  };
}

describe('CatalogueComponent (FR-STU-CAT-001/002/004)', () => {
  let fixture: ComponentFixture<CatalogueComponent>;
  let component: CatalogueComponent;
  let service: { catalogue: jest.Mock; redeem: jest.Mock };

  function setup(sessions: CatalogueSession[]) {
    service = {
      catalogue: jest.fn().mockReturnValue(of(sessions)),
      redeem: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [CatalogueComponent],
      providers: [provideRouter([]), { provide: CatalogueService, useValue: service }],
    });
    fixture = TestBed.createComponent(CatalogueComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    return fixture;
  }

  const root = () => fixture.nativeElement as HTMLElement;
  const thumbs = (): HTMLElement[] =>
    Array.from(root().querySelectorAll<HTMLElement>('sb-session-thumb'));
  const text = () => root().textContent ?? '';

  it('renders a SessionThumb card per published session', async () => {
    setup([makeSession({ id: 's1' }), makeSession({ id: 's2', title: 'Geometry' })]);
    await fixture.whenStable();
    expect(thumbs()).toHaveLength(2);
  });

  it('renders the extra card data: materials count + Quiz/Assignment badges', async () => {
    setup([makeSession({ videoCount: 8, materialCount: 5, hasQuiz: true, hasAssignment: false })]);
    await fixture.whenStable();

    const card = thumbs()[0];
    expect(card.textContent).toContain('8');
    expect(card.textContent).toContain('videos');
    expect(card.textContent).toContain('5');
    expect(card.textContent).toContain('materials');
    expect(card.textContent).toContain('Quiz');
    // Assignment badge is hidden when the session has none.
    expect(card.textContent).not.toContain('Assignment');
  });

  it('shows the mascot empty state when there are no sessions (FR-STU-CAT-001)', async () => {
    setup([]);
    await fixture.whenStable();

    expect(thumbs()).toHaveLength(0);
    const empty = root().querySelector('.cat__empty');
    expect(empty).toBeTruthy();
    expect(empty?.querySelector('img')?.getAttribute('src')).toContain('salah-mascot');
    expect(text()).toContain('Nothing here');
  });

  it('filters client-side by the specialization chip-bar and restores on "All"', async () => {
    setup([
      makeSession({ id: 's1', specializationId: 'spec1', specializationName: 'Pure Maths' }),
      makeSession({ id: 's2', specializationId: 'spec2', specializationName: 'Physics' }),
    ]);
    await fixture.whenStable();
    expect(thumbs()).toHaveLength(2);

    const chips = Array.from(root().querySelectorAll<HTMLButtonElement>('.cat__chip'));
    // All + 2 specs.
    expect(chips.map((c) => c.textContent?.trim())).toEqual(['All', 'Pure Maths', 'Physics']);

    component.selectSpec('spec1');
    fixture.detectChanges();
    expect(thumbs()).toHaveLength(1);

    component.selectSpec(null);
    fixture.detectChanges();
    expect(thumbs()).toHaveLength(2);
  });

  it('renders Open for an enrolled card and Enroll for a not-enrolled one (CTA by state)', async () => {
    setup([
      makeSession({ id: 's1', enrollmentState: 'NotEnrolled' }),
      makeSession({ id: 's2', enrollmentState: 'Enrolled' }),
    ]);
    await fixture.whenStable();

    const labels = thumbs().map((t) => t.querySelector('button')?.textContent?.trim());
    expect(labels).toContain('Enroll');
    expect(labels).toContain('Open');
  });

  it('shows a dimmed Locked button + amber "Requires" badge when prerequisiteSatisfied is false (FR-STU-CAT-002)', async () => {
    setup([
      makeSession({
        prerequisiteSessionId: 'p1',
        prerequisiteTitle: 'Foundations',
        prerequisiteSatisfied: false,
      }),
    ]);
    await fixture.whenStable();

    const btn = root().querySelector<HTMLButtonElement>('sb-session-thumb button');
    expect(btn?.textContent?.trim()).toBe('Locked');
    expect(btn?.disabled).toBe(true);
    expect(text()).toContain('Requires:');
    expect(text()).toContain('Foundations');
    // No red "Complete … first" hint anymore — the Locked button + badge carry the state.
    expect(text()).not.toContain('Complete');
  });

  it('shows a green "Prerequisite met" badge + an active Enroll when a met prereq exists', async () => {
    setup([
      makeSession({
        prerequisiteSessionId: 'p1',
        prerequisiteTitle: 'Foundations',
        prerequisiteSatisfied: true,
      }),
    ]);
    await fixture.whenStable();

    const btn = root().querySelector<HTMLButtonElement>('sb-session-thumb button');
    expect(btn?.textContent?.trim()).toBe('Enroll');
    expect(btn?.disabled).toBe(false);
    expect(text()).toContain('Prerequisite met:');
    expect(text()).toContain('Foundations');
  });

  it('opens the enroll modal scoped to a card when its Enroll CTA fires', async () => {
    setup([makeSession({ id: 's1', title: 'Algebra Basics' })]);
    await fixture.whenStable();

    component.openEnroll(makeSession({ id: 's1', title: 'Algebra Basics' }));
    fixture.detectChanges();

    expect(component.modalOpen()).toBe(true);
    expect(component.modalSessionId()).toBe('s1');
    expect(component.modalTitle()).toBe('Algebra Basics');
  });

  it('refetches the catalogue after a successful enroll (the card flips to Enrolled)', async () => {
    setup([makeSession()]);
    await fixture.whenStable();
    expect(service.catalogue).toHaveBeenCalledTimes(1);

    component.onEnrolled({
      id: 'e1',
      studentId: 'stu1',
      studentName: null,
      sessionId: 's1',
      sessionTitle: 'Algebra Basics',
      status: 'Active',
      method: 'Code',
      amount: 150,
      codeId: 'c1',
      codeSerial: 'SB-ABCDE-FGHIJ',
      enrolledAtUtc: '2026-06-21T00:00:00Z',
      expiresAtUtc: null,
    });
    fixture.detectChanges();

    expect(service.catalogue).toHaveBeenCalledTimes(2);
  });

  it('auto-opens a blank redeem modal when routed with openRedeem (the shell FAB target)', async () => {
    service = { catalogue: jest.fn().mockReturnValue(of([])), redeem: jest.fn() };
    TestBed.configureTestingModule({
      imports: [CatalogueComponent],
      providers: [provideRouter([]), { provide: CatalogueService, useValue: service }],
    });
    fixture = TestBed.createComponent(CatalogueComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('openRedeem', true);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.modalOpen()).toBe(true);
    expect(component.modalSessionId()).toBeNull();
  });
});
