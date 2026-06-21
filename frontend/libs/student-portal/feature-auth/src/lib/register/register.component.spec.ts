import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { RegisterComponent } from './register.component';
import { RegistrationService } from '@sb/student-portal/data-access';

// The data-access barrel imports @angular/fire (ESM) — replace it with token-only + const doubles.
jest.mock('@sb/student-portal/data-access', () => ({
  RegistrationService: class RegistrationService {},
  ID_IMAGE_MAX_BYTES: 5 * 1024 * 1024,
  ID_IMAGE_ACCEPTED_TYPES: ['image/jpeg', 'image/png', 'image/webp'],
  ID_IMAGE_ACCEPT_ATTR: 'image/jpeg,image/png,image/webp',
}));

function makeService() {
  return {
    grades: jest.fn().mockReturnValue(of([{ id: 'g1', name: 'Grade 1 Secondary' }])),
    cities: jest.fn().mockReturnValue(of([{ id: 'c1', nameEn: 'Cairo', nameAr: 'القاهرة' }])),
    regions: jest
      .fn()
      .mockReturnValue(of([{ id: 'r1', cityId: 'c1', nameEn: 'Maadi', nameAr: 'المعادي' }])),
    register: jest.fn().mockReturnValue(of({ studentId: 'stu-1', status: 'Pending' })),
    createEmailAccount: jest.fn().mockResolvedValue(undefined),
    signUpWithGoogle: jest
      .fn()
      .mockResolvedValue({ fullName: 'Lina Hassan', email: 'lina@example.com' }),
    reset: jest.fn(),
  };
}

function pngFile(name = 'id.png', bytes = 3): File {
  return new File([new Uint8Array(bytes)], name, { type: 'image/png' });
}

