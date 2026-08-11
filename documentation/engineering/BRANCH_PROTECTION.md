# Master branch protection

The recommended GitHub ruleset for `master` is:

- require pull requests before merging;
- require these status checks:
  - `Windows build and test`;
  - `Ubuntu build and test`;
  - `macOS build and test`;
  - `Windows package consumer smoke test`;
  - `Ubuntu package consumer smoke test`;
- dismiss stale pull-request approvals when new commits are pushed;
- require all review conversations to be resolved;
- block force pushes;
- block branch deletion; and
- prevent bypass of these requirements, including administrator bypass, where repository policy permits.

Formatting is a step inside each build-and-test job. Ubuntu qpdf/Poppler validation is a step inside `Ubuntu build and test`. Neither is a separate required status-check name.

This document records the recommended ruleset only. The connected environment does not expose a branch-protection/ruleset read endpoint and the GitHub CLI is unavailable, so the current repository setting has not been verified here. A repository administrator must compare the GitHub ruleset UI with this list before claiming it is configured.
