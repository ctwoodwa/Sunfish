export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    'type-enum': [2, 'always',
      ['feat', 'fix', 'docs', 'style', 'refactor', 'perf',
       'test', 'build', 'ci', 'chore', 'revert']],
    'scope-enum': [1, 'always',
      ['foundation', 'foundation-catalog', 'foundation-multitenancy',
       'foundation-featuremanagement', 'foundation-localfirst',
       'foundation-integrations', 'ui-core', 'ui-adapters-blazor',
       'ui-adapters-react', 'blocks-leases', 'compat-telerik',
       'bridge', 'kitchen-sink', 'apps-docs', 'scaffolding-cli',
       'icm', 'adrs', 'governance', 'docs', 'deps', 'repo']],
    'header-max-length': [2, 'always', 100],
    'body-max-line-length': [2, 'always', 100],
    // Relaxed to warning: existing main history routinely uses sentence-case
    // in subjects (referring to ADRs, Plans, Waves — "Plan 4B §5 ...",
    // "ADR 0034 Amendment 1 — ..."). The conventions doc
    // (_shared/engineering/commit-conventions.md) requires imperative mood
    // but does not mandate lowercase; the strict-error setting was
    // mismatched with actual project practice and blocked legitimate PRs.
    // Severity 1 keeps the signal in commitlint output for authors to
    // self-correct without blocking the merge.
    'subject-case': [1, 'never',
      ['sentence-case', 'start-case', 'pascal-case', 'upper-case']],
  },
};
