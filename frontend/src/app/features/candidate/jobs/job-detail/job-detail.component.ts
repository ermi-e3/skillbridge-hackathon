import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { JobDto } from '../../../../core/models/job.models';
import { CandidateProfileDto } from '../../../../core/models/profile.models';
import { ApplicationsService } from '../../../../core/services/applications.service';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
import { ProfileService } from '../../../../core/services/profile.service';
import { RealtimeService } from '../../../../core/services/realtime.service';
import { calculateCountdown, CountdownDetails } from '../../../../core/utils/countdown';
import { extractErrorMessage } from '../../../../core/utils/error-formatter';
import { UserProfileMenuComponent } from '../../../../shared/components/user-profile-menu/user-profile-menu.component';

@Component({
  selector: 'app-job-detail',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, DatePipe, UserProfileMenuComponent],
  templateUrl: './job-detail.component.html',
  styleUrl: './job-detail.component.scss',
})
export class JobDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly jobsService = inject(JobsService);
  private readonly applicationsService = inject(ApplicationsService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly profileService = inject(ProfileService);
  readonly authService = inject(AuthService);

  // Component Signals
  readonly isLoading = signal<boolean>(true);
  readonly isSubmitting = signal<boolean>(false);
  readonly isWithdrawing = signal<boolean>(false);
  readonly showWithdrawModal = signal<boolean>(false);
  readonly job = signal<JobDto | null>(null);
  readonly countdown = signal<CountdownDetails | null>(null);
  readonly hasAppliedState = signal<boolean>(false);
  readonly applicationStatusState = signal<string | null>(null);
  readonly applicationIdState = signal<number | null>(null);
  readonly hasCvState = signal<boolean>(false);
  readonly cvOriginalFileNameState = signal<string | null>(null);

  // Candidate Profile Default CV & Application Options
  readonly candidateProfile = signal<CandidateProfileDto | null>(null);
  readonly hasDefaultCv = signal<boolean>(false);
  readonly defaultCvOriginalFileName = signal<string | null>(null);
  readonly defaultCvFileSizeBytes = signal<number | null>(null);
  readonly cvApplyOption = signal<'default' | 'upload'>('default');

  // Specific CV file upload
  readonly selectedCvFile = signal<File | null>(null);
  readonly cvFileError = signal<string | null>(null);
  readonly isDownloadingCv = signal<boolean>(false);

  // CV validation signal: CV is strictly mandatory before application can be submitted
  readonly isCvValid = computed(() => {
    const opt = this.cvApplyOption();
    if (opt === 'default') {
      return this.hasDefaultCv();
    }
    if (opt === 'upload') {
      return this.selectedCvFile() !== null && !this.cvFileError();
    }
    return false;
  });

  // CV Preview Modal Signals
  readonly showCvPreviewModal = signal<boolean>(false);
  readonly cvPreviewLoading = signal<boolean>(false);
  readonly cvPreviewError = signal<string | null>(null);
  readonly cvPreviewBlob = signal<Blob | null>(null);
  readonly cvPreviewSafeUrl = signal<SafeResourceUrl | null>(null);
  readonly isDocxPreview = signal<boolean>(false);
  readonly previewCvFileName = signal<string | null>(null);
  private cvPreviewObjectUrl: string | null = null;
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  private countdownIntervalId: any = null;

  constructor() {
    this.destroyRef.onDestroy(() => {
      if (this.countdownIntervalId) {
        clearInterval(this.countdownIntervalId);
        this.countdownIntervalId = null;
      }
    });
  }

  // Reactive Apply Form
  readonly applyForm: FormGroup = this.fb.group({
    coverNote: ['', [Validators.maxLength(1000)]],
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const jobId = idParam ? parseInt(idParam, 10) : null;

    if (jobId) {
      this.loadCandidateProfile();
      this.loadJobDetail(jobId);
      this.setupRealtimeListeners(jobId);
      this.countdownIntervalId = setInterval(() => {
        this.updateCountdown();
      }, 1000);
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

    this.realtime.jobStatusChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.jobId === jobId) {
          this.job.update((current) =>
            current ? { ...current, isOpen: event.isOpen, deadline: event.deadline ?? current.deadline } : null
          );
          this.updateCountdown();
        }
      });

    this.realtime.jobUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.job.id === jobId) {
          this.loadJobDetail(jobId);
        }
      });
  }

  get coverNoteControl() {
    return this.applyForm.get('coverNote');
  }

  get coverNoteLength(): number {
    return this.coverNoteControl?.value?.length || 0;
  }

  loadCandidateProfile(): void {
    if (!this.authService.isAuthenticated()) return;
    this.profileService.getMyProfile().subscribe({
      next: (profile) => {
        this.candidateProfile.set(profile);
        const hasCv = !!profile.hasDefaultCv;
        this.hasDefaultCv.set(hasCv);
        this.defaultCvOriginalFileName.set(profile.defaultCvOriginalFileName || null);
        this.defaultCvFileSizeBytes.set(profile.defaultCvFileSizeBytes ?? null);
        this.cvApplyOption.set(hasCv ? 'default' : 'upload');
      },
      error: () => {
        this.hasDefaultCv.set(false);
        this.cvApplyOption.set('upload');
      },
    });
  }

  setCvApplyOption(option: 'default' | 'upload'): void {
    this.cvApplyOption.set(option);
    this.cvFileError.set(null);
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
        this.updateCountdown();

        if (data.hasApplied && data.applicationId && typeof this.applicationsService.getApplicationById === 'function') {
          this.applicationsService.getApplicationById(data.applicationId).subscribe({
            next: (app) => {
              this.hasCvState.set(!!app.hasCv);
              this.cvOriginalFileNameState.set(app.cvOriginalFileName || null);
            },
            error: () => {
              // Non-critical, ignore
            },
          });
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        const msg = extractErrorMessage(err, 'Failed to load job details. This posting may have been removed or closed.');
        this.errorMessage.set(msg);
      },
    });
  }

  updateCountdown(): void {
    const j = this.job();
    if (!j) {
      this.countdown.set(null);
      return;
    }

    const cd = calculateCountdown(j.deadline, j.isOpen ?? true);
    this.countdown.set(cd);

    if (j.deadline && cd.isExpired && j.isOpen !== false) {
      this.job.update((curr) => (curr ? { ...curr, isOpen: false } : null));
    }
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

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.validateAndSetFile(file, input);
  }

  onFileDropped(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const file = event.dataTransfer?.files?.[0];
    if (!file) return;

    this.validateAndSetFile(file);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
  }

  private validateAndSetFile(file: File, inputElement?: HTMLInputElement): void {
    this.cvFileError.set(null);
    const validExts = ['.pdf', '.docx'];
    const lowerName = file.name.toLowerCase();
    const hasValidExt = validExts.some((ext) => lowerName.endsWith(ext));

    if (!hasValidExt) {
      this.cvFileError.set('Only PDF and DOCX files are allowed (.pdf, .docx).');
      this.selectedCvFile.set(null);
      if (inputElement) inputElement.value = '';
      return;
    }

    const maxBytes = 5 * 1024 * 1024; // 5 MB
    if (file.size > maxBytes) {
      this.cvFileError.set(
        `File size exceeds 5 MB limit (${this.formatFileSize(file.size)}). Please upload a smaller file.`
      );
      this.selectedCvFile.set(null);
      if (inputElement) inputElement.value = '';
      return;
    }

    this.selectedCvFile.set(file);
  }

  removeSelectedCvFile(inputElement?: HTMLInputElement): void {
    this.selectedCvFile.set(null);
    this.cvFileError.set(null);
    if (inputElement) {
      inputElement.value = '';
    }
  }

  formatFileSize(bytes?: number | null): string {
    if (!bytes || bytes <= 0) return '0 B';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  }

  downloadMyCv(): void {
    const appId = this.applicationIdState();
    if (!appId || this.isDownloadingCv()) return;

    this.isDownloadingCv.set(true);
    this.applicationsService.downloadCv(appId).subscribe({
      next: (blob) => {
        this.isDownloadingCv.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = this.cvOriginalFileNameState() || 'cv.pdf';
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        a.remove();
      },
      error: (err) => {
        this.isDownloadingCv.set(false);
        const msg = extractErrorMessage(err, 'Failed to download CV file.');
        this.errorMessage.set(msg);
      },
    });
  }

  openMyCvPreview(): void {
    const appId = this.applicationIdState();
    if (!appId) return;

    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }

    const fileName = this.cvOriginalFileNameState() || 'my-cv.pdf';
    this.previewCvFileName.set(fileName);
    this.showCvPreviewModal.set(true);
    this.cvPreviewLoading.set(true);
    this.cvPreviewError.set(null);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);

    const isPdf = fileName.toLowerCase().endsWith('.pdf');
    this.isDocxPreview.set(!isPdf);

    this.applicationsService.downloadCv(appId).subscribe({
      next: (blob) => {
        this.cvPreviewLoading.set(false);
        const mime = isPdf
          ? 'application/pdf'
          : blob.type || 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
        const typedBlob = new Blob([blob], { type: mime });
        this.cvPreviewBlob.set(typedBlob);
        this.cvPreviewObjectUrl = window.URL.createObjectURL(typedBlob);
        this.cvPreviewSafeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.cvPreviewObjectUrl));
      },
      error: (err) => {
        this.cvPreviewLoading.set(false);
        this.cvPreviewError.set(extractErrorMessage(err, 'Failed to load CV preview.'));
      },
    });
  }

  openProfileDefaultCvPreview(): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }

    const fileName = this.defaultCvOriginalFileName() || 'profile-cv.pdf';
    this.previewCvFileName.set(fileName);
    this.showCvPreviewModal.set(true);
    this.cvPreviewLoading.set(true);
    this.cvPreviewError.set(null);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);

    const isPdf = fileName.toLowerCase().endsWith('.pdf');
    this.isDocxPreview.set(!isPdf);

    this.profileService.downloadDefaultCv().subscribe({
      next: (blob) => {
        this.cvPreviewLoading.set(false);
        const mime = isPdf
          ? 'application/pdf'
          : blob.type || 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
        const typedBlob = new Blob([blob], { type: mime });
        this.cvPreviewBlob.set(typedBlob);
        this.cvPreviewObjectUrl = window.URL.createObjectURL(typedBlob);
        this.cvPreviewSafeUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.cvPreviewObjectUrl));
      },
      error: (err) => {
        this.cvPreviewLoading.set(false);
        this.cvPreviewError.set(extractErrorMessage(err, 'Failed to load profile default CV.'));
      },
    });
  }

  closeCvPreview(): void {
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }
    this.showCvPreviewModal.set(false);
    this.cvPreviewLoading.set(false);
    this.cvPreviewError.set(null);
    this.cvPreviewBlob.set(null);
    this.cvPreviewSafeUrl.set(null);
  }

  downloadCurrentPreviewCv(): void {
    const blob = this.cvPreviewBlob();
    const fileName = this.previewCvFileName() || this.cvOriginalFileNameState() || 'cv.pdf';
    if (blob) {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      a.remove();
    } else {
      this.downloadMyCv();
    }
  }

  ngOnDestroy(): void {
    if (this.countdownIntervalId) {
      clearInterval(this.countdownIntervalId);
      this.countdownIntervalId = null;
    }
    if (this.cvPreviewObjectUrl) {
      window.URL.revokeObjectURL(this.cvPreviewObjectUrl);
      this.cvPreviewObjectUrl = null;
    }
  }

  onApplySubmit(): void {
    const currentJob = this.job();
    if (
      !currentJob ||
      currentJob.isOpen === false ||
      (currentJob.deadline && this.countdown()?.isExpired) ||
      this.hasAppliedState() ||
      this.isSubmitting() ||
      this.applyForm.invalid
    ) {
      return;
    }

    // Enforce mandatory CV rule
    if (!this.isCvValid()) {
      this.errorMessage.set('A valid CV must always be attached before an application can be submitted.');
      return;
    }

    this.dismissAlert();
    this.dismissSuccess();
    this.isSubmitting.set(true);

    const note = this.applyForm.value.coverNote?.trim() || null;
    const isUsingDefault = this.cvApplyOption() === 'default';
    const cvFile = isUsingDefault ? null : this.selectedCvFile();

    this.applicationsService.applyWithCv(currentJob.id, note, cvFile, isUsingDefault).subscribe({
      next: (application) => {
        this.isSubmitting.set(false);
        this.hasAppliedState.set(true);
        this.applicationStatusState.set(application?.status || 'Received');
        this.applicationIdState.set(application?.id || null);
        this.hasCvState.set(!!application?.hasCv);
        const resolvedFileName =
          application?.cvOriginalFileName ||
          (isUsingDefault ? this.defaultCvOriginalFileName() : cvFile?.name) ||
          null;
        this.cvOriginalFileNameState.set(resolvedFileName);
        this.selectedCvFile.set(null);
        this.successMessage.set(
          application?.hasCv
            ? 'Application submitted successfully with your attached CV! Your application status is now Received.'
            : 'Application submitted successfully! Your application status is now Received.'
        );
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
