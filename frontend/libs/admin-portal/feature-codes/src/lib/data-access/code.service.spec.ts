import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CodeBatchDto, CodeListDto } from './code.models';
import { CodeService } from './code.service';

const code = (over: Partial<CodeListDto> = {}): CodeListDto => ({
  id: 'c1',
  serial: 'SB-2ABCD-3EFGH',
  value: 150,
  status: 'Active',
  batchId: 'b1',
  batchLabel: 'NEW-20260620-01',
  sessionId: 's1',
  sessionTitle: "Newton's Laws",
  redeemedByStudentId: null,
  redeemedByStudentName: null,
  redeemedAtUtc: null,
  createdByName: 'Salah Bahzad',
  createdAtUtc: '2026-06-20T09:00:00Z',
  ...over,
});

const batch: CodeBatchDto = {
  batchId: 'b9',
  label: 'NEW-20260620-02',
  sessionId: 's1',
  sessionTitle: "Newton's Laws",
  value: 150,
  quantity: 50,
  createdAtUtc: '2026-06-20T10:00:00Z',
};

describe('CodeService', () => {
  let service: CodeService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CodeService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CodeService);
    http = TestBed.inject(HttpTestingController);
    // jsdom has no Blob URL plumbing — stub it so download side-effects don't throw.
    (URL as unknown as { createObjectURL: unknown }).createObjectURL = jest.fn(() => 'blob:x');
    (URL as unknown as { revokeObjectURL: unknown }).revokeObjectURL = jest.fn();
    jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
  });

  afterEach(() => {
    http.verify();
    jest.restoreAllMocks();
  });

  it('list() GETs /api/codes with search/status/session filters and updates signals', async () => {
    const promise = service.list({
      search: 'SB-2',
      status: 'Inactive',
      sessionId: 's1',
      page: 2,
      pageSize: 10,
    });

    const req = http.expectOne((r) => r.url.endsWith('/api/codes'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('search')).toBe('SB-2');
    expect(req.request.params.get('status')).toBe('Inactive');
    expect(req.request.params.get('sessionId')).toBe('s1');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({ items: [code()], total: 1, page: 2, pageSize: 10, totalPages: 1 });

    await promise;
    expect(service.codes().length).toBe(1);
    expect(service.total()).toBe(1);
    expect(service.isLoading()).toBe(false);
  });

  it('generateBatch() POSTs { sessionId, value, quantity } to /api/codes/batches (#2)', async () => {
    const body = { sessionId: 's1', value: 150, quantity: 50 };
    const promise = service.generateBatch(body);

    const req = http.expectOne((r) => r.url.endsWith('/api/codes/batches'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush(batch);

    expect((await promise).batchId).toBe('b9');
  });

  it('export() GETs /api/codes/export as a blob with filters only (no page/pageSize) (#3)', async () => {
    const promise = service.export({ status: 'Active', sessionId: 's1', page: 3, pageSize: 10 });

    const req = http.expectOne((r) => r.url.endsWith('/api/codes/export'));
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    expect(req.request.params.get('status')).toBe('Active');
    expect(req.request.params.get('sessionId')).toBe('s1');
    expect(req.request.params.get('page')).toBeNull();
    expect(req.request.params.get('pageSize')).toBeNull();
    req.flush(new Blob(['serial,value'], { type: 'text/csv' }));

    await promise;
    expect(URL.createObjectURL).toHaveBeenCalled();
  });

  it('exportBatch() GETs /api/codes/batches/{id}/export as a blob (#4)', async () => {
    const promise = service.exportBatch('b9');

    const req = http.expectOne((r) => r.url.endsWith('/api/codes/batches/b9/export'));
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['serial,value'], { type: 'text/csv' }));

    await promise;
    expect(URL.createObjectURL).toHaveBeenCalled();
  });

  it('disable() and enable() POST to their endpoints (#5/#6)', async () => {
    const p1 = service.disable('c1');
    const r1 = http.expectOne((r) => r.url.endsWith('/api/codes/c1/disable'));
    expect(r1.request.method).toBe('POST');
    r1.flush(code({ status: 'Inactive' }));
    expect((await p1).status).toBe('Inactive');

    const p2 = service.enable('c1');
    const r2 = http.expectOne((r) => r.url.endsWith('/api/codes/c1/enable'));
    expect(r2.request.method).toBe('POST');
    r2.flush(code({ status: 'Active' }));
    expect((await p2).status).toBe('Active');
  });

  it('remove() DELETEs the code (#7)', async () => {
    const promise = service.remove('c1');
    const req = http.expectOne((r) => r.url.endsWith('/api/codes/c1'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    await promise;
  });

  it('exportRows() builds the selection CSV client-side (no HTTP request)', () => {
    service.exportRows([code({ serial: 'SB-1', status: 'Inactive' })]);
    // No outstanding request — afterEach http.verify() asserts that.
    expect(URL.createObjectURL).toHaveBeenCalled();
  });

  it('has NO redeem method — redemption (#12) is the student-portal path, never the admin portal', () => {
    expect((service as unknown as Record<string, unknown>)['redeem']).toBeUndefined();
  });

  it('loadSessions() GETs /api/sessions once and maps to { id, title } (cached on 2nd call)', async () => {
    const promise = service.loadSessions();
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('pageSize')).toBe('200');
    req.flush({
      items: [{ id: 's1', title: "Newton's Laws", extra: 'ignored' }],
      total: 1,
      page: 1,
      pageSize: 200,
      totalPages: 1,
    });
    await promise;
    expect(service.sessions()).toEqual([{ id: 's1', title: "Newton's Laws" }]);

    await service.loadSessions(); // cached → no second request (verified by afterEach)
  });

  it('loadSessionPrice() GETs /api/sessions/{id} and returns its price', async () => {
    const promise = service.loadSessionPrice('s1');
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1'));
    expect(req.request.method).toBe('GET');
    req.flush({ id: 's1', title: "Newton's Laws", price: 175 });
    expect(await promise).toBe(175);
  });
});
