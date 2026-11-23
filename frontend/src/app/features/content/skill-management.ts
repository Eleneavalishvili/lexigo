import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, StaffCourse } from '../../core/content/content-api';
import { ManagedSkill, SkillPage, SkillsApi } from '../../core/content/skills-api';

@Component({
  selector: 'app-skill-management',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './skill-management.html',
  styleUrl: './skill-management.scss',
})
export class SkillManagementPage {
  private readonly auth = inject(AuthStore);
  private readonly api = inject(SkillsApi);
  private readonly content = inject(ContentApi);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  protected readonly allowed = computed(() => {
    const user = this.auth.user();
    return (
      user?.id === this.owner &&
      !!user?.roles.some((role) => ['Admin', 'SuperAdmin'].includes(role))
    );
  });
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly courseId = signal('');
  protected readonly result = signal<SkillPage | null>(null);
  protected readonly editing = signal<ManagedSkill | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly archived = signal(false);
  protected readonly pageSize = signal(20);
  protected readonly search = inject(FormBuilder).nonNullable.control(
    '',
    Validators.maxLength(100),
  );
  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    category: ['Vocabulary', [Validators.required, Validators.maxLength(40)]],
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
  });

  constructor() {
    effect(() => {
      if (!this.allowed()) {
        this.generation++;
        this.courses.set([]);
        this.result.set(null);
        this.editing.set(null);
        this.form.reset();
        this.notice.set('');
        this.error.set('');
      }
    });
    void this.initialize();
  }

  private current(generation: number): boolean {
    return this.allowed() && !this.destroy.destroyed && generation === this.generation;
  }

  private async initialize(): Promise<void> {
    if (!this.allowed()) return;
    const generation = ++this.generation;
    this.loading.set(true);
    try {
      const courses = await this.content.courses();
      if (!this.current(generation)) return;
      this.courses.set(courses);
      this.courseId.set(courses[0]?.id ?? '');
      await this.load();
    } catch (error) {
      if (this.current(generation)) this.failure(error);
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }

  protected async chooseCourse(id: string): Promise<void> {
    if (this.busy()) return;
    this.courseId.set(id);
    this.newSkill();
    await this.load();
  }

  protected async reload(): Promise<void> {
    if (this.busy()) return;
    this.newSkill();
    if (this.courses().length === 0) await this.initialize();
    else await this.load();
  }

  protected searchPage(event: Event): void {
    event.preventDefault();
    if (!this.busy()) void this.load();
  }

  protected async load(page = 1): Promise<void> {
    if (!this.allowed() || !this.courseId() || this.search.invalid) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    this.result.set(null);
    try {
      const result = await this.api.list(
        this.courseId(),
        page,
        this.pageSize(),
        this.search.value,
        this.archived(),
      );
      if (this.current(generation)) this.result.set(result);
    } catch (error) {
      if (this.current(generation)) this.failure(error);
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }

  protected newSkill(): void {
    this.editing.set(null);
    this.form.reset({ name: '', category: 'Vocabulary', reason: '' });
  }

  protected edit(skill: ManagedSkill): void {
    if (this.busy()) return;
    this.editing.set(skill);
    this.form.reset({ name: skill.name, category: skill.category, reason: '' });
    this.notice.set('');
  }

  protected async save(visibility = false): Promise<void> {
    const skill = this.editing();
    const fields = this.form.getRawValue();
    if (
      !this.allowed() ||
      this.busy() ||
      this.loading() ||
      !this.courseId() ||
      this.form.controls.name.invalid ||
      this.form.controls.category.invalid ||
      (skill && this.form.controls.reason.invalid)
    )
      return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const value = skill
        ? visibility
          ? await this.api.visibility(skill, fields.reason)
          : await this.api.edit(skill, fields.name, fields.category, fields.reason)
        : await this.api.create(this.courseId(), fields.name, fields.category);
      if (!this.current(generation)) return;
      this.editing.set(value);
      this.form.reset({ name: value.name, category: value.category, reason: '' });
      this.notice.set(
        visibility
          ? value.isArchived
            ? 'Skill archived. Existing lessons and progress are preserved.'
            : 'Skill restored.'
          : 'Skill saved.',
      );
      await this.load();
    } catch (error) {
      if (this.current(generation)) this.failure(error);
    } finally {
      if (this.allowed() && !this.destroy.destroyed) this.busy.set(false);
    }
  }

  protected filterArchived(value: boolean): void {
    this.archived.set(value);
    void this.load();
  }

  protected changeSize(value: string): void {
    this.pageSize.set(Number(value));
    void this.load();
  }

  private failure(error: unknown): void {
    this.error.set(
      error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
        ? error.error.title
        : 'Skills could not be loaded or saved. Please retry.',
    );
  }
}
