import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, StaffCourse } from '../../core/content/content-api';
import {
  DialogueAuthoringApi,
  DialogueChoiceDraft,
  DialogueDraftDetail,
  DialogueDraftRow,
  DialogueNodeDraft,
} from '../../core/content/dialogue-authoring-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { ReviewedAudioPicker } from './reviewed-audio-picker';

@Component({
  selector: 'app-dialogue-workshop',
  imports: [RouterLink, ReactiveFormsModule, BrandLogo, ReviewedAudioPicker],
  templateUrl: './dialogue-workshop.html',
  styleUrls: ['./workbench.scss', './workbench-fields.scss', './dialogue-workshop.scss'],
})
export class DialogueWorkshop {
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(DialogueAuthoringApi);
  private readonly catalog = inject(ContentApi);
  private readonly builder = inject(FormBuilder).nonNullable;
  private readonly destroy = inject(DestroyRef);
  private readonly router = inject(Router);
  private owner = this.auth.user()?.id;
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly courseId = signal('');
  protected readonly languageId = computed(
    () => this.courses().find((course) => course.id === this.courseId())?.targetLanguageId ?? 0,
  );
  protected readonly rows = signal<readonly DialogueDraftRow[]>([]);
  protected readonly current = signal<DialogueDraftDetail | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly page = signal(1);
  protected readonly totalCount = signal(0);
  protected readonly audioNode = signal<number | null>(null);
  protected readonly admin = computed(
    () =>
      this.auth.user()?.roles.some((role) => role === 'Admin' || role === 'SuperAdmin') ?? false,
  );
  protected readonly canAuthor = computed(
    () =>
      this.admin() || !!this.courses().find((course) => course.id === this.courseId())?.canAuthor,
  );
  protected readonly canEdit = computed(
    () =>
      this.current()?.dialogue.status === 'Draft' &&
      (this.admin() ||
        (this.canAuthor() && this.current()?.dialogue.ownerId === this.auth.user()?.id)),
  );
  protected readonly canReview = computed(
    () =>
      this.auth.user()?.roles.includes('Reviewer') &&
      this.current()?.dialogue.ownerId !== this.auth.user()?.id,
  );
  protected readonly newDraft = this.builder.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    scenario: ['', [Validators.required, Validators.pattern(/^[a-z0-9][a-z0-9-]{0,39}$/)]],
    requiresPremium: [false],
  });
  protected readonly metadata = this.builder.group({
    title: ['', [Validators.required, Validators.maxLength(160)]],
    requiresPremium: [false],
  });
  protected readonly comment = this.builder.control('', [
    Validators.required,
    Validators.minLength(3),
    Validators.maxLength(2000),
  ]);
  protected readonly nodes = this.builder.array<ReturnType<DialogueWorkshop['nodeForm']>>([]);

  constructor() {
    effect(() => {
      const owner = this.auth.user()?.id;
      if (owner === this.owner) return;
      this.owner = owner;
      this.courses.set([]);
      this.rows.set([]);
      this.current.set(null);
      this.nodes.clear();
      this.metadata.reset();
      this.newDraft.reset();
      this.comment.reset();
      this.error.set('');
      this.notice.set('');
      void this.router.navigate(['/account']);
    });
    void this.load();
  }

  protected async load(): Promise<void> {
    await this.perform(async () => {
      const courses = await this.catalog.courses();
      return () => this.courses.set(courses);
    });
  }
  protected async selectCourse(id: string, page = 1): Promise<void> {
    await this.perform(async () => {
      const rows = await this.api.list(id, page);
      return () => {
        this.courseId.set(id);
        this.page.set(page);
        this.rows.set(rows.items);
        this.totalCount.set(rows.totalCount);
        this.current.set(null);
        this.nodes.clear();
      };
    });
  }
  protected async open(id: string): Promise<void> {
    await this.perform(async () => {
      const detail = await this.api.detail(id);
      return () => this.apply(detail);
    });
  }
  protected async create(): Promise<void> {
    if (!this.canAuthor() || this.newDraft.invalid || !this.courseId()) return;
    await this.perform(async () => {
      const data = this.newDraft.getRawValue();
      const { id } = await this.api.create(
        this.courseId(),
        data.title,
        data.scenario,
        data.requiresPremium,
      );
      const detail = await this.api.detail(id);
      const rows = await this.api.list(this.courseId(), 1);
      return () => {
        this.rows.set(rows.items);
        this.totalCount.set(rows.totalCount);
        this.page.set(1);
        this.apply(detail);
        this.newDraft.reset();
        this.notice.set('Draft created. Add at least two connected nodes.');
      };
    });
  }
  protected addNode(): void {
    if (this.nodes.length >= 40) return;
    this.nodes.push(
      this.nodeForm({
        key: crypto.randomUUID(),
        text: '',
        isStart: this.nodes.length === 0,
        isTerminal: false,
        choices: [],
      }),
    );
    this.nodes.markAsDirty();
  }
  protected addChoice(index: number): void {
    const choices = this.nodes.at(index).controls.choices;
    if (choices.length < 6)
      choices.push(this.choiceForm({ text: '', feedback: '', isCorrect: true, nextNodeKey: null }));
    this.nodes.markAsDirty();
  }
  protected async save(): Promise<void> {
    const current = this.current();
    this.metadata.markAllAsTouched();
    this.nodes.markAllAsTouched();
    if (!current || !this.canEdit() || this.metadata.invalid || this.nodes.invalid) return;
    await this.perform(async () => {
      const metadata = this.metadata.getRawValue();
      const nodes = this.nodes.getRawValue().map((node) => ({
        ...node,
        choices: node.choices.map((choice) => ({
          ...choice,
          nextNodeKey: choice.nextNodeKey || null,
        })),
      }));
      await this.api.save(
        current.dialogue.id,
        current.dialogue.editToken,
        metadata.title,
        metadata.requiresPremium,
        nodes,
      );
      const detail = await this.api.detail(current.dialogue.id);
      return () => {
        this.apply(detail);
        this.notice.set('Draft saved. Learner conversations were not changed.');
      };
    });
  }
  protected async transition(
    operation: 'submit' | 'approve' | 'return' | 'publish' | 'archive',
  ): Promise<void> {
    const current = this.current();
    this.comment.markAsTouched();
    if (!current || this.comment.invalid) return;
    await this.perform(async () => {
      await this.api.transition(
        current.dialogue.id,
        operation,
        current.dialogue.editToken,
        this.comment.value.trim(),
      );
      if (
        operation === 'return' &&
        !this.admin() &&
        current.dialogue.ownerId !== this.auth.user()?.id
      ) {
        const rows = await this.api.list(this.courseId(), this.page());
        return () => {
          this.rows.set(rows.items);
          this.totalCount.set(rows.totalCount);
          this.current.set(null);
          this.nodes.clear();
          this.notice.set('Returned to its author for changes.');
        };
      }
      const detail = await this.api.detail(current.dialogue.id);
      const rows = await this.api.list(this.courseId(), this.page());
      return () => {
        this.rows.set(rows.items);
        this.totalCount.set(rows.totalCount);
        this.apply(detail);
        this.notice.set('Dialogue action saved.');
      };
    });
  }
  protected async revision(): Promise<void> {
    const current = this.current();
    if (!current || !this.canAuthor()) return;
    await this.perform(async () => {
      const { id } = await this.api.revision(current.dialogue.id);
      const detail = await this.api.detail(id);
      const rows = await this.api.list(this.courseId(), 1);
      return () => {
        this.page.set(1);
        this.rows.set(rows.items);
        this.totalCount.set(rows.totalCount);
        this.apply(detail);
      };
    });
  }
  private apply(detail: DialogueDraftDetail): void {
    this.audioNode.set(null);
    this.current.set(detail);
    this.metadata.reset({
      title: detail.dialogue.title,
      requiresPremium: detail.dialogue.requiresPremium,
    });
    this.nodes.clear();
    for (const node of detail.nodes) this.nodes.push(this.nodeForm(node));
    this.nodes.markAsPristine();
    this.comment.reset();
  }
  private nodeForm(node: DialogueNodeDraft) {
    return this.builder.group({
      key: [node.key],
      audioMediaId: [node.audioMediaId ?? (null as string | null)],
      text: [node.text, [Validators.required, Validators.maxLength(4000)]],
      isStart: [node.isStart],
      isTerminal: [node.isTerminal],
      choices: this.builder.array(node.choices.map((choice) => this.choiceForm(choice))),
    });
  }
  private choiceForm(choice: DialogueChoiceDraft) {
    return this.builder.group({
      text: [choice.text, [Validators.required, Validators.maxLength(1000)]],
      feedback: [choice.feedback, [Validators.required, Validators.maxLength(4000)]],
      isCorrect: [choice.isCorrect],
      nextNodeKey: [choice.nextNodeKey ?? ''],
    });
  }
  private async perform(operation: () => Promise<() => void>): Promise<void> {
    if (this.busy()) return;
    const owner = this.auth.user()?.id;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const apply = await operation();
      if (!this.destroy.destroyed && this.auth.user()?.id === owner) apply();
    } catch (error) {
      if (!this.destroy.destroyed && this.auth.user()?.id === owner)
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'Could not complete this action. Reload and try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
