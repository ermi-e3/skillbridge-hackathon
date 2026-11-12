import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  QueryList,
  signal,
  ViewChildren,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ThemeToggleComponent } from '../../shared/components/theme-toggle/theme-toggle.component';
import { WalkthroughShowcaseComponent } from './walkthrough-showcase/walkthrough-showcase.component';

export type AudienceMode = 'talent' | 'teams';

export interface HeroSkillNode {
  id: string;
  label: string;
  tag: string;
  weight: number;
  pathD: string;
}

export interface SandboxSkill {
  id: string;
  label: string;
  category: string;
  weight: number;
}

interface SandboxResult {
  roleTitle: string;
  categoryMeta: string;
  fitSummary: string;
  compatibility: number;
  synergyLabel: string;
  matchedHighlights: string[];
}

const TALENT_NODES: HeroSkillNode[] = [
  { id: 'fin', label: 'Financial Modeling', tag: 'REQUIRED · CORE', weight: 32, pathD: 'M 10 48 C 90 48, 110 130, 195 130' },
  { id: 'budget', label: 'Budgeting & Forecasting', tag: 'REQUIRED · CORE', weight: 34, pathD: 'M 10 130 C 85 130, 115 130, 195 130' },
  { id: 'hybrid', label: 'Hybrid · Full-Time', tag: 'PREFERENCE FIT', weight: 22.5, pathD: 'M 10 212 C 90 212, 110 130, 195 130' },
];

const TEAM_NODES: HeroSkillNode[] = [
  { id: 'req', label: 'Required Skills Match', tag: 'CORE WEIGHT', weight: 36, pathD: 'M 10 48 C 90 48, 110 130, 195 130' },
  { id: 'exp', label: 'Experience Level Fit', tag: 'SENIORITY + YRS', weight: 30, pathD: 'M 10 130 C 85 130, 115 130, 195 130' },
  { id: 'pref', label: 'Nice-to-Have & Mode', tag: 'BONUS SIGNAL', weight: 26.0, pathD: 'M 10 212 C 90 212, 110 130, 195 130' },
];

