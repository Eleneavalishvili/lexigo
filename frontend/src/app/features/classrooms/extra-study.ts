import { HttpErrorResponse } from '@angular/common/http';
import { CurrencyPipe } from '@angular/common';
import { TutorPortrait } from '../../shared/tutor-portrait';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  ExtraStudyApi,
  ExtraStudyRequest,
  ExtraStudyTeacher,
} from '../../core/classrooms/extra-study-api';
import { CatalogApi, CatalogCourse } from '../../core/content/catalog-api';

@Component({
  selector: 'app-extra-study',
  imports: [RouterLink, CurrencyPipe, TutorPortrait],
  templateUrl: './extra-study.html',
  styleUrl: './classrooms.scss',
})
export class ExtraStudy {
  private readonly auth = inject(AuthStore);
  private readonly api = inject(ExtraStudyApi);
  private readonly catalog = inject(CatalogApi);
  protected readonly canTeach = computed(
    () => this.auth.user()?.roles.includes('Teacher') ?? false,
  );
  protected readonly courses = signal<readonly CatalogCourse[]>([]);
  protected readonly enabled = signal(new Set<string>());
  protected readonly teacherSettingsReady = signal(false);
  protected readonly courseId = signal('');
  protected readonly teachers = signal<ExtraStudyTeacher[]>([]);
  protected readonly cursor = signal<string | undefined>(undefined);
  protected readonly directoryState = signal<'idle' | 'loading' | 'ready' | 'denied' | 'error'>(
    'idle',
  );
  protected readonly notice = signal('');
  protected readonly error = signal('');
  protected readonly saving = signal(false);
  protected readonly requests = signal<ExtraStudyRequest[]>([]);
  protected readonly closing = signal<string | undefined>(undefined);
  protected readonly requestCursor = signal<string | undefined>(undefined);
  protected readonly requestsLoading = signal(false);
  protected readonly requestLoadError = signal(false);
  protected readonly requestedOffers = computed(
    () =>
      new Set(
        this.requests()
          .filter((item) => !item.incoming)
          .map((item) => item.offerId),
      ),
  );
  private directoryRequest = 0;
  private requestListGeneration = 0;

  constructor() {
    effect(() => {
      const owner = this.auth.user()?.id;
      const canTeach = this.canTeach();
      this.courseId.set('');
      this.teachers.set([]);
      this.cursor.set(undefined);
      this.directoryState.set('idle');
      this.enabled.set(new Set());
      this.teacherSettingsReady.set(false);
      this.notice.set('');
      this.error.set('');
      this.saving.set(false);
      this.requests.set([]);
      this.closing.set(undefined);
      this.requestCursor.set(undefined);
      this.requestLoadError.set(false);
      this.requestsLoading.set(false);
      if (owner) {
        void this.load(owner, canTeach);
        void this.loadRequests();
      }
    });
  }

  protected async loadRequests(after?: string): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner) return;
    const generation = ++this.requestListGeneration;
    const current = () =>
      this.auth.user()?.id === owner && this.requestListGeneration === generation;
    this.requestsLoading.set(true);
    this.requestLoadError.set(false);
    try {
      const page = await this.api.requests(after);
      if (!current()) return;
      this.requests.update((items) => (after ? [...items, ...page] : page));
      this.requestCursor.set(page.length === 24 ? page.at(-1)?.id : undefined);
    } catch {
      if (current()) this.requestLoadError.set(true);
    } finally {
      if (current()) this.requestsLoading.set(false);
    }
  }

  protected requestTeacher(offerId: string): Promise<void> {
    return this.saveRequest(() => this.api.request(offerId), 'Your request is with the teacher.');
  }

  protected closeRequest(id: string): Promise<void> {
    this.closing.set(undefined);
    return this.saveRequest(() => this.api.close(id), 'Study request closed.');
  }

  protected decide(id: string, accepted: boolean): Promise<void> {
    return this.saveRequest(
      () => this.api.decide(id, accepted),
      accepted ? 'Request accepted.' : 'Request declined.',
    );
  }

  private async saveRequest(action: () => Promise<unknown>, message: string): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.saving()) return;
    this.saving.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await action();
      if (this.auth.user()?.id !== owner) return;
      this.notice.set(message);
      await this.loadRequests();
    } catch (error) {
      if (this.auth.user()?.id !== owner) return;
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 409
          ? 'This request can no longer be changed. Check its current status.'
          : 'The request could not be saved. Check your access and try again.',
      );
      await this.loadRequests();
    } finally {
      if (this.auth.user()?.id === owner) this.saving.set(false);
    }
  }

  private async load(owner: string, canTeach: boolean): Promise<void> {
    try {
      const courses = await this.catalog.courses();
      if (this.auth.user()?.id !== owner) return;
      this.courses.set(courses);
      await this.showTeachers('');
      if (this.auth.user()?.id !== owner) return;
      if (!canTeach) return;
      const offers = await this.api.availability();
      if (this.auth.user()?.id !== owner || !this.canTeach()) return;
      this.enabled.set(
        new Set(offers.filter((offer) => offer.enabled).map((offer) => offer.courseId)),
      );
      this.teacherSettingsReady.set(true);
    } catch {
      if (this.auth.user()?.id === owner)
        this.error.set('Extra study settings could not load. Refresh to try again.');
    }
  }

  protected async showTeachers(courseId: string, after?: string): Promise<void> {
    const owner = this.auth.user()?.id;
    const request = ++this.directoryRequest;
    const isCurrent = () => request === this.directoryRequest && this.auth.user()?.id === owner;
    this.courseId.set(courseId);
    if (!after) this.teachers.set([]);
    this.cursor.set(undefined);
    this.directoryState.set('loading');
    if (!owner) return;
    try {
      const teachers = await this.api.teachers(courseId, after);
      if (!isCurrent()) return;
      this.teachers.update((current) => [...current, ...teachers]);
      this.cursor.set(teachers.length === 24 ? teachers.at(-1)?.id : undefined);
      this.directoryState.set('ready');
    } catch (error) {
      if (!isCurrent()) return;
      this.teachers.set([]);
      this.directoryState.set(
        error instanceof HttpErrorResponse && error.status === 403 ? 'denied' : 'error',
      );
    }
  }

  protected async toggleAvailability(courseId: string): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || !this.teacherSettingsReady() || this.saving()) return;
    this.saving.set(true);
    this.notice.set('');
    this.error.set('');
    const enabled = !this.enabled().has(courseId);
    try {
      await this.api.setAvailability(courseId, enabled);
      if (this.auth.user()?.id !== owner || !this.canTeach()) return;
      const next = new Set(this.enabled());
      if (enabled) next.add(courseId);
      else next.delete(courseId);
      this.enabled.set(next);
      this.notice.set(
        enabled ? 'You’re now listed for this course.' : 'You’re no longer listed for this course.',
      );
    } catch {
      if (this.auth.user()?.id === owner)
        this.error.set('Availability could not be saved. Refresh to check its current status.');
    } finally {
      if (this.auth.user()?.id === owner) this.saving.set(false);
    }
  }
}
