import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with token-only doubles. The
// component injects ProfileService, RegistrationService and StudentAuthStore from this barrel; the
// StudentProfile/CityRef/RegionRef types it uses are erased at runtime, so they need no double.
jest.mock('@sb/student-portal/data-access', () => ({
  ProfileService: class ProfileService {},
  RegistrationService: class RegistrationService {},
  StudentAuthStore: class StudentAuthStore {},
}));

import { ProfileComponent } from './profile.component';
import {
  ProfileService,
  RegistrationService,
  StudentAuthStore,
  StudentProfile,
} from '@sb/student-portal/data-access';
import { ToastService } from '@sb/shared/ui';

function makeProfile(over: Partial<StudentProfile> = {}): StudentProfile {
  return {
    id: 'stu1',
    fullName: 'Lina Hassan',
    phoneNumber: '+20 100 000 0000',
    parentPhonePrimary: '+20 111 111 1111',
    parentPhoneSecondary: '+20 122 222 2222',
    schoolName: 'Cairo STEM',
    gradeId: 'g12',
    gradeName: 'Grade 12',
    cityId: 'c1',
    cityName: 'Cairo',
    regionId: 'r1',
    regionName: 'Nasr City',
    status: 'Active',
    boundDevice: { summary: 'Windows / Chrome', boundAtUtc: '2026-06-20T08:30:00Z' },
    ...over,
  };
}

const CITIES = [
  { id: 'c1', nameEn: 'Cairo', nameAr: 'القاهرة' },
  { id: 'c2', nameEn: 'Giza', nameAr: 'الجيزة' },
];
const REGIONS = [
  { id: 'r1', cityId: 'c1', nameEn: 'Nasr City', nameAr: 'مدينة نصر' },
  { id: 'r2', cityId: 'c1', nameEn: 'Maadi', nameAr: 'المعادي' },
];

