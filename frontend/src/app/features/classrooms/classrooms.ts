import { Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import { ClassroomApi, ClassroomView, ClassPage } from '../../core/classrooms/classroom-api';
import { CatalogApi, CatalogCourse } from '../../core/content/catalog-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-classrooms',
  imports: [RouterLink, ReactiveFormsModule, BrandLogo],
  templateUrl: './classrooms.html',
  styleUrl: './classrooms.scss',
})
export class Classrooms {
  private readonly api = inject(ClassroomApi);
  private readonly catalog = inject(CatalogApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly builder = inject(FormBuilder).nonNullable;
  private generation = 0;
  private requestKey = crypto.randomUUID();
  protected readonly teacher = computed(() => this.auth.user()?.roles.includes('Teacher') ?? false);
  protected readonly value = signal<ClassPage<ClassroomView> | null>(null);
  protected readonly courses = signal<readonly CatalogCourse[]>([]);
  protected readonly loading = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected readonly page = signal(1);
  protected readonly joinForm = this.builder.group({
    code: ['', [Validators.required, Validators.maxLength(64)]],
  });
  protected readonly createForm = this.builder.group({
    courseId: ['', Validators.required],
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', Validators.maxLength(2000)],
  });

  constructor() {
    effect(() => {
      this.auth.user();
      untracked(() => {
        this.page.set(1);
        this.joinForm.reset();
        this.createForm.reset();
        this.requestKey = crypto.randomUUID();
        void this.load();
      });
    });
  }
  private current(generation: number, owner: string): boolean {
    return (
      !this.destroy.destroyed && generation === this.generation && this.auth.user()?.id === owner
    );
  }
  protected async load(): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    this.value.set(null);
    this.courses.set([]);
    this.error.set('');
    this.working.set(false);
    this.loading.set(!!owner);
    if (!owner) return;
    const results = await Promise.allSettled([
      this.api.list(this.page()),
      this.teacher() ? this.catalog.courses() : Promise.resolve([]),
    ]);
    if (!this.current(generation, owner)) return;
    const [classes, courses] = results;
    if (classes.status === 'fulfilled') this.value.set(classes.value);
    else this.error.set('Your classes could not load. Please refresh.');
    if (courses.status === 'fulfilled') this.courses.set(courses.value);
    else this.error.set('Course choices could not load. Refresh before creating a class.');
    this.loading.set(false);
  }
  protected changePage(direction: number): void {
    if (this.loading() || this.working()) return;
    this.page.update((value) => Math.max(1, value + direction));
    void this.load();
  }
  protected async join(): Promise<void> {
    this.joinForm.markAllAsTouched();
    if (this.joinForm.invalid) return;
    const code = this.joinForm.controls.code.value.trim();
    await this.write(() => this.api.join(code));
  }
  protected async create(): Promise<void> {
    this.createForm.markAllAsTouched();
    if (!this.teacher() || this.createForm.invalid) return;
    const input = { ...this.createForm.getRawValue(), requestKey: this.requestKey };
    await this.write(() => this.api.create(input));
  }
  private async write(action: () => Promise<{ id: string }>): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.loading() || this.working()) return;
    const generation = this.generation;
    this.working.set(true);
    this.error.set('');
    try {
      const result = await action();
      if (!this.current(generation, owner)) return;
      this.joinForm.reset();
      await this.router.navigate(['/classes', result.id]);
    } catch (error) {
      if (!this.current(generation, owner)) return;
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 404
          ? 'That invitation is unavailable, expired or fully used. Ask your teacher for a new code.'
          : error instanceof HttpErrorResponse && error.status === 409
            ? (error.error?.title ?? 'Refresh the class list before retrying.')
            : 'The result could not be confirmed. Refresh the list or retry the same details safely.',
      );
    } finally {
      if (this.current(generation, owner)) this.working.set(false);
    }
  }
}
