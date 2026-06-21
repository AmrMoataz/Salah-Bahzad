import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  MySessionsService: class MySessionsService {},
}));

import { SessionDetailComponent } from './session-detail.component';
import {
  MySessionsService,
  MySessionDetail,
  MySessionVideo,
  PlaybackHandoff,
} from '@sb/student-portal/data-access';

function makeVideo(over: Partial<MySessionVideo> = {}): MySessionVideo {
  return {
    id: 'v1',
    title: 'First',
    order: 0,
    lengthSeconds: 90,
    processingStatus: 'Ready',
    accessAllowed: 3,
    accessRemaining: 3,
    lockState: 'Playable',
    ...over,
  };
}

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
    videoCount: 1,
    videosWatched: 0,
    progressPercent: 0,
    gateState: 'Open',
    hasGatingQuiz: false,
    quizPassed: false,
    minPassPercent: 0,
    videos: [makeVideo()],
    materials: [],
    assignment: null,
    quiz: null,
    ...over,
  };
}

describe('SessionDetail — deep-link Play flow (FR-STU-VID-001/003/004, §D/§E.5)', () => {
  let fixture: ComponentFixture<SessionDetailComponent>;
  let component: SessionDetailComponent;
  let service: { session: jest.Mock; startPlayback: jest.Mock; materialUrl: jest.Mock };

  function setup(detail: MySessionDetail, playback: Observable<PlaybackHandoff>) {
    service = {
      session: jest.fn().mockReturnValue(of(detail)),
      startPlayback: jest.fn().mockReturnValue(playback),
      materialUrl: jest.fn(),
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

  afterEach(() => fixture?.destroy()); // clears the pending install-prompt timer

  const root = () => fixture.nativeElement as HTMLElement;
  const playButton = () => root().querySelector<HTMLButtonElement>('sb-video-row button');

  it('Play on a Playable video calls the gate ONCE then builds the salah-bahazad:// deep link', async () => {
    setup(makeDetail(), of({ handoffCode: 'HANDOFF123', expiresAtUtc: '2026-06-21T00:01:00Z' }));
    await fixture.whenStable();

    const extSpy = jest.spyOn(component as unknown as { openExternal: (u: string) => void }, 'openExternal').mockImplementation(() => undefined);

    playButton()!.click();
    fixture.detectChanges();

    // The gate (the state-changing POST) is fired exactly once — never speculatively/double.
    expect(service.startPlayback).toHaveBeenCalledTimes(1);
    expect(service.startPlayback).toHaveBeenCalledWith('v1');

    const expected = 'salah-bahazad://stream?videoId=v1&sessionId=sess-1&handoff=HANDOFF123';
    expect(component.lastDeepLink()).toBe(expected);
    expect(extSpy).toHaveBeenCalledWith(expected);
  });

  it('refreshes the detail after a successful Play (the view was decremented)', async () => {
    setup(makeDetail(), of({ handoffCode: 'H', expiresAtUtc: '2026-06-21T00:01:00Z' }));
    await fixture.whenStable();
    jest.spyOn(component as unknown as { openExternal: (u: string) => void }, 'openExternal').mockImplementation(() => undefined);
    expect(service.session).toHaveBeenCalledTimes(1); // initial load

    playButton()!.click();
    fixture.detectChanges();

    expect(service.session).toHaveBeenCalledTimes(2); // re-fetched after the decrement
  });

  it('opens the install prompt when no app grabs the scheme (no blur fires)', async () => {
    setup(makeDetail(), of({ handoffCode: 'H', expiresAtUtc: '2026-06-21T00:01:00Z' }));
    await fixture.whenStable();
    jest.spyOn(component as unknown as { openExternal: (u: string) => void }, 'openExternal').mockImplementation(() => undefined);

    playButton()!.click();
    fixture.detectChanges();
    expect(component.installPromptOpen()).toBe(false);

    // Simulate the install-prompt timer firing with the tab never having blurred.
    component.checkAppLaunched();
    expect(component.installPromptOpen()).toBe(true);
  });

  it('does NOT open the install prompt when the tab blurs (an app handled the scheme)', async () => {
    setup(makeDetail(), of({ handoffCode: 'H', expiresAtUtc: '2026-06-21T00:01:00Z' }));
    await fixture.whenStable();
    jest.spyOn(component as unknown as { openExternal: (u: string) => void }, 'openExternal').mockImplementation(() => undefined);

    playButton()!.click();
    fixture.detectChanges();

    component.onWindowBlur(); // the app opened — the tab lost focus
    component.checkAppLaunched();
    expect(component.installPromptOpen()).toBe(false);
  });

  it('renders the gate reason inline and does NOT deep-link on a 403 quiz_required', async () => {
    const detail = 'Pass the prerequisite quiz to unlock this video.';
    setup(
      makeDetail(),
      throwError(() => new HttpErrorResponse({ status: 403, error: { reason: 'quiz_required', detail } })),
    );
    await fixture.whenStable();
    const extSpy = jest.spyOn(component as unknown as { openExternal: (u: string) => void }, 'openExternal').mockImplementation(() => undefined);

    playButton()!.click();
    fixture.detectChanges();

    expect(component.videoError()).toBe(detail);
    expect(component.errorVideoId()).toBe('v1');
    expect(root().querySelector('.row__error')?.textContent).toContain(detail);
    // The browser must NOT navigate / deep-link on a gate failure.
    expect(extSpy).not.toHaveBeenCalled();
    expect(component.installPromptOpen()).toBe(false);
  });

  it('a locked (🔒) video Play button is disabled and never fires the gate', async () => {
    setup(makeDetail({ videos: [makeVideo({ lockState: 'Exhausted', accessRemaining: 0 })] }), of({ handoffCode: 'H', expiresAtUtc: '' }));
    await fixture.whenStable();

    const btn = playButton();
    expect(btn?.disabled).toBe(true);
    expect(btn?.textContent?.trim()).toBe('🔒');

    btn!.click();
    fixture.detectChanges();
    expect(service.startPlayback).not.toHaveBeenCalled();
  });
});
