import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import {
  AlertComponent,
  ButtonComponent,
  CardComponent,
  FileUploadComponent,
  FormFieldComponent,
  LatexPreviewComponent,
  SwitchComponent,
  ToastService,
} from '@sb/shared/ui';
import {
  OptionInput,
  QuestionDto,
  QuestionVariationDto,
  SaveQuestionRequest,
  SaveVariationRequest,
} from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';

interface EditableOption {
  id: string | null;
  text: string;
  isCorrect: boolean;
}
interface EditableUnit {
  /** Question id for the base unit, variation id for variations; null until persisted. */
  id: string | null;
  bodyLatex: string;
  imageUrl: string | null;
  options: EditableOption[];
}

const blankOption = (isCorrect = false): EditableOption => ({ id: null, text: '', isCorrect });
const blankUnit = (): EditableUnit => ({
  id: null,
  bodyLatex: '',
  imageUrl: null,
  options: [blankOption(true), blankOption()],
});

/** A→ letter for option rows. */
const letter = (i: number): string => String.fromCharCode(65 + i);

/**
 * Question editor (FR-ADM-QB-001..006, FR-ADM-QZ-002, mockup `scrQuestionEditor`). Authors MCQ
 * questions with a LaTeX body + dependency-free live preview, single-correct options, a per-question
 * mark / quiz-eligibility / hint URL, an optional image, and multiple variations (unit 1 is the base
 * question; units 2+ are variations with their own body/image/options). Variations and images need a
 * persisted question, so on a new question only the base is captured (POST) before landing in edit
 * mode — mirroring how a session is created before its content is added.
 */
