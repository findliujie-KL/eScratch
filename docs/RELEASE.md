# Release Guide

## Steps

1. **Bump version** in `package.json`

2. **Commit and push**

   ```bash
   git add package.json
   git commit -m "Bump version to X.Y.Z"
   git push origin main
   ```

3. **Create and push a tag**

   ```bash
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

   This triggers a GitHub Actions workflow that builds for macOS, Windows, and Linux. The Mac target explicitly builds two DMGs: `arm64` for Apple Silicon and `x64` for Intel Macs. Both have architecture suffixes, so the existing `release/*.dmg` artifact upload publishes both without filename collisions.

4. **Wait for the build to complete**

   ```bash
   gh run watch --repo findliujie-KL/eScratch
   ```

5. **Publish the release**

   ```bash
   gh release edit vX.Y.Z --repo findliujie-KL/eScratch --draft=false --latest
   ```

6. **Update homebrew-tap**

   ```bash
   # Download the dmg and get sha256
   gh release download vX.Y.Z --repo findliujie-KL/eScratch --pattern "*.dmg" --dir release-download
   shasum -a 256 release-download/*.dmg
   # Move to ~/Downloads when done
   mv release-download/*.dmg ~/Downloads/
   ```

   Edit `Casks/escratch.rb` in your Homebrew tap:

   - Update `version` to the new version
   - Set architecture-specific checksums from the two downloaded files:

     ```ruby
     arch arm: "arm64", intel: "x64"
     sha256 arm: "<SHA256 of the arm64 DMG>", intel: "<SHA256 of the x64 DMG>"
     url "https://github.com/findliujie-KL/eScratch/releases/download/v#{version}/eScratch-#{version}-#{arch}.dmg"
     ```

   - Replace both checksum placeholders with the actual hashes. Confirm the asset URLs match the published release filenames. The existing tap is arm64-only; building an Intel DMG alone does not change Homebrew's selection.
   - Check both DMGs are attached to the draft release and test the arm64 app on Apple Silicon and the Intel app on an Intel Mac before publishing.

   Commit and push.

7. **Verify**

   ```bash
   brew update
   brew upgrade --cask escratch
   ```

## Local Mac packaging

`npm run dist -- --mac --publish never` builds both Mac architectures using the configured targets. To produce just one architecture for a local check, use an explicit CLI target:

```bash
npm run build
npx electron-builder --mac dmg --arm64 --publish never
# Or: npx electron-builder --mac dmg --x64 --publish never
```

Run `node --test tests/*.cjs` to check menu-bar behavior and verify that the installed packager selects both Mac targets regardless of the runner architecture. This checks target selection, not successful execution of either packaged app.
