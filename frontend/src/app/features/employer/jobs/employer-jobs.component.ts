import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmployerJobDto } from '../../../core/models/job.models';
import { JobsService } from '../../../core/services/jobs.service';
import { extractErrorMessage } from '../../../core/utils/error-formatter';

@Component({
  selector: 'app-employer-jobs',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  templateUrl: './employer-jobs.component.html',
  styleUrl: './employer-jobs.component.scss',
})
export class EmployerJobsComponent implements OnInit {
  private readonly jobsService = inject(JobsService);

  readonly jobs = signal<EmployerJobDto[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadJobs();
  }

  loadJobs(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.jobsService.getEmployerJobs().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.isLoading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(
          extractErrorMessage(error, 'Unable to load your jobs. Please try again.')
        );
        this.isLoading.set(false);
      },
    });
  }
}
