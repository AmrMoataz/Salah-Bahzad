import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { HomeAccent, HomeIconName, accentBg, accentFg, homeIconSvg } from '../home-presentation';

/**
 * A single KPI "widget" stat-card on the Home dashboard — matches the Student Portal Home mock: a
 * top row with the label and a tinted **rounded-square** icon, a large value, and a muted caption.
 * Not clickable (no `route`) and the icon is decorative (`aria-hidden`); the label is the
 * accessible name.
 */
@Component({
  selector: 'sb-home-kpi',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="kpi">
      <div class="kpi__top">
        <span class="kpi__label">{{ label() }}</span>
        <span
          class="kpi__icon"
          aria-hidden="true"
          [style.background]="bg()"
          [style.color]="fg()"
          [innerHTML]="iconHtml()"
        ></span>
      </div>
      <div class="kpi__value">{{ value() }}</div>
      @if (caption()) {
        <div class="kpi__caption">{{ caption() }}</div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; height: 100%; }
    .kpi {
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      box-shadow: var(--sb-shadow-sm);
      padding: var(--sb-space-5);
      display: flex;
      flex-direction: column;
      gap: var(--sb-space-2);
      height: 100%;
    }
    .kpi__top { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--sb-space-3); }
    .kpi__label { font-size: var(--sb-label-lg-size); font-weight: 600; color: var(--sb-text-muted); }
    .kpi__icon {
      width: 38px; height: 38px; border-radius: var(--sb-radius-md);
      display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0;
    }
    .kpi__value {
      margin-top: var(--sb-space-1);
      font-size: var(--sb-display-md-size); font-weight: 800; color: var(--sb-text);
      line-height: 1; font-variant-numeric: tabular-nums;
    }
    .kpi__caption { font-size: var(--sb-body-sm-size); color: var(--sb-text-subtle); font-weight: 600; }
  `],
})
export class HomeKpiCardComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly caption = input<string>('');
  readonly icon = input.required<HomeIconName>();
  readonly accent = input.required<HomeAccent>();

  readonly #sanitizer = inject(DomSanitizer);

  readonly bg = computed(() => accentBg(this.accent()));
  readonly fg = computed(() => accentFg(this.accent()));
  readonly iconHtml = computed<SafeHtml>(() =>
    this.#sanitizer.bypassSecurityTrustHtml(homeIconSvg(this.icon(), 20)),
  );
}
