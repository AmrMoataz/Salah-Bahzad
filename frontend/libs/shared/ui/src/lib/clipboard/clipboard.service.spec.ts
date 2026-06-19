import { TestBed } from '@angular/core/testing';
import { ClipboardService } from './clipboard.service';
import { ToastService } from '../toast/toast.service';

describe('ClipboardService', () => {
  let service: ClipboardService;
  let toast: { info: jest.Mock };

  beforeEach(() => {
    toast = { info: jest.fn() };
    TestBed.configureTestingModule({
      providers: [ClipboardService, { provide: ToastService, useValue: toast }],
    });
    service = TestBed.inject(ClipboardService);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('writes via the async Clipboard API and toasts the copied text', async () => {
    const writeText = jest.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const ok = await service.copy('\\times');

    expect(ok).toBe(true);
    expect(writeText).toHaveBeenCalledWith('\\times');
    expect(toast.info).toHaveBeenCalledWith('Copied  \\times', 'Copied');
  });

  it('uses a custom toast message when provided', async () => {
    const writeText = jest.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    await service.copy('$$ … $$', 'Copied example');

    expect(toast.info).toHaveBeenCalledWith('Copied example', 'Copied');
  });

  it('falls back to execCommand when the Clipboard API rejects', async () => {
    const writeText = jest.fn().mockRejectedValue(new Error('denied'));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    // jsdom doesn't implement execCommand — define it so we can assert the legacy path runs.
    const exec = jest.fn().mockReturnValue(true);
    Object.defineProperty(document, 'execCommand', { value: exec, configurable: true });

    const ok = await service.copy('\\pi');

    expect(ok).toBe(true);
    expect(exec).toHaveBeenCalledWith('copy');
    expect(toast.info).toHaveBeenCalledWith('Copied  \\pi', 'Copied');
  });

  it('nudges the user when every copy strategy fails', async () => {
    Object.defineProperty(navigator, 'clipboard', { value: undefined, configurable: true });
    Object.defineProperty(document, 'execCommand', { value: jest.fn().mockReturnValue(false), configurable: true });

    const ok = await service.copy('\\pi');

    expect(ok).toBe(false);
    expect(toast.info).toHaveBeenCalledWith('Press Ctrl/Cmd+C to copy', 'Copy');
  });
});
