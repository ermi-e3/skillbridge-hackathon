import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthResponse } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { ThemeService } from '../../../core/services/theme.service';
import { UserProfileMenuComponent } from './user-profile-menu.component';

@Component({ standalone: true, template: '' })
class DummyRouteComponent {}

function makeAuthUser(overrides: Partial<AuthResponse>): AuthResponse {
  return {
    accessToken: 'jwt-token',
    expiresAt: '2026-12-31T23:59:59Z',
    userId: 'user-1',
    email: 'abebe.bikila@example.com',
    fullName: 'Abebe Bikila',
    role: 'Candidate',
    ...overrides,
  };
}

describe('UserProfileMenuComponent', () => {
  let fixture: ComponentFixture<UserProfileMenuComponent>;
  let component: UserProfileMenuComponent;
  let currentUserSignal: ReturnType<typeof signal<AuthResponse | null>>;
  let authServiceMock: {
    currentUser: ReturnType<typeof signal<AuthResponse | null>>;
    getRole: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    currentUserSignal = signal<AuthResponse | null>(makeAuthUser({}));

    authServiceMock = {
      currentUser: currentUserSignal,
      getRole: vi.fn(() => currentUserSignal()?.role ?? null),
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [UserProfileMenuComponent],
      providers: [
        provideRouter([
          { path: 'candidate/profile', component: DummyRouteComponent },
          { path: 'my-applications', component: DummyRouteComponent },
          { path: 'account/security', component: DummyRouteComponent },
          { path: 'employer/jobs', component: DummyRouteComponent },
          { path: 'employer/applications', component: DummyRouteComponent },
        ]),
        { provide: AuthService, useValue: authServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserProfileMenuComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('computes dynamic initials for standard, single-word, titled, and email-only users', () => {
    expect(component.initials()).toBe('AB');

    currentUserSignal.set(
      makeAuthUser({ userId: 'u2', email: 'almaz@example.com', fullName: 'Dr. Almaz Belay' })
    );
    fixture.detectChanges();
    expect(component.initials()).toBe('AB');

    currentUserSignal.set(
      makeAuthUser({ userId: 'u3', email: 'mononym@example.com', fullName: 'Tewodros' })
    );
    fixture.detectChanges();
    expect(component.initials()).toBe('TE');

    currentUserSignal.set(
      makeAuthUser({ userId: 'u4', email: 'zara@example.com', fullName: '' })
    );
    fixture.detectChanges();
    expect(component.initials()).toBe('Z');
  });

  it('displays dynamic candidate full name, email, and candidate navigation links when opened', () => {
    const trigger = fixture.nativeElement.querySelector('.user-avatar-trigger') as HTMLButtonElement;
    expect(trigger.textContent).toContain('AB');
    expect(fixture.nativeElement.querySelector('.profile-dropdown-panel')).toBeNull();

    trigger.click();
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('.profile-dropdown-panel') as HTMLElement;
    expect(panel).toBeTruthy();
    expect(panel.querySelector('.user-full-name')?.textContent?.trim()).toBe('Abebe Bikila');
    expect(panel.querySelector('.user-email')?.textContent?.trim()).toBe('abebe.bikila@example.com');
    expect(panel.querySelector('.role-pill')?.textContent?.trim()).toBe('Candidate');

    const links = Array.from(panel.querySelectorAll('a.dropdown-item')) as HTMLAnchorElement[];
    const titles = links.map((a) => a.querySelector('.item-title')?.textContent?.trim());
    const hrefs = links.map((a) => a.getAttribute('href'));

    expect(titles).toEqual(['Profile', 'Applications', 'Settings']);
    expect(hrefs).toEqual(['/candidate/profile', '/my-applications', '/account/security']);
  });

  it('displays dynamic employer navigation links and company name for Employer role', () => {
    currentUserSignal.set(
      makeAuthUser({
        userId: 'emp-1',
        email: 'hr@abyssiniatech.et',
        fullName: 'Sara Mekonnen',
        role: 'Employer',
        companyName: 'Abyssinia Tech',
      })
    );
    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector('.user-avatar-trigger') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('.profile-dropdown-panel') as HTMLElement;
    expect(panel.querySelector('.user-full-name')?.textContent?.trim()).toBe('Sara Mekonnen');
    expect(panel.querySelector('.user-email')?.textContent?.trim()).toBe('hr@abyssiniatech.et');
    expect(panel.querySelector('.role-pill')?.textContent?.trim()).toBe('Employer');
    expect(panel.querySelector('.company-meta')?.textContent?.trim()).toBe('Abyssinia Tech');

    const links = Array.from(panel.querySelectorAll('a.dropdown-item')) as HTMLAnchorElement[];
    const titles = links.map((a) => a.querySelector('.item-title')?.textContent?.trim());
    const hrefs = links.map((a) => a.getAttribute('href'));

    expect(titles).toEqual(['Profile', 'Jobs', 'Applications', 'Settings']);
    expect(hrefs).toEqual([
      '/account/security#profile',
      '/employer/jobs',
      '/employer/applications',
      '/account/security#security',
    ]);
  });

  it('closes dropdown on outside click, Escape key, and menu item click', async () => {
    const trigger = fixture.nativeElement.querySelector('.user-avatar-trigger') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();
    expect(component.isOpen()).toBe(true);

    // Outside click
    document.body.click();
    fixture.detectChanges();
    expect(component.isOpen()).toBe(false);

    // Reopen and press Escape
    trigger.click();
    fixture.detectChanges();
    expect(component.isOpen()).toBe(true);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(component.isOpen()).toBe(false);

    // Reopen and click a navigation link
    trigger.click();
    fixture.detectChanges();
    const firstLink = fixture.nativeElement.querySelector('a.dropdown-item') as HTMLAnchorElement;
    firstLink.click();
    fixture.detectChanges();
    expect(component.isOpen()).toBe(false);
    await fixture.whenStable();
  });

  it('allows switching between Light, Dark, and System themes inside the dropdown without closing it', () => {
    const themeService = TestBed.inject(ThemeService);
    themeService.setTheme('light');

    const trigger = fixture.nativeElement.querySelector('.user-avatar-trigger') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    const segButtons = Array.from(
      fixture.nativeElement.querySelectorAll('.dropdown-theme-section .seg-option')
    ) as HTMLButtonElement[];
    expect(segButtons.length).toBe(3);

    segButtons[1].click(); // Dark
    fixture.detectChanges();
    expect(themeService.isDark()).toBe(true);
    expect(component.isOpen()).toBe(true);
  });

  it('calls authService.logout and closes menu when clicking Sign Out', () => {
    const trigger = fixture.nativeElement.querySelector('.user-avatar-trigger') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    const signoutBtn = fixture.nativeElement.querySelector('.dropdown-signout-btn') as HTMLButtonElement;
    expect(signoutBtn).toBeTruthy();
    signoutBtn.click();
    fixture.detectChanges();

    expect(authServiceMock.logout).toHaveBeenCalledOnce();
    expect(component.isOpen()).toBe(false);
  });
});
