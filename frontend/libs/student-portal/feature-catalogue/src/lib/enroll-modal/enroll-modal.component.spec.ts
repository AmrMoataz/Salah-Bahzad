import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double. The
// modal only needs `CatalogueService` as a runtime value (the model types are erased at runtime).
jest.mock('@sb/student-portal/data-access', () => ({
  CatalogueService: class CatalogueService {},
}));

import { EnrollModalComponent } from './enroll-modal.component';
import { CatalogueService, Enrollment } from '@sb/student-portal/data-access';

function makeEnrollment(over: Partial<Enrollment> = {}): Enrollment {
  return {
    id: 'e1',
    studentId: 'stu1',
    studentName: 'Lina',
    sessionId: 's1',
    sessionTitle: 'Algebra Basics',
    status: 'Active',
    method: 'Code',
    amount: 150,
    codeId: 'c1',
    codeSerial: 'SB-ABCDE-FGHIJ',
    enrolledAtUtc: '2026-06-21T00:00:00Z',
    expiresAtUtc: '2026-07-21T00:00:00Z',
    ...over,
  };
}

function http(status: number, detail?: string): HttpErrorResponse {
  return new HttpErrorResponse({
    status,
    statusText: status === 400 ? 'Bad Request' : 'Conflict',
    error: detail ? { detail } : null,
  });
}

describe('EnrollModalComponent (FR-STU-CAT-004/005)', () => {
  let fixture: ComponentFixture<EnrollModalComponent>;
  let component: EnrollModalComponent;
  let service: { redeem: jest.Mock };

  function setup() {
    service = { redeem: jest.fn() };
    TestBed.configureTestingModule({
      imports: [EnrollModalComponent],
      providers: [{ provide: CatalogueService, useValue: service }],
    });
    fixture = TestBed.createComponent(EnrollModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges(); // runs the reset-on-open effect
    component.serialControl.setValue('SB-ABCDE-FGHIJ');
    fixture.detectChanges();
  }

  const text = () => fixture.nativeElement.textContent as string;

  it('submit() redeems with the entered serial and, on 201, flips to success + emits enrolled', async () => {
    setup();
    const enrollment = makeEnrollment();
    service.redeem.mockReturnValue(of(enrollment));

    let emitted: Enrollment | undefined;
    component.enrolled.subscribe((e) => (emitted = e));

    component.submit();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(service.redeem).toHaveBeenCalledWith('SB-ABCDE-FGHIJ');
    expect(component.success()).toBe(true);
    expect(emitted).toEqual(enrollment);
    expect(text()).toContain('You’re enrolled!');
  });

  it.each([
    ['This code is invalid or no longer available.'],
    ['This student already has an active enrollment for this session.'],
    ['Complete the prerequisite assignment first.'],
  ])('renders the 409 problem.detail verbatim: "%s"', async (detail) => {
    setup();
    service.redeem.mockReturnValue(throwError(() => http(409, detail)));

    component.submit();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.success()).toBe(false);
    expect(component.error()).toBe(detail);
    expect(text()).toContain(detail);
    // The serial stays put so the student can retry / fix a typo.
    expect(component.serialControl.value).toBe('SB-ABCDE-FGHIJ');
  });

  it('maps a 400 to "Enter a valid code." (mirrors the validator)', async () => {
    setup();
    service.redeem.mockReturnValue(throwError(() => http(400)));

    component.submit();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.error()).toBe('Enter a valid code.');
    expect(text()).toContain('Enter a valid code.');
    expect(component.serialControl.value).toBe('SB-ABCDE-FGHIJ');
  });

  it('keeps the serial and shows a fallback message on an unexpected error', async () => {
    setup();
    service.redeem.mockReturnValue(throwError(() => http(500)));

    component.submit();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(component.error()).toBe('Something went wrong. Please try again.');
    expect(component.serialControl.value).toBe('SB-ABCDE-FGHIJ');
  });
});
