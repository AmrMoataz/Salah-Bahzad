import { Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '@sb/shared/data-access';
import { StaffListItem } from '../data-access/staff.models';
import { StaffService } from '../data-access/staff.service';
import { StaffListComponent } from './staff-list.component';

// Replace the real AuthStore module so jest doesn't pull in @angular/fire (ESM) at runtime.
// We only need AuthStore as a DI token; the test provides a fake implementation.
jest.mock('@sb/shared/data-access', () => ({
  AuthStore: class AuthStore {},
}));

const ROWS: StaffListItem[] = [
  {
    id: 'me',
    displayName: 'Head Teacher',
    email: 'head@x.com',
    role: 'Teacher',
    isActive: true,
    lastSeenAtUtc: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: null,
  },
  {
    id: 'a1',
    displayName: 'Mariam Adel',
    email: 'mariam@x.com',
    role: 'Assistant',
    isActive: true,
    lastSeenAtUtc: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: null,
  },
];

function setup(opts: { permissions?: string[]; role?: 'Teacher' | 'Assistant' } = {}) {
  const perms = new Set(opts.permissions ?? []);
  const serviceMock: Partial<StaffService> & { staff: Signal<StaffListItem[]> } = {
    staff: signal<StaffListItem[]>(ROWS),
    total: signal(ROWS.length),
    isLoading: signal(false),
    error: signal<string | null>(null),
    list: jest.fn().mockResolvedValue(undefined),
    create: jest.fn(),
    update: jest.fn(),
    setActive: jest.fn(),
    remove: jest.fn(),
    sendPasswordReset: jest.fn(),
  } as unknown as Partial<StaffService> & { staff: Signal<StaffListItem[]> };

  const authMock = {
    role: signal(opts.role ?? 'Teacher'),
    staff: signal({
      id: 'me',
      displayName: 'Head Teacher',
      email: 'head@x.com',
      role: 'Teacher',
      permissions: [...perms],
    }),
    hasPermission: (p: string) => perms.has(p),
  };

  TestBed.configureTestingModule({
    imports: [StaffListComponent],
    providers: [
      { provide: StaffService, useValue: serviceMock },
      { provide: AuthStore, useValue: authMock },
    ],
  });

  const fixture = TestBed.createComponent(StaffListComponent);
  fixture.detectChanges();
  return { fixture, serviceMock, authMock };
}

function buttonsByText(host: HTMLElement, text: string): HTMLButtonElement[] {
  return Array.from(host.querySelectorAll('button')).filter((b) =>
    b.textContent?.includes(text),
  ) as HTMLButtonElement[];
}

describe('StaffListComponent', () => {
  it('renders a row per staff member', () => {
    const { fixture } = setup({ permissions: ['StaffRead'] });
    expect(fixture.nativeElement.textContent).toContain('Head Teacher');
    expect(fixture.nativeElement.textContent).toContain('Mariam Adel');
  });

  it('hides "Add staff" without StaffCreate', () => {
    const { fixture } = setup({ permissions: ['StaffRead'] });
    expect(buttonsByText(fixture.nativeElement, 'Add staff')).toHaveLength(0);
  });

  it('shows "Add staff" with StaffCreate', () => {
    const { fixture } = setup({ permissions: ['StaffRead', 'StaffCreate'] });
    expect(buttonsByText(fixture.nativeElement, 'Add staff').length).toBeGreaterThan(0);
  });

  it('shows a role-gate for non-teachers and no table', () => {
    const { fixture } = setup({ role: 'Assistant', permissions: ['StaffRead'] });
    expect(fixture.nativeElement.textContent).toContain('Teacher access required');
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });

  it('offers Remove only for non-teacher rows that are not yourself', () => {
    const { fixture } = setup({ permissions: ['StaffRead', 'StaffDelete'] });
    const removeButtons = Array.from(
      fixture.nativeElement.querySelectorAll('button'),
    ).filter((b) => (b as HTMLButtonElement).getAttribute('aria-label')?.startsWith('Remove'));
    // Teacher row (also "me") is excluded; only the assistant row is removable.
    expect(removeButtons).toHaveLength(1);
  });

  it('loads staff on init', () => {
    const { serviceMock } = setup({ permissions: ['StaffRead'] });
    expect(serviceMock.list).toHaveBeenCalled();
  });
});
