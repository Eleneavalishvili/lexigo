import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, StaffCourse } from '../../core/content/content-api';
import { MediaApi, StaffMedia } from '../../core/content/media-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-media-library',
  imports: [RouterLink, ReactiveFormsModule, BrandLogo],
  templateUrl: './media-library.html',
  styleUrl: './media-library.scss',
})
export class MediaLibrary {
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(MediaApi);
  private readonly content = inject(ContentApi);
  private readonly router = inject(Router);
  private readonly builder = inject(FormBuilder).nonNullable;
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  private playbackTimer?: ReturnType<typeof setTimeout>;
  @ViewChild('audioPreview') private audioPreview?: ElementRef<HTMLAudioElement>;
  @ViewChild('fileInput') private fileInput?: ElementRef<HTMLInputElement>;
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly media = signal<readonly StaffMedia[]>([]);
  protected readonly page = signal(1);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly file = signal<File | null>(null);
  protected readonly playback = signal<{ id: string; url: string } | null>(null);
  protected readonly deleting = signal<string | null>(null);
  protected readonly reviewing = signal<string | null>(null);
  protected readonly uploadCourses = computed(() =>
    this.courses().filter((course) => course.canAuthor),
  );
  protected readonly form = this.builder.group({
    languageId: [0, [Validators.min(1)]],
    source: ['', [Validators.required, Validators.maxLength(2000)]],
    license: ['', [Validators.required, Validators.maxLength(1000)]],
  });
  protected readonly reviewComment = this.builder.control('', [
    Validators.required,
    Validators.maxLength(2000),
  ]);

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
        this.media.set([]);
        this.courses.set([]);
        this.stopPlayback();
        this.clearFile();
        this.form.reset();
        this.reviewComment.reset();
        this.deleting.set(null);
        this.reviewing.set(null);
        this.error.set('');
        this.notice.set('');
        this.busy.set(false);
        void this.router.navigate(['/account']);
      }
    });
    this.destroy.onDestroy(() => {
      this.generation++;
      this.stopPlayback();
      this.clearFile();
    });
    void this.load();
  }

  protected async load(page = this.page()): Promise<void> {
    await this.run(async () => {
      const [courses, rows] = await Promise.all([this.content.courses(), this.api.list(page)]);
      return () => {
        this.courses.set(courses);
        this.media.set(rows);
        this.page.set(page);
        this.stopPlayback();
        this.reviewing.set(null);
        this.reviewComment.reset();
        const first = courses.find((course) => course.canAuthor);
        if (!this.form.controls.languageId.value && first)
          this.form.controls.languageId.setValue(first.targetLanguageId);
      };
    });
  }

  protected chooseFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.error.set('');
    if (file && (file.size === 0 || file.size > 10 * 1024 * 1024)) {
      this.clearFile();
      this.error.set('Choose a non-empty WAV file up to 10 MiB.');
      return;
    }
    this.file.set(file);
  }

  protected async upload(): Promise<void> {
    const file = this.file();
    this.form.markAllAsTouched();
    if (!file || this.form.invalid) return;
    const input = this.form.getRawValue();
    await this.run(async () => {
      await this.api.upload(file, input.languageId, input.source, input.license);
      const rows = await this.api.list(1);
      return () => {
        this.media.set(rows);
        this.page.set(1);
        this.clearFile();
        this.form.controls.source.reset();
        this.form.controls.license.reset();
        this.notice.set('Audio uploaded. An independent language reviewer can now check it.');
      };
    });
  }

  protected async preview(item: StaffMedia): Promise<void> {
    this.stopPlayback();
    await this.run(async () => {
      const link = await this.api.preview(item.id);
      return () => {
        this.playback.set({ id: item.id, url: link.url });
        this.playbackTimer = setTimeout(
          () => this.stopPlayback(),
          Math.min(120, Math.max(1, link.expiresInSeconds)) * 1000,
        );
      };
    });
  }

  protected stopPlayback(): void {
    const player = this.audioPreview?.nativeElement;
    if (player) {
      player.pause();
      player.removeAttribute('src');
      player.load();
    }
    if (this.playbackTimer) clearTimeout(this.playbackTimer);
    this.playbackTimer = undefined;
    this.playback.set(null);
  }

  protected playbackError(): void {
    this.stopPlayback();
    this.error.set(
      'This playback link is unavailable or has expired. Choose Listen to recording to try again.',
    );
  }

  protected openReview(item: StaffMedia): void {
    if (!this.canReview(item)) return;
    this.reviewComment.reset();
    this.reviewing.set(item.id);
  }

  protected async review(item: StaffMedia, decision: 'approve' | 'reject'): Promise<void> {
    this.reviewComment.markAsTouched();
    if (this.reviewComment.invalid || !this.canReview(item) || this.reviewing() !== item.id) return;
    const comment = this.reviewComment.value;
    await this.run(async () => {
      await this.api.review(item.id, decision, comment);
      const rows = await this.api.list(this.page());
      return () => {
        this.media.set(rows);
        this.reviewComment.reset();
        this.reviewing.set(null);
        this.notice.set('Your review has been saved.');
      };
    });
  }

  protected async remove(item: StaffMedia): Promise<void> {
    if (this.deleting() !== item.id || !this.canDelete(item)) return;
    this.stopPlayback();
    await this.run(async () => {
      await this.api.remove(item.id);
      const rows = await this.api.list(this.page());
      return () => {
        this.media.set(rows);
        this.deleting.set(null);
        this.notice.set('Deletion requested. Unused audio will be removed from private storage.');
      };
    });
  }

  protected canReview(item: StaffMedia): boolean {
    return (
      item.storageState === 'Ready' &&
      !item.isVerified &&
      item.uploadedBy !== this.owner &&
      this.courses().some(
        (course) => course.targetLanguageId === item.languageId && course.canReview,
      )
    );
  }

  protected async finishDeletion(item: StaffMedia): Promise<void> {
    if (item.storageState !== 'Deleting' || !this.canManage(item)) return;
    this.stopPlayback();
    await this.run(async () => {
      await this.api.finishDeletion(item.id);
      const rows = await this.api.list(this.page());
      return () => {
        this.media.set(rows);
        this.notice.set('Recording removed from private storage.');
      };
    });
  }

  protected canDelete(item: StaffMedia): boolean {
    return item.storageState !== 'Deleting' && this.canManage(item);
  }

  protected canManage(item: StaffMedia): boolean {
    return (
      this.auth.user()?.roles.some((role) => ['Admin', 'SuperAdmin'].includes(role)) ||
      (item.uploadedBy === this.owner &&
        this.courses().some(
          (course) => course.targetLanguageId === item.languageId && course.canAuthor,
        ))
    );
  }

  protected language(id: number | null): string {
    return (
      this.courses().find((course) => course.targetLanguageId === id)?.title ?? 'Language audio'
    );
  }

  private clearFile(): void {
    this.file.set(null);
    if (this.fileInput) this.fileInput.nativeElement.value = '';
  }

  private async run(work: () => Promise<() => void>): Promise<void> {
    if (this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const apply = await work();
      if (generation === this.generation) apply();
    } catch (error) {
      if (generation === this.generation)
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'The audio library could not finish that request. Please try again.',
        );
    } finally {
      if (generation === this.generation) this.busy.set(false);
    }
  }
}
