import { CommonModule, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { JobDto } from '../../../../core/models/job.models';
import { ApplicationsService } from '../../../../core/services/applications.service';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
import { RealtimeService } from '../../../../core/services/realtime.service';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './job-detail.component.html',
  styleUrl: './job-detail.component.scss',
})
export class JobDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly jobsService = inject(JobsService);
  private readonly applicationsService = inject(ApplicationsService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly authService = inject(AuthService);

  // Component Signals
  readonly isLoading = signal<boolean>(true);
  readonly isSubmitting = signal<boolean>(false);
  readonly isWithdrawing = signal<boolean>(false);
  readonly showWithdrawModal = signal<boolean>(false);
  readonly job = signal<JobDto | null>(null);
  readonly hasAppliedState = signal<boolean>(false);
  readonly applicationStatusState = signal<string | null>(null);
  readonly applicationIdState = signal<number | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Reactive Apply Form
  readonly applyForm: FormGroup = this.fb.group({
    coverNote: ['', [Validators.maxLength(1000)]],
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const jobId = idParam ? parseInt(idParam, 10) : null;

    if (jobId) {
      this.loadJobDetail(jobId);
      this.setupRealtimeListeners(jobId);
    } else {
      this.isLoading.set(false);
      this.errorMessage.set('Invalid job identifier.');
    }
  }

  private setupRealtimeListeners(jobId: number): void {
    this.realtime.applicationStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.jobId === jobId) {
          this.applicationStatusState.set(event.newStatus);
        }
      });

    this.realtime.applicationWithdrawn$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.jobId === jobId) {
          this.applicationStatusState.set('Withdrawn');
        }
      });

    this.realtime.applicationSubmitted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        const currentId = typeof this.authService?.currentUser === 'function'
          ? this.authService.currentUser()?.userId
          : (this.authService as any)?.getUserId?.();
        if (event.jobId === jobId && (!currentId || event.candidateId === currentId)) {
          this.hasAppliedState.set(true);
          this.applicationStatusState.set('Received');
          this.applicationIdState.set(event.applicationId);
        }
      });
  }

  get coverNoteControl() {
    return this.applyForm.get('coverNote');
  }

  get coverNoteLength(): number {
    return this.coverNoteControl?.value?.length || 0;
  }

  loadJobDetail(jobId: number): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.jobsService.getJobById(jobId).subscribe({
      next: (data) => {
        this.job.set(data);
        this.hasAppliedState.set(!!data.hasApplied);
        this.applicationStatusState.set(data.applicationStatus || (data.hasApplied ? 'Received' : null));
        this.applicationIdState.set(data.applicationId || null);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = extractErrorMessage(err, 'Failed to load job details. This posting may have been removed or closed.');
        this.errorMessage.set(msg);
      },
    });
  }

  getMatchPercent(job: JobDto): number | null {
    if (typeof job.myMatchPercent === 'number') {
      return job.myMatchPercent;
    }
    if (job.match && typeof job.match.percent === 'number') {
      return job.match.percent;
    }
    return null;
  }

  getMatchClass(percent: number | null): 'match-high' | 'match-mid' | 'match-low' | 'match-none' {
    if (percent === null || percent === undefined) {
      return 'match-none';
    }
    if (percent >= 75) {
      return 'match-high';
    }
    if (percent >= 50) {
      return 'match-mid';
    }
    return 'match-low';
  }

  dismissAlert(): void {
    this.errorMessage.set(null);
  }

  dismissSuccess(): void {
    this.successMessage.set(null);
  }

  onApplySubmit(): void {
    const currentJob = this.job();
    if (!currentJob || this.hasAppliedState() || this.isSubmitting() || this.applyForm.invalid) {
      return;
    }

    this.dismissAlert();
    this.dismissSuccess();
    this.isSubmitting.set(true);

    const note = this.applyForm.value.coverNote?.trim() || null;

    this.applicationsService.apply(currentJob.id, { coverNote: note }).subscribe({
      next: (application) => {
        this.isSubmitting.set(false);
        this.hasAppliedState.set(true);
        this.applicationStatusState.set(application?.status || 'Received');
        this.applicationIdState.set(application?.id || null);
        this.successMessage.set('Application submitted successfully! Your application status is now Received.');
        this.applyForm.reset();
      },
      error: (err) => {
        this.isSubmitting.set(false);

        // ProblemDetails.detail extraction
        const errorDetail = extractErrorMessage(err, 'Failed to submit application. Please try again.');
        this.errorMessage.set(errorDetail);

        // If duplicate application error, reflect applied state in UI
        if (err.status === 400 && (err.error?.code === 'applications.duplicate' || errorDetail.toLowerCase().includes('already applied'))) {
          this.hasAppliedState.set(true);
        }
      },
    });
  }

  promptWithdraw(): void {
    if (this.applicationStatusState() === 'Withdrawn') return;
    this.showWithdrawModal.set(true);
  }

  cancelWithdraw(): void {
    this.showWithdrawModal.set(false);
  }

  confirmWithdraw(): void {
    const appId = this.applicationIdState();
    if (!appId || this.isWithdrawing()) return;

    this.isWithdrawing.set(true);
    this.dismissAlert();
    this.dismissSuccess();

    this.applicationsService.withdraw(appId).subscribe({
      next: () => {
        this.isWithdrawing.set(false);
        this.showWithdrawModal.set(false);
        this.applicationStatusState.set('Withdrawn');
        this.successMessage.set('Application withdrawn successfully. Your application status is now Withdrawn.');
      },
      error: (err) => {
        this.isWithdrawing.set(false);
        this.showWithdrawModal.set(false);
        const msg = extractErrorMessage(err, 'Failed to withdraw application.');
        this.errorMessage.set(msg);
      },
    });
  }
}
