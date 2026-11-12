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
    expect(component.heroCompatibility()).toBe('98.4');

    const tabs = fixture.nativeElement.querySelectorAll('.audience-btn') as NodeListOf<HTMLButtonElement>;
    expect(tabs.length).toBe(2);

    tabs[1].click();
    fixture.detectChanges();

    expect(component.audience()).toBe('teams');
    expect(fixture.nativeElement.textContent).toContain('Verified capability');
  });

  it('recalculates compatibility score when toggling hero skill nodes', () => {
    expect(component.heroCompatibility()).toBe('98.4');

    const nodes = fixture.nativeElement.querySelectorAll('.skill-node-card') as NodeListOf<HTMLButtonElement>;
    expect(nodes.length).toBe(3);

    nodes[0].click();
    fixture.detectChanges();

    expect(Number(component.heroCompatibility())).toBeLessThan(98.4);
  });

  it('updates live skill sandbox role and compensation when toggling capability pills', () => {
    const initialRole = component.sandboxMatch().roleTitle;
    const pills = fixture.nativeElement.querySelectorAll('.sandbox-pill') as NodeListOf<HTMLButtonElement>;
    expect(pills.length).toBeGreaterThanOrEqual(4);

    // Toggle Applied AI / ML (index 4)
    pills[4].click();
    fixture.detectChanges();

    expect(component.sandboxMatch().roleTitle).not.toBe(initialRole);
    expect(component.sandboxMatch().roleTitle).toContain('AI');
  });

  it('toggles Accept Intro state in the match engine card', () => {
    const btn = fixture.nativeElement.querySelector('.accept-intro-btn') as HTMLButtonElement;
    expect(component.introAccepted()).toBe(false);

    btn.click();
    fixture.detectChanges();

    expect(component.introAccepted()).toBe(true);
    expect(btn.textContent).toContain('Intro Accepted');
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