describe('RegisterComponent', () => {
  let service: ReturnType<typeof makeService>;

  function setup() {
    service = makeService();
    TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [provideRouter([]), { provide: RegistrationService, useValue: service }],
    });
    const fixture = TestBed.createComponent(RegisterComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: { nativeElement: HTMLElement }) => f.nativeElement;

  function fillStep1Manual(c: RegisterComponent): void {
    c.form.patchValue({
      fullName: 'Lina Hassan',
      email: 'lina@example.com',
      password: 'password1',
      confirmPassword: 'password1',
      phoneNumber: '+201000000000',
    });
  }

  function fillStep2(c: RegisterComponent): void {
    c.onCityChange('c1'); // loads + enables region (the cascade)
    c.form.patchValue({
      schoolName: 'Cairo STEM',
      gradeId: 'g1',
      cityId: 'c1',
      regionId: 'r1',
      parentPhonePrimary: '+201111111111',
      terms: true,
    });
  }

  // ── Step gating ──────────────────────────────────────────────────
  it('blocks "Continue" while Step 1 is invalid and advances once it is valid', () => {
    const fixture = setup();
    const c = fixture.componentInstance;

    c.goToStep2();
    expect(c.step()).toBe(1);

    fillStep1Manual(c);
    c.goToStep2();
    expect(c.step()).toBe(2);
  });

  // ── ≥ 1 parent phone ─────────────────────────────────────────────
  it('blocks submit when no parent phone is given (≥ 1 required, FR-STU-REG-005)', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;
    fillStep1Manual(c);
    c.goToStep2();
    fillStep2(c);
    c.form.controls.parentPhonePrimary.setValue('');
    c.onIdPicked([pngFile()]);

    await c.submit();

    expect(service.register).not.toHaveBeenCalled();
    expect(c.step()).toBe(2);
    expect(c.fieldError('parentPhonePrimary')).toContain('parent');
  });

  // ── Terms gate ───────────────────────────────────────────────────
  it('blocks submit until the terms + one-device policy are accepted', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;
    fillStep1Manual(c);
    c.goToStep2();
    fillStep2(c);
    c.form.controls.terms.setValue(false);
    c.onIdPicked([pngFile()]);

    await c.submit();

    expect(service.register).not.toHaveBeenCalled();
  });

  // ── ID client guard ──────────────────────────────────────────────
  it('rejects an over-sized ID image client-side (≤ 5 MB, contract §D)', () => {
    const fixture = setup();
    const c = fixture.componentInstance;

    c.onIdPicked([pngFile('big.png', 6 * 1024 * 1024)]);

    expect(c.idError()).toContain('5 MB');
    expect(c.idFiles().length).toBe(0);
  });

  it('rejects a wrong-type ID file client-side (jpeg|png|webp only)', () => {
    const fixture = setup();
    const c = fixture.componentInstance;

    c.onIdPicked([new File([new Uint8Array([1])], 'id.pdf', { type: 'application/pdf' })]);

    expect(c.idError()).toContain('JPEG');
    expect(c.idFiles().length).toBe(0);
  });

  it('accepts a valid ID image', () => {
    const fixture = setup();
    const c = fixture.componentInstance;

    c.onIdPicked([pngFile()]);

    expect(c.idError()).toBeNull();
    expect(c.idFiles().length).toBe(1);
  });

  // ── City → region cascade ────────────────────────────────────────
  it('loads regions for the chosen city and resets a stale region (the cascade)', () => {
    const fixture = setup();
    const c = fixture.componentInstance;

    c.onCityChange('c1');
    expect(service.regions).toHaveBeenCalledWith('c1');
    expect(c.regionOptions().length).toBe(1);
    expect(c.form.controls.regionId.disabled).toBe(false);

    c.form.controls.regionId.setValue('r1');
    c.onCityChange('c2');
    expect(service.regions).toHaveBeenLastCalledWith('c2');
    expect(c.form.controls.regionId.value).toBeFalsy();
  });

  // ── Manual submit (happy path) ───────────────────────────────────
  it('manual submit: creates the Firebase account, posts the registration, and shows the pending state', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;
    fillStep1Manual(c);
    c.goToStep2();
    fillStep2(c);
    const img = pngFile();
    c.onIdPicked([img]);

    await c.submit();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.createEmailAccount).toHaveBeenCalledWith('lina@example.com', 'password1');
    expect(service.register).toHaveBeenCalledWith(
      expect.objectContaining({
        fullName: 'Lina Hassan',
        phoneNumber: '+201000000000',
        parentPhonePrimary: '+201111111111',
        gradeId: 'g1',
        cityId: 'c1',
        regionId: 'r1',
        schoolName: 'Cairo STEM',
        idImage: img,
      }),
    );
    expect(c.submitted()).toBe(true);
    expect(el(fixture).textContent).toContain('Account created!');
    expect(el(fixture).textContent).toContain('pending approval');
  });

  // ── Google path ──────────────────────────────────────────────────
  it('Google path: prefills name/email (email read-only), still requires phone, and submits with the held user', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;

    await c.onGoogle();
    expect(c.method()).toBe('google');
    expect(c.googleProfile()).toEqual({ fullName: 'Lina Hassan', email: 'lina@example.com' });
    expect(c.form.controls.fullName.value).toBe('Lina Hassan');
    expect(c.form.controls.email.disabled).toBe(true);
    expect(c.form.controls.password.disabled).toBe(true);

    // Phone is still required in the Google sub-view.
    c.goToStep2();
    expect(c.step()).toBe(1);
    c.form.controls.phoneNumber.setValue('+201000000000');
    c.goToStep2();
    expect(c.step()).toBe(2);

    fillStep2(c);
    c.onIdPicked([pngFile()]);
    await c.submit();

    expect(service.createEmailAccount).not.toHaveBeenCalled();
    expect(service.register).toHaveBeenCalled();
    expect(c.submitted()).toBe(true);
  });

  // ── Error mapping ────────────────────────────────────────────────
  async function submitValidManual(c: RegisterComponent): Promise<void> {
    fillStep1Manual(c);
    c.goToStep2();
    fillStep2(c);
    c.onIdPicked([pngFile()]);
    await c.submit();
  }

  it('400: restores FluentValidation field errors onto the controls and returns to the failing step', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;
    service.register.mockReturnValueOnce(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { errors: { FullName: ['Name is required.'] } },
          }),
      ),
    );

    await submitValidManual(c);

    expect(c.form.controls.fullName.hasError('server')).toBe(true);
    expect(c.fieldError('fullName')).toBe('Name is required.');
    expect(c.step()).toBe(1); // FullName is a Step-1 field
    expect(c.submitted()).toBe(false);
    // Values survive the error.
    expect(c.form.controls.phoneNumber.value).toBe('+201000000000');
  });

  it('409: surfaces "already registered → sign in"', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;
    service.register.mockReturnValueOnce(
      throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'exists' } })),
    );

    await submitValidManual(c);

    expect(c.alreadyRegistered()).toBe(true);
    expect(c.topError()?.toLowerCase()).toContain('sign in');
  });

  it('429: shows a throttle notice', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;
    service.register.mockReturnValueOnce(
      throwError(() => new HttpErrorResponse({ status: 429 })),
    );

    await submitValidManual(c);

    expect(c.topError()).toContain('Too many attempts');
  });

  it('Firebase email-already-in-use → sign-in hint (no register POST)', async () => {
    const fixture = setup();
    const c = fixture.componentInstance;
    service.createEmailAccount.mockRejectedValueOnce(
      Object.assign(new Error('x'), { code: 'auth/email-already-in-use' }),
    );

    await submitValidManual(c);

    expect(service.register).not.toHaveBeenCalled();
    expect(c.alreadyRegistered()).toBe(true);
    expect(c.topError()?.toLowerCase()).toContain('sign in');
    expect(c.step()).toBe(1);
  });
});
