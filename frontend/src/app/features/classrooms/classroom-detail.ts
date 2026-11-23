import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  ClassroomApi,
  ClassroomDetail,
  InvitationCreated,
} from '../../core/classrooms/classroom-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { ClassAssignments } from './class-assignments';
import { ClassTransfer } from './class-transfer';

@Component({
  selector: 'app-classroom-detail',
  imports: [RouterLink, ReactiveFormsModule, DatePipe, BrandLogo, ClassAssignments, ClassTransfer],
  templateUrl: './classroom-detail.html',
  styleUrl: './classroom-detail.scss',
})
export class ClassroomDetailPage {
  private readonly api = inject(ClassroomApi);
  private readonly auth = inject(AuthStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly builder = inject(FormBuilder).nonNullable;
  private readonly id = signal('');
  private generation = 0;
  protected readonly value = signal<ClassroomDetail | null>(null);
  protected readonly invitation = signal<InvitationCreated | null>(null);
  protected readonly memberPage = signal(1);
  protected readonly loading = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly showAssignments = signal(false);
  protected readonly editForm = this.builder.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', Validators.maxLength(2000)],
    confirmArchive: [false],
  });
  protected readonly invitationForm = this.builder.group({
    days: [7, [Validators.required, Validators.min(1), Validators.max(30)]],
    maxUses: [30, [Validators.required, Validators.min(1), Validators.max(200)]],
  });

  constructor() {
    this.route.paramMap
      .pipe(takeUntilDestroyed())
      .subscribe((params) => this.id.set(params.get('classId') ?? ''));
    effect(() => {
      this.auth.user();
      this.id();
      untracked(() => {
        this.memberPage.set(1);
        void this.load();
      });
    });
  }
  private current(generation: number, owner: string): boolean {
    return (
      !this.destroy.destroyed && generation === this.generation && this.auth.user()?.id === owner
    );
  }
  protected async load(preserveNotice = false): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    this.value.set(null);
    this.showAssignments.set(false);
    this.invitation.set(null);
    this.error.set('');
    if (!preserveNotice) this.notice.set('');
    this.working.set(false);
    this.loading.set(!!owner);
    this.editForm.reset();
    if (!owner || !this.id()) {
      this.loading.set(false);
      return;
    }
    try {
      const detail = await this.api.detail(this.id(), this.memberPage());
      if (!this.current(generation, owner)) return;
      this.value.set(detail);
      this.editForm.reset({
        name: detail.classroom.name,
        description: detail.classroom.description,
        confirmArchive: false,
      });
    } catch {
      if (this.current(generation, owner))
        this.error.set(
          'This class is unavailable to your account, or could not load. Return to your classes or refresh.',
        );
    } finally {
      if (this.current(generation, owner)) this.loading.set(false);
    }
  }

  protected changePage(direction: number): void {
    if (this.loading() || this.working()) return;
    this.memberPage.update((page) => Math.max(1, page + direction));
    void this.load();
  }
  protected save(): void {
    const classroom = this.value()?.classroom;
    this.editForm.markAllAsTouched();
    if (!classroom?.isTeacher || classroom.isArchived || this.editForm.invalid) return;
    const input = this.editForm.getRawValue();
    void this.write(
      () =>
        this.api.edit(classroom.id, {
          name: input.name,
          description: input.description,
          isArchived: false,
          revision: classroom.revision,
        }),
      'Class details saved.',
    );
  }
  protected archive(): void {
    const classroom = this.value()?.classroom;
    if (
      !classroom?.isTeacher ||
      classroom.isArchived ||
      !this.editForm.controls.confirmArchive.value
    )
      return;
    void this.write(
      () =>
        this.api.edit(classroom.id, {
          name: classroom.name,
          description: classroom.description,
          isArchived: true,
          revision: classroom.revision,
        }),
      'Class archived. Existing learning progress stays saved.',
    );
  }
  protected async rotate(): Promise<void> {
    const classroom = this.value()?.classroom;
    const owner = this.auth.user()?.id;
    this.invitationForm.markAllAsTouched();
    if (
      !owner ||
      !classroom?.isTeacher ||
      classroom.isArchived ||
      this.invitationForm.invalid ||
      this.working()
    )
      return;
    const generation = this.generation;
    const input = this.invitationForm.getRawValue();
    this.working.set(true);
    this.error.set('');
    this.invitation.set(null);
    this.notice.set('');
    try {
      const result = await this.api.invite(classroom.id, input.days, input.maxUses);
      if (this.current(generation, owner)) this.invitation.set(result);
    } catch {
      if (this.current(generation, owner))
        this.error.set(
          'The new code could not be confirmed. Generate another code to replace any unconfirmed one.',
        );
    } finally {
      if (this.current(generation, owner)) this.working.set(false);
    }
  }
  protected remove(userId: string): void {
    const classroom = this.value()?.classroom;
    if (!classroom?.isTeacher || classroom.isArchived) return;
    void this.write(
      () => this.api.remove(classroom.id, userId),
      'Member removed. Their independent learning history stays saved.',
    );
  }
  protected restore(userId: string): void {
    const classroom = this.value()?.classroom;
    if (!classroom?.isTeacher || classroom.isArchived) return;
    void this.write(
      () => this.api.restore(classroom.id, userId),
      'The removed membership was restored.',
    );
  }
  protected leave(): void {
    const classroom = this.value()?.classroom;
    const userId = this.auth.user()?.id;
    if (!classroom?.canLeave || !userId) return;
    void this.write(
      async () => {
        await this.api.remove(classroom.id, userId);
        return { left: true };
      },
      '',
      true,
    );
  }
  private async write(
    action: () => Promise<unknown>,
    message: string,
    leave = false,
  ): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.working() || this.loading()) return;
    const generation = this.generation;
    this.working.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await action();
      if (!this.current(generation, owner)) return;
      if (leave) {
        await this.router.navigate(['/classes']);
        return;
      }
      this.notice.set(message);
      await this.load(true);
    } catch (error) {
      if (this.current(generation, owner))
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? (error.error?.title ?? 'The class changed. Refresh before retrying.')
            : 'The result could not be confirmed. Refresh before retrying.',
        );
    } finally {
      if (this.current(generation, owner)) this.working.set(false);
    }
  }
}
