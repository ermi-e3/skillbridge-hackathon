import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { JobDto } from '../../../../core/models/job.models';
import { SkillDto } from '../../../../core/models/profile.models';
import { AuthService } from '../../../../core/services/auth.service';
import { JobsService } from '../../../../core/services/jobs.service';
import { SkillsService } from '../../../../core/services/skills.service';
import { JobListComponent } from './job-list.component';

describe('JobListComponent', () => {
  let component: JobListComponent;
  let fixture: ComponentFixture<JobListComponent>;

  let jobsServiceSpy: {
    getJobs: ReturnType<typeof vi.fn>;
  };
  let skillsServiceSpy: {
    getSkills: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: {
    logout: ReturnType<typeof vi.fn>;
  };

  const mockSkills: SkillDto[] = [
    { id: 1, name: 'ASP.NET Core' },
    { id: 2, name: 'Angular' },
    { id: 3, name: 'C#' },
  ];

  const mockJobs: JobDto[] = [
    {
      id: 101,
      title: 'Senior .NET Engineer',
      description: 'Lead backend microservices architecture.',
      companyName: 'Abyssinia Tech',
      location: 'Addis Ababa',
      createdAt: '2026-10-10T08:55:00Z',
      requiredSkills: [
        { id: 1, name: 'ASP.NET Core' },
        { id: 3, name: 'C#' },
      ],
      myMatchPercent: 85, // Green >= 75%
      hasApplied: false,
    },
    {
      id: 102,
      title: 'Full Stack Developer',
      description: 'Build web applications using Angular and .NET.',
      companyName: 'Ethio Soft',
      location: 'Remote',
      createdAt: '2026-10-09T10:00:00Z',
      requiredSkills: [
        { id: 1, name: 'ASP.NET Core' },
        { id: 2, name: 'Angular' },
      ],
      myMatchPercent: 60, // Yellow 50-74%
      hasApplied: true, // Applied badge test
    },
    {
      id: 103,
      title: 'Data Analyst',
      description: 'Analyze data metrics and pipelines.',
      companyName: 'Fintech Addis',
      location: 'Bole, Addis Ababa',
      createdAt: '2026-10-08T12:00:00Z',
      requiredSkills: [{ id: 3, name: 'C#' }],
      myMatchPercent: 33, // Red < 50%
      hasApplied: false,
    },
  ];

  beforeEach(async () => {
    jobsServiceSpy = {
      getJobs: vi.fn().mockReturnValue(of(mockJobs)),
    };
    skillsServiceSpy = {
      getSkills: vi.fn().mockReturnValue(of(mockSkills)),
    };
    authServiceSpy = {
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [JobListComponent],
      providers: [
        provideRouter([]),
        { provide: JobsService, useValue: jobsServiceSpy },
        { provide: SkillsService, useValue: skillsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(JobListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component and fetch skills and jobs on init', () => {
    expect(component).toBeTruthy();
    expect(skillsServiceSpy.getSkills).toHaveBeenCalled();
    expect(jobsServiceSpy.getJobs).toHaveBeenCalledWith([]);
    expect(component.allSkills()).toEqual(mockSkills);
    expect(component.jobs()).toEqual(mockJobs);
    expect(component.isLoadingJobs()).toBe(false);
  });

  it('should render skill filter chips and toggle selection', () => {
    const chipButtons = fixture.nativeElement.querySelectorAll('.filter-chip');
    expect(chipButtons.length).toBe(3);

    // Click first chip (ASP.NET Core, id: 1)
    chipButtons[0].click();
    fixture.detectChanges();

    expect(component.selectedSkillIds()).toEqual([1]);
    expect(jobsServiceSpy.getJobs).toHaveBeenCalledWith([1]);

    // Click again to unselect
    chipButtons[0].click();
    fixture.detectChanges();

    expect(component.selectedSkillIds()).toEqual([]);
    expect(jobsServiceSpy.getJobs).toHaveBeenCalledWith([]);
  });

  it('should clear skill filters when clear button is clicked', () => {
    component.toggleSkillFilter(1);
    component.toggleSkillFilter(2);
    expect(component.selectedSkillIds()).toEqual([1, 2]);

    component.clearSkillFilters();
    expect(component.selectedSkillIds()).toEqual([]);
    expect(jobsServiceSpy.getJobs).toHaveBeenCalledWith([]);
  });

  it('should display color-coded match % badges (Green >= 75%, Yellow 50-74%, Red < 50%)', () => {
    const cards = fixture.nativeElement.querySelectorAll('.job-card');
    expect(cards.length).toBe(3);

    // Job 1: 85% -> match-high (Green)
    const matchBadge1 = cards[0].querySelector('.match-badge');
    expect(matchBadge1.classList).toContain('match-high');
    expect(matchBadge1.textContent).toContain('85%');

    // Job 2: 60% -> match-mid (Yellow)
    const matchBadge2 = cards[1].querySelector('.match-badge');
    expect(matchBadge2.classList).toContain('match-mid');
    expect(matchBadge2.textContent).toContain('60%');

    // Job 3: 33% -> match-low (Red)
    const matchBadge3 = cards[2].querySelector('.match-badge');
    expect(matchBadge3.classList).toContain('match-low');
    expect(matchBadge3.textContent).toContain('33%');
  });

  it('should display applied badge and distinct styling for jobs already applied to', () => {
    const cards = fixture.nativeElement.querySelectorAll('.job-card');

    // Job 1: hasApplied = false
    expect(cards[0].querySelector('.applied-badge')).toBeNull();
    expect(cards[0].classList).not.toContain('has-applied-card');

    // Job 2: hasApplied = true
    const appliedBadge = cards[1].querySelector('.applied-badge');
    expect(appliedBadge).toBeTruthy();
    expect(appliedBadge.textContent).toContain('Applied');
    expect(cards[1].classList).toContain('has-applied-card');
  });

  it('should render View Job link with correct routerLink', () => {
    const cards = fixture.nativeElement.querySelectorAll('.job-card');
    const viewBtn = cards[0].querySelector('.view-job-btn');

    expect(viewBtn).toBeTruthy();
    expect(viewBtn.getAttribute('href')).toBe('/jobs/101');
  });

  it('should display empty state placeholder when no jobs match', () => {
    jobsServiceSpy.getJobs.mockReturnValue(of([]));
    component.loadJobs();
    fixture.detectChanges();

    const emptyCard = fixture.nativeElement.querySelector('.empty-state-card');
    expect(emptyCard).toBeTruthy();
    expect(emptyCard.textContent).toContain('No jobs match your filter criteria');
  });

  it('should display dismissible error alert banner when loading jobs fails', () => {
    jobsServiceSpy.getJobs.mockReturnValue(throwError(() => new Error('Server connection error')));
    component.loadJobs();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('Failed to load jobs. Please try again.');
    const banner = fixture.nativeElement.querySelector('.alert-banner');
    expect(banner).toBeTruthy();

    component.dismissAlert();
    fixture.detectChanges();
    expect(component.errorMessage()).toBeNull();
  });
});
