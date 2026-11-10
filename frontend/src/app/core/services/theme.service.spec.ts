import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('data-theme-preference');
    TestBed.configureTestingModule({});
  });

  it('defaults to system preference when localStorage has no saved theme', () => {
    const service = TestBed.inject(ThemeService);
    expect(service.preference()).toBe('system');
    expect(['light', 'dark']).toContain(service.resolvedTheme());
    expect(document.documentElement.getAttribute('data-theme')).toBe(service.resolvedTheme());
  });

  it('persists explicit dark and light selection and updates DOM attributes immediately', () => {
    const service = TestBed.inject(ThemeService);

    service.setTheme('dark');
    expect(service.preference()).toBe('dark');
    expect(service.resolvedTheme()).toBe('dark');
    expect(service.isDark()).toBe(true);
    expect(localStorage.getItem('skillbridge_theme')).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme-preference')).toBe('dark');

    service.setTheme('light');
    expect(service.preference()).toBe('light');
    expect(service.resolvedTheme()).toBe('light');
    expect(service.isDark()).toBe(false);
    expect(localStorage.getItem('skillbridge_theme')).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('toggles between dark and light mode and restores saved preference on new instance', () => {
    localStorage.setItem('skillbridge_theme', 'dark');
    const service = TestBed.inject(ThemeService);

    expect(service.preference()).toBe('dark');
    expect(service.isDark()).toBe(true);

    service.toggleTheme();
    expect(service.preference()).toBe('light');
    expect(service.isDark()).toBe(false);
    expect(localStorage.getItem('skillbridge_theme')).toBe('light');
  });
});

