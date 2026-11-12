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
  salaryBoost: number;
}

interface SandboxResult {
  roleTitle: string;
  teamLabel: string;
  salaryRange: string;
  compatibility: number;
  synergyLabel: string;
  matchedHighlights: string[];
}

const TALENT_NODES: HeroSkillNode[] = [
  { id: 'ts', label: 'TypeScript', tag: 'CORE · 5Y', weight: 34, pathD: 'M 10 48 C 90 48, 110 130, 195 130' },
  { id: 'arch', label: 'System Architecture', tag: 'L5 · VERIFIED', weight: 36, pathD: 'M 10 130 C 85 130, 115 130, 195 130' },
  { id: 'prod', label: 'Product Strategy', tag: 'LEAD · SIGNAL', weight: 28.4, pathD: 'M 10 212 C 90 212, 110 130, 195 130' },
];

const TEAM_NODES: HeroSkillNode[] = [
  { id: 'staff-fe', label: 'Staff Frontend', tag: 'ANGULAR / TS', weight: 35, pathD: 'M 10 48 C 90 48, 110 130, 195 130' },
  { id: 'dotnet', label: '.NET Core APIs', tag: 'CLEAN ARCH', weight: 33, pathD: 'M 10 130 C 85 130, 115 130, 195 130' },
  { id: 'scale', label: 'Distributed Systems', tag: 'HIGH SIGNAL', weight: 30.4, pathD: 'M 10 212 C 90 212, 110 130, 195 130' },
];

