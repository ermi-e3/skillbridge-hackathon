import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  signal,
  ViewChild,
} from '@angular/core';

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  r: number;
  alpha: number;
}

@Component({
  selector: 'app-walkthrough-showcase',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './walkthrough-showcase.component.html',
  styleUrl: './walkthrough-showcase.component.scss',
})
export class WalkthroughShowcaseComponent implements AfterViewInit, OnDestroy {
  @ViewChild('particleCanvas') canvasRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('stageZone') stageZoneRef?: ElementRef<HTMLElement>;
  @ViewChild('matchedCardEl') matchedCardRef?: ElementRef<HTMLElement>;

  readonly scene = signal<0 | 1 | 2 | 3 | 4>(0);
  readonly showHeroIntro = signal<boolean>(true);
  readonly heroIntroVisible = signal<boolean>(false);
  readonly outroFading = signal<boolean>(false);
  readonly beamPaths = signal<string[]>([
    'M 270 140 C 420 140, 445 280, 540 280',
    'M 270 232 C 410 232, 455 280, 540 280',
    'M 270 326 C 410 326, 455 280, 540 280',
    'M 270 420 C 420 420, 445 280, 540 280',
  ]);
  readonly svgViewBox = signal<string>('0 0 1280 560');
  readonly dropzoneState = signal<'idle' | 'visible' | 'absorbing' | 'dissolved'>('idle');
  readonly fileState = signal<'hidden' | 'glide-in' | 'dropped'>('hidden');
  readonly visiblePillsCount = signal<number>(0);
  readonly pillsBeamReady = signal<boolean>(false);
  readonly pillsFaded = signal<boolean>(false);
  readonly beamsShot = signal<boolean>(false);
  readonly beamsDimmed = signal<boolean>(false);
  readonly blurCardsState = signal<'hidden' | 'show' | 'fade-out'>('hidden');
  readonly matchedCardState = signal<'hidden' | 'focus' | 'zoom-in'>('hidden');
  readonly matchPercent = signal<string>('0.0%');
  readonly ringOffset = signal<number>(201.06);
  readonly checkedCount = signal<number>(0);
  readonly cursorState = signal<'hidden' | 'glide' | 'click' | 'exit'>('hidden');
  readonly introConfirmed = signal<boolean>(false);
  readonly rippleActive = signal<boolean>(false);
  readonly stackState = signal<'hidden' | 'stack-in' | 'logo-show'>('hidden');
  readonly videoAvailable = signal<boolean>(false);

  readonly skills = ['TypeScript', 'Financial Modeling', 'System Design', 'Product Strategy'];

  private readonly DURATION_MS = 22000;
  private readonly INTRO_END_SEC = 2.0;
  private readonly RING_CIRCUMFERENCE = 201.06;
  private readonly TARGET_MATCH = 98.4;

  private rafId: number | null = null;
  private lastTs: number | null = null;
  private elapsedMs = 0;
  private particles: Particle[] = [];
  private particlesSpawned = false;
  private ctx: CanvasRenderingContext2D | null = null;
  private resizeHandler = () => this.resizeCanvas();

  ngAfterViewInit(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (canvas && typeof window !== 'undefined') {
      try {
        this.ctx = canvas.getContext('2d');
      } catch {
        this.ctx = null;
      }
      this.resizeCanvas();
      window.addEventListener('resize', this.resizeHandler);
    }

    if (typeof requestAnimationFrame !== 'undefined') {
      this.rafId = requestAnimationFrame((ts) => this.tick(ts));
    }
  }

  ngOnDestroy(): void {
    if (this.rafId !== null && typeof cancelAnimationFrame !== 'undefined') {
      cancelAnimationFrame(this.rafId);
    }
    if (typeof window !== 'undefined') {
      window.removeEventListener('resize', this.resizeHandler);
    }
  }

  onVideoCanPlay(): void {
    this.videoAvailable.set(true);
  }

  onVideoError(): void {
    this.videoAvailable.set(false);
  }

