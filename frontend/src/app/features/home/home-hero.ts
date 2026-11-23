import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';

@Component({
  selector: 'app-home-hero',
  imports: [RouterLink],
  templateUrl: './home-hero.html',
  styleUrl: './home-hero.scss',
})
export class HomeHero {
  protected readonly auth = inject(AuthStore);
  protected readonly depth = signal(true);
  protected readonly motion = signal(true);
  protected readonly tiltX = signal(0);
  protected readonly tiltY = signal(0);

  protected tilt(event: PointerEvent, stage: HTMLElement): void {
    if (!this.depth() || !this.motion() || event.pointerType !== 'mouse') return;

    const bounds = stage.getBoundingClientRect();
    this.tiltX.set(12 * (0.5 - (event.clientY - bounds.top) / bounds.height));
    this.tiltY.set(14 * ((event.clientX - bounds.left) / bounds.width - 0.5));
  }

  protected resetTilt(): void {
    this.tiltX.set(0);
    this.tiltY.set(0);
  }
}
