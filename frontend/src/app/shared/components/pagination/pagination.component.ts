import { CommonModule } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.scss',
})
export class PaginationComponent {
  readonly currentPage = input.required<number>();
  readonly totalItems = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly pageSizeOptions = input<number[]>([5, 10, 20, 50]);
  readonly itemLabel = input<string>('items');

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  readonly totalPages = computed(() => {
    const size = Math.max(1, this.pageSize());
    return Math.max(1, Math.ceil(this.totalItems() / size));
  });

  readonly startIndex = computed(() => {
    if (this.totalItems() === 0) return 0;
    return (this.currentPage() - 1) * this.pageSize() + 1;
  });

  readonly endIndex = computed(() => {
    if (this.totalItems() === 0) return 0;
    return Math.min(this.currentPage() * this.pageSize(), this.totalItems());
  });

  /**
   * Builds a compact array of page tokens (numbers and 'ellipsis') for clean navigation.
   */
  readonly pageTokens = computed<(number | 'ellipsis')[]>(() => {
    const total = this.totalPages();
    const current = this.currentPage();

    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }

    const tokens: (number | 'ellipsis')[] = [1];

    if (current > 3) {
      tokens.push('ellipsis');
    }

    const rangeStart = Math.max(2, current - 1);
    const rangeEnd = Math.min(total - 1, current + 1);

    for (let i = rangeStart; i <= rangeEnd; i++) {
      tokens.push(i);
    }

    if (current < total - 2) {
      tokens.push('ellipsis');
    }

    tokens.push(total);
    return tokens;
  });

  goToPage(page: number): void {
    const target = Math.max(1, Math.min(page, this.totalPages()));
    if (target !== this.currentPage()) {
      this.pageChange.emit(target);
    }
  }

  onPageSizeSelect(event: Event): void {
    const val = Number((event.target as HTMLSelectElement).value);
    if (!Number.isNaN(val) && val > 0 && val !== this.pageSize()) {
      this.pageSizeChange.emit(val);
    }
  }
}

