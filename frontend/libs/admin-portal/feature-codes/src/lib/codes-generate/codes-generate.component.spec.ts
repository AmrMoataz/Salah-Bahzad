import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { CodeBatchDto } from '../data-access/code.models';
import { CodeService } from '../data-access/code.service';
import { CodesGenerateComponent } from './codes-generate.component';

jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

const batch: CodeBatchDto = {
  batchId: 'b9',
  label: 'NEW-20260620-02',
  sessionId: 's1',
  sessionTitle: "Newton's Laws",
  value: 175,
  quantity: 50,
  createdAtUtc: '2026-06-20T10:00:00Z',
};

function setup(perms: string[] = ['CodesGenerate']) {
  const service = {
    sessions: signal([{ id: 's1', title: "Newton's Laws" }]),
    error: signal<string | null>(null),
    loadSessions: jest.fn().mockResolvedValue(undefined),
    loadSessionPrice: jest.fn().mockResolvedValue(175),
    generateBatch: jest.fn().mockResolvedValue(batch),
    exportBatch: jest.fn().mockResolvedValue(undefined),
  };
  TestBed.configureTestingModule({
    imports: [CodesGenerateComponent],
    providers: [
      provideRouter([]),
      { provide: CodeService, useValue: service },
      { provide: AuthStore, useValue: { hasPermission: (p: string) => perms.includes(p) } },
    ],
  });
  const fixture = TestBed.createComponent(CodesGenerateComponent);
  return { fixture, service };
}

describe('CodesGenerateComponent', () => {
  const text = (fixture: { nativeElement: HTMLElement }) =>
    fixture.nativeElement.textContent ?? '';

  it('renders the Teacher role gate for non-Teachers and loads no sessions', () => {
    const { fixture, service } = setup([]);
    fixture.detectChanges();
    expect(text(fixture)).toContain('Teacher access required');
    expect(service.loadSessions).not.toHaveBeenCalled();
  });

  it('pre-fills Value from the picked session’s current price (#5)', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.form.controls.sessionId.setValue('s1');
    await fixture.whenStable(); // flush the prefill microtask

    expect(service.loadSessionPrice).toHaveBeenCalledWith('s1');
    expect(fixture.componentInstance.form.controls.value.value).toBe(175);
  });

  it('generates a batch (#2) and flips the right card to "Batch ready"', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    const c = fixture.componentInstance;
    c.form.controls.sessionId.setValue('s1');
    await fixture.whenStable();
    c.form.controls.quantity.setValue(50);
    expect(c.canSubmit()).toBe(true);

    await c.submit();
    fixture.detectChanges();

    expect(service.generateBatch).toHaveBeenCalledWith({ sessionId: 's1', value: 175, quantity: 50 });
    expect(c.batch()).toEqual(batch);
    expect(text(fixture)).toContain('50 codes minted');
  });

  it('downloads the just-minted batch via exportBatch (#4)', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance.download(batch);

    expect(service.exportBatch).toHaveBeenCalledWith('b9');
  });
});
