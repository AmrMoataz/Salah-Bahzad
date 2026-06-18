import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StaffListItem } from './staff.models';
import { StaffService } from './staff.service';

const sample = (over: Partial<StaffListItem> = {}): StaffListItem => ({
  id: '1',
  displayName: 'Mariam Adel',
  email: 'mariam@x.com',
  role: 'Assistant',
  isActive: true,
  lastSeenAtUtc: null,
  createdAtUtc: '2026-01-01T00:00:00Z',
  updatedAtUtc: null,
  ...over,
});

describe('StaffService', () => {
  let service: StaffService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [StaffService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(StaffService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() GETs /api/staff and updates signals', async () => {
    const promise = service.list({ page: 1, pageSize: 20 });

    const req = http.expectOne((r) => r.url.endsWith('/api/staff'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ items: [sample()], total: 1, page: 1, pageSize: 20, totalPages: 1 });

    await promise;
    expect(service.staff().length).toBe(1);
    expect(service.total()).toBe(1);
    expect(service.isLoading()).toBe(false);
  });

  it('create() POSTs and prepends to the signal', async () => {
    const promise = service.create({ displayName: 'New', email: 'new@x.com', role: 'Assistant' });

    const req = http.expectOne((r) => r.url.endsWith('/api/staff'));
    expect(req.request.method).toBe('POST');
    req.flush(sample({ id: '2', displayName: 'New', email: 'new@x.com' }));

    await promise;
    expect(service.staff()[0].id).toBe('2');
  });

  it('update() PUTs to /api/staff/:id', async () => {
    const promise = service.update('1', {
      displayName: 'Renamed',
      email: 'mariam@x.com',
      role: 'Assistant',
    });

    const req = http.expectOne((r) => r.url.endsWith('/api/staff/1'));
    expect(req.request.method).toBe('PUT');
    req.flush(sample({ displayName: 'Renamed' }));

    const result = await promise;
    expect(result.displayName).toBe('Renamed');
  });

  it('setActive() POSTs to /active with the flag', async () => {
    const promise = service.setActive('1', false);

    const req = http.expectOne((r) => r.url.endsWith('/api/staff/1/active'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ isActive: false });
    req.flush(sample({ isActive: false }));

    await promise;
  });

  it('sendPasswordReset() POSTs to /password-reset', async () => {
    const promise = service.sendPasswordReset('1');

    const req = http.expectOne((r) => r.url.endsWith('/api/staff/1/password-reset'));
    expect(req.request.method).toBe('POST');
    req.flush({ email: 'reset@x.com' });

    expect(await promise).toEqual({ email: 'reset@x.com' });
  });

  it('remove() DELETEs and drops it from the signal', async () => {
    const listPromise = service.list();
    http
      .expectOne((r) => r.url.endsWith('/api/staff'))
      .flush({
        items: [sample({ id: '1' }), sample({ id: '2' })],
        total: 2,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      });
    await listPromise;
    expect(service.staff().length).toBe(2);

    const removePromise = service.remove('1');
    const req = http.expectOne((r) => r.url.endsWith('/api/staff/1'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    await removePromise;

    expect(service.staff().map((s) => s.id)).toEqual(['2']);
    expect(service.total()).toBe(1);
  });
});
