import { TestBed } from '@angular/core/testing';
import { ModalComponent } from './modal.component';

describe('ModalComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [ModalComponent] }));

  it('renders nothing when closed', () => {
    const fixture = TestBed.createComponent(ModalComponent);
    fixture.componentRef.setInput('open', false);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sb-modal')).toBeNull();
  });

  it('renders the title when open', () => {
    const fixture = TestBed.createComponent(ModalComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('title', 'Add staff');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sb-modal__title').textContent).toContain('Add staff');
  });

  it('emits close when Escape is pressed', () => {
    const fixture = TestBed.createComponent(ModalComponent);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();

    const closeSpy = jest.fn();
    fixture.componentInstance.close.subscribe(closeSpy);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));

    expect(closeSpy).toHaveBeenCalledTimes(1);
  });

  it('emits close when the scrim is clicked', () => {
    const fixture = TestBed.createComponent(ModalComponent);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();

    const closeSpy = jest.fn();
    fixture.componentInstance.close.subscribe(closeSpy);

    fixture.nativeElement.querySelector('.sb-modal__scrim').click();

    expect(closeSpy).toHaveBeenCalledTimes(1);
  });

  it('does not close on scrim click when closeOnScrim is false', () => {
    const fixture = TestBed.createComponent(ModalComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('closeOnScrim', false);
    fixture.detectChanges();

    const closeSpy = jest.fn();
    fixture.componentInstance.close.subscribe(closeSpy);

    fixture.nativeElement.querySelector('.sb-modal__scrim').click();

    expect(closeSpy).not.toHaveBeenCalled();
  });
});
