import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SelectComponent, SelectOption } from './select.component';

const OPTIONS: SelectOption[] = [
  { value: 'Assistant', label: 'Assistant' },
  { value: 'Teacher', label: 'Teacher' },
];

function create(): ComponentFixture<SelectComponent> {
  const fixture = TestBed.createComponent(SelectComponent);
  fixture.componentRef.setInput('options', OPTIONS);
  fixture.detectChanges();
  return fixture;
}

describe('SelectComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [SelectComponent] }));

  it('shows the placeholder and no menu when closed', () => {
    const fixture = create();
    fixture.componentRef.setInput('placeholder', 'Select a role');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sb-select__value').textContent).toContain('Select a role');
    expect(fixture.nativeElement.querySelector('.sb-select__menu')).toBeNull();
  });

  it('opens the listbox on trigger click', () => {
    const fixture = create();
    fixture.nativeElement.querySelector('.sb-select__trigger').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.sb-select__option')).toHaveLength(2);
  });

  it('selecting an option notifies the form, closes, and shows the label', () => {
    const fixture = create();
    const onChange = jest.fn();
    fixture.componentInstance.registerOnChange(onChange);

    fixture.nativeElement.querySelector('.sb-select__trigger').click();
    fixture.detectChanges();

    const teacher = Array.from(
      fixture.nativeElement.querySelectorAll('.sb-select__option'),
    ).find((li) => (li as HTMLElement).textContent?.includes('Teacher')) as HTMLElement;
    teacher.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    fixture.detectChanges();

    expect(onChange).toHaveBeenCalledWith('Teacher');
    expect(fixture.nativeElement.querySelector('.sb-select__menu')).toBeNull();
    expect(fixture.nativeElement.querySelector('.sb-select__value').textContent).toContain('Teacher');
  });

  it('writeValue reflects the selected label', () => {
    const fixture = create();
    fixture.componentInstance.writeValue('Assistant');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.sb-select__value').textContent).toContain('Assistant');
  });

  it('emits valueChange when an option is chosen', () => {
    const fixture = create();
    const spy = jest.fn();
    fixture.componentInstance.valueChange.subscribe(spy);

    fixture.componentInstance.choose(OPTIONS[0]);

    expect(spy).toHaveBeenCalledWith('Assistant');
  });
});
