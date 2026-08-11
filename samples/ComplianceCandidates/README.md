# Compliance candidates

This CI-oriented sample creates sanitised PDF/A-2b, PDF/A-3b, and PDF/UA-1 candidates from
a caller-approved ICC profile and an external font file. No font or ICC binaries are committed.
Local preflight must pass, but the sample deliberately does not claim conformance; CI validates
each output independently with pinned veraPDF before retaining the reports.
