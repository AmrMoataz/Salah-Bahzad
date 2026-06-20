import { behaviourVisual, mmss, quizFlagPill, relativeTime } from './attendance.presentation';

describe('attendance.presentation', () => {
  describe('quizFlagPill (contract §B, scrReview line 1129)', () => {
    it('maps Clean → success ("active")', () => {
      expect(quizFlagPill('Clean')).toEqual({ label: 'Clean', variant: 'success' });
    });
    it('maps Timeout → danger ("rejected")', () => {
      expect(quizFlagPill('Timeout')).toEqual({ label: 'Timeout', variant: 'danger' });
    });
    it('maps Forfeit → warning ("pending")', () => {
      expect(quizFlagPill('Forfeit')).toEqual({ label: 'Forfeit', variant: 'warning' });
    });
  });

  describe('behaviourVisual — quiz focus events (5B-2)', () => {
    it('FocusLost → x / red (so focus-loss rows show in the Behaviour tab)', () => {
      expect(behaviourVisual('FocusLost')).toEqual({ icon: 'x', accent: 'red' });
    });
    it('FocusReturned → logout / mustard', () => {
      expect(behaviourVisual('FocusReturned')).toEqual({ icon: 'logout', accent: 'mustard' });
    });
  });

  describe('mmss — Time spent column', () => {
    it('formats whole seconds as m:ss (898 → 14:58, 702 → 11:42)', () => {
      expect(mmss(898)).toBe('14:58');
      expect(mmss(702)).toBe('11:42');
    });
  });

  describe('relativeTime — When column', () => {
    const now = new Date('2026-06-20T12:00:00Z');
    it('renders a recent attempt as "minutes ago"', () => {
      expect(relativeTime('2026-06-20T11:30:00Z', now)).toBe('30 minutes ago');
    });
    it('renders a null/invalid timestamp as the em-dash', () => {
      expect(relativeTime(null, now)).toBe('—');
      expect(relativeTime('not-a-date', now)).toBe('—');
    });
  });
});
