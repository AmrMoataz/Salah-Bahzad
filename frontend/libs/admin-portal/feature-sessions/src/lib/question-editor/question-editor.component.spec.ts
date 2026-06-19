import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { QuestionDto } from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';
import { QuestionEditorComponent } from './question-editor.component';

// Replace the real AuthStore module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

/** Flush the constructor effect + its queued #load microtask. */
const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve));
const evt = (value: string): Event => ({ target: { value } } as unknown as Event);

const createdQuestion: QuestionDto = {
  id: 'q9',
  sessionId: 's1',
  bodyLatex: 'Q?',
  imageUrl: null,
  mark: 1,
  isValidForQuiz: true,
  hintUrl: null,
  options: [
    { id: 'o1', text: 'A', isCorrect: false },
    { id: 'o2', text: 'B', isCorrect: true },
  ],
  variations: [],
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: null,
};

function makeServiceMock() {
  return {
    error: signal<string | null>(null),
    listQuestions: jest.fn(),
    createQuestion: jest.fn().mockResolvedValue(createdQuestion),
    updateQuestion: jest.fn().mockResolvedValue(createdQuestion),
  };
}

describe('QuestionEditorComponent (single-correct MCQ)', () => {
  function setup() {
    const service = makeServiceMock();
    TestBed.configureTestingModule({
      imports: [QuestionEditorComponent],
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: () => true } },
      ],
    });
    const fixture = TestBed.createComponent(QuestionEditorComponent);
    const router = TestBed.inject(Router);
    jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.componentRef.setInput('id', 's1'); // new question (no questionId)
    fixture.detectChanges();
    return { fixture, service, router };
  }

  it('keeps exactly one option correct when a new one is selected', async () => {
    const { fixture } = setup();
    await settle();
    const c = fixture.componentInstance;

    c.setCorrect(1);
    const opts = c.units()[0].options;
    expect(opts.filter((o) => o.isCorrect).length).toBe(1);
    expect(opts[1].isCorrect).toBe(true);
    expect(opts[0].isCorrect).toBe(false);
  });

  it('blocks save until each option has text and one is correct', async () => {
    const { fixture, service } = setup();
    await settle();
    const c = fixture.componentInstance;

    // Options have no text yet → invalid.
    await c.saveBase();
    expect(service.createQuestion).not.toHaveBeenCalled();
    expect(c.unitError(c.units()[0])).toContain('text');
  });

  it('creates the question with exactly one correct option', async () => {
    const { fixture, service, router } = setup();
    await settle();
    const c = fixture.componentInstance;

    c.setBody(evt('A car accelerates at $a$.'));
    c.setOptionText(0, evt('10 m/s'));
    c.setOptionText(1, evt('20 m/s'));
    c.setCorrect(1);

    await c.saveBase();

    expect(service.createQuestion).toHaveBeenCalledTimes(1);
    const [sessionId, payload] = service.createQuestion.mock.calls[0];
    expect(sessionId).toBe('s1');
    expect(payload.options).toHaveLength(2);
    expect(payload.options.filter((o: { isCorrect: boolean }) => o.isCorrect)).toHaveLength(1);
    expect(payload.options[1].isCorrect).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(['/sessions', 's1', 'questions', 'q9', 'edit']);
  });
});
