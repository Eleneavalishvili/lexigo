import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PreferenceStore } from '../../core/accounts/preference-store';
import { AnalyticsChart } from './analytics-chart';

const chart = vi.hoisted(() => ({ create: vi.fn(), destroy: vi.fn() }));

vi.mock('chart.js', () => ({
  BarController: {},
  BarElement: {},
  CategoryScale: {},
  Legend: {},
  LinearScale: {},
  Tooltip: {},
  Chart: class {
    static register(): void {}

    constructor(context: unknown, config: unknown) {
      chart.create(context, config);
    }

    destroy(): void {
      chart.destroy();
    }
  },
}));

describe('Analytics charts', () => {
  it('replaces the canvas chart when filtered values change and releases it on exit', async () => {
    const context = {} as CanvasRenderingContext2D;
    const getContext = vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(context);
    try {
      TestBed.configureTestingModule({
        providers: [{ provide: PreferenceStore, useValue: { preferences: signal(null) } }],
      });
      const fixture = TestBed.createComponent(AnalyticsChart);
      fixture.componentRef.setInput('title', 'Error rates');
      fixture.componentRef.setInput('labels', ['Translation']);
      fixture.componentRef.setInput('series', [{ label: 'Error rate', values: [25] }]);
      fixture.componentRef.setInput('percentage', true);
      await fixture.whenStable();

      expect(chart.create).toHaveBeenLastCalledWith(
        context,
        expect.objectContaining({
          data: expect.objectContaining({
            datasets: [expect.objectContaining({ data: [25] })],
          }),
          options: expect.objectContaining({
            scales: expect.objectContaining({ x: expect.objectContaining({ max: 100 }) }),
          }),
        }),
      );

      fixture.componentRef.setInput('series', [{ label: 'Error rate', values: [50] }]);
      await fixture.whenStable();
      expect(chart.destroy).toHaveBeenCalledTimes(1);
      expect(chart.create.mock.lastCall?.[1].data.datasets[0].data).toEqual([50]);
      fixture.destroy();
      expect(chart.destroy).toHaveBeenCalledTimes(2);
    } finally {
      getContext.mockRestore();
    }
  });
});
