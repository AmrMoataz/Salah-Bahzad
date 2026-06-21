import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  MySessionsService: class MySessionsService {},
}));

import { SessionDetailComponent } from './session-detail.component';
import { MySessionsService, MySessionDetail } from '@sb/student-portal/data-access';

function makeDetail(over: Partial<MySessionDetail> = {}): MySessionDetail {
  return {
    id: 'sess-1',
    title: 'Algebra',
    description: null,
    gradeId: 'g',
    gradeName: 'Grade 1',
    subjectId: 'sub',
    subjectName: 'Maths',
    specializationId: 'spec',
    specializationName: 'Pure Maths',
    thumbnailUrl: null,
    enrollmentId: 'e1',
    enrolledAtUtc: '2026-06-01T00:00:00Z',
    expiresAtUtc: null,
    isExpired: false,
    videoCount: 0,
    videosWatched: 0,
    progressPercent: 0,
    gateState: 'Open',
    hasGatingQuiz: false,
    quizPassed: false,
    minPassPercent: 0,
    videos: [],
    materials: [
      { id: 'm1', fileName: 'Worksheet.pdf', kind: 'PDF', sizeBytes: 1_200_000 },
      { id: 'm2', fileName: 'Notes.pdf', kind: 'PDF', sizeBytes: 350_000 },
    ],
    assignment: null,
    quiz: null,
    ...over,
  };
}

describe('SessionDetail — materials download (FR-STU-SES-003, §C)', () => {
  let fixture: ComponentFixture<SessionDetailComponent>;
  let component: SessionDetailComponent;
  let service: { session: jest.Mock; startPlayback: jest.Mock; materialUrl: jest.Mock };

  function setup(detail: MySessionDetail) {
    service = {
      session: jest.fn().mockReturnValue(of(detail)),
      startPlayback: jest.fn(),
      materialUrl: jest.fn().mockReturnValue(of({ url: 'https://r2.example/Worksheet.pdf?sig=abc', expiresAtUtc: '2026-06-21T00:05:00Z' })),
    };
    TestBed.configureTestingModule({
      imports: [SessionDetailComponent],
      providers: [provideRouter([]), { provide: MySessionsService, useValue: service }],
    });
    fixture = TestBed.createComponent(SessionDetailComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('id', detail.id);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => fixture?.destroy());

  const root = () => fixture.nativeElement as HTMLElement;
  const materialButtons = (): HTMLButtonElement[] =>
    Array.from(root().querySelectorAll<HTMLButtonElement>('.sd__material'));

  it('lists the materials with humanised sizes', async () => {
    setup(makeDetail());
    await fixture.whenStable();

    expect(materialButtons()).toHaveLength(2);
    expect(root().textContent).toContain('Worksheet.pdf');
    expect(root().textContent).toContain('1.1 MB');
  });

  it('Download fetches the signed URL for the right material then opens it', async () => {
    setup(makeDetail());
    await fixture.whenStable();
    const openSpy = jest.spyOn(component as unknown as { openUrl: (u: string) => void }, 'openUrl').mockImplementation(() => undefined);

    materialButtons()[0].click();
    fixture.detectChanges();

    expect(service.materialUrl).toHaveBeenCalledWith('sess-1', 'm1');
    expect(openSpy).toHaveBeenCalledWith('https://r2.example/Worksheet.pdf?sig=abc');
  });
});
