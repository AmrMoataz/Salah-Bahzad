import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { StudentAuthStore, StudentBlockReason } from '@sb/student-portal/data-access';
import { StatusComponent } from './status.component';

jest.mock('@sb/student-portal/data-access', () => ({ StudentAuthStore: class StudentAuthStore {} }));

function makeStore() {
  return {
    status: signal<StudentBlockReason | null>(null),
    statusDetail: signal<string | null>(null),
    clearStatus: jest.fn(),
  };
}

describe('StatusComponent', () => {
  let store: ReturnType<typeof makeStore>;
  let router: { navigate: jest.Mock };

  function setup(reason: StudentBlockReason | null, detail: string | null = null) {
    store = makeStore();
    store.status.set(reason);
    store.statusDetail.set(detail);
    router = { navigate: jest.fn().mockResolvedValue(true) };

    TestBed.configureTestingModule({
      imports: [StatusComponent],
      providers: [
        { provide: StudentAuthStore, useValue: store },
        { provide: Router, useValue: router },
      ],
    });
    const fixture = TestBed.createComponent(StatusComponent);
    fixture.detectChanges();
    return fixture;
  }

  const text = (f: { nativeElement: HTMLElement }) => f.nativeElement.textContent ?? '';

  it('renders the pending state', () => {
    const fixture = setup('account_pending');
    expect(text(fixture)).toContain('Your account is pending approval');
    expect(fixture.nativeElement.querySelector('.status__mascot')?.getAttribute('src')).toContain(
      'salah-passed.png',
    );
  });

  it('renders the rejected state with the server RejectionReason', () => {
    const fixture = setup('account_rejected', 'Your ID photo was blurry — please re-upload.');
    expect(text(fixture)).toContain('wasn’t approved');
    expect(fixture.nativeElement.querySelector('.status__reason')?.textContent).toContain(
      'Your ID photo was blurry — please re-upload.',
    );
  });

  it('renders the inactive state', () => {
    const fixture = setup('account_inactive', 'Your account has been deactivated. Contact support.');
    expect(text(fixture)).toContain('Your account is inactive');
    expect(text(fixture)).toContain('deactivated');
  });

  it('renders the device_not_recognized state with the one-device copy', () => {
    const fixture = setup('device_not_recognized');
    expect(text(fixture)).toContain('isn’t recognised');
    expect(text(fixture)).toContain('Only one device can access content');
    expect(fixture.nativeElement.querySelector('.status__mascot')?.getAttribute('src')).toContain(
      'salah-prerequisite.png',
    );
  });

  it('clears the status and returns to /login on "Back to sign in"', () => {
    const fixture = setup('account_pending');
    (fixture.nativeElement.querySelector('.status__btn') as HTMLElement).click();

    expect(store.clearStatus).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});
