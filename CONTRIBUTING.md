# Contributing

Read AGENTS.md, architecture, reference licenses and validation limits first. Keep collectors read-only and cancellation-aware. Never request serial/account/MAC/personal-file fields by default. Add a fixture/regression for parsing or classification changes and label synthetic versus anonymized real input. No unsafe driver packs or copied incompatible-license code.

Use .NET10 SDK, dotnet restore/build/test from the solution root. Build the desktop UI and run the CLI on the changed native platform. Report denied/partial sources; do not treat every warning as hardware failure. Export/report additions must escape untrusted content and preserve explicit user control. Keep PRs scoped with problem, behavior, test result and limits.
