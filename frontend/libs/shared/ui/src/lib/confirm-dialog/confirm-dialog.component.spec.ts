import { TestBed } from '@angular/core/testing';
import { ConfirmDialogComponent } from './confirm-dialog.component';

function buttonByText(host: HTMLElement, text: string): HTMLButtonElement {
  const match = Array.from(host.querySelectorAll('button')).find((b) =>
    b.textContent?.includes(text),
  );
  if (!match) throw new Error(`No button containing "${text}"`);
  return match as HTMLButtonElement;
}

describe('ConfirmDialogComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [ConfirmDialogComponent] }));

  it('shows the title and message when open', () => {
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('title', 'Remove Hossam?');
    fixture.componentRef.setInput('message', 'The account will be soft-deleted.');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Remove Hossam?');
    expect(fixture.nativeElement.textContent).toContain('The account will be soft-deleted.');
  });

  it('emits confirm when the confirm button is clicked', () => {
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('confirmLabel', 'Remove');
    fixture.detectChanges();

    const confirmSpy = jest.fn();
    fixture.componentInstance.confirm.subscribe(confirmSpy);

    buttonByText(fixture.nativeElement, 'Remove').click();

    expect(confirmSpy).toHaveBeenCalledTimes(1);
  });

  it('emits cancel when the cancel button is clicked', () => {
    const fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('cancelLabel', 'Cancel');
    fixture.detectChanges();

    const cancelSpy = jest.fn();
    fixture.componentInstance.cancel.subscribe(cancelSpy);

    buttonByText(fixture.nativeElement, 'Cancel').click();

    expect(cancelSpy).toHaveBeenCalledTimes(1);
  });
});
