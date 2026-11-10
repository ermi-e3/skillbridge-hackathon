import { CommonModule } from '@angular/common';
import { Component, inject, input } from '@angular/core';
import { ThemePreference, ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (variant() === 'segmented') {
      <div class="theme-segmented" role="radiogroup" aria-label="Theme preference">
        <button
          type="button"
          class="seg-option"
          [class.active]="themeService.preference() === 'light'"
          role="radio"
          [attr.aria-checked]="themeService.preference() === 'light'"
          (click)="selectTheme('light', $event)"
          title="Light mode"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <circle cx="12" cy="12" r="4"></circle>
            <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"></path>
          </svg>
          <span>Light</span>
        </button>

        <button
          type="button"
          class="seg-option"
          [class.active]="themeService.preference() === 'dark'"
          role="radio"
          [attr.aria-checked]="themeService.preference() === 'dark'"
          (click)="selectTheme('dark', $event)"
          title="Dark mode"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
          </svg>
          <span>Dark</span>
        </button>

        <button
          type="button"
          class="seg-option"
          [class.active]="themeService.preference() === 'system'"
          role="radio"
          [attr.aria-checked]="themeService.preference() === 'system'"
          (click)="selectTheme('system', $event)"
          title="Follow system theme"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
            <line x1="8" y1="21" x2="16" y2="21"></line>
            <line x1="12" y1="17" x2="12" y2="21"></line>
          </svg>
          <span>System</span>
        </button>
      </div>
    } @else {
      <button
        type="button"
        class="theme-icon-btn"
        (click)="onToggleClick($event)"
        [attr.aria-pressed]="themeService.isDark()"
        [attr.aria-label]="themeService.isDark() ? 'Switch to Light Mode' : 'Switch to Dark Mode'"
        [title]="themeService.isDark() ? 'Switch to Light Mode' : 'Switch to Dark Mode'"
      >
        @if (themeService.isDark()) {
          <svg class="theme-icon sun-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <circle cx="12" cy="12" r="4"></circle>
            <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"></path>
          </svg>
        } @else {
          <svg class="theme-icon moon-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
          </svg>
        }
      </button>
    }
  `,
  styles: [`
    :host {
      display: inline-flex;
      align-items: center;
    }

    .theme-icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 2.5rem;
      height: 2.5rem;
      padding: 0;
      border-radius: 9999px;
      border: 1.5px solid var(--line);
      background: var(--surface);
      color: var(--ink-soft);
      cursor: pointer;
      box-shadow: var(--shadow-xs);
      transition: background-color 0.18s ease, border-color 0.18s ease, color 0.18s ease, transform 0.15s ease, box-shadow 0.18s ease;

      &:hover {
        background: var(--bg-subtle);
        border-color: var(--line-strong);
        color: var(--primary);
        box-shadow: var(--shadow-sm);
      }

      &:active {
        transform: scale(0.95);
      }

      &:focus-visible {
        outline: none;
        border-color: var(--primary);
        box-shadow: 0 0 0 3px var(--primary-ring);
      }

      .theme-icon {
        width: 1.15rem;
        height: 1.15rem;
        transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1);
      }

      &:hover .sun-icon {
        transform: rotate(20deg) scale(1.06);
      }

      &:hover .moon-icon {
        transform: rotate(-12deg) scale(1.06);
      }
    }

    .theme-segmented {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.2rem;
      width: 100%;
      padding: 0.2rem;
      background: var(--bg-subtle);
      border: 1px solid var(--line);
      border-radius: 10px;
    }

    .seg-option {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.3rem;
      padding: 0.38rem 0.45rem;
      border: none;
      border-radius: 7px;
      background: transparent;
      color: var(--ink-soft);
      font-family: inherit;
      font-size: 0.74rem;
      font-weight: 600;
      cursor: pointer;
      transition: background-color 0.15s ease, color 0.15s ease, box-shadow 0.15s ease;

      svg {
        width: 0.85rem;
        height: 0.85rem;
        flex-shrink: 0;
      }

      &:hover:not(.active) {
        color: var(--ink);
        background: var(--surface-hover);
      }

      &:focus-visible {
        outline: none;
        box-shadow: 0 0 0 2px var(--primary-ring);
      }

      &.active {
        background: var(--surface-elevated);
        color: var(--primary);
        box-shadow: var(--shadow-xs);
      }
    }
  `],
})
export class ThemeToggleComponent {
  readonly themeService = inject(ThemeService);
  readonly variant = input<'icon' | 'segmented'>('icon');

  onToggleClick(event: Event): void {
    event.stopPropagation();
    this.themeService.toggleTheme();
  }

  selectTheme(pref: ThemePreference, event: Event): void {
    event.stopPropagation();
    this.themeService.setTheme(pref);
  }
}