  private resizeCanvas(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas || !this.ctx) return;
    const rect = canvas.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = rect.width * dpr;
    canvas.height = rect.height * dpr;
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.syncBeamGeometry();
  }

  private syncBeamGeometry(): void {
    const stageEl = this.stageZoneRef?.nativeElement;
    const cardEl = this.matchedCardRef?.nativeElement;
    if (!stageEl || !cardEl) return;

    const stageRect = stageEl.getBoundingClientRect();
    if (!stageRect.width || !stageRect.height) return;

    this.svgViewBox.set(`0 0 ${Math.round(stageRect.width)} ${Math.round(stageRect.height)}`);

    const cardRect = cardEl.getBoundingClientRect();
    const targetX = cardRect.left - stageRect.left;
    const targetY = cardRect.top + cardRect.height / 2 - stageRect.top;

    const pillEls = Array.from(stageEl.querySelectorAll('.skill-pill')) as HTMLElement[];
    if (pillEls.length !== 4) return;

    const updated = pillEls.map((pill) => {
      const r = pill.getBoundingClientRect();
      const startX = r.right - stageRect.left;
      const startY = r.top + r.height / 2 - stageRect.top;
      const span = Math.max(40, targetX - startX);
      const cp1x = startX + span * 0.45;
      const cp2x = targetX - span * 0.35;
      return `M ${startX.toFixed(1)} ${startY.toFixed(1)} C ${cp1x.toFixed(1)} ${startY.toFixed(1)}, ${cp2x.toFixed(1)} ${targetY.toFixed(1)}, ${targetX.toFixed(1)} ${targetY.toFixed(1)}`;
    });

    this.beamPaths.set(updated);
  }

  private spawnParticles(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const cx = (rect.width || 960) / 2;
    const cy = (rect.height || 520) / 2;
    this.particles = [];
    for (let i = 0; i < 44; i++) {
      const angle = (Math.PI * 2 * i) / 44 + (Math.random() - 0.5) * 0.35;
      const speed = 1.6 + Math.random() * 4;
      this.particles.push({
        x: cx,
        y: cy,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        r: 2 + Math.random() * 3,
        alpha: 1,
      });
    }
  }

  private stepParticles(): void {
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas || !this.ctx) return;
    const rect = canvas.getBoundingClientRect();
    this.ctx.clearRect(0, 0, rect.width || canvas.width, rect.height || canvas.height);

    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.x += p.vx;
      p.y += p.vy;
      p.vx *= 0.96;
      p.vy *= 0.96;
      p.alpha -= 0.022;
      if (p.alpha <= 0) {
        this.particles.splice(i, 1);
        continue;
      }
      this.ctx.beginPath();
      this.ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
      this.ctx.fillStyle = `rgba(13, 148, 136, ${p.alpha})`;
      this.ctx.fill();
    }
  }

  private resetSequence(): void {
    this.particles = [];
    this.particlesSpawned = false;
    this.scene.set(0);
    this.showHeroIntro.set(true);
    this.heroIntroVisible.set(false);
    this.outroFading.set(false);
    this.dropzoneState.set('idle');
    this.fileState.set('hidden');
    this.visiblePillsCount.set(0);
    this.pillsBeamReady.set(false);
    this.pillsFaded.set(false);
    this.beamsShot.set(false);
    this.beamsDimmed.set(false);
    this.blurCardsState.set('hidden');
    this.matchedCardState.set('hidden');
    this.matchPercent.set('0.0%');
    this.ringOffset.set(this.RING_CIRCUMFERENCE);
    this.checkedCount.set(0);
    this.cursorState.set('hidden');
    this.introConfirmed.set(false);
    this.rippleActive.set(false);
    this.stackState.set('hidden');
  }

  private renderTimeline(ms: number): void {
    const sec = ms / 1000;

    // 0s -> 2s: Opening Hero Text Only (with smooth fade-in)
    if (sec < this.INTRO_END_SEC) {
      this.scene.set(0);
      this.showHeroIntro.set(true);
      this.heroIntroVisible.set(sec >= 0.06);
      this.outroFading.set(false);
      this.stepParticles();
      return;
    }

    this.heroIntroVisible.set(false);
    this.showHeroIntro.set(false);
    const t = sec - this.INTRO_END_SEC; // 0s -> 20s motion sequence

    if (t < 5) {
      this.scene.set(1);
      if (t >= 0.15) this.dropzoneState.set('visible');
      if (t >= 0.7 && t < 1.8) this.fileState.set('glide-in');
      if (t >= 1.8) {
        this.fileState.set('dropped');
        this.dropzoneState.set('absorbing');
        if (!this.particlesSpawned) {
          this.spawnParticles();
          this.particlesSpawned = true;
        }
      }
      if (t >= 2.5) {
        this.dropzoneState.set('dissolved');
        const count = Math.min(4, Math.floor((t - 2.5) / 0.15) + 1);
        this.visiblePillsCount.set(count);
      }
    } else if (t < 11) {
      this.scene.set(2);
      this.visiblePillsCount.set(4);
      this.pillsBeamReady.set(true);

      if (t >= 5.05 && t <= 6.6) {
        this.syncBeamGeometry();
      }
      if (t >= 5.35) {
        this.beamsShot.set(true);
        this.blurCardsState.set('show');
      }
      if (t >= 6.8) {
        this.blurCardsState.set('fade-out');
        this.matchedCardState.set('focus');
      }
      if (t >= 7.35) {
        const p = Math.min(1, (t - 7.35) / 2.2);
        const eased = 1 - Math.pow(1 - p, 3);
        const val = this.TARGET_MATCH * eased;
        this.matchPercent.set(`${val.toFixed(1)}%`);
        this.ringOffset.set(this.RING_CIRCUMFERENCE * (1 - val / 100));
      }
    } else if (t < 16) {
      this.scene.set(3);
      this.matchedCardState.set('zoom-in');
      this.pillsFaded.set(true);
      this.beamsShot.set(true);
      this.beamsDimmed.set(true);
      this.matchPercent.set('98.4%');
      this.ringOffset.set(this.RING_CIRCUMFERENCE * (1 - 0.984));

      const checks = Math.min(4, Math.max(0, Math.floor((t - 11.2) / 0.16) + 1));
      this.checkedCount.set(checks);

      if (t >= 12.1 && t < 13.45) {
        this.cursorState.set('glide');
      } else if (t >= 13.45 && t < 14.0) {
        this.cursorState.set('click');
        this.rippleActive.set(true);
      } else if (t >= 14.0) {
        this.cursorState.set('exit');
        this.introConfirmed.set(true);
      }
    } else {
      this.scene.set(4);
      this.stackState.set(t < 17.3 ? 'stack-in' : 'logo-show');
      this.outroFading.set(t >= 19.15);
    }

    this.stepParticles();
  }

  private tick(ts: number): void {
    if (this.lastTs === null) this.lastTs = ts;
    const delta = ts - this.lastTs;
    this.lastTs = ts;

    this.elapsedMs += delta;
    if (this.elapsedMs >= this.DURATION_MS) {
      this.elapsedMs = 0;
      this.resetSequence();
    }
    this.renderTimeline(this.elapsedMs);
    this.rafId = requestAnimationFrame((next) => this.tick(next));
  }
}

