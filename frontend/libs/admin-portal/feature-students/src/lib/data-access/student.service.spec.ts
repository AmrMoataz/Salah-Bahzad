import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StudentDetail, StudentListItem } from './student.models';
import { StudentService } from './student.service';

const listRow = (over: Partial<StudentListItem> = {}): StudentListItem => ({
  id: '1',
  fullName: 'Yousef Adel',
  phoneNumber: '+201090000000',
  status: 'Pending',
  gradeId: 'g1',
  gradeName: 'Grade 10',
  cityName: 'Cairo',
  schoolName: 'Cairo STEM',
  parentPhonePrimary: '+201000000000',
  activeDeviceSummary: null,
  createdAtUtc: '2026-06-01T00:00:00Z',
  lastSeenAtUtc: null,
  ...over,
});

const detail = (over: Partial<StudentDetail> = {}): StudentDetail => ({
  id: '1',
  fullName: 'Yousef Adel',
  phoneNumber: '+201090000000',
  status: 'Active',
  rejectionReason: null,
  gradeId: 'g1',
  gradeName: 'Grade 10',
  cityId: 'c1',
  cityName: 'Cairo',
  regionId: 'r1',
  regionName: 'Nasr City',
  schoolName: 'Cairo STEM',
  parentPhonePrimary: '+201000000000',
  parentPhoneSecondary: null,
  hasIdImage: true,
  termsVersion: 'v1',
  termsAcceptedAtUtc: '2026-06-01T00:00:00Z',
  lastSeenAtUtc: null,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: null,
  activeDevice: null,
  ...over,
});

describe('StudentService', () => {
  let service: StudentService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [StudentService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(StudentService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() GETs /api/students with status/grade filters and updates signals', async () => {
    const promise = service.list({ status: 'Pending', gradeId: 'g1', search: 'you', page: 1, pageSize: 20 });

    const req = http.expectOne((r) => r.url.endsWith('/api/students'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('Pending');
    expect(req.request.params.get('gradeId')).toBe('g1');
    expect(req.request.params.get('search')).toBe('you');
    req.flush({ items: [listRow()], total: 1, page: 1, pageSize: 20, totalPages: 1 });

    await promise;
    expect(service.students().length).toBe(1);
    expect(service.total()).toBe(1);
    expect(service.isLoading()).toBe(false);
  });

  it('approve() POSTs to /approve and returns the refreshed detail', async () => {
    const promise = service.approve('1');

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/approve'));
    expect(req.request.method).toBe('POST');
    req.flush(detail({ status: 'Active' }));

    expect((await promise).status).toBe('Active');
  });

  it('reject() POSTs the mandatory reason', async () => {
    const promise = service.reject('1', 'Blurry ID');

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/reject'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ reason: 'Blurry ID' });
    req.flush(detail({ status: 'Rejected', rejectionReason: 'Blurry ID' }));

    expect((await promise).status).toBe('Rejected');
  });

  it('setActive() POSTs the flag to /active', async () => {
    const promise = service.setActive('1', false);

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/active'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ isActive: false });
    req.flush(detail({ status: 'Inactive' }));

    await promise;
  });

  it('updateContact() PUTs to /contact', async () => {
    const promise = service.updateContact('1', {
      gradeId: 'g2',
      phoneNumber: '+201090000000',
      parentPhonePrimary: '+201111111111',
      parentPhoneSecondary: null,
    });

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/contact'));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      gradeId: 'g2',
      phoneNumber: '+201090000000',
      parentPhonePrimary: '+201111111111',
      parentPhoneSecondary: null,
    });
    req.flush(detail({ gradeId: 'g2' }));

    expect((await promise).gradeId).toBe('g2');
  });

  it('clearDevice() POSTs the mandatory reason to /clear-device', async () => {
    const promise = service.clearDevice('1', 'New phone');

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/clear-device'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ reason: 'New phone' });
    req.flush(detail({ activeDevice: null }));

    await promise;
  });

  it('getIdImageUrl() GETs the signed-URL endpoint', async () => {
    const promise = service.getIdImageUrl('1');

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/id-image'));
    expect(req.request.method).toBe('GET');
    req.flush({ url: 'https://signed.example/x', expiresAtUtc: '2026-06-19T00:05:00Z' });

    expect((await promise).url).toContain('https://');
  });

  it('listLoginHistory() GETs the paged login-history endpoint', async () => {
    const promise = service.listLoginHistory('1', 2, 10);

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/login-history'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({ items: [], total: 0, page: 2, pageSize: 10, totalPages: 0 });

    expect((await promise).total).toBe(0);
  });

  it('listEnrollments() GETs the paged enrolments endpoint (#11)', async () => {
    const promise = service.listEnrollments('1', 2, 10);

    const req = http.expectOne((r) => r.url.endsWith('/api/students/1/enrollments'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({
      items: [
        {
          enrollmentId: 'e1',
          sessionId: 's1',
          sessionTitle: "Newton's Laws",
          method: 'Code',
          status: 'Active',
          amount: 150,
          enrolledAtUtc: '2026-06-20T09:00:00Z',
          codeSerial: 'SB-1',
        },
      ],
      total: 1,
      page: 2,
      pageSize: 10,
      totalPages: 1,
    });

    expect((await promise).items[0].sessionTitle).toBe("Newton's Laws");
  });
});
