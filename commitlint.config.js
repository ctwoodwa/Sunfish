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
    'subject-case': [2, 'never',
      ['sentence-case', 'start-case', 'pascal-case', 'upper-case']],
  },
};
