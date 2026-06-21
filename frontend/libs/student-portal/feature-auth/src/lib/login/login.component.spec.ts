import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';
import { StudentAuthStore } from '@sb/student-portal/data-access';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({ StudentAuthStore: class StudentAuthStore {} }));

function makeStore() {
  return {
    isLoading: signal(false),
    error: signal<string | null>(null),
    status: signal<string | null>(null),
    statusDetail: signal<string | null>(null),
    signIn: jest.fn().mockResolvedValue(undefined),
    signInWithGoogle: jest.fn().mockResolvedValue(undefined),
    requestPasswordReset: jest.fn().mockResolvedValue(undefined),
  };
}

describe('LoginComponent', () => {
  let store: ReturnType<typeof makeStore>;

  function setup() {
    store = makeStore();
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), { provide: StudentAuthStore, useValue: store }],
    });
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return fixture;
  }

  const el = (f: { nativeElement: HTMLElement }) => f.nativeElement;

  it('signs in with Google when the Google button is clicked', async () => {
    const fixture = setup();
    (el(fixture).querySelector('.login__google') as HTMLElement).click();
    await fixture.whenStable();

    expect(store.signInWithGoogle).toHaveBeenCalled();
  });

  it('submits email + password to the store', async () => {
    const fixture = setup();
    fixture.componentInstance.form.setValue({
      email: 'lina@example.com',
      password: 'password',
      remember: true,
    });

    (el(fixture).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(store.signIn).toHaveBeenCalledWith('lina@example.com', 'password');
  });

  it('does not call the store and shows validation errors on an empty submit', async () => {
    const fixture = setup();
    (el(fixture).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(store.signIn).not.toHaveBeenCalled();
    expect(el(fixture).textContent).toContain('Email is required.');
  });

  it('surfaces the store error message after a failed sign-in', async () => {
    const fixture = setup();
    store.signIn.mockRejectedValueOnce(new Error('nope'));
    store.error.set('Invalid email or password.');

    fixture.componentInstance.form.setValue({
      email: 'lina@example.com',
      password: 'password',
      remember: true,
    });
    (el(fixture).querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el(fixture).querySelector('.login__alert--danger')?.textContent).toContain(
      'Invalid email or password.',
    );
  });

  it('sends a password reset when an email is present', async () => {
    const fixture = setup();
    fixture.componentInstance.form.controls.email.setValue('lina@example.com');

    (el(fixture).querySelector('.login__reset-link') as HTMLElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(store.requestPasswordReset).toHaveBeenCalledWith('lina@example.com');
    expect(el(fixture).querySelector('.login__alert--success')).toBeTruthy();
  });

  it('refuses a password reset without a valid email and prompts for one', async () => {
    const fixture = setup();
    (el(fixture).querySelector('.login__reset-link') as HTMLElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(store.requestPasswordReset).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('.login__alert--danger')).toBeTruthy();
  });

  it('links to the registration placeholder', () => {
    const fixture = setup();
    const link = el(fixture).querySelector('a[routerLink="/register"]');
    expect(link?.textContent).toContain('Create an account');
  });
});
