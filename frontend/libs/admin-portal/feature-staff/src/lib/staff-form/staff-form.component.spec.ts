import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StaffFormComponent } from './staff-form.component';

function openBlank(fixture: ComponentFixture<StaffFormComponent>): void {
  fixture.componentRef.setInput('open', true);
  fixture.detectChanges();
}

describe('StaffFormComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [StaffFormComponent] }));

  it('does not emit save when required fields are empty', () => {
    const fixture = TestBed.createComponent(StaffFormComponent);
    openBlank(fixture);

    const saveSpy = jest.fn();
    fixture.componentInstance.save.subscribe(saveSpy);

    fixture.componentInstance.onSubmit();

    expect(saveSpy).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.displayName.touched).toBe(true);
  });

  it('flags an invalid email', () => {
    const fixture = TestBed.createComponent(StaffFormComponent);
    openBlank(fixture);

    const email = fixture.componentInstance.form.controls.email;
    email.setValue('not-an-email');
    email.markAsTouched();

    expect(fixture.componentInstance.emailError).toContain('valid email');
  });

  it('emits save with the form value when valid', () => {
    const fixture = TestBed.createComponent(StaffFormComponent);
    openBlank(fixture);

    fixture.componentInstance.form.setValue({
      displayName: 'Mariam Adel',
      email: 'mariam@bahzad.edu.eg',
      role: 'Assistant',
    });

    const saveSpy = jest.fn();
    fixture.componentInstance.save.subscribe(saveSpy);

    fixture.componentInstance.onSubmit();

    expect(saveSpy).toHaveBeenCalledWith({
      displayName: 'Mariam Adel',
      email: 'mariam@bahzad.edu.eg',
      role: 'Assistant',
    });
  });

  it('shows a server error passed from the parent', () => {
    const fixture = TestBed.createComponent(StaffFormComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('error', 'A staff member with that email already exists.');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('already exists');
  });

  it('seeds the form with the staff member when editing', () => {
    const fixture = TestBed.createComponent(StaffFormComponent);
    fixture.componentRef.setInput('staff', {
      id: '1',
      displayName: 'Edit Me',
      email: 'edit@x.com',
      role: 'Teacher',
      isActive: true,
      createdAtUtc: '2026-01-01T00:00:00Z',
      updatedAtUtc: null,
    });
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();

    expect(fixture.componentInstance.isEdit()).toBe(true);
    expect(fixture.componentInstance.form.controls.displayName.value).toBe('Edit Me');
    expect(fixture.componentInstance.form.controls.role.value).toBe('Teacher');
  });
});
