import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  CatalogApi,
  CatalogCourse,
  CatalogLesson,
  CatalogUnit,
} from '../../core/content/catalog-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-catalog',
  imports: [RouterLink, BrandLogo],
  templateUrl: './catalog.html',
  styleUrl: './catalog.scss',
})
export class Catalog {
  private readonly api = inject(CatalogApi);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  protected readonly courses = signal<readonly CatalogCourse[]>([]);
  protected readonly units = signal<readonly CatalogUnit[]>([]);
  protected readonly lessons = signal<readonly CatalogLesson[]>([]);
  protected readonly selectedCourse = signal<CatalogCourse | null>(null);
  protected readonly selectedUnit = signal<CatalogUnit | null>(null);
  protected readonly selectedLesson = signal<CatalogLesson | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  constructor() {
    void this.load();
  }
  protected async load(): Promise<void> {
    await this.fetch(async () => {
      const result = await this.api.courses();
      return () => this.courses.set(result);
    });
  }
  protected async chooseCourse(course: CatalogCourse): Promise<void> {
    this.selectedCourse.set(course);
    this.selectedUnit.set(null);
    this.selectedLesson.set(null);
    this.units.set([]);
    this.lessons.set([]);
    await this.fetch(async () => {
      const result = await this.api.units(course.id);
      return () => this.units.set(result);
    });
  }
  protected async chooseUnit(unit: CatalogUnit): Promise<void> {
    this.selectedUnit.set(unit);
    this.selectedLesson.set(null);
    this.lessons.set([]);
    await this.fetch(async () => {
      const result = await this.api.lessons(unit.id);
      return () => this.lessons.set(result);
    });
  }
  protected async retry(): Promise<void> {
    const unit = this.selectedUnit();
    const course = this.selectedCourse();
    if (unit) await this.chooseUnit(unit);
    else if (course) await this.chooseCourse(course);
    else await this.load();
  }
  private async fetch(operation: () => Promise<() => void>): Promise<void> {
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    try {
      const apply = await operation();
      if (!this.destroy.destroyed && generation === this.generation) apply();
    } catch {
      if (!this.destroy.destroyed && generation === this.generation)
        this.error.set('We couldn’t open this part of the catalog. Please try again.');
    } finally {
      if (!this.destroy.destroyed && generation === this.generation) this.loading.set(false);
    }
  }
}
