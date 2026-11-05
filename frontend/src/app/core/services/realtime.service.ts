import { effect, inject, Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApplicationStatusChangedRealtimeEvent,
  ApplicationSubmittedRealtimeEvent,
  ApplicationWithdrawnRealtimeEvent,
  CandidateProfileUpdatedRealtimeEvent,
  JobCreatedRealtimeEvent,
  RealtimeNotification,
} from '../models/realtime.models';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root',
})
export class RealtimeService {
  private readonly authService = inject(AuthService);
  private hubConnection: signalR.HubConnection | null = null;

  // Subjects for typed observables
  private readonly applicationSubmittedSubject = new Subject<ApplicationSubmittedRealtimeEvent>();
  private readonly applicationWithdrawnSubject = new Subject<ApplicationWithdrawnRealtimeEvent>();
  private readonly applicationStatusChangedSubject = new Subject<ApplicationStatusChangedRealtimeEvent>();
  private readonly jobCreatedSubject = new Subject<JobCreatedRealtimeEvent>();
  private readonly candidateProfileUpdatedSubject = new Subject<CandidateProfileUpdatedRealtimeEvent>();
  private readonly notificationSubject = new Subject<RealtimeNotification>();

  // Public streams
  readonly applicationSubmitted$: Observable<ApplicationSubmittedRealtimeEvent> =
    this.applicationSubmittedSubject.asObservable();
  readonly applicationWithdrawn$: Observable<ApplicationWithdrawnRealtimeEvent> =
    this.applicationWithdrawnSubject.asObservable();
  readonly applicationStatusChanged$: Observable<ApplicationStatusChangedRealtimeEvent> =
    this.applicationStatusChangedSubject.asObservable();
  readonly jobCreated$: Observable<JobCreatedRealtimeEvent> =
    this.jobCreatedSubject.asObservable();
  readonly candidateProfileUpdated$: Observable<CandidateProfileUpdatedRealtimeEvent> =
    this.candidateProfileUpdatedSubject.asObservable();
  readonly notification$: Observable<RealtimeNotification> =
    this.notificationSubject.asObservable();

  // Active notifications signal for UI toast overlay
  readonly activeNotifications = signal<RealtimeNotification[]>([]);
  readonly connectionState = signal<'disconnected' | 'connecting' | 'connected'>('disconnected');

  constructor() {
    // Automatically manage connection lifecycle based on authentication token
    effect(() => {
      let token: string | null = null;
      try {
        if (typeof this.authService?.token === 'function') {
          token = this.authService.token();
        } else if (typeof (this.authService as any)?.getStoredToken === 'function') {
          token = (this.authService as any).getStoredToken();
        }
      } catch {
        token = null;
      }

      if (token) {
        this.startConnection();
      } else {
        this.stopConnection();
      }
    });

    // Auto-dismiss notifications after 6 seconds
    this.notification$.subscribe((notification) => {
      this.activeNotifications.update((list) => [notification, ...list]);
      setTimeout(() => {
        this.dismissNotification(notification.id);
      }, 6000);
    });
  }

  async startConnection(): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (this.hubConnection) {
      await this.stopConnection();
    }

    this.connectionState.set('connecting');

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, {
        accessTokenFactory: () => {
          if (typeof this.authService?.token === 'function') {
            return this.authService.token() || '';
          }
          if (typeof (this.authService as any)?.getStoredToken === 'function') {
            return (this.authService as any).getStoredToken() || '';
          }
          return '';
        },
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.None)
      .build();

    this.registerHandlers();

    try {
      await this.hubConnection.start();
      this.connectionState.set('connected');
    } catch {
      this.connectionState.set('disconnected');
    }

    this.hubConnection.onreconnected(() => {
      this.connectionState.set('connected');
    });

    this.hubConnection.onreconnecting(() => {
      this.connectionState.set('connecting');
    });

    this.hubConnection.onclose(() => {
      this.connectionState.set('disconnected');
    });
  }

  async stopConnection(): Promise<void> {
    if (this.hubConnection) {
      try {
        await this.hubConnection.stop();
      } catch (err) {
        console.warn('Error while stopping realtime connection:', err);
      }
      this.hubConnection = null;
      this.connectionState.set('disconnected');
    }
  }

  async joinJobRoom(jobId: number): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.hubConnection.invoke('JoinJobRoom', jobId);
      } catch (err) {
        console.warn(`Failed to join job room ${jobId}:`, err);
      }
    }
  }

  async leaveJobRoom(jobId: number): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.hubConnection.invoke('LeaveJobRoom', jobId);
      } catch (err) {
        console.warn(`Failed to leave job room ${jobId}:`, err);
      }
    }
  }

  dismissNotification(id: string): void {
    this.activeNotifications.update((list) => list.filter((n) => n.id !== id));
  }

  private registerHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('ApplicationSubmitted', (payload: ApplicationSubmittedRealtimeEvent) => {
      this.applicationSubmittedSubject.next(payload);
    });

    this.hubConnection.on('ApplicationWithdrawn', (payload: ApplicationWithdrawnRealtimeEvent) => {
      this.applicationWithdrawnSubject.next(payload);
    });

    this.hubConnection.on('ApplicationStatusChanged', (payload: ApplicationStatusChangedRealtimeEvent) => {
      this.applicationStatusChangedSubject.next(payload);
    });

    this.hubConnection.on('JobCreated', (payload: JobCreatedRealtimeEvent) => {
      this.jobCreatedSubject.next(payload);
    });

    this.hubConnection.on('CandidateProfileUpdated', (payload: CandidateProfileUpdatedRealtimeEvent) => {
      this.candidateProfileUpdatedSubject.next(payload);
    });

    this.hubConnection.on('NotificationReceived', (payload: RealtimeNotification) => {
      this.notificationSubject.next(payload);
    });
  }
}
