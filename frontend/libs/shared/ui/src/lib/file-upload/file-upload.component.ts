import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { ProgressComponent } from '../progress/progress.component';

/** A selected file shown in the dropzone's list (name + optional size). */
export interface UploadedFile {
  name: string;
  sizeBytes?: number;
}

/**
 * Drag-and-drop file dropzone (design-system `SBFileUpload`): a dashed target with a click-to-pick
 * fallback, hover/drag affordance, an optional uploaded-file list (ext badge · name · size · remove),
 * and an optional upload progress bar. Emits the picked files; the parent performs the actual upload.
 * Matches the prototype's dropzone (dashed border, circular up-glyph, "Click to upload" copy + hint).
 */
@Component({
  selector: 'sb-file-upload',
  standalone: true,
  imports: [ProgressComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (label()) {
      <label class="fu__label">{{ label() }}</label>
    }

    <div
      class="fu__zone"
      [class.fu__zone--drag]="dragging()"
      [class.fu__zone--disabled]="disabled()"
      role="button"
      tabindex="0"
      [attr.aria-label]="label() || 'Upload a file'"
      [attr.aria-disabled]="disabled() || null"
      (click)="open()"
      (keydown.enter)="open()"
      (keydown.space)="$event.preventDefault(); open()"
      (dragover)="onDragOver($event)"
      (dragleave)="onDragLeave()"
      (drop)="onDrop($event)"
    >
      <span class="fu__glyph" aria-hidden="true">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M12 19V5M5 12l7-7 7 7"/>
        </svg>
      </span>
      <span class="fu__copy"><b>Click to upload</b> or drag &amp; drop</span>
      <span class="fu__hint">{{ hint() }}</span>
      <input
        #input
        type="file"
        class="fu__input"
        [accept]="accept()"
        [multiple]="multiple()"
        [disabled]="disabled()"
        (change)="onChange($event)"
      />
    </div>

    @if (files().length > 0) {
      <div class="fu__list">
        @for (file of files(); track $index; let i = $index) {
          <div class="fu__file">
            <span class="fu__file-badge" aria-hidden="true">{{ ext(file.name) }}</span>
            <span class="fu__file-name">{{ file.name }}</span>
            @if (file.sizeBytes != null) {
              <span class="fu__file-size">{{ sizeLabel(file.sizeBytes) }}</span>
            }
            <button type="button" class="fu__file-remove" aria-label="Remove" (click)="remove.emit(i)">×</button>
          </div>
        }
      </div>
    }

    @if (progress() != null) {
      <div class="fu__progress">
        <sb-progress [value]="progress() ?? 0" [showValue]="true" label="Uploading…" />
      </div>
    }

    <ng-content />
  `,
  styles: [`
    :host { display: block; font-family: var(--sb-font-sans); width: 100%; }

    .fu__label {
      display: block;
      font-size: var(--sb-label-lg-size);
      font-weight: 600;
      color: var(--sb-text);
      margin-bottom: var(--sb-space-2);
    }

    .fu__zone {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--sb-space-2);
      padding: var(--sb-space-8) var(--sb-space-6);
      text-align: center;
      cursor: pointer;
      border: 2px dashed var(--sb-border-strong);
      border-radius: var(--sb-radius-lg);
      background: var(--sb-surface-sunken);
      transition: border-color var(--sb-timing-fast) var(--sb-easing-standard),
                  background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .fu__zone:hover, .fu__zone--drag { border-color: var(--sb-primary); background: var(--sb-primary-50); }
    .fu__zone:focus-visible { outline: none; border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
    .fu__zone--disabled { cursor: not-allowed; opacity: 0.55; pointer-events: none; }

    .fu__glyph {
      width: 40px;
      height: 40px;
      border-radius: var(--sb-radius-circle);
      background: var(--sb-primary-100);
      color: var(--sb-primary-700);
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
    .fu__copy { font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .fu__copy b { color: var(--sb-primary-700); }
    .fu__hint { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }

    .fu__input { display: none; }

    /* Picked-file list (matches the prototype's SBFileUpload list) */
    .fu__list { display: flex; flex-direction: column; gap: var(--sb-space-2); margin-top: var(--sb-space-3); }
    .fu__file {
      display: flex; align-items: center; gap: var(--sb-space-3);
      padding: var(--sb-space-2) var(--sb-space-3);
      background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-md);
    }
    .fu__file-badge {
      width: 28px; height: 28px; flex-shrink: 0; border-radius: var(--sb-radius-sm);
      background: var(--sb-subject-blue-bg); color: var(--sb-subject-blue-deep);
      display: inline-flex; align-items: center; justify-content: center;
      font-size: 13px; font-weight: 800;
    }
    .fu__file-name { flex: 1; min-width: 0; font-size: var(--sb-body-sm-size); color: var(--sb-text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .fu__file-size { font-size: var(--sb-label-sm-size); color: var(--sb-text-muted); flex-shrink: 0; }
    .fu__file-remove { border: none; background: none; color: var(--sb-text-subtle); cursor: pointer; font-size: 17px; line-height: 1; padding: 0; flex-shrink: 0; }
    .fu__file-remove:hover { color: var(--sb-danger-fg); }

    .fu__progress { margin-top: var(--sb-space-3); }
  `],
})
export class FileUploadComponent {
  readonly label = input<string>('');
  readonly accept = input<string>('');
  readonly multiple = input<boolean>(false);
  readonly hint = input<string>('PNG, JPG or WebP · up to 5 MB');
  readonly disabled = input<boolean>(false);
  /** Selected files to list under the dropzone (parent-owned). */
  readonly files = input<UploadedFile[]>([]);
  /** 0–100 while an upload is in flight; null hides the bar. Driven by the parent. */
  readonly progress = input<number | null>(null);

  readonly filesPicked = output<File[]>();
  readonly remove = output<number>();

  private readonly input = viewChild<ElementRef<HTMLInputElement>>('input');
  protected readonly dragging = signal(false);

  open(): void {
    if (this.disabled()) return;
    this.input()?.nativeElement.click();
  }

  onChange(event: Event): void {
    const el = event.target as HTMLInputElement;
    this.#emit(el.files);
    el.value = '';
  }

  onDragOver(event: DragEvent): void {
    if (this.disabled()) return;
    event.preventDefault();
    this.dragging.set(true);
  }

  onDragLeave(): void {
    this.dragging.set(false);
  }

  onDrop(event: DragEvent): void {
    if (this.disabled()) return;
    event.preventDefault();
    this.dragging.set(false);
    this.#emit(event.dataTransfer?.files ?? null);
  }

  ext(name: string): string {
    return (name.includes('.') ? name.split('.').pop() ?? '' : name).slice(0, 4).toUpperCase() || 'FILE';
  }

  sizeLabel(bytes: number): string {
    return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(0)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  #emit(list: FileList | null): void {
    if (!list || list.length === 0) return;
    this.filesPicked.emit(Array.from(list));
  }
}
