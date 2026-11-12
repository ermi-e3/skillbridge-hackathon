import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ThemeService } from '../../../core/services/theme.service';
import { ThemeToggleComponent } from './theme-toggle.component';

describe('ThemeToggleComponent', () => {
  let fixture: ComponentFixture<ThemeToggleComponent>;
  let themeService: ThemeService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [ThemeToggleComponent],
    }).compileComponents();

    themeService = TestBed.inject(ThemeService);
    themeService.setTheme('light');

    fixture = TestBed.createComponent(ThemeToggleComponent);
    fixture.detectChanges();
  });

  it('toggles between light and dark mode on icon button click', () => {
    const btn = fixture.nativeElement.querySelector('.theme-icon-btn') as HTMLButtonElement;
    expect(btn).toBeTruthy();
    expect(btn.getAttribute('aria-pressed')).toBe('false');

    btn.click();
    fixture.detectChanges();
    expect(themeService.isDark()).toBe(true);
    expect(btn.getAttribute('aria-pressed')).toBe('true');

    btn.click();
    fixture.detectChanges();
    expect(themeService.isDark()).toBe(false);
  });

  it('renders segmented Light / Dark / System controls when variant is segmented', () => {
    fixture.componentRef.setInput('variant', 'segmented');
    fixture.detectChanges();

    const options = Array.from(
      fixture.nativeElement.querySelectorAll('.seg-option')
    ) as HTMLButtonElement[];
    expect(options.length).toBe(3);

    options[1].click(); // Dark
    fixture.detectChanges();
    expect(themeService.preference()).toBe('dark');
    expect(options[1].classList.contains('active')).toBe(true);

    options[2].click(); // System
    fixture.detectChanges();
    expect(themeService.preference()).toBe('system');
    expect(options[2].classList.contains('active')).toBe(true);
  });
});

