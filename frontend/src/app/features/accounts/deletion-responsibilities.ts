import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AccountApi, DeletionResponsibility } from '../../core/accounts/account-api';

@Component({
  selector: 'app-deletion-responsibilities',
  imports: [RouterLink],
  templateUrl: './deletion-responsibilities.html',
  styleUrl: './deletion-responsibilities.scss',
})
export class DeletionResponsibilities implements OnInit {
  private readonly api = inject(AccountApi);
  private readonly lifetime = inject(DestroyRef);
  protected readonly pending = signal<DeletionResponsibility[]>([]);
  protected readonly loading = signal(false);
  protected readonly checked = signal(false);
  protected readonly error = signal('');

  ngOnInit(): void {
    void this.refresh();
  }

  protected async refresh(): Promise<void> {
    if (this.loading()) return;
    this.loading.set(true);
    this.checked.set(false);
    this.pending.set([]);
    this.error.set('');
    try {
      const pending = await this.api.deletionResponsibilities();
      if (this.lifetime.destroyed) return;
      this.pending.set(pending);
      this.checked.set(true);
    } catch {
      if (!this.lifetime.destroyed)
        this.error.set('Your responsibilities could not be checked. Try again.');
    } finally {
      if (!this.lifetime.destroyed) this.loading.set(false);
    }
  }

  protected destination(code: string): string {
    switch (code) {
      case 'classes':
        return '/classes';
      case 'extra-study':
        return '/classes/extra-study';
      case 'lesson-drafts':
        return '/manage/lessons';
      case 'vocabulary-drafts':
        return '/manage/vocabulary/revisions';
      case 'recordings':
        return '/manage/media';
      default:
        return '/contact';
    }
  }
}
