import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RealtimeService } from './realtime.service';
import { AuthService } from './auth.service';

describe('RealtimeService', () => {
  let service: RealtimeService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        RealtimeService,
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    service = TestBed.inject(RealtimeService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should manage notifications list and dismiss by id', () => {
    service.activeNotifications.set([
      {
        id: 'test-1',
        title: 'Title 1',
        message: 'Message 1',
        type: 'info',
        timestamp: new Date().toISOString(),
      },
      {
        id: 'test-2',
        title: 'Title 2',
        message: 'Message 2',
        type: 'success',
        timestamp: new Date().toISOString(),
      },
    ]);

    expect(service.activeNotifications().length).toBe(2);
    service.dismissNotification('test-1');
    expect(service.activeNotifications().length).toBe(1);
    expect(service.activeNotifications()[0].id).toBe('test-2');
  });

  it('should expose jobUpdated$ observable', () => {
    expect(service.jobUpdated$).toBeTruthy();
  });
});