const SANDBOX_SKILLS: SandboxSkill[] = [
  { id: 'typescript', label: 'TypeScript', category: 'Engineering', weight: 24, salaryBoost: 14 },
  { id: 'architecture', label: 'System Architecture', category: 'Systems', weight: 26, salaryBoost: 22 },
  { id: 'product', label: 'Product Strategy', category: 'Product', weight: 22, salaryBoost: 16 },
  { id: 'cloud', label: 'Cloud & Kubernetes', category: 'Infra', weight: 24, salaryBoost: 18 },
  { id: 'ai', label: 'Applied AI / ML', category: 'Intelligence', weight: 25, salaryBoost: 24 },
  { id: 'design', label: 'Design Systems', category: 'Craft', weight: 20, salaryBoost: 12 },
];

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ThemeToggleComponent],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent implements AfterViewInit, OnDestroy {
  private readonly router = inject(Router);
  private observer: IntersectionObserver | null = null;

  @ViewChildren('revealEl') revealElements!: QueryList<ElementRef<HTMLElement>>;

  // Header & Navigation
  readonly mobileMenuOpen = signal(false);
  readonly magneticTransform = signal('translate3d(0, 0, 0)');

  // Hero Audience Switcher
  readonly audience = signal<AudienceMode>('talent');
  readonly activeHeroNodes = signal<Set<string>>(new Set(['ts', 'arch', 'prod']));
  readonly introAccepted = signal(false);

  readonly heroNodes = computed<HeroSkillNode[]>(() =>
    this.audience() === 'talent' ? TALENT_NODES : TEAM_NODES
  );

  readonly heroCompatibility = computed<string>(() => {
    const nodes = this.heroNodes();
    const active = this.activeHeroNodes();
    const score = nodes
      .filter((n) => active.has(n.id))
      .reduce((sum, n) => sum + n.weight, 0);
    const clamped = Math.min(98.4, Math.max(0, score));
    return clamped.toFixed(1);
  });

  // Live Skill Sandbox
  readonly sandboxSkills = SANDBOX_SKILLS;
  readonly selectedSandboxIds = signal<Set<string>>(
    new Set(['typescript', 'architecture', 'product'])
  );

  readonly sandboxMatch = computed<SandboxResult>(() => {
    const selected = this.sandboxSkills.filter((s) => this.selectedSandboxIds().has(s.id));
    if (selected.length === 0) {
      return {
        roleTitle: 'Select skills above to calibrate',
        teamLabel: 'SkillBridge Live Engine · Waiting for signal',
        salaryRange: '$—',
        compatibility: 0,
        synergyLabel: 'Toggle 1–4 skills to preview live market fit',
        matchedHighlights: [],
      };
    }

    const hasArch = selected.some((s) => s.id === 'architecture');
    const hasAi = selected.some((s) => s.id === 'ai');
    const hasCloud = selected.some((s) => s.id === 'cloud');
    const hasDesign = selected.some((s) => s.id === 'design');
    const hasProduct = selected.some((s) => s.id === 'product');

    let roleTitle = 'Senior Product Engineer';
    let teamLabel = 'Series B Fintech · Remote / Hybrid';
    if (hasAi && hasArch) {
      roleTitle = 'Staff AI Systems Architect';
      teamLabel = 'Applied Intelligence Lab · High Signal';
    } else if (hasArch && hasCloud) {
      roleTitle = 'Principal Platform Architect';
      teamLabel = 'Distributed Cloud Infra · Series C';
    } else if (hasDesign && !hasCloud) {
      roleTitle = 'Staff Design Systems Engineer';
      teamLabel = 'Developer Tooling · Remote-First';
    } else if (hasProduct && hasArch) {
      roleTitle = 'Founding Product Architect';
      teamLabel = 'Seed / Series A · Direct-to-Founder';
    } else if (selected.length === 1) {
      roleTitle = `${selected[0].label} Specialist`;
      teamLabel = 'Verified Growth Team · Direct Match';
    }

    const baseMin = 72 + selected.reduce((acc, s) => acc + Math.round(s.salaryBoost * 0.65), 0);
    const baseMax = baseMin + 28 + selected.length * 4;
    const rawScore = Math.min(98.6, 54 + selected.reduce((acc, s) => acc + s.weight * 0.62, 0));

    return {
      roleTitle,
      teamLabel,
      salaryRange: `$${baseMin}k – $${baseMax}k / yr`,
      compatibility: Number(rawScore.toFixed(1)),
      synergyLabel:
        selected.length >= 3
          ? `Top ${Math.max(2, 9 - selected.length)}% rare skill intersection`
          : 'Add 1–2 complementary skills to boost signal',
      matchedHighlights: selected.map((s) => s.label),
    };
  });

  // Bento Chat/Calendar Preview state
  readonly selectedSlot = signal<'thu-10' | 'fri-14'>('thu-10');
  readonly slotBooked = signal(false);

  // Marquee companies
  readonly marqueeCompanies = [
    'ABYSSINIA LABS',
    'NILE CLOUD',
    'KINETIX AI',
    'ZEMA SYSTEMS',
    'ADDIS PAY',
    'HORIZON OPS',
    'ETHIOSTACK',
    'MERIDIAN DATA',
  ];

  // Closing Interactive Dropzone
  readonly quickStartInput = signal('');
  readonly droppedFileName = signal<string | null>(null);
  readonly isDragOver = signal(false);

  ngAfterViewInit(): void {
    if (typeof IntersectionObserver === 'undefined') {
      this.revealElements?.forEach((el) => el.nativeElement.classList.add('is-visible'));
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
      { threshold: 0.12, rootMargin: '0px 0px -40px 0px' }
    );

    this.revealElements?.forEach((el) => this.observer?.observe(el.nativeElement));
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  setAudience(mode: AudienceMode): void {
    if (this.audience() === mode) return;
    this.audience.set(mode);
    this.introAccepted.set(false);
    const defaultIds = (mode === 'talent' ? TALENT_NODES : TEAM_NODES).map((n) => n.id);
    this.activeHeroNodes.set(new Set(defaultIds));
  }

  toggleHeroNode(id: string): void {
    this.introAccepted.set(false);
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

  acceptIntro(): void {
    this.introAccepted.update((v) => !v);
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

  selectSlot(slot: 'thu-10' | 'fri-14'): void {
    this.selectedSlot.set(slot);
    this.slotBooked.set(true);
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
