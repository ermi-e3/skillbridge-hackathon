import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApplicationDto } from '../../../core/models/application.models';
import { ApplicationsService } from '../../../core/services/applications.service';
import { AuthService } from '../../../core/services/auth.service';
import { CandidateApplicationsComponent } from './candidate-applications.component';

describe('CandidateApplicationsComponent', () => {
  let component: CandidateApplicationsComponent;
  let fixture: ComponentFixture<CandidateApplicationsComponent>;

  let applicationsServiceSpy: {
    getMyApplications: ReturnType<typeof vi.fn>;
    withdraw: ReturnType<typeof vi.fn>;
  };
  let authServiceSpy: {
    logout: ReturnType<typeof vi.fn>;
  };

  const mockApplications: ApplicationDto[] = [
    {
      id: 1,
      jobId: 101,
      jobTitle: 'Junior .NET Developer',
      companyName: 'Abyssinia Tech',
      status: 'Received',
      appliedAt: '2026-10-09T09:00:00Z',
      matchPercent: 75,
    },
    {
      id: 2,
      jobId: 102,
      jobTitle: 'Frontend Angular Specialist',
      companyName: 'Ethio Soft',
      status: 'Shortlisted',
      appliedAt: '2026-10-10T14:30:00Z', // newer date
      matchPercent: 88,
    },
    {
      id: 3,
      jobId: 103,
      jobTitle: 'Data Engineer',
      companyName: 'Fintech Addis',
      status: 'Rejected',
      appliedAt: '2026-10-05T11:00:00Z', // oldest date
      matchPercent: 40,
    },
    {
      id: 4,
      jobId: 104,
      jobTitle: 'QA Automation Intern',
      companyName: 'Alpha Tech',
      status: 'Withdrawn',
      appliedAt: '2026-10-07T08:00:00Z',
      matchPercent: 60,
    },
  ];

  beforeEach(async () => {
    applicationsServiceSpy = {
      getMyApplications: vi.fn().mockReturnValue(of(mockApplications)),
      withdraw: vi.fn().mockReturnValue(of({ ...mockApplications[0], status: 'Withdrawn' })),
    };
    authServiceSpy = {
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [CandidateApplicationsComponent],
      providers: [
        provideRouter([]),
        { provide: ApplicationsService, useValue: applicationsServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CandidateApplicationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and fetch applications on init', () => {
    expect(component).toBeTruthy();
    expect(applicationsServiceSpy.getMyApplications).toHaveBeenCalled();
    expect(component.applications()).toEqual(mockApplications);
    expect(component.isLoading()).toBe(false);
  });

  it('should sort applications newest first', () => {
    const sorted = component.sortedApplications();
    expect(sorted.length).toBe(4);
    // Newest is 2026-10-10 (id: 2)
    expect(sorted[0].id).toBe(2);
    // Oldest is 2026-10-05 (id: 3)
    expect(sorted[3].id).toBe(3);
  });

  it('should map status badges correctly (Received=Blue, Shortlisted=Green, Rejected=Red, Withdrawn=Gray)', () => {
    expect(component.getStatusBadgeClass('Received')).toBe('badge-blue');
    expect(component.getStatusBadgeClass('Shortlisted')).toBe('badge-green');
    expect(component.getStatusBadgeClass('Rejected')).toBe('badge-red');
    expect(component.getStatusBadgeClass('Withdrawn')).toBe('badge-gray');

    const rows = fixture.nativeElement.querySelectorAll('.app-row');
    expect(rows.length).toBe(4);

    // Row 0 is id 2 (Shortlisted)
    expect(rows[0].querySelector('.status-badge').classList).toContain('badge-green');
    // Row 1 is id 1 (Received)
    expect(rows[1].querySelector('.status-badge').classList).toContain('badge-blue');
  });

  it('should display Withdraw button ONLY when status === "Received"', () => {
    const rows = fixture.nativeElement.querySelectorAll('.app-row');

    // Row 0: id 2 (Shortlisted) -> No withdraw button
    expect(rows[0].querySelector('.action-btn-withdraw')).toBeNull();

    // Row 1: id 1 (Received) -> Withdraw button present
    const withdrawBtn = rows[1].querySelector('.action-btn-withdraw');
    expect(withdrawBtn).toBeTruthy();
    expect(withdrawBtn.textContent).toContain('Withdraw');

    // Row 2: id 4 (Withdrawn) -> No withdraw button
    expect(rows[2].querySelector('.action-btn-withdraw')).toBeNull();

    // Row 3: id 3 (Rejected) -> No withdraw button
    expect(rows[3].querySelector('.action-btn-withdraw')).toBeNull();
  });

  it('should open confirmation modal when Withdraw is clicked and cancel if dismissed', () => {
    const receivedApp = mockApplications[0]; // id: 1
    component.promptWithdraw(receivedApp);
    fixture.detectChanges();

    expect(component.pendingWithdrawApp()).toEqual(receivedApp);

    const modal = fixture.nativeElement.querySelector('.modal-card');
    expect(modal).toBeTruthy();
    expect(modal.textContent).toContain('Withdraw Application');
    expect(modal.textContent).toContain('Junior .NET Developer');

    // Cancel withdraw
    component.cancelWithdraw();
    fixture.detectChanges();
    expect(component.pendingWithdrawApp()).toBeNull();
    expect(fixture.nativeElement.querySelector('.modal-card')).toBeNull();
  });

  it('should confirm withdrawal, call service, and update status optimistically to Withdrawn', () => {
    const receivedApp = mockApplications[0]; // id: 1
    component.promptWithdraw(receivedApp);
    fixture.detectChanges();

    component.confirmWithdraw();

    expect(applicationsServiceSpy.withdraw).toHaveBeenCalledWith(1);

    const updated = component.applications().find((a) => a.id === 1);
    expect(updated?.status).toBe('Withdrawn');

    expect(component.successToast()).toContain('withdrawn');
  });

  it('should display empty state when applications list is empty', () => {
    applicationsServiceSpy.getMyApplications.mockReturnValue(of([]));
    component.loadApplications();
    fixture.detectChanges();

    const emptyBox = fixture.nativeElement.querySelector('.empty-state-box');
    expect(emptyBox).toBeTruthy();
    expect(emptyBox.textContent).toContain("You haven't applied to any jobs yet");
  });

  it('should display dismissible red error banner on API error', () => {
    const errorResponse = new HttpErrorResponse({
      status: 400,
      error: {
        code: 'applications.cannot_withdraw',
        detail: 'Only applications still in Received can be withdrawn.',
      },
    });
    applicationsServiceSpy.withdraw.mockReturnValue(throwError(() => errorResponse));

    component.promptWithdraw(mockApplications[0]);
    component.confirmWithdraw();
    fixture.detectChanges();

    expect(component.errorMessage()).toBe('Only applications still in Received can be withdrawn.');
    const banner = fixture.nativeElement.querySelector('.alert-banner');
    expect(banner).toBeTruthy();
    expect(banner.textContent).toContain('Only applications still in Received can be withdrawn.');

    component.dismissAlert();
    fixture.detectChanges();
    expect(component.errorMessage()).toBeNull();
  });
});