const SANDBOX_SKILLS: SandboxSkill[] = [
  { id: 'typescript', label: 'TypeScript', category: 'Technology', weight: 24 },
  { id: 'financial-modeling', label: 'Financial Modeling', category: 'Finance', weight: 25 },
  { id: 'project-mgmt', label: 'Project Management', category: 'Business', weight: 22 },
  { id: 'patient-care', label: 'Patient Care', category: 'Healthcare', weight: 24 },
  { id: 'curriculum', label: 'Curriculum Design', category: 'Education', weight: 21 },
  { id: 'sql', label: 'SQL & Reporting', category: 'Analytics', weight: 23 },
];

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ThemeToggleComponent, WalkthroughShowcaseComponent],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private observer: IntersectionObserver | null = null;
  private revealFallbackTimer: ReturnType<typeof setTimeout> | null = null;

  @ViewChildren('revealEl') revealElements!: QueryList<ElementRef<HTMLElement>>;

  // Header & Navigation
  readonly mobileMenuOpen = signal(false);
  readonly magneticTransform = signal('translate3d(0, 0, 0)');

  // Hero Audience Switcher
  readonly audience = signal<AudienceMode>('talent');
  readonly activeHeroNodes = signal<Set<string>>(new Set(['fin', 'budget', 'hybrid']));
  readonly previewActionDone = signal(false);

  readonly heroNodes = computed<HeroSkillNode[]>(() =>
    this.audience() === 'talent' ? TALENT_NODES : TEAM_NODES
  );

  readonly heroCompatibility = computed<string>(() => {
    const nodes = this.heroNodes();
    const active = this.activeHeroNodes();
    const score = nodes
      .filter((n) => active.has(n.id))
      .reduce((sum, n) => sum + n.weight, 0);
    const clamped = Math.min(96, Math.max(0, score));
    return clamped.toFixed(1);
  });

  // Live Skill Sandbox
  readonly sandboxSkills = SANDBOX_SKILLS;
  readonly selectedSandboxIds = signal<Set<string>>(
    new Set(['typescript', 'project-mgmt', 'sql'])
  );

  readonly sandboxMatch = computed<SandboxResult>(() => {
    const selected = this.sandboxSkills.filter((s) => this.selectedSandboxIds().has(s.id));
    if (selected.length === 0) {
      return {
        roleTitle: 'Select skills above to preview matching',
        categoryMeta: 'SkillBridge Multi-Industry Catalog',
        fitSummary: '—',
        compatibility: 0,
        synergyLabel: 'Toggle 1–4 skills to see how job matching scores your profile',
        matchedHighlights: [],
      };
    }

    const hasFin = selected.some((s) => s.id === 'financial-modeling');
    const hasCare = selected.some((s) => s.id === 'patient-care');
    const hasEdu = selected.some((s) => s.id === 'curriculum');
    const hasTs = selected.some((s) => s.id === 'typescript');
    const hasSql = selected.some((s) => s.id === 'sql');

    let roleTitle = 'Operations & Project Coordinator';
    let categoryMeta = 'Business & Administration · Full-Time · Hybrid';
    if (hasCare) {
      roleTitle = 'Clinical Care Coordinator';
      categoryMeta = 'Healthcare & Medical · Full-Time · Onsite';
    } else if (hasFin && hasSql) {
      roleTitle = 'Senior Financial Analyst';
      categoryMeta = 'Finance & Accounting · Full-Time · Hybrid';
    } else if (hasFin) {
      roleTitle = 'Financial & Budget Specialist';
      categoryMeta = 'Finance & Accounting · Full-Time · Hybrid';
    } else if (hasEdu) {
      roleTitle = 'Instructional & Curriculum Lead';
      categoryMeta = 'Education & Training · Full-Time · Hybrid';
    } else if (hasTs && hasSql) {
      roleTitle = 'Full-Stack Software Engineer';
      categoryMeta = 'Technology · Full-Time · Remote / Hybrid';
    } else if (hasTs) {
      roleTitle = 'Frontend Web Developer';
      categoryMeta = 'Technology · Full-Time · Remote';
    }

    const rawScore = Math.min(96, 38 + selected.reduce((acc, s) => acc + s.weight * 0.72, 0));

    return {
      roleTitle,
      categoryMeta,
      fitSummary: `${selected.length} of ${selected.length} Core Matched · 0 Missing`,
      compatibility: Number(rawScore.toFixed(1)),
      synergyLabel: 'Scored by required skills, experience level, work arrangement & job type',
      matchedHighlights: selected.map((s) => s.label),
    };
  });

  // Bento Employer Review Snippet state
  readonly applicantStatus = signal<'Received' | 'Shortlisted'>('Received');

  // Supported Industries Marquee
  readonly marqueeIndustries = [
    'TECHNOLOGY',
    'HEALTHCARE & MEDICAL',
    'FINANCE & ACCOUNTING',
    'EDUCATION & TRAINING',
    'ENGINEERING',
    'BUSINESS & ADMIN',
    'SALES & MARKETING',
    'SKILLED TRADES',
  ];

  // Closing Quick-Start Input
  readonly quickStartInput = signal('');
  readonly droppedFileName = signal<string | null>(null);
  readonly isDragOver = signal(false);

  ngAfterViewInit(): void {
    if (typeof IntersectionObserver === 'undefined') {
      this.revealAllSections();
      return;
    }

    this.observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            this.observer?.unobserve(entry.target);
          }
        }
      },
      { threshold: 0.01, rootMargin: '120px 0px 120px 0px' }
    );

    this.revealElements?.forEach((el) => this.observer?.observe(el.nativeElement));

    // Ensure sections never stay invisible on tall viewports or full-page captures
    this.revealFallbackTimer = setTimeout(() => this.revealAllSections(), 450);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    if (this.revealFallbackTimer) {
      clearTimeout(this.revealFallbackTimer);
    }
  }

  private revealAllSections(): void {
    this.revealElements?.forEach((el) => el.nativeElement.classList.add('is-visible'));
  }

  setAudience(mode: AudienceMode): void {
    if (this.audience() === mode) return;
    this.audience.set(mode);
    this.previewActionDone.set(false);
    const defaultIds = (mode === 'talent' ? TALENT_NODES : TEAM_NODES).map((n) => n.id);
    this.activeHeroNodes.set(new Set(defaultIds));
  }

  toggleHeroNode(id: string): void {
    this.previewActionDone.set(false);
    this.activeHeroNodes.update((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        if (next.size > 1) {
          next.delete(id);
        }
      } else {
        next.add(id);
      }
      return next;
    });
  }

  isHeroNodeActive(id: string): boolean {
    return this.activeHeroNodes().has(id);
  }

  triggerPreviewAction(): void {
    this.previewActionDone.update((v) => !v);
  }

  toggleSandboxSkill(id: string): void {
    this.selectedSandboxIds.update((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  isSandboxSelected(id: string): boolean {
    return this.selectedSandboxIds().has(id);
  }

  setApplicantStatus(status: 'Received' | 'Shortlisted'): void {
    this.applicantStatus.set(status);
  }

  onCardPointerMove(event: MouseEvent): void {
    const target = event.currentTarget as HTMLElement | null;
    if (!target) return;
    const rect = target.getBoundingClientRect();
    const x = event.clientX - rect.left;
    const y = event.clientY - rect.top;
    target.style.setProperty('--mouse-x', `${x}px`);
    target.style.setProperty('--mouse-y', `${y}px`);
  }

  onMagneticMove(event: MouseEvent): void {
    const target = event.currentTarget as HTMLElement | null;
    if (!target) return;
    const rect = target.getBoundingClientRect();
    const offsetX = (event.clientX - (rect.left + rect.width / 2)) * 0.22;
    const offsetY = (event.clientY - (rect.top + rect.height / 2)) * 0.22;
    this.magneticTransform.set(`translate3d(${offsetX.toFixed(1)}px, ${offsetY.toFixed(1)}px, 0)`);
  }

  onMagneticLeave(): void {
    this.magneticTransform.set('translate3d(0, 0, 0)');
  }

  scrollToSection(sectionId: string, event: Event): void {
    event.preventDefault();
    this.mobileMenuOpen.set(false);
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((v) => !v);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.droppedFileName.set(file.name);
      if (!this.quickStartInput()) {
        this.quickStartInput.set(file.name);
      }
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  onDragLeave(): void {
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.droppedFileName.set(file.name);
      this.quickStartInput.set(file.name);
    }
  }

  launchQuickStart(): void {
    this.router.navigate(['/register']);
  }
}
