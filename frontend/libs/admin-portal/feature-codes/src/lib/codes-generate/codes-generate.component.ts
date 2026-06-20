import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import {
  ButtonComponent,
  CardComponent,
  ComboboxComponent,
  SelectOption,
  ToastService,
} from '@sb/shared/ui';
import { CodeBatchDto } from '../data-access/code.models';
import { CodeService } from '../data-access/code.service';
import { egp } from '../code.presentation';

/**
 * Codes generate (FR-ADM-COD-001, mockup `scrCodesGenerate`). Teacher-only: mint a batch of enrollment
 * codes with Excel/CSV export. Two columns — left "Batch settings" (session combo, value pre-filled from
 * the session's current price, quantity) drives generate (#2); the right card flips Preview → "Batch
 * ready" on success, with a "Download Excel" that re-exports the just-minted batch (#4). Assistants get
 * the role gate (the server enforces `CodesGenerate` regardless).
 */
@Component({
  selector: 'sb-codes-generate',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, CardComponent, ComboboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="cg__back" routerLink="/codes">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M19 12H5M12 19l-7-7 7-7"/>
      </svg>
      Back to codes
    </a>

    <div class="cg__head">
      <h1 class="cg__title">Generate codes</h1>
      <p class="cg__subtitle">Mint a batch of enrollment codes with Excel export</p>
    </div>

    @if (!canGenerate()) {
      <div class="cg__gate">
        <span class="cg__gate-icon" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 10 0v4"/>
          </svg>
        </span>
        <h3 class="cg__gate-title">Teacher access required</h3>
        <p class="cg__gate-text">Generating codes is a Teacher-only action. Switch to the Teacher role to mint a batch.</p>
      </div>
    } @else {
      <div class="cg__grid" [formGroup]="form">
        <!-- Batch settings -->
        <sb-card title="Batch settings">
          <div class="cg__fields">
            <div>
              <label class="cg__label" for="cg-session">Session</label>
              <sb-combobox
                inputId="cg-session"
                formControlName="sessionId"
                [options]="sessionOptions()"
                placeholder="Choose a session…"
                emptyText="No sessions"
              />
            </div>
            <div class="cg__row">
              <div>
                <label class="cg__label" for="cg-value">Value (EGP)</label>
                <input id="cg-value" type="number" min="0" class="cg__input" formControlName="value" />
              </div>
              <div>
                <label class="cg__label" for="cg-qty">Quantity</label>
                <input id="cg-qty" type="number" min="1" max="1000" class="cg__input" formControlName="quantity" />
              </div>
            </div>
            <sb-button variant="primary" [disabled]="!canSubmit()" [loading]="submitting()" (clicked)="submit()">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M3 9a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2 2 2 0 0 0 0 4 2 2 0 0 1-2 2H5a2 2 0 0 1-2-2 2 2 0 0 0 0-4zM9 7v10"/>
              </svg>
              Generate {{ quantityLabel() }} codes
            </sb-button>
          </div>
        </sb-card>

        <!-- Preview → Batch ready -->
        @if (batch(); as b) {
          <sb-card title="Batch ready">
            <div class="cg__done">
              <span class="cg__check" aria-hidden="true">
                <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M20 6L9 17l-5-5"/>
                </svg>
              </span>
              <h3 class="cg__done-title">{{ b.quantity }} codes minted</h3>
              <p class="cg__done-sub">{{ b.sessionTitle }} · {{ value(b.value) }} each</p>
              <sb-button variant="accent" [loading]="downloading()" (clicked)="download(b)">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
                </svg>
                Download Excel
              </sb-button>
            </div>
          </sb-card>
        } @else {
          <sb-card title="Preview">
            <p class="cg__preview">
              You are about to mint <strong>{{ quantityLabel() }} codes</strong> worth
              <strong>{{ value(valueNumber()) }}</strong> each for
              <strong>{{ selectedSessionTitle() || '—' }}</strong>. On success you can download an Excel of
              the batch (serials are unique and tenant-isolated).
            </p>
          </sb-card>
        }
      </div>
    }
  `,
  styles: [`
    :host { display: block; }

    .cg__back { display: inline-flex; align-items: center; gap: var(--sb-space-2); margin-bottom: var(--sb-space-4); color: var(--sb-text-muted); font-size: var(--sb-body-md-size); font-weight: 700; text-decoration: none; }
    .cg__back:hover { color: var(--sb-primary); }

    .cg__head { margin-bottom: var(--sb-space-5); }
    .cg__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .cg__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .cg__grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: var(--sb-space-4); align-items: start; }
    @media (max-width: 760px) { .cg__grid { grid-template-columns: 1fr; } }

    .cg__fields { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .cg__row { display: grid; grid-template-columns: 1fr 1fr; gap: var(--sb-space-3); }
    .cg__label { display: block; margin-bottom: var(--sb-space-2); font-size: var(--sb-body-sm-size); font-weight: 600; color: var(--sb-text); }
    .cg__input {
      width: 100%; height: 40px; padding: 0 12px;
      border: 1px solid var(--sb-border-strong); border-radius: var(--sb-radius-md);
      background: var(--sb-surface); font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); color: var(--sb-text);
      outline: none; transition: border-color var(--sb-timing-fast) var(--sb-easing-standard), box-shadow var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .cg__input:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }

    .cg__preview { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); line-height: 1.7; }
    .cg__preview strong { color: var(--sb-text); }

    .cg__done { text-align: center; padding: var(--sb-space-2) 0; }
    .cg__check { display: inline-flex; align-items: center; justify-content: center; width: 56px; height: 56px; margin: 0 auto var(--sb-space-3); border-radius: var(--sb-radius-circle); background: var(--sb-success-bg); color: var(--sb-success-fg); }
    .cg__done-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 800; color: var(--sb-text); }
    .cg__done-sub { margin: 0 0 var(--sb-space-4); color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .cg__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .cg__gate-icon { display: inline-flex; align-items: center; justify-content: center; width: 56px; height: 56px; margin: 0 auto var(--sb-space-3); border-radius: var(--sb-radius-circle); background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .cg__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .cg__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class CodesGenerateComponent implements OnInit {
  readonly #service = inject(CodeService);
  readonly #auth = inject(AuthStore);
  readonly #fb = inject(FormBuilder);
  readonly #toast = inject(ToastService);

  readonly canGenerate = computed(() => this.#auth.hasPermission('CodesGenerate'));

  readonly form = this.#fb.group({
    sessionId: [''],
    value: [0],
    quantity: [50],
  });

  /** Live mirror of the form so computed previews react to typing (valueChanges → signal). */
  readonly #formValue = signal(this.form.getRawValue());

  readonly batch = signal<CodeBatchDto | null>(null);
  readonly submitting = signal(false);
  readonly downloading = signal(false);

  readonly sessionOptions = computed<SelectOption[]>(() =>
    this.#service.sessions().map((s) => ({ value: s.id, label: s.title })),
  );

  readonly valueNumber = computed(() => Number(this.#formValue().value) || 0);
  readonly quantityLabel = computed(() => String(Number(this.#formValue().quantity) || 0));

  readonly selectedSessionTitle = computed(() => {
    const id = this.#formValue().sessionId;
    return this.#service.sessions().find((s) => s.id === id)?.title ?? '';
  });

  readonly canSubmit = computed(() => {
    const f = this.#formValue();
    const qty = Number(f.quantity) || 0;
    return !!f.sessionId && qty >= 1 && qty <= 1000 && (Number(f.value) || 0) >= 0;
  });

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      this.#formValue.set(this.form.getRawValue());
    });

    // Pre-fill the value from the chosen session's current price (contract §5).
    this.form.controls.sessionId.valueChanges.pipe(takeUntilDestroyed()).subscribe((id) => {
      if (id) void this.#prefillValue(id);
    });
  }

  ngOnInit(): void {
    if (this.canGenerate()) void this.#service.loadSessions();
  }

  async #prefillValue(sessionId: string): Promise<void> {
    try {
      const price = await this.#service.loadSessionPrice(sessionId);
      this.form.controls.value.setValue(price);
    } catch {
      /* leave the current value untouched */
    }
  }

  async submit(): Promise<void> {
    if (!this.canSubmit()) return;
    const f = this.form.getRawValue();
    this.submitting.set(true);
    try {
      const created = await this.#service.generateBatch({
        sessionId: f.sessionId ?? '',
        value: Number(f.value) || 0,
        quantity: Number(f.quantity) || 0,
      });
      this.batch.set(created);
      this.#toast.success(`${created.quantity} codes minted`);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not generate the batch.');
    } finally {
      this.submitting.set(false);
    }
  }

  async download(batch: CodeBatchDto): Promise<void> {
    this.downloading.set(true);
    try {
      await this.#service.exportBatch(batch.batchId);
    } catch {
      this.#toast.error('Could not download the batch.');
    } finally {
      this.downloading.set(false);
    }
  }

  // Presentation helpers
  value = egp;
}