describe('ProfileComponent (FR-STU-PRO-001/002/003)', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let component: ProfileComponent;
  let service: { getProfile: jest.Mock; updateProfile: jest.Mock };
  let registration: { cities: jest.Mock; regions: jest.Mock };
  let store: { getCurrentEmail: jest.Mock; requestPasswordReset: jest.Mock; signOut: jest.Mock };
  let toast: { success: jest.Mock; error: jest.Mock; info: jest.Mock };

  async function setup(profile: StudentProfile = makeProfile()) {
    service = {
      getProfile: jest.fn().mockReturnValue(of(profile)),
      updateProfile: jest.fn(),
    };
    registration = {
      cities: jest.fn().mockReturnValue(of(CITIES)),
      regions: jest.fn().mockReturnValue(of(REGIONS)),
    };
    store = {
      getCurrentEmail: jest.fn().mockReturnValue('lina@example.com'),
      requestPasswordReset: jest.fn().mockResolvedValue(undefined),
      signOut: jest.fn().mockResolvedValue(undefined),
    };
    toast = { success: jest.fn(), error: jest.fn(), info: jest.fn() };

    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: service },
        { provide: RegistrationService, useValue: registration },
        { provide: StudentAuthStore, useValue: store },
        { provide: ToastService, useValue: toast },
      ],
    });
    fixture = TestBed.createComponent(ProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent ?? '';
  const input = (id: string) => root().querySelector<HTMLInputElement>(`#${id}`);

  afterEach(() => TestBed.resetTestingModule());

  it('renders the header band: initials avatar, name, "{gradeName} · {cityName}" sub-line, and the "Active" chip', async () => {
    await setup();

    expect(component.initials()).toBe('LH');
    expect(root().querySelector('sb-avatar')).toBeTruthy();
    expect(text()).toContain('Lina Hassan');
    expect(text()).toContain('Grade 12 · Cairo');
    const pill = root().querySelector('sb-status-pill');
    expect(pill?.textContent).toContain('Active');
  });

  it('pre-fills the form from the loaded profile; Email + Grade are DISABLED and show the right values', async () => {
    await setup();

    // Editable controls pre-filled.
    expect(component.form.controls.fullName.value).toBe('Lina Hassan');
    expect(component.form.controls.schoolName.value).toBe('Cairo STEM');
    expect(component.form.controls.cityId.value).toBe('c1');
    expect(component.form.controls.regionId.value).toBe('r1');
    expect(component.form.controls.parentPhonePrimary.value).toBe('+20 111 111 1111');
    expect(component.form.controls.parentPhoneSecondary.value).toBe('+20 122 222 2222');

    // Email — disabled, read-only Firebase identity (decision 4 / §C.2).
    const emailEl = input('email')!;
    expect(emailEl.disabled).toBe(true);
    expect(emailEl.value).toBe('lina@example.com');
    expect(store.getCurrentEmail).toHaveBeenCalled();

    // Grade — disabled, shows the resolved gradeName (§C.1).
    const gradeEl = input('grade')!;
    expect(gradeEl.disabled).toBe(true);
    expect(gradeEl.value).toBe('Grade 12');

    // Email/Grade are NOT form controls (never sent on PUT).
    expect(component.form.contains('email')).toBe(false);
    expect(component.form.contains('gradeId')).toBe(false);
  });

  it('changing City re-fetches regions for the new city and resets the Region control (§C.3 cascade)', async () => {
    await setup();
    // Initial load already fetched regions for the seeded city.
    expect(registration.regions).toHaveBeenCalledWith('c1');

    component.onCityChange('c2');
    fixture.detectChanges();

    expect(registration.regions).toHaveBeenCalledWith('c2');
    expect(component.form.controls.regionId.value).toBe('');
  });

  it('Save PUTs exactly the seven writable fields (no email/grade), then re-seeds + toasts success', async () => {
    await setup();
    const updated = makeProfile({ fullName: 'Lina H.', schoolName: 'New School' });
    service.updateProfile.mockReturnValue(of(updated));

    component.form.controls.fullName.setValue('Lina H.');
    component.form.controls.schoolName.setValue('New School');
    component.save();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.updateProfile).toHaveBeenCalledTimes(1);
    const body = service.updateProfile.mock.calls[0][0];
    expect(Object.keys(body).sort()).toEqual(
      [
        'cityId',
        'fullName',
        'parentPhonePrimary',
        'parentPhoneSecondary',
        'phoneNumber',
        'regionId',
        'schoolName',
      ].sort(),
    );
    expect(body).not.toHaveProperty('gradeId');
    expect(body).not.toHaveProperty('email');
    expect(body).not.toHaveProperty('status');
    // phoneNumber is preserved unchanged from the loaded profile (§C.1).
    expect(body.phoneNumber).toBe('+20 100 000 0000');
    expect(body.fullName).toBe('Lina H.');

    // Re-seeded from the returned DTO + success toast.
    expect(component.profile()?.fullName).toBe('Lina H.');
    expect(component.form.controls.fullName.value).toBe('Lina H.');
    expect(toast.success).toHaveBeenCalledWith('Profile updated.');
  });

  it('renders the server `detail` inline when Save returns a 400 (mismatched city/region)', async () => {
    await setup();
    service.updateProfile.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { detail: 'Region does not belong to the selected city.' },
          }),
      ),
    );

    component.form.controls.fullName.setValue('Lina H.');
    component.save();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.formError()).toBe('Region does not belong to the selected city.');
    expect(text()).toContain('Region does not belong to the selected city.');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('parent Primary is required (Save disabled when blank); Secondary is optional and sent as null when blank', async () => {
    await setup();

    // Primary required.
    component.form.controls.parentPhonePrimary.setValue('');
    component.form.controls.parentPhonePrimary.markAsDirty();
    component.form.markAsDirty();
    fixture.detectChanges();
    expect(component.form.controls.parentPhonePrimary.hasError('required')).toBe(true);
    expect(component.form.invalid).toBe(true);
    const saveBtn = root().querySelector<HTMLButtonElement>('.pf__save button');
    expect(saveBtn?.disabled).toBe(true);

    // Secondary optional → null in the body when blank.
    component.form.controls.parentPhonePrimary.setValue('+20 111 111 1111');
    component.form.controls.parentPhoneSecondary.setValue('');
    service.updateProfile.mockReturnValue(of(makeProfile()));
    component.save();
    await fixture.whenStable();

    const body = service.updateProfile.mock.calls[0][0];
    expect(body.parentPhoneSecondary).toBeNull();
  });

  it('the Bound device card shows the summary + "Bound {date}" + the Reset button', async () => {
    await setup();
    expect(text()).toContain('Windows / Chrome');
    expect(text()).toContain('Bound');
    expect(text()).toContain('Reset device');
  });

  it('the Bound device card shows a generic label when boundDevice is null (Reset still shown)', async () => {
    await setup(makeProfile({ boundDevice: null }));
    expect(text()).toContain('No device bound yet.');
    expect(text()).toContain('Reset device');
  });

  it('the Bound device card shows a generic device name when the summary is null but a device is bound (§A.1)', async () => {
    await setup(makeProfile({ boundDevice: { summary: null, boundAtUtc: '2026-06-20T08:30:00Z' } }));
    expect(text()).toContain('Your current device');
    expect(text()).toContain('Bound');
  });

  it('the device-reset modal opens on "Reset device" and its confirm action calls NO service (decision 2 / §D.2)', async () => {
    await setup();

    component.openDeviceModal();
    fixture.detectChanges();
    expect(component.deviceModalOpen()).toBe(true);
    expect(text()).toContain('Reset bound device?');

    component.requestDeviceReset();
    fixture.detectChanges();

    // No API touched — getProfile only from the initial load, updateProfile never.
    expect(service.getProfile).toHaveBeenCalledTimes(1);
    expect(service.updateProfile).not.toHaveBeenCalled();
    expect(component.deviceModalOpen()).toBe(false);
    expect(toast.info).toHaveBeenCalled();
  });

  it('Change password calls StudentAuthStore.requestPasswordReset(email) and shows "Check your inbox" (decision 3 / §D.3)', async () => {
    await setup();

    component.openPasswordModal();
    fixture.detectChanges();
    expect(component.passwordModalOpen()).toBe(true);

    await component.confirmPasswordReset();
    fixture.detectChanges();

    expect(store.requestPasswordReset).toHaveBeenCalledWith('lina@example.com');
    expect(component.pwSent()).toBe(true);
    expect(text()).toContain('Check your inbox');
    // No backend / no profile API involved.
    expect(service.updateProfile).not.toHaveBeenCalled();
  });

  it('Sign out opens the confirm modal and its confirm calls StudentAuthStore.signOut (§D.4)', async () => {
    await setup();

    component.openSignOutModal();
    fixture.detectChanges();
    expect(component.signOutModalOpen()).toBe(true);
    expect(text()).toContain('Sign out?');

    await component.confirmSignOut();
    expect(store.signOut).toHaveBeenCalled();
  });
});
