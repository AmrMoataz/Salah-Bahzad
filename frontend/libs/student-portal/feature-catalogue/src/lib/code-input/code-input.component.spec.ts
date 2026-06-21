import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CodeInputComponent } from './code-input.component';

/** Host so the CodeInput binds through a real `FormControl` (exercises the CVA round-trip). */
@Component({
  standalone: true,
  imports: [ReactiveFormsModule, CodeInputComponent],
  template: `<sb-code-input [formControl]="ctrl" [error]="error" />`,
})
class HostComponent {
  ctrl = new FormControl('', { nonNullable: true });
  error: string | null = null;
}

describe('CodeInputComponent (FR-STU-CAT-003)', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  const root = () => fixture.nativeElement as HTMLElement;
  const boxes = (): HTMLInputElement[] =>
    Array.from(root().querySelectorAll<HTMLInputElement>('.ci__box'));

  function type(index: number, char: string): void {
    const box = boxes()[index];
    box.value = char;
    box.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('renders the SB prefix + 2 wide group boxes (the SB-XXXXX-XXXXX serial)', () => {
    expect(boxes()).toHaveLength(2);
    expect(boxes()[0].maxLength).toBe(5);
    expect(fixture.nativeElement.querySelector('.ci__prefix').textContent).toContain('SB');
  });

  it('normalises typed input to upper-case and auto-advances once a group fills', async () => {
    boxes()[0].focus();
    type(0, 'abc');
    await fixture.whenStable();
    // A partial group stays put — no premature hand-off.
    expect(boxes()[0].value).toBe('ABC');
    expect(document.activeElement).toBe(boxes()[0]);

    type(0, 'abcde');
    await fixture.whenStable();
    expect(boxes()[0].value).toBe('ABCDE');
    // Focus advanced to the second group box.
    expect(document.activeElement).toBe(boxes()[1]);
  });

  it('Backspace on an empty group box retreats to the previous group (without clearing it)', async () => {
    type(0, 'ABCDE'); // fills group 0, focus advances to group 1
    await fixture.whenStable();
    expect(document.activeElement).toBe(boxes()[1]);

    boxes()[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Backspace' }));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(document.activeElement).toBe(boxes()[0]);
    expect(boxes()[0].value).toBe('ABCDE');
  });

  it('pastes a full dashed serial, fills the boxes, and emits the normalized serial', async () => {
    paste('SB-ABCDE-FGHIJ');
    await fixture.whenStable();

    expect(boxes().map((b) => b.value).join('')).toBe('ABCDEFGHIJ');
    expect(host.ctrl.value).toBe('SB-ABCDE-FGHIJ');
  });

  it('normalizes a dash-less / lower-case pasted serial to the same value', async () => {
    paste('sbabcdefghij');
    await fixture.whenStable();
    expect(host.ctrl.value).toBe('SB-ABCDE-FGHIJ');

    paste('sb-abcde-fghij');
    await fixture.whenStable();
    expect(host.ctrl.value).toBe('SB-ABCDE-FGHIJ');
  });

  it('ControlValueAccessor round-trips a value written from the form', async () => {
    host.ctrl.setValue('SB-ABCDE-FGHIJ');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(boxes().map((b) => b.value).join('')).toBe('ABCDEFGHIJ');
    expect(host.ctrl.value).toBe('SB-ABCDE-FGHIJ');
  });

  it('renders the inline error message verbatim and marks the boxes invalid', () => {
    host.error = 'This code is invalid or no longer available.';
    fixture.detectChanges();

    const msg = root().querySelector('.ci__msg');
    expect(msg?.textContent).toContain('This code is invalid or no longer available.');
    expect(boxes()[0].getAttribute('aria-invalid')).toBe('true');
  });

  function paste(text: string): void {
    const box = boxes()[0];
    box.focus();
    const event = new Event('paste') as Event & { clipboardData: unknown };
    Object.defineProperty(event, 'clipboardData', { value: { getData: () => text } });
    box.dispatchEvent(event);
    fixture.detectChanges();
  }
});
