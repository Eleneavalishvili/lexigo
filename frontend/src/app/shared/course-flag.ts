import { Component, input } from '@angular/core';

@Component({
  selector: 'app-course-flag',
  host: { 'aria-hidden': 'true' },
  template: `
    <svg viewBox="0 0 32 24" width="32" height="24" focusable="false">
      <rect width="32" height="24" fill="#fff" />
      @switch (language()) {
        @case ('es') {
          <path d="M0 0h32v24H0Z" fill="#b92835" />
          <path d="M0 6h32v12H0Z" fill="#edc349" />
        }
        @case ('it') {
          <path d="M0 0h11v24H0Z" fill="#27825b" />
          <path d="M21 0h11v24H21Z" fill="#bd383d" />
        }
        @case ('de') {
          <path d="M0 0h32v8H0Z" fill="#292a30" />
          <path d="M0 8h32v8H0Z" fill="#bc3637" />
          <path d="M0 16h32v8H0Z" fill="#edc349" />
        }
        @case ('ru') {
          <path d="M0 8h32v8H0Z" fill="#315f9c" />
          <path d="M0 16h32v8H0Z" fill="#bd383d" />
        }
        @default {
          <circle cx="16" cy="12" r="8" fill="none" stroke="currentColor" />
          <path d="M8 12h16M16 4v16" stroke="currentColor" />
        }
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex-shrink: 0;
    }
    svg {
      border-radius: 3px;
      overflow: hidden;
      box-shadow: 0 0 0 1px var(--line);
    }
  `,
})
export class CourseFlag {
  readonly language = input('');
}
