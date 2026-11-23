import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../../app.routes';

@Component({ template: '<p>Course destination</p>' })
class CourseDestination {}

describe('Unknown web addresses', () => {
  it('shows a recovery screen without echoing query data and navigates back to courses', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'courses', component: CourseDestination },
          ...routes.filter((route) => route.path === '**'),
        ]),
      ],
    });
    const harness = await RouterTestingHarness.create('/missing/old-link?code=private-query-value');
    const host = harness.routeNativeElement!;
    expect(host.querySelector('h1')?.textContent).toContain('back on your path');
    expect(host.textContent).not.toContain('private-query-value');
    expect(TestBed.inject(Router).url).toBe('/missing/old-link?code=private-query-value');
    host.querySelector<HTMLAnchorElement>('a[href="/courses"]')!.click();
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/courses');
    expect(harness.routeNativeElement?.textContent).toContain('Course destination');
  });
});
