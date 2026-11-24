import { Component, computed, input } from '@angular/core';

const portraits: Record<string, { skin: string; hair: string; shirt: string; long: boolean }> = {
  ava: { skin: '#e7ad85', hair: '#482d28', shirt: '#b44d38', long: true },
  leo: { skin: '#bd7e5c', hair: '#332b29', shirt: '#386c6c', long: false },
  sofia: { skin: '#f1c4a6', hair: '#70432e', shirt: '#b98537', long: true },
  luca: { skin: '#d99b74', hair: '#352a29', shirt: '#425d84', long: false },
  nina: { skin: '#ad7155', hair: '#302723', shirt: '#65734c', long: true },
  noah: { skin: '#e9b392', hair: '#9a5e37', shirt: '#99576a', long: false },
  maya: { skin: '#d39071', hair: '#3d2b2a', shirt: '#816b9b', long: true },
  alex: { skin: '#eac5ad', hair: '#614c3c', shirt: '#527b83', long: false },
};

@Component({
  selector: 'app-tutor-portrait',
  template: `<svg viewBox="0 0 160 160" role="img" [attr.aria-label]="label()">
    <rect x="1" y="1" width="158" height="158" rx="34" fill="#ede5d3" />
    <circle cx="122" cy="35" r="19" fill="#e8bf5b" opacity=".65" />
    @if (palette().long) {
      <path d="M42 74Q34 23 79 24Q124 21 120 82L127 117H36Z" [attr.fill]="palette().hair" />
    }
    <path d="M23 160Q26 114 62 110H98Q135 115 138 160" [attr.fill]="palette().shirt" />
    <path d="M68 98V115Q80 127 92 115V98" [attr.fill]="palette().skin" />
    <ellipse cx="80" cy="73" rx="34" ry="41" [attr.fill]="palette().skin" />
    <path
      d="M45 70Q39 28 76 25Q116 19 116 66L100 58L92 40Q77 61 45 59Z"
      [attr.fill]="palette().hair"
    />
    <path
      d="M59 72Q64 68 69 72M91 72Q96 68 101 72"
      fill="none"
      [attr.stroke]="palette().hair"
      stroke-width="3"
      stroke-linecap="round"
    />
    <ellipse cx="65" cy="80" rx="2.6" ry="3.3" fill="#263746" />
    <ellipse cx="96" cy="80" rx="2.6" ry="3.3" fill="#263746" />
    <path
      d="M80 81L77 91H83M68 98Q80 108 92 98"
      fill="none"
      stroke="#824d43"
      stroke-width="2.5"
      stroke-linecap="round"
    />
    <path d="M61 113L80 131L99 113" fill="none" stroke="#f7f0df" stroke-width="5" />
    <path d="M80 132V160" stroke="#f7f0df" stroke-width="2" opacity=".6" />
  </svg>`,
  styles:
    ':host { display: block; width: 100%; max-width: 160px; } svg { display: block; width: 100%; height: auto; }',
})
export class TutorPortrait {
  readonly variant = input('default');
  readonly label = input('Tutor illustration');
  protected readonly palette = computed(() => portraits[this.variant()] ?? portraits['ava']);
}