@Component({
  selector: 'sb-question-editor',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    CardComponent,
    ButtonComponent,
    SwitchComponent,
    FormFieldComponent,
    FileUploadComponent,
    LatexPreviewComponent,
    AlertComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="qe__back" (click)="back()">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M19 12H5M12 19l-7-7 7-7"/>
      </svg>
      Back to question bank
    </button>

    @if (loadError()) {
      <sb-alert variant="danger" title="Couldn’t load question">{{ loadError() }}</sb-alert>
    } @else {
      <div class="qe__head">
        <div>
          <h1 class="qe__title">{{ isNew() ? 'New question' : 'Edit question' }}</h1>
          <p class="qe__subtitle">MCQ authoring with LaTeX, variations and flags</p>
        </div>
        <div class="qe__head-actions">
          <sb-button variant="ghost" (clicked)="back()">Cancel</sb-button>
          <sb-button variant="primary" [loading]="saving()" (clicked)="saveBase()">Save question</sb-button>
        </div>
      </div>

      <!-- Variation tabs -->
      <div class="qe__variations">
        @for (u of units(); track $index; let i = $index) {
          <button type="button" class="qe__vtab" [class.is-active]="i === activeIndex()" (click)="activeIndex.set(i)">
            Variation {{ i + 1 }}
            @if (i > 0 && u.id === null) { <span class="qe__draft">draft</span> }
          </button>
        }
        @if (!isNew()) {
          <button type="button" class="qe__vadd" (click)="addVariationDraft()">+ Add variation</button>
        }
      </div>

      @if (activeUnit(); as unit) {
        <div class="qe__grid">
          <!-- LEFT -->
          <div class="qe__col">
            <sb-card [title]="activeIndex() === 0 ? 'Question (LaTeX supported)' : 'Variation ' + (activeIndex() + 1) + ' (LaTeX supported)'">
              <div class="qe__field">
                <textarea
                  class="qe__latex"
                  rows="5"
                  [value]="unit.bodyLatex"
                  (input)="setBody($event)"
                  placeholder="e.g. A car accelerates from rest at $a = 4\\,\\mathrm&#123;m/s^2&#125;$. Find its speed after $t = 5\\,\\mathrm&#123;s&#125;$."
                ></textarea>

                <div class="qe__image">
                  @if (unit.imageUrl) {
                    <div class="qe__image-preview">
                      <img [src]="unit.imageUrl" alt="Question image" />
                      @if (activeIndex() === 0) {
                        <sb-button variant="danger-ghost" size="sm" [loading]="imageBusy()" (clicked)="clearImage()">Remove image</sb-button>
                      }
                    </div>
                  }
                  @if (unit.id) {
                    <sb-file-upload
                      [label]="unit.imageUrl ? 'Replace image' : 'Question image (optional)'"
                      accept="image/jpeg,image/png,image/webp"
                      hint="JPG, PNG or WebP · max 5 MB"
                      [progress]="imageBusy() ? 100 : null"
                      (filesPicked)="uploadImage($event)"
                    />
                  } @else {
                    <p class="qe__hint">Save the question to attach an image.</p>
                  }
                </div>
              </div>
            </sb-card>

            <sb-card title="Answer options">
              <div class="qe__options">
                @for (opt of unit.options; track $index; let i = $index) {
                  <label class="qe__option" [class.is-correct]="opt.isCorrect">
                    <input type="radio" name="qe-correct" class="qe__radio" [checked]="opt.isCorrect" (change)="setCorrect(i)" />
                    <span class="qe__option-letter">{{ ltr(i) }}</span>
                    <input type="text" class="qe__option-text" [value]="opt.text" (input)="setOptionText(i, $event)" [placeholder]="'Option ' + ltr(i)" />
                    @if (opt.isCorrect) {
                      <span class="qe__correct" aria-label="Correct answer">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6L9 17l-5-5"/></svg>
                        Correct
                      </span>
                    }
                    @if (unit.options.length > 2) {
                      <button type="button" class="qe__opt-remove" (click)="removeOption(i)" aria-label="Remove option">×</button>
                    }
                  </label>
                }
                <div class="qe__options-foot">
                  <button type="button" class="qe__addopt" [disabled]="unit.options.length >= 6" (click)="addOption()">+ Add option</button>
                  <span class="qe__hint">Select the radio of the correct option (exactly one).</span>
                </div>
                @if (showErrors() && unitError(unit); as e) {
                  <p class="qe__error" role="alert">{{ e }}</p>
                }
              </div>
            </sb-card>

            @if (activeIndex() > 0) {
              <div class="qe__variation-actions">
                <sb-button variant="primary" size="sm" [loading]="variationBusy()" (clicked)="saveVariation()">
                  {{ unit.id ? 'Save variation' : 'Add variation' }}
                </sb-button>
                <sb-button variant="danger-ghost" size="sm" (clicked)="removeVariation()">Remove variation</sb-button>
              </div>
            }
          </div>

          <!-- RIGHT (sticky) -->
          <div class="qe__col qe__col--side">
            <sb-card title="Live preview">
              <div class="qe__preview">
                @if (unit.imageUrl) {
                  <img class="qe__preview-img" [src]="unit.imageUrl" alt="Question image" />
                }
                <sb-latex-preview [latex]="unit.bodyLatex" placeholder="Type a question body to preview it here." />
                <div class="qe__preview-options">
                  @for (opt of unit.options; track $index; let i = $index) {
                    <div class="qe__preview-option" [class.is-correct]="opt.isCorrect">
                      <span class="qe__preview-letter">{{ ltr(i) }}</span>
                      <span>{{ opt.text || '—' }}</span>
                    </div>
                  }
                </div>
              </div>
            </sb-card>

            <sb-card title="Settings">
              @if (activeIndex() === 0) {
                <form [formGroup]="settings" class="qe__settings" novalidate>
                  <sb-form-field label="Mark" fieldId="qe-mark" [error]="markError()">
                    <input id="qe-mark" type="number" min="1" class="qe__input" formControlName="mark" />
                  </sb-form-field>
                  <sb-switch formControlName="isValidForQuiz" label="Eligible for gating quiz" />
                  <sb-form-field label="Hint URL (assignments only)" fieldId="qe-hint" hint="Shown only while solving an assignment — never in a quiz.">
                    <input id="qe-hint" type="url" class="qe__input" formControlName="hintUrl" placeholder="https://…" />
                  </sb-form-field>
                </form>
              } @else {
                <p class="qe__muted">Mark, quiz-eligibility and the hint URL are set on the base question (Variation 1). This variation only varies the body, image and options.</p>
              }
            </sb-card>
          </div>
        </div>
      }
    }
  `,
  styles: [`
    :host { display: block; }

    .qe__back { display: inline-flex; align-items: center; gap: var(--sb-space-2); margin-bottom: var(--sb-space-4); border: none; background: transparent; cursor: pointer; color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); font-weight: 700; padding: 0; }
    .qe__back:hover { color: var(--sb-primary); }

    .qe__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; margin-bottom: var(--sb-space-4); }
    .qe__head-actions { display: flex; gap: var(--sb-space-2); }
    .qe__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-lg-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .qe__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .qe__variations { display: flex; gap: var(--sb-space-2); margin-bottom: var(--sb-space-4); flex-wrap: wrap; }
    .qe__vtab { display: inline-flex; align-items: center; gap: 6px; padding: 7px 14px; border-radius: var(--sb-radius-pill); border: 1px solid var(--sb-border-strong); background: var(--sb-surface); color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-weight: 700; font-size: var(--sb-body-sm-size); cursor: pointer; }
    .qe__vtab.is-active { border-color: var(--sb-primary); background: var(--sb-primary-50); color: var(--sb-primary); }
    .qe__draft { font-size: var(--sb-label-sm-size); font-weight: 700; color: var(--sb-warning-fg); background: var(--sb-warning-bg); padding: 1px 6px; border-radius: var(--sb-radius-pill); }
    .qe__vadd { padding: 7px 12px; border-radius: var(--sb-radius-pill); border: 1px dashed var(--sb-border-strong); background: transparent; color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-weight: 700; font-size: var(--sb-body-sm-size); cursor: pointer; }
    .qe__vadd:hover { border-color: var(--sb-primary); color: var(--sb-primary); }

    .qe__grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: var(--sb-space-4); align-items: start; }
    @media (max-width: 900px) { .qe__grid { grid-template-columns: 1fr; } }
    .qe__col { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .qe__col--side { position: sticky; top: var(--sb-space-2); }
    @media (max-width: 900px) { .qe__col--side { position: static; } }

    .qe__field { display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .qe__latex { width: 100%; min-height: 120px; padding: var(--sb-space-3); border: 1px solid var(--sb-border-strong); border-radius: var(--sb-radius-md); font-size: var(--sb-code-size); font-family: var(--sb-font-mono); line-height: 1.6; color: var(--sb-text); background: var(--sb-surface); resize: vertical; outline: none; }
    .qe__latex:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }

    .qe__image { display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .qe__image-preview { display: flex; flex-direction: column; gap: var(--sb-space-2); align-items: flex-start; }
    .qe__image-preview img { max-width: 100%; max-height: 220px; border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); }

    .qe__options { display: flex; flex-direction: column; gap: var(--sb-space-2); }
    .qe__option { display: flex; align-items: center; gap: var(--sb-space-3); padding: var(--sb-space-2) var(--sb-space-3); border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); background: var(--sb-surface); }
    .qe__option.is-correct { border-color: var(--sb-success); background: var(--sb-success-bg); }
    .qe__radio { width: 18px; height: 18px; accent-color: var(--sb-success); flex-shrink: 0; }
    .qe__option-letter { font-weight: 700; color: var(--sb-text-muted); width: 16px; }
    .qe__option-text { flex: 1; min-width: 0; border: none; outline: none; background: transparent; font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .qe__correct { display: inline-flex; align-items: center; gap: 4px; color: var(--sb-success-fg); font-weight: 700; font-size: var(--sb-body-sm-size); white-space: nowrap; }
    .qe__opt-remove { border: none; background: none; color: var(--sb-text-subtle); cursor: pointer; font-size: 18px; line-height: 1; padding: 0 4px; }
    .qe__opt-remove:hover { color: var(--sb-danger-fg); }
    .qe__options-foot { display: flex; align-items: center; justify-content: space-between; gap: var(--sb-space-3); flex-wrap: wrap; margin-top: var(--sb-space-1); }
    .qe__addopt { border: none; background: none; color: var(--sb-primary); font-family: var(--sb-font-sans); font-weight: 700; font-size: var(--sb-body-md-size); cursor: pointer; padding: 0; }
    .qe__addopt:disabled { color: var(--sb-text-subtle); cursor: not-allowed; }
    .qe__error { margin: var(--sb-space-1) 0 0; color: var(--sb-danger-fg); font-size: var(--sb-body-sm-size); font-weight: 600; }

    .qe__variation-actions { display: flex; gap: var(--sb-space-2); }

    .qe__preview { background: var(--sb-surface-sunken); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-md); padding: var(--sb-space-4); }
    .qe__preview-img { max-width: 100%; max-height: 200px; border-radius: var(--sb-radius-sm); margin-bottom: var(--sb-space-3); display: block; }
    .qe__preview-options { display: flex; flex-direction: column; gap: var(--sb-space-2); margin-top: var(--sb-space-3); }
    .qe__preview-option { display: flex; align-items: center; gap: var(--sb-space-3); padding: var(--sb-space-2) var(--sb-space-3); border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); background: var(--sb-surface); font-size: var(--sb-body-md-size); }
    .qe__preview-option.is-correct { border-color: var(--sb-success-border); }
    .qe__preview-letter { font-weight: 700; color: var(--sb-text-muted); }

    .qe__settings { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .qe__muted { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-sm-size); line-height: 1.5; }
    .qe__hint { margin: 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }

    .qe__input { width: 100%; height: 40px; padding: 0 var(--sb-space-3); border: 1px solid var(--sb-border-strong); border-radius: var(--sb-radius-md); font-size: var(--sb-body-md-size); font-family: var(--sb-font-sans); color: var(--sb-text); background: var(--sb-surface); outline: none; }
    .qe__input:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
  `],
})
export class QuestionEditorComponent {
  readonly #service = inject(SessionService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #fb = inject(NonNullableFormBuilder);
  readonly #toast = inject(ToastService);

  /** Bound from the route: session id and (edit only) the question id. */
  readonly id = input.required<string>();
  readonly questionId = input<string>();

  readonly isNew = computed(() => !this.questionId());

  readonly units = signal<EditableUnit[]>([blankUnit()]);
  readonly activeIndex = signal(0);
  readonly activeUnit = computed<EditableUnit | null>(() => this.units()[this.activeIndex()] ?? null);

  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly variationBusy = signal(false);
  readonly imageBusy = signal(false);
  readonly showErrors = signal(false);

  readonly settings = this.#fb.group({
    mark: [1, [Validators.required, Validators.min(1)]],
    isValidForQuiz: [true],
    hintUrl: ['', [Validators.maxLength(2000)]],
  });

  readonly ltr = letter;

  constructor() {
    if (!this.#auth.hasPermission('QuestionsRead')) {
      this.loadError.set('You don’t have permission to view the question bank.');
    }
    effect(() => {
      const qid = this.questionId();
      queueMicrotask(() => void this.#load(qid));
    });
  }

  async #load(questionId: string | undefined): Promise<void> {
    if (this.loadError()) return;
    this.activeIndex.set(0);
    this.showErrors.set(false);
    if (!questionId) {
      this.units.set([blankUnit()]);
      this.settings.reset({ mark: 1, isValidForQuiz: true, hintUrl: '' });
      return;
    }
    try {
      // No single-question GET in the contract — fetch the bank page and find it.
      const res = await this.#service.listQuestions(this.id(), 1, 200);
      const q = res.items.find((x) => x.id === questionId);
      if (!q) {
        this.loadError.set('This question no longer exists in the bank.');
        return;
      }
      this.#applyQuestion(q);
    } catch {
      this.loadError.set('Could not load this question. It may not exist or you may not have access.');
    }
  }

  #applyQuestion(q: QuestionDto): void {
    const base: EditableUnit = {
      id: q.id,
      bodyLatex: q.bodyLatex ?? '',
      imageUrl: q.imageUrl,
      options: q.options.map((o) => ({ id: o.id, text: o.text, isCorrect: o.isCorrect })),
    };
    const variations: EditableUnit[] = q.variations.map((v) => this.#unitFromVariation(v));
    this.units.set([base, ...variations]);
    this.settings.reset({ mark: q.mark, isValidForQuiz: q.isValidForQuiz, hintUrl: q.hintUrl ?? '' });
  }

  #unitFromVariation(v: QuestionVariationDto): EditableUnit {
    return {
      id: v.id,
      bodyLatex: v.bodyLatex ?? '',
      imageUrl: v.imageUrl,
      options: v.options.map((o) => ({ id: o.id, text: o.text, isCorrect: o.isCorrect })),
    };
  }

  // ── Active-unit editing ─────────────────────────────────────────────────────────
  #patchActive(fn: (u: EditableUnit) => EditableUnit): void {
    const i = this.activeIndex();
    this.units.update((list) => list.map((u, idx) => (idx === i ? fn(u) : u)));
  }

  setBody(event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.#patchActive((u) => ({ ...u, bodyLatex: value }));
  }

  setOptionText(index: number, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.#patchActive((u) => ({
      ...u,
      options: u.options.map((o, i) => (i === index ? { ...o, text: value } : o)),
    }));
  }

  setCorrect(index: number): void {
    this.#patchActive((u) => ({
      ...u,
      options: u.options.map((o, i) => ({ ...o, isCorrect: i === index })),
    }));
  }

  addOption(): void {
    this.#patchActive((u) => (u.options.length >= 6 ? u : { ...u, options: [...u.options, blankOption()] }));
  }

  removeOption(index: number): void {
    this.#patchActive((u) => {
      if (u.options.length <= 2) return u;
      const options = u.options.filter((_, i) => i !== index);
      if (!options.some((o) => o.isCorrect)) options[0] = { ...options[0], isCorrect: true };
      return { ...u, options };
    });
  }

  // ── Validation ───────────────────────────────────────────────────────────────────
  unitError(unit: EditableUnit): string | null {
    if (unit.options.length < 2) return 'Add at least two options.';
    if (unit.options.some((o) => !o.text.trim())) return 'Every option needs text.';
    if (unit.options.filter((o) => o.isCorrect).length !== 1) return 'Mark exactly one option as correct.';
    if (!unit.bodyLatex.trim() && !unit.imageUrl) return 'Add a question body (LaTeX) and/or an image.';
    return null;
  }

  markError(): string {
    const c = this.settings.controls.mark;
    if (!c.touched || c.valid) return '';
    return 'Mark must be 1 or more.';
  }

  #toOptionInputs(unit: EditableUnit): OptionInput[] {
    return unit.options.map((o) =>
      o.id ? { id: o.id, text: o.text.trim(), isCorrect: o.isCorrect } : { text: o.text.trim(), isCorrect: o.isCorrect },
    );
  }

  // ── Save base question ─────────────────────────────────────────────────────────
  async saveBase(): Promise<void> {
    this.activeIndex.set(0);
    this.showErrors.set(true);
    this.settings.markAllAsTouched();
    const base = this.units()[0];
    if (this.settings.invalid || this.unitError(base)) {
      this.#toast.error('Please fix the highlighted fields.');
      return;
    }
    const s = this.settings.getRawValue();
    const payload: SaveQuestionRequest = {
      bodyLatex: base.bodyLatex.trim() || null,
      mark: Number(s.mark),
      isValidForQuiz: s.isValidForQuiz,
      hintUrl: s.hintUrl.trim() || null,
      options: this.#toOptionInputs(base),
    };
    this.saving.set(true);
    try {
      if (this.isNew()) {
        const created = await this.#service.createQuestion(this.id(), payload);
        this.#toast.success('Question added to the bank');
        void this.#router.navigate(['/sessions', this.id(), 'questions', created.id, 'edit']);
      } else {
        const updated = await this.#service.updateQuestion(this.id(), this.questionId()!, payload);
        // Refresh only the base unit so unsaved variation drafts survive.
        this.units.update((list) => [
          { id: updated.id, bodyLatex: updated.bodyLatex ?? '', imageUrl: updated.imageUrl, options: updated.options.map((o) => ({ id: o.id, text: o.text, isCorrect: o.isCorrect })) },
          ...list.slice(1),
        ]);
        this.showErrors.set(false);
        this.#toast.success('Question saved');
      }
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not save the question.');
    } finally {
      this.saving.set(false);
    }
  }

  // ── Variations ───────────────────────────────────────────────────────────────────
  addVariationDraft(): void {
    this.units.update((list) => [...list, blankUnit()]);
    this.activeIndex.set(this.units().length - 1);
    this.showErrors.set(false);
  }

  async saveVariation(): Promise<void> {
    const i = this.activeIndex();
    if (i === 0) return;
    this.showErrors.set(true);
    const unit = this.units()[i];
    if (this.unitError(unit)) {
      this.#toast.error('Please fix the highlighted fields.');
      return;
    }
    const payload: SaveVariationRequest = {
      bodyLatex: unit.bodyLatex.trim() || null,
      options: this.#toOptionInputs(unit),
    };
    this.variationBusy.set(true);
    try {
      const saved = unit.id
        ? await this.#service.updateVariation(this.id(), this.questionId()!, unit.id, payload)
        : await this.#service.addVariation(this.id(), this.questionId()!, payload);
      this.units.update((list) => list.map((u, idx) => (idx === i ? this.#unitFromVariation(saved) : u)));
      this.showErrors.set(false);
      this.#toast.success(unit.id ? 'Variation saved' : 'Variation added');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not save the variation.');
    } finally {
      this.variationBusy.set(false);
    }
  }

  async removeVariation(): Promise<void> {
    const i = this.activeIndex();
    if (i === 0) return;
    const unit = this.units()[i];
    try {
      if (unit.id) await this.#service.removeVariation(this.id(), this.questionId()!, unit.id);
      this.units.update((list) => list.filter((_, idx) => idx !== i));
      this.activeIndex.set(Math.max(0, i - 1));
      if (unit.id) this.#toast.info('Variation removed');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not remove the variation.');
    }
  }

  // ── Image (active unit, persisted only) ────────────────────────────────────────────
  async uploadImage(files: File[]): Promise<void> {
    const unit = this.activeUnit();
    const file = files[0];
    if (!unit?.id || !file) return;
    this.imageBusy.set(true);
    try {
      if (this.activeIndex() === 0) {
        const q = await this.#service.uploadQuestionImage(this.id(), unit.id, file);
        this.#setActiveImage(q.imageUrl);
      } else {
        const v = await this.#service.uploadVariationImage(this.id(), this.questionId()!, unit.id, file);
        this.#setActiveImage(v.imageUrl);
      }
      this.#toast.success('Image attached');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not upload the image.');
    } finally {
      this.imageBusy.set(false);
    }
  }

  async clearImage(): Promise<void> {
    const unit = this.activeUnit();
    if (this.activeIndex() !== 0 || !unit?.id) return;
    this.imageBusy.set(true);
    try {
      const q = await this.#service.clearQuestionImage(this.id(), unit.id);
      this.#setActiveImage(q.imageUrl);
      this.#toast.info('Image removed');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not remove the image.');
    } finally {
      this.imageBusy.set(false);
    }
  }

  #setActiveImage(imageUrl: string | null): void {
    this.#patchActive((u) => ({ ...u, imageUrl }));
  }

  back(): void {
    void this.#router.navigate(['/sessions', this.id()]);
  }
}
