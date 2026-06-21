import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import {
  ButtonComponent,
  FileUploadComponent,
  FormFieldComponent,
  ModalComponent,
  UploadedFile,
} from '@sb/shared/ui';
import { SessionVideoDto } from '../data-access/session.models';

/** Add-mode payload: a new source file plus its metadata (contract §2.12). Length is computed by transcode. */
export interface AddVideoPayload {
  file: File;
  title: string;
  accessCount: number;
}

/** Edit-mode payload: metadata only — title and access (contract §2.13). */
export interface EditVideoPayload {
  title: string;
  accessCount: number;
}

/**
 * Add / edit video modal (FR-ADM-SES-003, mockup `scrSessionEdit`). Both modes collect a title and the
 * per-enrollment access count; <code>add</code> mode also uploads the source file through the secure pipeline.
 * The running length is computed by the transcode pipeline (ffprobe), so it is not entered here. The parent
 * performs the request and shows upload progress.
 */
@Component({
  selector: 'sb-video-dialog',
  standalone: true,
  imports: [ModalComponent, ButtonComponent, FormFieldComponent, FileUploadComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-modal [open]="open()" [title]="heading()" size="form" (close)="cancel.emit()">
      @if (error()) {
        <div class="vd__error" role="alert">{{ error() }}</div>
      }

      <div class="vd">
        @if (mode() === 'add') {
          <sb-form-field label="Video file" fieldId="vd-file" [error]="fileError()" [required]="true">
            <sb-file-upload
              accept="video/*"
              hint="MP4, MOV or WebM · up to 2 GB"
              [files]="selectedFiles()"
              [progress]="progress()"
              (filesPicked)="onFile($event)"
              (remove)="file.set(null)"
            />
          </sb-form-field>
        }

        <sb-form-field label="Title" fieldId="vd-title" [error]="titleError()" [required]="true">
          <input id="vd-title" type="text" class="sb-input" [value]="title()" (input)="onTitle($event)"
                 placeholder="e.g. Worked examples" autocomplete="off" />
        </sb-form-field>

        <sb-form-field
          label="Allowed access count"
          fieldId="vd-access"
          hint="Number of times each enrolled student may watch this video (0 or more)."
          [error]="accessError()"
        >
          <input id="vd-access" type="number" min="0" class="sb-input" [value]="accessCount()"
                 (input)="onAccess($event)" />
        </sb-form-field>
      </div>

      <div modalFooter class="vd__actions">
        <sb-button variant="ghost" (clicked)="cancel.emit()">Cancel</sb-button>
        <sb-button variant="primary" [loading]="submitting()" (clicked)="submit()">
          {{ mode() === 'add' ? 'Add video' : 'Save changes' }}
        </sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .vd { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .vd__error {
      margin-bottom: var(--sb-space-3); background: var(--sb-danger-bg); color: var(--sb-danger-fg);
      border: 1px solid var(--sb-danger-border); border-radius: var(--sb-radius-md);
      padding: var(--sb-space-2) var(--sb-space-3); font-size: var(--sb-body-md-size); font-weight: 600;
    }
    .vd__actions { display: flex; gap: var(--sb-space-2); justify-content: flex-end; }

    .sb-input {
      width: 100%; height: 40px; padding: 0 var(--sb-space-3);
      border: 1px solid var(--sb-border-strong); border-radius: var(--sb-radius-md);
      font-size: var(--sb-body-md-size); font-family: var(--sb-font-sans); color: var(--sb-text);
      background: var(--sb-surface); outline: none;
      transition: border-color var(--sb-timing) var(--sb-easing-standard), box-shadow var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-input:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
  `],
})
export class VideoDialogComponent {
  readonly open = input<boolean>(false);
  readonly mode = input<'add' | 'edit'>('add');
  readonly video = input<SessionVideoDto | null>(null);
  readonly submitting = input<boolean>(false);
  readonly error = input<string | null>(null);
  /** 0–100 while the source file uploads; null hides the bar. */
  readonly progress = input<number | null>(null);

  readonly add = output<AddVideoPayload>();
  readonly save = output<EditVideoPayload>();
  readonly cancel = output<void>();

  readonly file = signal<File | null>(null);
  readonly title = signal('');
  readonly accessCount = signal(3);
  readonly touched = signal(false);

  readonly heading = computed(() => (this.mode() === 'add' ? 'Add video' : 'Edit video'));
  readonly selectedFiles = computed<UploadedFile[]>(() => {
    const f = this.file();
    return f ? [{ name: f.name, sizeBytes: f.size }] : [];
  });

  readonly fileError = computed(() =>
    this.touched() && this.mode() === 'add' && !this.file() ? 'Choose a video file to upload.' : '',
  );
  readonly titleError = computed(() =>
    this.touched() && !this.title().trim() ? 'A title is required.' : '',
  );
  readonly accessError = computed(() =>
    this.touched() && this.accessCount() < 0 ? 'Access count cannot be negative.' : '',
  );

  constructor() {
    // Re-seed each time the dialog opens (edit mode pre-fills from the chosen video).
    effect(() => {
      if (this.open()) {
        const v = this.video();
        const editing = this.mode() === 'edit';
        this.touched.set(false);
        this.file.set(null);
        this.title.set(editing ? v?.title ?? '' : '');
        this.accessCount.set(v?.accessCount ?? 3);
      }
    });
  }

  onFile(files: File[]): void {
    this.file.set(files[0] ?? null);
  }
  onTitle(event: Event): void {
    this.title.set((event.target as HTMLInputElement).value);
  }
  onAccess(event: Event): void {
    this.accessCount.set(Number((event.target as HTMLInputElement).value));
  }

  submit(): void {
    this.touched.set(true);
    if (!this.title().trim() || this.accessCount() < 0) return;
    const meta = {
      title: this.title().trim(),
      accessCount: this.accessCount(),
    };
    if (this.mode() === 'add') {
      const file = this.file();
      if (!file) return;
      this.add.emit({ file, ...meta });
    } else {
      this.save.emit(meta);
    }
  }
}
