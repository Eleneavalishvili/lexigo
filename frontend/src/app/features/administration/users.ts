import { Dialog, DialogModule, DialogRef } from '@angular/cdk/dialog';
import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  AdministrationApi,
  AuditEntry,
  ManagedUser,
  Page,
} from '../../core/administration/administration-api';
import { AuthStore } from '../../core/accounts/auth-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';
import { BillingPermissionEditor } from './billing-permission-editor';
import { BlockAccountDialog } from './block-account-dialog';

@Component({
  selector: 'app-managed-users',
  imports: [
    RouterLink,
    DialogModule,
    DatePipe,
    ReactiveFormsModule,
    BrandLogo,
    PasswordToggle,
    BillingPermissionEditor,
  ],
  templateUrl: './users.html',
  styleUrl: './users.scss',
})
export class ManagedUsers {
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(AdministrationApi);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly dialog = inject(Dialog);
  private blockDialog: DialogRef<boolean> | undefined;
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly roles = [
    'Learner',
    'Teacher',
    'ContentAuthor',
    'Reviewer',
    'Moderator',
    'Admin',
    'SuperAdmin',
  ];
  protected readonly results = signal<Page<ManagedUser> | null>(null);
  protected readonly selected = signal<ManagedUser | null>(null);
  protected readonly selectedRoles = signal<readonly string[]>([]);
  protected readonly selectedLanguages = signal<readonly number[]>([]);
  protected readonly languages = signal<readonly { id: number; englishName: string }[]>([]);
  protected readonly history = signal<Page<AuditEntry> | null>(null);
  protected readonly busy = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly showPassword = signal(false);
  protected readonly filters = this.builder.group({
    search: ['', Validators.maxLength(80)],
    role: [''],
    status: [''],
  });
  protected readonly proof = this.builder.group({
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
  });
  private version = 0;
  private historyVersion = 0;
  private ownerId = this.auth.user()?.id;
  protected get superAdmin(): boolean {
    return this.auth.user()?.roles.includes('SuperAdmin') ?? false;
  }

  constructor() {
    this.destroy.onDestroy(() => this.blockDialog?.close());
    effect(() => {
      const id = this.auth.user()?.id;
      if (id !== this.ownerId) {
        this.blockDialog?.close();
        this.ownerId = id;
        this.version++;
        this.historyVersion++;
        this.results.set(null);
        this.selected.set(null);
        this.history.set(null);
        this.proof.reset();
        if (this.auth.user()?.roles.some((role) => role === 'Admin' || role === 'SuperAdmin'))
          untracked(() => void this.load());
      }
      if (!this.auth.user()?.roles.some((role) => role === 'Admin' || role === 'SuperAdmin')) {
        this.blockDialog?.close();
        this.version++;
        this.results.set(null);
        this.selected.set(null);
        this.history.set(null);
        this.proof.reset();
        void this.router.navigate(['/account']);
      }
    });
    void this.load();
  }

  protected async load(page = 1): Promise<void> {
    if (this.filters.invalid) return;
    const version = ++this.version;
    const owner = this.auth.user()?.id;
    this.loading.set(true);
    this.error.set('');
    try {
      const value = this.filters.getRawValue();
      const [users, languages] = await Promise.all([
        this.api.users(value.search, value.role, value.status, page),
        this.superAdmin ? this.api.languages() : Promise.resolve([]),
      ]);
      if (this.destroy.destroyed || version !== this.version || owner !== this.auth.user()?.id)
        return;
      this.results.set(users);
      this.languages.set(languages);
    } catch {
      if (!this.destroy.destroyed && version === this.version)
        this.error.set('We couldn’t load accounts. Please try again.');
    } finally {
      if (!this.destroy.destroyed && version === this.version) this.loading.set(false);
    }
  }

  protected select(user: ManagedUser): void {
    this.historyVersion++;
    this.selected.set(user);
    this.selectedRoles.set(user.roles);
    this.selectedLanguages.set(user.languageIds);
    this.proof.reset();
    this.history.set(null);
    this.notice.set('');
    this.error.set('');
  }
  protected toggleRole(role: string): void {
    if (role !== 'Learner')
      this.selectedRoles.update((value) =>
        value.includes(role) ? value.filter((item) => item !== role) : [...value, role],
      );
  }
  protected toggleLanguage(id: number): void {
    this.selectedLanguages.update((value) =>
      value.includes(id) ? value.filter((item) => item !== id) : [...value, id],
    );
  }

  protected async save(roleChange: boolean): Promise<void> {
    this.proof.markAllAsTouched();
    const target = this.selected();
    if (!target || this.proof.invalid || this.busy() || this.blockDialog) return;
    if (!roleChange && target.status === 'Active') {
      const owner = this.auth.user()?.id;
      this.blockDialog = this.dialog.open<boolean, string>(BlockAccountDialog, {
        data: target.displayName,
        ariaLabelledBy: 'block-account-title',
        ariaDescribedBy: 'block-account-description',
        ariaModal: true,
        autoFocus: '.button-outline',
        width: '28rem',
        maxWidth: 'calc(100vw - 2rem)',
      });
      this.blockDialog.closed.subscribe((confirmed) => {
        this.blockDialog = undefined;
        if (
          confirmed &&
          !this.destroy.destroyed &&
          owner === this.auth.user()?.id &&
          this.selected()?.id === target.id &&
          this.selected()?.status === 'Active' &&
          !this.proof.invalid
        )
          void this.applyChange(target, false);
      });
      return;
    }
    await this.applyChange(target, roleChange);
  }

  private async applyChange(target: ManagedUser, roleChange: boolean): Promise<void> {
    const owner = this.auth.user()?.id;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const proof = this.proof.getRawValue();
      if (roleChange)
        await this.api.roles(
          target.id,
          this.selectedRoles(),
          this.selectedLanguages(),
          proof.reason,
          proof.currentPassword,
        );
      else
        await this.api.status(
          target.id,
          target.status === 'Active' ? 'Blocked' : 'Active',
          proof.reason,
          proof.currentPassword,
        );
      if (this.destroy.destroyed || owner !== this.auth.user()?.id) return;
      this.proof.reset();
      if (target.id === this.auth.user()?.id) {
        this.auth.credentialsChanged();
        await this.router.navigate(['/login']);
        return;
      }
      this.selected.set(null);
      this.history.set(null);
      this.notice.set(
        'Account updated. Existing sessions were revoked and the change was recorded.',
      );
      await this.load(this.results()?.page ?? 1);
    } catch (error) {
      if (this.destroy.destroyed || owner !== this.auth.user()?.id) return;
      this.error.set(
        error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
          ? error.error.title
          : 'That change could not be completed. Sign in again if your five-minute confirmation window has passed.',
      );
    } finally {
      this.busy.set(false);
    }
  }

  protected async loadHistory(page = 1): Promise<void> {
    const version = ++this.historyVersion;
    const target = this.selected();
    const owner = this.auth.user()?.id;
    if (!target) return;
    this.error.set('');
    try {
      const result = await this.api.history(target.id, page);
      if (
        version === this.historyVersion &&
        !this.destroy.destroyed &&
        this.selected()?.id === target.id &&
        this.auth.user()?.id === owner
      )
        this.history.set(result);
    } catch {
      this.error.set('We couldn’t load this account’s history. Please try again.');
    }
  }
}
