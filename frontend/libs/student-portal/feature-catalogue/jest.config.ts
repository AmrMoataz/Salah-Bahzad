export default {
  displayName: 'student-portal-feature-catalogue',
  preset: '../../../jest.preset.js',
  setupFilesAfterEnv: ['<rootDir>/src/test-setup.ts'],
  coverageDirectory: '../../../coverage/libs/student-portal/feature-catalogue',
  // Resolve the workspace path aliases for jest (these are not buildable libs).
  moduleNameMapper: {
    '^@sb/shared/ui$': '<rootDir>/../../shared/ui/src/index.ts',
    '^@sb/student-portal/data-access$': '<rootDir>/../data-access/src/index.ts',
  },
};
