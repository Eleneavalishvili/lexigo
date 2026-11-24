import { Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ContentApi,
  ExerciseDraft,
  LessonRow,
  StaffCourse,
  StaffLesson,
  StaffExercise,
  StaffUnit,
  StaffVersion,
} from '../../core/content/content-api';
import { AuthStore } from '../../core/accounts/auth-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { ExerciseComposer } from './exercise-composer';
import { LessonPreview } from './lesson-preview';
import { DraftOwnerTransfer } from './draft-owner-transfer';
import { CourseCreator } from './course-creator';

@Component({
  selector: 'app-content-workbench',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    BrandLogo,
    ExerciseComposer,
    LessonPreview,
    DraftOwnerTransfer,
    CourseCreator,
  ],
  templateUrl: './workbench.html',
  styleUrls: ['./workbench.scss', './workbench-fields.scss'],
})
export class ContentWorkbench {
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(ContentApi);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly builder = inject(FormBuilder).nonNullable;
  private generation = 0;
  private owner = this.auth.user()?.id;
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly units = signal<readonly StaffUnit[]>([]);
  protected readonly lessons = signal<readonly LessonRow[]>([]);
  protected readonly course = signal<StaffCourse | null>(null);
  protected readonly lesson = signal<StaffLesson | null>(null);
  protected readonly previewOpen = signal(false);
  protected readonly versionRows = signal<readonly StaffVersion[]>([]);
  protected readonly versionPage = signal(1);
  protected readonly confirmErasure = signal(false);
  protected readonly editingExercise = signal<StaffExercise | null>(null);
  protected readonly editingUnitId = signal('');
  protected readonly editingUnit = computed(() =>
    this.units().find((unit) => unit.id === this.editingUnitId()),
  );
  protected readonly unitArchiveReason = this.builder.control('');
  protected readonly unitPosition = this.builder.control(1, [
    Validators.required,
    Validators.min(1),
    Validators.max(10000),
  ]);
  protected readonly courseArchiveReason = this.builder.control('', [
    Validators.required,
    Validators.minLength(3),
    Validators.maxLength(500),
  ]);
  protected readonly coursePosition = this.builder.control(1, [
    Validators.required,
    Validators.min(1),
    Validators.max(10000),
  ]);
  protected readonly courseDetails = this.builder.group({
    title: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.required, Validators.maxLength(1000)]],
  });
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly busy = signal(false);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly unitForm = this.builder.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    description: ['', [Validators.required, Validators.maxLength(3000)]],
    difficulty: ['Beginner'],
    orderNumber: [1, [Validators.required, Validators.min(1), Validators.max(10000)]],
  });
  protected readonly createForm = this.builder.group({
    unitId: ['', Validators.required],
    title: ['', [Validators.required, Validators.maxLength(160)]],
    objective: ['', [Validators.required, Validators.maxLength(1000)]],
    explanation: ['', Validators.maxLength(20000)],
    orderNumber: [1, [Validators.required, Validators.min(1)]],
  });
  protected readonly editForm = this.builder.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    objective: ['', [Validators.required, Validators.maxLength(1000)]],
    explanation: ['', Validators.maxLength(20000)],
  });
  protected readonly reviewComment = this.builder.control('', [
    Validators.required,
    Validators.minLength(3),
    Validators.maxLength(3000),
  ]);
  protected get admin(): boolean {
    return (
      this.auth.user()?.roles.some((role) => role === 'Admin' || role === 'SuperAdmin') ?? false
    );
  }
  protected get canEdit(): boolean {
    const lesson = this.lesson();
    return (
      !!lesson &&
      (this.admin || (!!this.course()?.canAuthor && lesson.ownerId === this.auth.user()?.id))
    );
  }
  protected get canReview(): boolean {
    const lesson = this.lesson();
    return (
      !!lesson &&
      !!this.course()?.canReview &&
      lesson.ownerId !== this.auth.user()?.id &&
      lesson.versionCreatedBy !== this.auth.user()?.id
    );
  }
  protected get nextExercise(): number {
    return Math.max(0, ...(this.lesson()?.exercises.map((item) => item.orderNumber) ?? [])) + 1;
  }

  constructor() {
    effect(() => {
      const user = this.auth.user();
      if (
        user?.id !== this.owner ||
        !user?.roles.some((role) =>
          ['ContentAuthor', 'Reviewer', 'Admin', 'SuperAdmin'].includes(role),
        )
      ) {
        this.generation++;
        this.courses.set([]);
        this.units.set([]);
        this.lessons.set([]);
        this.course.set(null);
        this.createForm.reset();
        this.unitForm.reset();
        this.lesson.set(null);
        this.editForm.reset();
        this.reviewComment.reset();
        this.archiveReason.reset();
        this.courseArchiveReason.reset();
        this.courseDetails.reset();
        this.unitArchiveReason.reset();
        this.duplicateTitle.reset();
        void this.router.navigate(['/account']);
      }
    });
    void this.load();
  }
  protected async load(): Promise<void> {
    await this.read(async () => {
      const courses = await this.api.courses();
      return () => this.courses.set(courses);
    });
  }
  protected async chooseCourse(course: StaffCourse, page = 1): Promise<void> {
    if (this.course()?.id !== course.id) this.courseArchiveReason.reset();
    this.coursePosition.reset(course.orderNumber ?? 1);
    this.courseDetails.reset({ title: course.title, description: course.description ?? '' });
    this.course.set(course);
    this.lesson.set(null);
    this.units.set([]);
    this.lessons.set([]);
    this.selectUnit('');
    this.page.set(page);
    await this.read(async () => {
      const [units, lessons] = await Promise.all([
        this.api.units(course.id),
        this.api.lessons(course.id, page),
      ]);
      return () => {
        this.units.set(units);
        this.lessons.set(lessons.items);
        this.total.set(lessons.total);
        if (units[0])
          this.createForm.patchValue({
            unitId: units[0].id,
            orderNumber: units[0].nextLessonOrder,
          });
        this.unitForm.controls.orderNumber.setValue(
          Math.max(0, ...units.map((item) => item.orderNumber)) + 1,
        );
      };
    });
  }
  protected setUnit(): void {
    const unit = this.units().find((item) => item.id === this.createForm.controls.unitId.value);
    if (unit) this.createForm.controls.orderNumber.setValue(unit.nextLessonOrder);
  }
  protected async chooseLesson(id: string, versionId?: string): Promise<void> {
    this.previewOpen.set(false);
    if (this.lesson()?.id !== id) {
      this.versionRows.set([]);
      this.versionPage.set(1);
    }
    this.confirmErasure.set(false);
    this.editingExercise.set(null);
    this.lesson.set(null);
    this.reviewComment.reset();
    this.archiveReason.reset();
    this.duplicateTitle.reset();
    await this.read(async () => {
      const lesson = versionId ? await this.api.lesson(id, versionId) : await this.api.lesson(id);
      return () => {
        this.lesson.set(lesson);
        this.lessonPosition.reset(lesson.orderNumber ?? 1);
        this.editForm.reset({
          title: lesson.title,
          objective: lesson.objective,
          explanation: lesson.explanation,
        });
      };
    });
  }
  protected async loadVersions(page: number): Promise<void> {
    const lesson = this.lesson();
    if (!lesson) return;
    await this.read(async () => {
      const versions = await this.api.versions(lesson.id, page);
      return () => {
        this.versionRows.set(versions);
        this.versionPage.set(page);
      };
    });
  }

  protected async erasePrivateDraft(): Promise<void> {
    const lesson = this.lesson();
    if (!lesson || !this.confirmErasure()) return;
    await this.write(async () => {
      await this.api.erasePrivateDraft(lesson.id, lesson.versionId);
      const course = this.course();
      if (course) await this.chooseCourse(course, this.page());
      await this.chooseLesson(lesson.id, lesson.versionId);
    });
  }
  protected selectUnit(id: string): void {
    this.unitArchiveReason.reset();
    const unit = this.units().find((item) => item.id === id);
    this.editingUnitId.set(unit?.id ?? '');
    this.unitPosition.reset(unit?.orderNumber ?? 1);
    this.unitForm.reset({
      title: unit?.title ?? '',
      description: unit?.description ?? '',
      difficulty: unit?.difficulty ?? 'Beginner',
      orderNumber:
        unit?.orderNumber ?? Math.max(0, ...this.units().map((item) => item.orderNumber)) + 1,
    });
  }
  protected async setCourseArchive(): Promise<void> {
    const course = this.course();
    const reason = this.courseArchiveReason.value.trim();
    if (!this.admin || !course || reason.length < 3 || reason.length > 500) return;
    await this.write(async () => {
      await this.api.setCourseArchive(course.id, !course.archivedAt, reason);
      await this.load();
      const updated = this.courses().find((item) => item.id === course.id);
      if (updated) await this.chooseCourse(updated, this.page());
      this.courseArchiveReason.reset();
    });
  }
  protected async saveCourseDetails(): Promise<void> {
    const course = this.course();
    const { title, description } = this.courseDetails.getRawValue();
    if (
      !this.admin ||
      !course ||
      this.courseDetails.invalid ||
      !title.trim() ||
      !description.trim()
    )
      return;
    await this.write(async () => {
      await this.api.editCourse(course.id, title.trim(), description.trim());
      await this.load();
      const updated = this.courses().find((item) => item.id === course.id);
      if (updated && this.course()?.id === course.id) {
        this.course.set(updated);
        this.courseDetails.reset({ title: updated.title, description: updated.description ?? '' });
      }
    });
  }
  protected async moveCourse(): Promise<void> {
    const course = this.course();
    const previousPosition = course?.orderNumber;
    const position = this.coursePosition.value;
    if (
      !this.admin ||
      !course ||
      !previousPosition ||
      !Number.isInteger(position) ||
      this.coursePosition.invalid ||
      position === previousPosition
    )
      return;
    await this.write(async () => {
      await this.api.moveCourse(course.id, previousPosition, position);
      await this.load();
      const updated = this.courses().find((item) => item.id === course.id);
      if (updated && this.course()?.id === course.id) {
        this.course.set(updated);
        this.coursePosition.reset(updated.orderNumber ?? 1);
      }
    });
  }
  protected async saveUnit(): Promise<void> {
    this.unitForm.markAllAsTouched();
    const course = this.course();
    if (!course || this.unitForm.invalid) return;
    await this.write(async () => {
      const body = this.unitForm.getRawValue();
      const unitId = this.editingUnitId();
      if (unitId) await this.api.editUnit(unitId, body);
      else await this.api.createUnit({ ...body, courseId: course.id });
      await this.chooseCourse(course);
    });
  }
  protected async moveUnit(): Promise<void> {
    const course = this.course();
    const unit = this.editingUnit();
    const position = this.unitPosition.value;
    if (
      !this.admin ||
      !course ||
      !unit ||
      !Number.isInteger(position) ||
      this.unitPosition.invalid ||
      position === unit.orderNumber
    )
      return;
    await this.write(async () => {
      await this.api.moveUnit(unit.id, unit.orderNumber, position);
      await this.chooseCourse(course, this.page());
      this.selectUnit(unit.id);
    });
  }
  protected async setUnitArchive(): Promise<void> {
    const course = this.course();
    const unit = this.editingUnit();
    const reason = this.unitArchiveReason.value.trim();
    if (!this.admin || !course || !unit || reason.length < 3 || reason.length > 500) return;
    await this.write(async () => {
      await this.api.setUnitArchive(unit.id, !unit.archivedAt, reason);
      await this.load();
      await this.chooseCourse(
        this.courses().find((item) => item.id === course.id) ?? course,
        this.page(),
      );
      this.selectUnit(unit.id);
    });
  }
  protected async createLesson(): Promise<void> {
    this.createForm.markAllAsTouched();
    const course = this.course();
    if (!course || this.createForm.invalid) return;
    await this.write(async () => {
      const result = await this.api.createLesson(this.createForm.getRawValue());
      await this.chooseCourse(course);
      await this.chooseLesson(result.id);
    });
  }
  protected async saveDraft(): Promise<void> {
    this.editForm.markAllAsTouched();
    const lesson = this.lesson();
    if (!lesson || this.editForm.invalid) return;
    await this.write(async () => {
      await this.api.edit(lesson.id, {
        ...this.editForm.getRawValue(),
        versionId: lesson.versionId,
      });
      await this.chooseLesson(lesson.id);
    });
  }
  protected async addExercise(draft: ExerciseDraft): Promise<void> {
    const lesson = this.lesson();
    if (!lesson) return;
    await this.write(async () => {
      const editing = this.editingExercise();
      if (editing) await this.api.editExercise(lesson.id, editing.id, lesson.versionId, draft);
      else await this.api.addExercise(lesson.id, lesson.versionId, draft);
      await this.chooseLesson(lesson.id);
    });
  }
  protected async moveExercise(index: number, direction: -1 | 1): Promise<void> {
    const lesson = this.lesson();
    const exercise = lesson?.exercises[index];
    const neighbor = lesson?.exercises[index + direction];
    if (!lesson || !exercise || !neighbor || this.editingExercise()) return;
    await this.write(async () => {
      await this.api.moveExercise(
        lesson.id,
        exercise.id,
        lesson.versionId,
        neighbor.id,
        direction < 0,
      );
      if (this.lesson()?.versionId !== lesson.versionId) return;
      await this.read(async () => {
        const updated = await this.api.lesson(lesson.id, lesson.versionId);
        return () => this.lesson.set(updated);
      });
    });
  }
  protected async removeExercise(exercise: StaffExercise): Promise<void> {
    const lesson = this.lesson();
    if (!lesson) return;
    await this.write(async () => {
      await this.api.removeExercise(lesson.id, exercise.id, lesson.versionId);
      await this.chooseLesson(lesson.id);
    });
  }
  protected readonly archiveReason = this.builder.control('');
  protected readonly lessonPosition = this.builder.control(1, [
    Validators.required,
    Validators.min(1),
    Validators.max(10000),
  ]);

  protected async moveLesson(): Promise<void> {
    const lesson = this.lesson();
    const previousPosition = lesson?.orderNumber;
    const position = this.lessonPosition.value;
    if (
      !this.admin ||
      !lesson ||
      !previousPosition ||
      !Number.isInteger(position) ||
      this.lessonPosition.invalid ||
      position === previousPosition
    )
      return;
    await this.write(async () => {
      await this.api.moveLesson(lesson.id, previousPosition, position);
      const course = this.course();
      if (course) await this.chooseCourse(course, this.page());
      await this.chooseLesson(lesson.id, lesson.versionId);
    });
  }
  protected readonly duplicateTitle = this.builder.control('');

  protected async duplicateDraft(): Promise<void> {
    const lesson = this.lesson();
    const course = this.course();
    const title = this.duplicateTitle.value.trim();
    if (!course || !lesson || !this.canEdit || lesson.versionStatus !== 'Draft' || !title) return;

    await this.write(async () => {
      const copy = await this.api.duplicate(lesson.id, lesson.versionId, title);
      await this.chooseCourse(course, this.page());
      await this.chooseLesson(copy.id);
    });
  }

  protected async transition(
    operation: 'submit' | 'publish' | 'reviews' | 'versions' | 'archive',
    decision?: 'Approved' | 'ChangesRequested',
  ): Promise<void> {
    const lesson = this.lesson();
    if (!lesson) return;
    const reason = this.archiveReason.value.trim();
    if (
      operation === 'archive' &&
      (!this.admin ||
        lesson.versionStatus !== 'Published' ||
        lesson.versionId !== lesson.currentVersionId ||
        reason.length < 3 ||
        reason.length > 500)
    )
      return;
    if (operation === 'reviews') {
      this.reviewComment.markAsTouched();
      if (this.reviewComment.invalid) return;
    }
    await this.write(async () => {
      if (operation === 'submit') await this.api.submit(lesson.id, lesson.versionId);
      else if (operation === 'publish') await this.api.publish(lesson.id, lesson.versionId);
      else if (operation === 'archive') await this.api.archive(lesson.id, lesson.versionId, reason);
      else if (operation === 'versions') await this.api.revision(lesson.id);
      else if (decision)
        await this.api.review(lesson.id, lesson.versionId, decision, this.reviewComment.value);
      const course = this.course();
      if (course) await this.chooseCourse(course, this.page());
      if (operation !== 'reviews' || decision !== 'ChangesRequested' || this.canEdit)
        await this.chooseLesson(lesson.id);
    });
  }
  private async read(operation: () => Promise<() => void>): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    this.loading.set(true);
    this.error.set('');
    try {
      const apply = await operation();
      if (
        !this.destroy.destroyed &&
        generation === this.generation &&
        owner === this.auth.user()?.id
      )
        apply();
    } catch {
      if (!this.destroy.destroyed && generation === this.generation)
        this.error.set('We couldn’t load this content. Please try again.');
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
  private async write(operation: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await operation();
      if (!this.destroy.destroyed) this.notice.set('Your change is saved.');
    } catch (error) {
      if (!this.destroy.destroyed)
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'We couldn’t save that change. Check your details and try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
