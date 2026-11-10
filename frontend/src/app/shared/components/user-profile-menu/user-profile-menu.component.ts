import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { UserRole } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';
import { ThemeToggleComponent } from '../theme-toggle/theme-toggle.component';

const TITLE_PREFIXES = new Set(['dr.', 'dr', 'mr.', 'mr', 'ms.', 'ms', 'mrs.', 'mrs', 'eng.', 'prof.']);

@Component({
  selector: 'app-user-profile-menu',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, ThemeToggleComponent],
  templateUrl: './user-profile-menu.component.html',
  styleUrl: './user-profile-menu.component.scss',
})
export class UserProfileMenuComponent {
  readonly authService = inject(AuthService, { optional: true });
  private readonly elementRef = inject(ElementRef<HTMLElement>);

  // Optional fallbacks if a page holds profile/status data independently
  readonly fallbackName = input<string | null>(null);
  readonly fallbackEmail = input<string | null>(null);
  readonly fallbackRole = input<UserRole | null>(null);

  readonly isOpen = signal<boolean>(false);

  private readonly authUser = computed(() => {
    const svc = this.authService as Partial<AuthService> | null;
    if (svc && typeof svc.currentUser === 'function') {
      return svc.currentUser();
    }
    return null;
  });

  readonly displayEmail = computed<string>(() => {
    return this.authUser()?.email?.trim() || this.fallbackEmail()?.trim() || '';
  });

  readonly displayName = computed<string>(() => {
    const raw = this.authUser()?.fullName?.trim() || this.fallbackName()?.trim();
    if (raw) {
      return raw;
    }
    const email = this.displayEmail();
    return email ? email.split('@')[0] : 'Account';
  });

  readonly displayRole = computed<UserRole>(() => {
    const userRole = this.authUser()?.role;
    if (userRole) {
      return userRole;
    }
    const svc = this.authService as Partial<AuthService> | null;
    if (svc && typeof svc.getRole === 'function') {
      const r = svc.getRole();
      if (r) return r;
    }
    return this.fallbackRole() ?? 'Candidate';
  });

  readonly companyName = computed<string | null>(() => {
    const comp = this.authUser()?.companyName?.trim();
    return comp || null;
  });

  readonly initials = computed<string>(() => {
    const name = this.authUser()?.fullName?.trim() || this.fallbackName()?.trim();
    if (name) {
      const rawWords = name.split(/\s+/).filter((w) => w.length > 0);
      const meaningful = rawWords.filter((w) => !TITLE_PREFIXES.has(w.toLowerCase()));
      const words = meaningful.length > 0 ? meaningful : rawWords;

      if (words.length >= 2) {
        return (words[0][0] + words[words.length - 1][0]).toUpperCase();
      }
      if (words.length === 1) {
        return words[0].slice(0, 2).toUpperCase();
      }
    }

    const email = this.displayEmail();
    if (email) {
      return email[0].toUpperCase();
    }
    return 'U';
  });

  toggleMenu(event?: Event): void {
    event?.stopPropagation();
    this.isOpen.update((open) => !open);
  }

  closeMenu(): void {
    this.isOpen.set(false);
  }

  onSignOut(): void {
    this.closeMenu();
    this.authService?.logout();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.isOpen()) return;
    const target = event.target as Node | null;
    if (target && !this.elementRef.nativeElement.contains(target)) {
      this.closeMenu();
    }
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.isOpen()) {
      this.closeMenu();
    }
  }
}
