import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  let fixture: ComponentFixture<PaginationComponent>;
  let component: PaginationComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaginationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(PaginationComponent);
    component = fixture.componentInstance;
  });

  it('displays range summary and renders page numbers for multi-page datasets', () => {
    fixture.componentRef.setInput('currentPage', 2);
    fixture.componentRef.setInput('totalItems', 25);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.componentRef.setInput('itemLabel', 'applications');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Showing 11–20 of 25 applications');

    const pageNums = fixture.nativeElement.querySelectorAll('.page-num-btn');
    expect(pageNums.length).toBe(3);
    expect(pageNums[1].classList.contains('active')).toBe(true);
  });

  it('emits pageChange when clicking next or specific page number', () => {
    fixture.componentRef.setInput('currentPage', 1);
    fixture.componentRef.setInput('totalItems', 30);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.detectChanges();

    const emitted: number[] = [];
    component.pageChange.subscribe((p) => emitted.push(p));

    const nextBtn = fixture.nativeElement.querySelector('.nav-next-btn') as HTMLButtonElement;
    nextBtn.click();
    expect(emitted).toEqual([2]);

    const pageThreeBtn = fixture.nativeElement.querySelectorAll('.page-num-btn')[2] as HTMLButtonElement;
    pageThreeBtn.click();
    expect(emitted).toEqual([2, 3]);
  });

  it('emits pageSizeChange when changing items per page', () => {
    fixture.componentRef.setInput('currentPage', 1);
    fixture.componentRef.setInput('totalItems', 30);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.componentRef.setInput('pageSizeOptions', [5, 10, 20]);
    fixture.detectChanges();

    const sizes: number[] = [];
    component.pageSizeChange.subscribe((s) => sizes.push(s));

    const select = fixture.nativeElement.querySelector('#pagination-page-size') as HTMLSelectElement;
    select.value = '20';
    select.dispatchEvent(new Event('change'));
    expect(sizes).toEqual([20]);
  });

  it('hides pagination bar when totalItems is 0', () => {
    fixture.componentRef.setInput('currentPage', 1);
    fixture.componentRef.setInput('totalItems', 0);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.pagination-bar')).toBeNull();
  });
});

