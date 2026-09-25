# Release guide

1. Update the version in both package.json and package-lock.json, and update README features/download instructions.
2. Run the documented Node tests, TypeScript build, and Markdown integration tests. Verify packaged Windows OCR independently of development dependencies.
3. Commit and push main, then tag the exact commit as vX.Y.Z-electron and push that tag.
4. The Release workflow builds Windows installer and portable EXEs, macOS arm64/x64 DMGs, and the Linux AppImage. It uploads a draft GitHub release after every platform succeeds.
5. Inspect the assets, add release notes and SHA-256 checksums, then publish the draft. Keep Mac/Linux runtime-testing limitations explicit if those platforms could not be tested locally.

Do not commit release binaries or downloaded artifacts. There is no Homebrew tap maintained by this fork.

## Local packaging

Run npm run build followed by npx electron-builder --win --publish never on Windows. Both NSIS and portable targets are configured. The portable EXE extracts its contents at launch; OCR dependencies must remain fully included in app.asar.unpacked.

On a Mac, npx electron-builder --mac --publish never builds both configured architectures. Explicit --arm64 or --x64 flags can select only one architecture.

Native WPF releases are built from wpf-rewrite; see windows/README.md on that branch.
