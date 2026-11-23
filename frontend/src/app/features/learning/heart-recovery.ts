import {
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { AuthStore } from '../../core/accounts/auth-store';
import { HeartStatus, HeartsApi } from '../../core/learning/hearts-api';

@Component({
  selector: 'app-heart-recovery',
  templateUrl: './heart-recovery.html',
  styleUrl: './heart-recovery.scss',
})
export class HeartRecovery {
  readonly count = input(5);
  readonly recovered = output<void>();
  private readonly api = inject(HeartsApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  private anchor = 0;
  private initialSeconds = 0;
  private nextCheck = 0;
  protected readonly status = signal<HeartStatus | null>(null);
  protected readonly remaining = signal(0);
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly countdown = computed(() => {
    const seconds = this.remaining();
    return `${Math.floor(seconds / 3600)
      .toString()
      .padStart(2, '0')}:${Math.floor((seconds % 3600) / 60)
      .toString()
      .padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`;
  });

  constructor() {
    effect(() => {
      const owner = this.auth.user()?.id;
      this.count();
      if (!this.owner || owner !== this.owner) {
        ++this.generation;
        this.status.set(null);
        this.error.set('');
        this.remaining.set(0);
        this.loading.set(false);
        return;
      }
      untracked(() => void this.load());
    });
    const timer = setInterval(() => {
      if (this.status()?.count !== 0 || !this.current(this.generation)) return;
      this.remaining.set(
        Math.max(0, Math.ceil(this.initialSeconds - (performance.now() - this.anchor) / 1000)),
      );
      if (this.remaining() === 0 && !this.loading() && performance.now() >= this.nextCheck)
        void this.load();
    }, 1000);
    const visible = () => {
      if (!document.hidden && this.current(this.generation)) void this.load();
    };
    document.addEventListener('visibilitychange', visible);
    this.destroy.onDestroy(() => {
      ++this.generation;
      clearInterval(timer);
      document.removeEventListener('visibilitychange', visible);
    });
  }

  protected async load(): Promise<void> {
    if (!this.current(this.generation)) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    try {
      const status = await this.api.status();
      if (!this.current(generation)) return;
      const wasEmpty = this.status()?.count === 0 || this.count() === 0;
      this.anchor = performance.now();
      this.initialSeconds = status.refillAt
        ? Math.max(0, (Date.parse(status.refillAt) - Date.parse(status.serverNow)) / 1000)
        : 0;
      this.remaining.set(Math.ceil(this.initialSeconds));
      this.status.set(status);
      this.nextCheck =
        this.anchor +
        (this.initialSeconds > 0 ? Math.min(15000, this.initialSeconds * 1000) : 15000);
      if (wasEmpty && status.count > 0) this.recovered.emit();
    } catch {
      if (this.current(generation)) {
        this.error.set('We couldn’t check your hearts. Try again shortly.');
        this.nextCheck = performance.now() + 30000;
      }
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }

  private current(generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      !!this.owner &&
      this.auth.user()?.id === this.owner
    );
  }
}
