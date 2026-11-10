import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmployerJobDto } from '../../../core/models/job.models';
import { JobsService } from '../../../core/services/jobs.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';
import { UserProfileMenuComponent } from '../../../shared/components/user-profile-menu/user-profile-menu.component';

@Component({
  selector: 'app-employer-applications',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe, UserProfileMenuComponent],
  templateUrl: './employer-applications.component.html',
  styleUrl: './employer-applications.component.scss',
})
export class EmployerApplicationsComponent implements OnInit {
  private readonly jobsService = inject(JobsService);

  readonly jobs = signal<EmployerJobDto[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly errorMessage = signal<string | null>(null);
  readonly searchQuery = signal<string>('');

  readonly totalApplications = computed(() =>
    this.jobs().reduce((sum, job) => sum + (job.applicantCounts?.total ?? 0), 0)
  );
  readonly totalReceived = computed(() =>
    this.jobs().reduce((sum, job) => sum + (job.applicantCounts?.received ?? 0), 0)
  );
  readonly totalShortlisted = computed(() =>
    this.jobs().reduce((sum, job) => sum + (job.applicantCounts?.shortlisted ?? 0), 0)
  );

  readonly filteredJobs = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    return [...this.jobs()]
      .filter((j) => !query || j.title.toLowerCase().includes(query))
      .sort((a, b) => (b.applicantCounts?.total ?? 0) - (a.applicantCounts?.total ?? 0));
  });

  ngOnInit(): void {
    this.loadEmployerJobs();
  }

  loadEmployerJobs(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.jobsService.getEmployerJobs().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs ?? []);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(
          extractErrorMessage(err, 'Unable to load job application summaries. Please try again.')
        );
      },
    });
  }

  onSearchInput(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }
}

