import { TestBed } from '@angular/core/testing';
import { MatchingBoard } from './matching-board';

describe('Matching exercise connections', () => {
  async function open() {
    TestBed.configureTestingModule({ imports: [MatchingBoard] });
    const fixture = TestBed.createComponent(MatchingBoard);
    fixture.componentRef.setInput('options', [
      { id: 'l1', text: 'Hola', side: 'left' },
      { id: 'r2', text: 'Goodbye', side: 'right' },
      { id: 'l2', text: 'Adiós', side: 'left' },
      { id: 'r1', text: 'Hello', side: 'right' },
    ]);
    await fixture.whenStable();
    return fixture;
  }
  function choose(select: HTMLSelectElement, id: string) {
    select.value = id;
    select.dispatchEvent(new Event('change'));
  }
  it('connects each right-side item once and frees it when the connection is cleared', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    const fields = host.querySelectorAll<HTMLSelectElement>('select');
    choose(fields[0], 'r1');
    await fixture.whenStable();
    expect(fields[1].querySelector<HTMLOptionElement>('option[value="r1"]')!.disabled).toBe(true);
    expect(host.querySelector('.pair-count')!.textContent).toContain('1 of 2');
    choose(fields[0], '');
    await fixture.whenStable();
    expect(fields[1].querySelector<HTMLOptionElement>('option[value="r1"]')!.disabled).toBe(false);
    choose(fields[1], 'r1');
    await fixture.whenStable();
    expect(fixture.componentInstance.pairs()).toEqual([
      { leftOptionId: 'l2', rightOptionId: 'r1' },
    ]);
    expect(host.querySelector('.correct, .incorrect')).toBeNull();
  });
  it('freezes controls while a request is pending and clears all connections explicitly', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    choose(host.querySelector<HTMLSelectElement>('select')!, 'r2');
    await fixture.whenStable();
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();
    expect(
      Array.from(host.querySelectorAll<HTMLSelectElement>('select')).every(
        (field) => field.disabled,
      ),
    ).toBe(true);
    expect(host.querySelector<HTMLButtonElement>('button')!.disabled).toBe(true);
    fixture.componentRef.setInput('disabled', false);
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(fixture.componentInstance.pairs()).toEqual([]);
  });
});
