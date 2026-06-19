const nxPresetRaw = require('@nx/jest/preset');
const nxPreset = nxPresetRaw.default ?? nxPresetRaw;
const { createCjsPreset } = require('jest-preset-angular/presets');

// The bare Nx preset has no Angular TS/HTML transform, jsdom env, or component snapshot serializers,
// so every lib's tests fail to even parse `test-setup.ts`. Compose jest-preset-angular in here once
// (rather than per lib): each project's own `tsconfig.spec.json` is picked up via `<rootDir>`.
const angularPreset = createCjsPreset();

module.exports = {
  ...nxPreset,
  ...angularPreset,
  testEnvironment: 'jsdom',
  // Libs without spec files yet (e.g. taxonomy, settings) shouldn't fail the suite.
  passWithNoTests: true,
  transform: {
    '^.+\\.(ts|mjs|js|html)$': [
      'jest-preset-angular',
      {
        tsconfig: '<rootDir>/tsconfig.spec.json',
        stringifyContentPathRegex: '\\.(html|svg)$',
      },
    ],
  },
};
