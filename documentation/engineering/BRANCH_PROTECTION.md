# Master branch protection

Configure the `master` branch in the repository settings to require pull requests and the following successful status checks before merge:

- `Windows unit tests`
- `Ubuntu unit tests`
- `macOS unit tests`
- `Formatting`
- `Ubuntu QPDF and Poppler validation`
- `Windows package smoke test`
- `Ubuntu package smoke test`

Also enable dismissal of stale approvals and prevent administrators from bypassing these requirements. Branch protection is a repository setting and cannot be enforced by the workflow file itself.
