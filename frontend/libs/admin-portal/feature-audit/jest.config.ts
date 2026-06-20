export default {
  displayName: 'admin-portal-feature-audit',
  preset: '../../../jest.preset.js',
  setupFilesAfterEnv: ['<rootDir>/src/test-setup.ts'],
  coverageDirectory: '../../../coverage/libs/admin-portal/feature-audit',
  // Resolve the workspace path aliases for jest (these are not buildable libs).
  moduleNameMapper: {
    '^@sb/shared/ui$': '<rootDir>/../../shared/ui/src/index.ts',
    '^@sb/shared/data-access$': '<rootDir>/../../shared/data-access/src/index.ts',
  },
};
