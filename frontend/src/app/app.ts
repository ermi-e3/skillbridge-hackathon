import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { RealtimeService } from './core/services/realtime.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  readonly realtime = inject(RealtimeService);
}
