import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdministrationApi, AuditEntry, Page } from '../../core/administration/administration-api';
import { AuthStore } from '../../core/accounts/auth-store';

@Component({
  selector: 'app-audit-log',
  imports: [DatePipe, RouterLink],
  templateUrl: './audit-log.html',
  styleUrl: './audit-log.scss',
})
export class AuditLog {
  private readonly api = inject(AdministrationApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  protected readonly canView = computed(() => {
    const user = this.auth.user();
    return !!user && user.id === this.owner && user.roles.includes('SuperAdmin');
  });
  protected readonly result = signal<Page<AuditEntry> | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  constructor() {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    if (!this.canView() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.history(null, page);
      if (this.current()) this.result.set(result);
    } catch {
      if (this.current()) this.error.set('Audit history could not be loaded. Try again.');
    } finally {
      if (this.current()) this.busy.set(false);
    }
  }

  private current(): boolean {
    return !this.destroy.destroyed && this.canView();
  }
}
