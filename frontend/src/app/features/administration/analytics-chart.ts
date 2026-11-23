import { Component, ElementRef, afterRenderEffect, inject, input, viewChild } from '@angular/core';
import {
  BarController,
  BarElement,
  CategoryScale,
  Chart,
  Legend,
  LinearScale,
  Tooltip,
} from 'chart.js';
import { PreferenceStore } from '../../core/accounts/preference-store';

Chart.register(BarController, BarElement, CategoryScale, LinearScale, Legend, Tooltip);

export interface AnalyticsSeries {
  label: string;
  values: number[];
}

@Component({
  selector: 'app-analytics-chart',
  template: `
    <figure>
      <figcaption>{{ title() }}</figcaption>
      <div class="chart" [style.height.px]="100 + labels().length * 48">
        <canvas #canvas aria-hidden="true"></canvas>
      </div>
    </figure>
  `,
  styles: `
    figure {
      margin: 1rem 0 0;
      padding: 0.85rem;
      border: 1px solid var(--line);
      border-radius: 0.8rem;
      background: var(--surface);
    }
    figcaption {
      font-weight: 700;
      margin-bottom: 0.5rem;
    }
    .chart {
      position: relative;
    }
  `,
})
export class AnalyticsChart {
  readonly title = input.required<string>();
  readonly labels = input.required<string[]>();
  readonly series = input.required<AnalyticsSeries[]>();
  readonly percentage = input(false);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly preferences = inject(PreferenceStore);

  constructor() {
    afterRenderEffect((onCleanup) => {
      const canvas = this.canvas().nativeElement;
      const context = canvas.getContext('2d');
      if (!context) return;

      this.preferences.preferences();
      const palette = getComputedStyle(canvas);
      const ink = palette.getPropertyValue('--ink').trim();
      const red = palette.getPropertyValue('--red').trim();
      const line = palette.getPropertyValue('--line').trim();
      const percentage = this.percentage();
      const series = this.series();
      const chart = new Chart(context, {
        type: 'bar',
        data: {
          labels: this.labels(),
          datasets: series.map((item, index) => ({
            label: item.label,
            data: item.values,
            backgroundColor: index === 0 ? ink : red,
            maxBarThickness: 20,
          })),
        },
        options: {
          animation: false,
          maintainAspectRatio: false,
          indexAxis: 'y',
          plugins: {
            legend: { display: series.length > 1, labels: { color: ink } },
            tooltip: {
              callbacks: {
                label: (item) =>
                  `${item.dataset.label}: ${item.formattedValue}${percentage ? '%' : ''}`,
              },
            },
          },
          scales: {
            x: {
              beginAtZero: true,
              max: percentage ? 100 : undefined,
              grid: { color: line },
              ticks: {
                color: ink,
                precision: 0,
                callback: (value) => (percentage ? `${value}%` : value),
              },
            },
            y: { grid: { display: false }, ticks: { color: ink, autoSkip: false } },
          },
        },
      });
      onCleanup(() => chart.destroy());
    });
  }
}
