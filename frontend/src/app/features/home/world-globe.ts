import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  NgZone,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { geoArea, geoContains, geoGraticule10, geoOrthographic, geoPath } from 'd3-geo';
import type { Feature, MultiPolygon, Polygon } from 'geojson';

interface Place {
  id: string;
  name: string;
  center: [number, number];
  languages: string[];
}

type CountryShape = Feature<Polygon | MultiPolygon, null>;

@Component({
  selector: 'app-world-globe',
  templateUrl: './world-globe.html',
  styleUrl: './world-globe.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorldGlobe {
  private readonly zone = inject(NgZone);
  private readonly destroy = inject(DestroyRef);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('map');
  private readonly projection = geoOrthographic().scale(230).translate([250, 250]).precision(0.8);
  private readonly grid = geoGraticule10();
  private readonly sphere = { type: 'Sphere' } as const;
  private readonly abort = new AbortController();
  private context?: CanvasRenderingContext2D;
  private shapes: CountryShape[] = [];
  private byId = new Map<string, Place>();
  private smallPlaces: Place[] = [];
  private rotation: [number, number] = [0, -20];
  private gesture?: { id: number; x: number; y: number; rotation: [number, number] };
  private frame = 0;
  private previousTime = 0;
  protected readonly places = signal<Place[]>([]);
  protected readonly search = signal('');
  protected readonly matchingPlaces = computed(() => {
    const query = this.search().trim().toLocaleLowerCase('en');
    return this.places().filter(
      (place) =>
        place.name.toLocaleLowerCase('en').includes(query) ||
        place.languages.some((language) => language.toLocaleLowerCase('en').includes(query)),
    );
  });
  protected readonly selected = signal<Place | undefined>(undefined);
  protected readonly spinning = signal(false);
  protected readonly direction = signal(1);
  protected readonly status = signal<'idle' | 'loading' | 'ready' | 'error'>('idle');

  constructor() {
    this.destroy.onDestroy(() => {
      this.abort.abort();
      cancelAnimationFrame(this.frame);
    });
  }

  protected async open(): Promise<void> {
    this.status.set('loading');
    try {
      const response = await fetch('world-languages.json', { signal: this.abort.signal });
      if (!response.ok) throw new Error('Map unavailable');
      const data: { places: Place[]; shapes: CountryShape[] } = await response.json();
      if (this.destroy.destroyed) return;
      this.context = this.canvas().nativeElement.getContext('2d') ?? undefined;
      this.shapes = data.shapes.map((shape) => this.orient(shape));
      this.byId = new Map(data.places.map((place) => [place.id, place]));
      this.byId.set('CYN', this.byId.get('CYP')!);
      this.byId.set('SOL', this.byId.get('SOM')!);
      const mapped = new Set(this.shapes.map((shape) => this.byId.get(String(shape.id))?.id));
      this.smallPlaces = data.places.filter((place) => !mapped.has(place.id));
      this.places.set(data.places);
      this.status.set('ready');
      this.draw();
      if (
        this.context &&
        !matchMedia('(prefers-reduced-motion: reduce)').matches &&
        document.documentElement.dataset['reduceMotion'] !== 'true'
      )
        this.toggleSpin();
    } catch {
      if (!this.destroy.destroyed) this.status.set('error');
    }
  }

  private orient(shape: CountryShape): CountryShape {
    // D3 expects small exterior rings to wind clockwise on the sphere.
    if (geoArea(shape) <= 2 * Math.PI) return shape;
    const geometry = shape.geometry;
    const reverse = (polygon: Polygon['coordinates']) => polygon.map((ring) => [...ring].reverse());
    return {
      ...shape,
      geometry:
        geometry.type === 'Polygon'
          ? { type: 'Polygon', coordinates: reverse(geometry.coordinates) }
          : { type: 'MultiPolygon', coordinates: geometry.coordinates.map(reverse) },
    };
  }

  protected toggleSpin(): void {
    if (this.spinning()) {
      this.stop();
      return;
    }
    this.spinning.set(true);
    this.previousTime = 0;
    this.zone.runOutsideAngular(() => {
      this.frame = requestAnimationFrame(this.spin);
    });
  }

  private readonly spin = (time: number): void => {
    if (this.previousTime) {
      const elapsed = Math.min(50, time - this.previousTime);
      this.rotation[0] = (this.rotation[0] + elapsed * 0.012 * this.direction()) % 360;
      this.draw();
    }
    this.previousTime = time;
    this.frame = requestAnimationFrame(this.spin);
  };

  private stop(): void {
    cancelAnimationFrame(this.frame);
    this.spinning.set(false);
  }

  protected turn(degrees: number): void {
    this.stop();
    this.rotation[0] = (this.rotation[0] + degrees) % 360;
    this.draw();
  }

  protected choose(id: string): void {
    this.stop();
    const place = this.byId.get(id);
    this.selected.set(place);
    if (place) this.rotation = [-place.center[0], -place.center[1]];
    this.draw();
  }

  protected grab(event: PointerEvent, canvas: HTMLCanvasElement): void {
    if (this.gesture || event.button !== 0) return;
    this.stop();
    this.gesture = {
      id: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      rotation: [...this.rotation],
    };
    canvas.setPointerCapture(event.pointerId);
  }

  protected move(event: PointerEvent, canvas: HTMLCanvasElement): void {
    const gesture = this.gesture;
    if (!gesture || gesture.id !== event.pointerId) return;
    const scale = 180 / canvas.clientWidth;
    this.rotation = [
      (gesture.rotation[0] + (event.clientX - gesture.x) * scale) % 360,
      Math.max(-90, Math.min(90, gesture.rotation[1] - (event.clientY - gesture.y) * scale)),
    ];
    this.draw();
  }

  protected release(event: PointerEvent, canvas: HTMLCanvasElement): void {
    const gesture = this.gesture;
    if (!gesture || gesture.id !== event.pointerId) return;
    this.gesture = undefined;
    if (
      event.type !== 'pointerup' ||
      Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) > 5
    )
      return;
    this.rotation = gesture.rotation;
    this.projection.rotate(this.rotation);
    const bounds = canvas.getBoundingClientRect();
    const point: [number, number] = [
      ((event.clientX - bounds.left) / bounds.width) * 500,
      ((event.clientY - bounds.top) / bounds.height) * 500,
    ];
    if (Math.hypot(point[0] - 250, point[1] - 250) > 230) return;
    const coordinate = this.projection.invert?.(point);
    if (!coordinate) return;
    const marker = this.smallPlaces.find((place) => {
      const position = this.visiblePoint(place);
      return position && Math.hypot(position[0] - point[0], position[1] - point[1]) < 5;
    });
    const shape = this.shapes.find((item) => geoContains(item, coordinate));
    this.selected.set(marker ?? (shape ? this.byId.get(String(shape.id)) : undefined));
    this.draw();
  }

  private visiblePoint(place: Place): [number, number] | null {
    const radians = Math.PI / 180;
    const latitude = place.center[1] * radians;
    const center = -this.rotation[1] * radians;
    const difference = (place.center[0] + this.rotation[0]) * radians;
    const facing =
      Math.sin(latitude) * Math.sin(center) +
      Math.cos(latitude) * Math.cos(center) * Math.cos(difference);
    return facing > 0 ? this.projection(place.center) : null;
  }

  private draw(): void {
    const ctx = this.context;
    if (!ctx) return;
    ctx.setTransform(2, 0, 0, 2, 0, 0);
    ctx.clearRect(0, 0, 500, 500);
    this.projection.rotate(this.rotation);
    const path = geoPath(this.projection, ctx);
    const ocean = ctx.createRadialGradient(180, 145, 20, 250, 250, 295);
    ocean.addColorStop(0, '#365d80');
    ocean.addColorStop(1, '#142e4a');
    ctx.beginPath();
    path(this.sphere);
    ctx.fillStyle = ocean;
    ctx.fill();
    ctx.beginPath();
    path(this.grid);
    ctx.strokeStyle = '#ffffff26';
    ctx.lineWidth = 0.5;
    ctx.stroke();
    for (const shape of this.shapes) {
      ctx.beginPath();
      path(shape);
      ctx.fillStyle =
        this.byId.get(String(shape.id))?.id === this.selected()?.id ? '#f1bd4b' : '#becbb0';
      ctx.fill();
      ctx.strokeStyle = '#213e62';
      ctx.lineWidth = 0.6;
      ctx.stroke();
    }
    for (const place of this.smallPlaces) {
      const point = this.visiblePoint(place);
      if (!point) continue;
      ctx.beginPath();
      ctx.arc(point[0], point[1], place.id === this.selected()?.id ? 4 : 1.5, 0, 2 * Math.PI);
      ctx.fillStyle = place.id === this.selected()?.id ? '#f1bd4b' : '#f7f1e3';
      ctx.fill();
    }
  }
}
