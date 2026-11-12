import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';

const THEME_STORAGE_KEY = 'skillbridge_theme';
const TRANSITION_CLASS = 'theme-transition';
const TRANSITION_DURATION_MS = 280;

@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private mediaQuery: MediaQueryList | null = null;
  private transitionTimer: ReturnType<typeof setTimeout> | null = null;
  private initialized = false;

  readonly preference = signal<ThemePreference>(this.readStoredPreference());
  readonly systemPrefersDark = signal<boolean>(this.detectSystemDark());

  readonly resolvedTheme = computed<ResolvedTheme>(() => {
    const pref = this.preference();
    if (pref === 'system') {
      return this.systemPrefersDark() ? 'dark' : 'light';
    }
    return pref;
  });

  readonly isDark = computed<boolean>(() => this.resolvedTheme() === 'dark');

  constructor() {
    this.initTheme();
  }

  initTheme(): void {
    if (!this.initialized && typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
      this.mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      this.systemPrefersDark.set(this.mediaQuery.matches);

      const handleMediaChange = (event: MediaQueryListEvent) => {
        this.systemPrefersDark.set(event.matches);
        if (this.preference() === 'system') {
          this.applyThemeToDom(false);
        }
      };

      if (typeof this.mediaQuery.addEventListener === 'function') {
        this.mediaQuery.addEventListener('change', handleMediaChange);
      } else if (typeof this.mediaQuery.addListener === 'function') {
        this.mediaQuery.addListener(handleMediaChange);
      }
      this.initialized = true;
    }

    this.applyThemeToDom(false);
  }

  setTheme(pref: ThemePreference): void {
    this.preference.set(pref);
    this.persistPreference(pref);
    this.applyThemeToDom(true);
  }

  toggleTheme(): void {
    const next: ThemePreference = this.isDark() ? 'light' : 'dark';
    this.setTheme(next);
  }

  private readStoredPreference(): ThemePreference {
    try {
      if (typeof localStorage !== 'undefined') {
        const stored = localStorage.getItem(THEME_STORAGE_KEY);
        if (stored === 'light' || stored === 'dark' || stored === 'system') {
          return stored;
        }
      }
    } catch {
      // Ignore storage access failures (private browsing / restricted context)
    }
    return 'system';
  }

  private persistPreference(pref: ThemePreference): void {
    try {
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(THEME_STORAGE_KEY, pref);
      }
    } catch {
      // Ignore storage write failures
    }
  }

  private detectSystemDark(): boolean {
    try {
      if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
      }
    } catch {
      // Ignore matchMedia errors
    }
    return false;
  }

  private applyThemeToDom(animate: boolean): void {
    const root = this.document?.documentElement;
    if (!root) return;

    const resolved = this.resolvedTheme();
    const pref = this.preference();

    if (animate) {
      root.classList.add(TRANSITION_CLASS);
      if (this.transitionTimer) {
        clearTimeout(this.transitionTimer);
      }
      this.transitionTimer = setTimeout(() => {
        root.classList.remove(TRANSITION_CLASS);
        this.transitionTimer = null;
      }, TRANSITION_DURATION_MS);
    }

    root.setAttribute('data-theme', resolved);
    root.setAttribute('data-theme-preference', pref);
    root.style.colorScheme = resolved;

    const themeMeta = this.document.querySelector('meta[name="theme-color"]');
    if (themeMeta) {
      themeMeta.setAttribute('content', resolved === 'dark' ? '#0b1120' : '#f8fafc');
    }
  }
}

