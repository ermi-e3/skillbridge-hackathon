import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { ThemeService } from '../../core/services/theme.service';
import { LandingComponent } from './landing.component';

describe('LandingComponent', () => {
  let fixture: ComponentFixture<LandingComponent>;
  let component: LandingComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LandingComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(LandingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('switches audience perspective between I Have Skills and I Need Talent', () => {
    expect(component.audience()).toBe('talent');
    expect(component.heroCompatibility()).toBe('88.5');

    const tabs = fixture.nativeElement.querySelectorAll('.audience-btn') as NodeListOf<HTMLButtonElement>;
    expect(tabs.length).toBe(2);

    tabs[1].click();
    fixture.detectChanges();

    expect(component.audience()).toBe('teams');
    expect(fixture.nativeElement.textContent).toContain('applicants ranked by fit');
  });

  it('recalculates compatibility score when toggling hero skill nodes', () => {
    expect(component.heroCompatibility()).toBe('88.5');

    const nodes = fixture.nativeElement.querySelectorAll('.skill-node-card') as NodeListOf<HTMLButtonElement>;
    expect(nodes.length).toBe(3);

    nodes[0].click();
    fixture.detectChanges();

    expect(Number(component.heroCompatibility())).toBeLessThan(88.5);
  });

  it('updates live skill sandbox role and breakdown when toggling capability pills', () => {
    const initialRole = component.sandboxMatch().roleTitle;
    const pills = fixture.nativeElement.querySelectorAll('.sandbox-pill') as NodeListOf<HTMLButtonElement>;
    expect(pills.length).toBeGreaterThanOrEqual(4);

    // Toggle Patient Care (index 3)
    pills[3].click();
    fixture.detectChanges();

    expect(component.sandboxMatch().roleTitle).not.toBe(initialRole);
    expect(component.sandboxMatch().roleTitle).toContain('Clinical');
  });

  it('toggles preview action state in the match card', () => {
    const btn = fixture.nativeElement.querySelector('.accept-intro-btn') as HTMLButtonElement;
    expect(component.previewActionDone()).toBe(false);

    btn.click();
    fixture.detectChanges();

    expect(component.previewActionDone()).toBe(true);
    expect(btn.textContent).toContain('Applied with Profile CV');
  });

  it('toggles Light / Dark mode from the floating header theme button', () => {
    const themeService = TestBed.inject(ThemeService);
    themeService.setTheme('dark');
    fixture.detectChanges();

    const toggleBtn = fixture.nativeElement.querySelector('.nav-actions .theme-icon-btn') as HTMLButtonElement;
    expect(toggleBtn).toBeTruthy();

    toggleBtn.click();
    fixture.detectChanges();
    expect(themeService.isDark()).toBe(false);

    toggleBtn.click();
    fixture.detectChanges();
    expect(themeService.isDark()).toBe(true);
  });
});

