import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { WalkthroughShowcaseComponent } from './walkthrough-showcase.component';

describe('WalkthroughShowcaseComponent', () => {
  let fixture: ComponentFixture<WalkthroughShowcaseComponent>;
  let component: WalkthroughShowcaseComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WalkthroughShowcaseComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(WalkthroughShowcaseComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders an edge-to-edge autoplay muted loop video element without native controls', () => {
    const video = fixture.nativeElement.querySelector('video.walkthrough-video') as HTMLVideoElement;
    expect(video).toBeTruthy();
    expect(video.hasAttribute('controls')).toBe(false);
    expect(video.hasAttribute('autoplay')).toBe(true);
    expect(video.hasAttribute('loop')).toBe(true);
    expect(video.hasAttribute('muted')).toBe(true);
    expect(video.hasAttribute('playsinline')).toBe(true);
    expect(video.hasAttribute('disablePictureInPicture')).toBe(true);
  });

  it('renders the live motion canvas without any interactive UI chrome or playback buttons', () => {
    const buttons = fixture.nativeElement.querySelectorAll('button');
    expect(buttons.length).toBe(0);
    expect(component.skills.length).toBe(4);
    expect(component.showHeroIntro()).toBe(true);
  });
});

