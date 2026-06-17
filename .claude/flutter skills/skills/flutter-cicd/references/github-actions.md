# GitHub Actions for Flutter CI/CD

## Basic Flutter CI Workflow

This workflow runs on every push and pull request. It checks out the code, sets up Flutter, resolves dependencies, runs static analysis, and executes tests.

```yaml
# .github/workflows/flutter-ci.yml
name: Flutter CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  analyze-and-test:
    name: Analyze & Test
    runs-on: ubuntu-latest
    timeout-minutes: 20

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Flutter
        uses: subosito/flutter-action@v2
        with:
          flutter-version: "3.24.0"
          channel: stable
          cache: true

      - name: Restore pub cache
        uses: actions/cache@v4
        with:
          path: |
            ~/.pub-cache
            ${{ env.FLUTTER_HOME }}/.pub-cache
          key: pub-cache-${{ runner.os }}-${{ hashFiles('**/pubspec.lock') }}
          restore-keys: |
            pub-cache-${{ runner.os }}-

      - name: Install dependencies
        run: flutter pub get

      - name: Verify formatting
        run: dart format --output=none --set-exit-if-changed .

      - name: Run static analysis
        run: flutter analyze --fatal-infos

      - name: Run tests with coverage
        run: flutter test --coverage --test-randomize-ordering-seed=random

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
          file: coverage/lcov.info
          fail_ci_if_error: false
```

## Running Tests (Unit, Widget, Integration)

### Unit and Widget Tests

Unit and widget tests run with `flutter test`. Separate them by directory convention.

```yaml
      - name: Run unit tests
        run: flutter test test/unit/ --coverage

      - name: Run widget tests
        run: flutter test test/widget/ --coverage
```

### Integration Tests

Integration tests require a device or emulator. On GitHub Actions, use the `reactivecircus/android-emulator-runner` action for Android.

```yaml
  integration-test:
    name: Integration Tests
    runs-on: ubuntu-latest
    timeout-minutes: 30

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Flutter
        uses: subosito/flutter-action@v2
        with:
          flutter-version: "3.24.0"
          channel: stable
          cache: true

      - name: Install dependencies
        run: flutter pub get

      - name: Enable KVM group perms
        run: |
          echo 'KERNEL=="kvm", GROUP="kvm", MODE="0666", OPTIONS+="static_node=kvm"' | sudo tee /etc/udev/rules.d/99-kvm4all.rules
          sudo udevadm control --reload-rules
          sudo udevadm trigger --name-match=kvm

      - name: Run integration tests on Android emulator
        uses: reactivecircus/android-emulator-runner@v2
        with:
          api-level: 34
          arch: x86_64
          profile: pixel_6
          emulator-options: -no-snapshot-save -no-window -gpu swiftshader_indirect -noaudio -no-boot-anim
          script: flutter test integration_test/ --flavor dev
```

## Code Coverage Upload

After running tests with `--coverage`, upload the `lcov.info` file to a coverage service.

```yaml
      - name: Run tests with coverage
        run: flutter test --coverage

      - name: Check coverage threshold
        run: |
          sudo apt-get install -y lcov
          COVERAGE=$(lcov --summary coverage/lcov.info 2>&1 | grep "lines" | awk '{print $2}' | sed 's/%//')
          echo "Current coverage: ${COVERAGE}%"
          THRESHOLD=80
          if (( $(echo "$COVERAGE < $THRESHOLD" | bc -l) )); then
            echo "Coverage ${COVERAGE}% is below threshold ${THRESHOLD}%"
            exit 1
          fi

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
          file: coverage/lcov.info
          flags: unittests
          fail_ci_if_error: true
```

## Build APK and IPA

### Build Android APK / App Bundle

```yaml
  build-android:
    name: Build Android
    runs-on: ubuntu-latest
    needs: analyze-and-test
    timeout-minutes: 25

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Java
        uses: actions/setup-java@v4
        with:
          distribution: temurin
          java-version: "17"

      - name: Set up Flutter
        uses: subosito/flutter-action@v2
        with:
          flutter-version: "3.24.0"
          channel: stable
          cache: true

      - name: Restore pub cache
        uses: actions/cache@v4
        with:
          path: ~/.pub-cache
          key: pub-cache-${{ runner.os }}-${{ hashFiles('**/pubspec.lock') }}

      - name: Install dependencies
        run: flutter pub get

      - name: Decode keystore
        env:
          KEYSTORE_BASE64: ${{ secrets.ANDROID_KEYSTORE_BASE64 }}
        run: echo "$KEYSTORE_BASE64" | base64 --decode > android/app/release-keystore.jks

      - name: Create key.properties
        env:
          KEY_ALIAS: ${{ secrets.ANDROID_KEY_ALIAS }}
          KEY_PASSWORD: ${{ secrets.ANDROID_KEY_PASSWORD }}
          STORE_PASSWORD: ${{ secrets.ANDROID_STORE_PASSWORD }}
        run: |
          cat > android/key.properties <<EOF
          storePassword=$STORE_PASSWORD
          keyPassword=$KEY_PASSWORD
          keyAlias=$KEY_ALIAS
          storeFile=release-keystore.jks
          EOF

      - name: Build release App Bundle
        run: |
          flutter build appbundle \
            --release \
            --flavor prod \
            --dart-define=ENV=production \
            --obfuscate \
            --split-debug-info=build/debug-info \
            --build-number=${{ github.run_number }}

      - name: Upload App Bundle artifact
        uses: actions/upload-artifact@v4
        with:
          name: android-release-aab
          path: build/app/outputs/bundle/prodRelease/*.aab
          retention-days: 14

      - name: Upload debug symbols
        uses: actions/upload-artifact@v4
        with:
          name: android-debug-symbols
          path: build/debug-info/
          retention-days: 30
```

### Build iOS IPA

```yaml
  build-ios:
    name: Build iOS
    runs-on: macos-latest
    needs: analyze-and-test
    timeout-minutes: 40

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Flutter
        uses: subosito/flutter-action@v2
        with:
          flutter-version: "3.24.0"
          channel: stable
          cache: true

      - name: Restore pub cache
        uses: actions/cache@v4
        with:
          path: ~/.pub-cache
          key: pub-cache-${{ runner.os }}-${{ hashFiles('**/pubspec.lock') }}

      - name: Install dependencies
        run: flutter pub get

      - name: Install CocoaPods dependencies
        run: cd ios && pod install

      - name: Import signing certificate
        env:
          P12_BASE64: ${{ secrets.IOS_P12_BASE64 }}
          P12_PASSWORD: ${{ secrets.IOS_P12_PASSWORD }}
          KEYCHAIN_PASSWORD: ${{ secrets.IOS_KEYCHAIN_PASSWORD }}
        run: |
          CERTIFICATE_PATH=$RUNNER_TEMP/certificate.p12
          KEYCHAIN_PATH=$RUNNER_TEMP/app-signing.keychain-db

          echo "$P12_BASE64" | base64 --decode > "$CERTIFICATE_PATH"

          security create-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
          security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
          security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
          security import "$CERTIFICATE_PATH" -P "$P12_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN_PATH"
          security set-key-partition-list -S apple-tool:,apple: -k "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
          security list-keychain -d user -s "$KEYCHAIN_PATH"

      - name: Install provisioning profile
        env:
          PROVISIONING_PROFILE_BASE64: ${{ secrets.IOS_PROVISIONING_PROFILE_BASE64 }}
        run: |
          PP_PATH=$RUNNER_TEMP/profile.mobileprovision
          echo "$PROVISIONING_PROFILE_BASE64" | base64 --decode > "$PP_PATH"
          mkdir -p ~/Library/MobileDevice/Provisioning\ Profiles
          UUID=$(/usr/libexec/PlistBuddy -c "Print UUID" /dev/stdin <<< $(/usr/bin/security cms -D -i "$PP_PATH"))
          cp "$PP_PATH" ~/Library/MobileDevice/Provisioning\ Profiles/"$UUID.mobileprovision"

      - name: Build release IPA
        run: |
          flutter build ipa \
            --release \
            --flavor prod \
            --dart-define=ENV=production \
            --obfuscate \
            --split-debug-info=build/debug-info \
            --build-number=${{ github.run_number }} \
            --export-options-plist=ios/ExportOptions.plist

      - name: Upload IPA artifact
        uses: actions/upload-artifact@v4
        with:
          name: ios-release-ipa
          path: build/ios/ipa/*.ipa
          retention-days: 14

      - name: Clean up keychain
        if: always()
        run: security delete-keychain $RUNNER_TEMP/app-signing.keychain-db
```

## Caching Strategies

### Pub Cache and Gradle Cache

```yaml
      # Pub dependency cache
      - name: Restore pub cache
        uses: actions/cache@v4
        with:
          path: |
            ~/.pub-cache
            ${{ env.FLUTTER_HOME }}/.pub-cache
          key: pub-cache-${{ runner.os }}-${{ hashFiles('**/pubspec.lock') }}
          restore-keys: |
            pub-cache-${{ runner.os }}-

      # Gradle cache (Android builds)
      - name: Restore Gradle cache
        uses: actions/cache@v4
        with:
          path: |
            ~/.gradle/caches
            ~/.gradle/wrapper
          key: gradle-${{ runner.os }}-${{ hashFiles('**/*.gradle*', '**/gradle-wrapper.properties') }}
          restore-keys: |
            gradle-${{ runner.os }}-

      # CocoaPods cache (iOS builds)
      - name: Restore CocoaPods cache
        uses: actions/cache@v4
        with:
          path: ios/Pods
          key: pods-${{ runner.os }}-${{ hashFiles('ios/Podfile.lock') }}
          restore-keys: |
            pods-${{ runner.os }}-
```

## Matrix Builds (Multiple Flutter Versions)

Test against multiple Flutter SDK versions to verify forward compatibility.

```yaml
  matrix-test:
    name: Test on Flutter ${{ matrix.flutter-version }}
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        flutter-version: ["3.22.0", "3.24.0"]

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Flutter ${{ matrix.flutter-version }}
        uses: subosito/flutter-action@v2
        with:
          flutter-version: ${{ matrix.flutter-version }}
          channel: stable
          cache: true

      - name: Install dependencies
        run: flutter pub get

      - name: Run analysis
        run: flutter analyze --fatal-infos

      - name: Run tests
        run: flutter test --test-randomize-ordering-seed=random
```

## Automated Deployment on Tag/Release

Trigger deployment workflows when a semantic version tag is pushed.

```yaml
# .github/workflows/flutter-release.yml
name: Flutter Release

on:
  push:
    tags:
      - "v[0-9]+.[0-9]+.[0-9]+*"

permissions:
  contents: write

jobs:
  extract-version:
    name: Extract Version
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
      build_number: ${{ steps.version.outputs.build_number }}
    steps:
      - name: Extract version from tag
        id: version
        run: |
          TAG=${GITHUB_REF#refs/tags/v}
          echo "version=$TAG" >> "$GITHUB_OUTPUT"
          # Build number from run number ensures unique, incrementing values
          echo "build_number=${{ github.run_number }}" >> "$GITHUB_OUTPUT"

  build-and-deploy-android:
    name: Deploy Android
    runs-on: ubuntu-latest
    needs: extract-version
    timeout-minutes: 30

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Java
        uses: actions/setup-java@v4
        with:
          distribution: temurin
          java-version: "17"

      - name: Set up Flutter
        uses: subosito/flutter-action@v2
        with:
          flutter-version: "3.24.0"
          channel: stable
          cache: true

      - name: Install dependencies
        run: flutter pub get

      - name: Decode keystore
        env:
          KEYSTORE_BASE64: ${{ secrets.ANDROID_KEYSTORE_BASE64 }}
        run: echo "$KEYSTORE_BASE64" | base64 --decode > android/app/release-keystore.jks

      - name: Create key.properties
        env:
          KEY_ALIAS: ${{ secrets.ANDROID_KEY_ALIAS }}
          KEY_PASSWORD: ${{ secrets.ANDROID_KEY_PASSWORD }}
          STORE_PASSWORD: ${{ secrets.ANDROID_STORE_PASSWORD }}
        run: |
          cat > android/key.properties <<EOF
          storePassword=$STORE_PASSWORD
          keyPassword=$KEY_PASSWORD
          keyAlias=$KEY_ALIAS
          storeFile=release-keystore.jks
          EOF

      - name: Build release App Bundle
        run: |
          flutter build appbundle \
            --release \
            --flavor prod \
            --dart-define=ENV=production \
            --obfuscate \
            --split-debug-info=build/debug-info \
            --build-number=${{ needs.extract-version.outputs.build_number }}

      - name: Upload to Play Store (internal track)
        uses: r0adkll/upload-google-play@v1
        with:
          serviceAccountJsonPlainText: ${{ secrets.GOOGLE_PLAY_SERVICE_ACCOUNT_JSON }}
          packageName: com.example.myapp
          releaseFiles: build/app/outputs/bundle/prodRelease/*.aab
          track: internal
          status: completed
          mappingFile: build/app/outputs/mapping/prodRelease/mapping.txt

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          tag_name: ${{ github.ref_name }}
          name: Release ${{ needs.extract-version.outputs.version }}
          generate_release_notes: true
          files: |
            build/app/outputs/bundle/prodRelease/*.aab

  build-and-deploy-ios:
    name: Deploy iOS
    runs-on: macos-latest
    needs: extract-version
    timeout-minutes: 45

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Set up Flutter
        uses: subosito/flutter-action@v2
        with:
          flutter-version: "3.24.0"
          channel: stable
          cache: true

      - name: Install dependencies
        run: flutter pub get

      - name: Install CocoaPods dependencies
        run: cd ios && pod install

      - name: Import signing certificate
        env:
          P12_BASE64: ${{ secrets.IOS_P12_BASE64 }}
          P12_PASSWORD: ${{ secrets.IOS_P12_PASSWORD }}
          KEYCHAIN_PASSWORD: ${{ secrets.IOS_KEYCHAIN_PASSWORD }}
        run: |
          CERTIFICATE_PATH=$RUNNER_TEMP/certificate.p12
          KEYCHAIN_PATH=$RUNNER_TEMP/app-signing.keychain-db

          echo "$P12_BASE64" | base64 --decode > "$CERTIFICATE_PATH"

          security create-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
          security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
          security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
          security import "$CERTIFICATE_PATH" -P "$P12_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN_PATH"
          security set-key-partition-list -S apple-tool:,apple: -k "$KEYCHAIN_PASSWORD" "$KEYCHAIN_PATH"
          security list-keychain -d user -s "$KEYCHAIN_PATH"

      - name: Install provisioning profile
        env:
          PROVISIONING_PROFILE_BASE64: ${{ secrets.IOS_PROVISIONING_PROFILE_BASE64 }}
        run: |
          PP_PATH=$RUNNER_TEMP/profile.mobileprovision
          echo "$PROVISIONING_PROFILE_BASE64" | base64 --decode > "$PP_PATH"
          mkdir -p ~/Library/MobileDevice/Provisioning\ Profiles
          UUID=$(/usr/libexec/PlistBuddy -c "Print UUID" /dev/stdin <<< $(/usr/bin/security cms -D -i "$PP_PATH"))
          cp "$PP_PATH" ~/Library/MobileDevice/Provisioning\ Profiles/"$UUID.mobileprovision"

      - name: Build release IPA
        run: |
          flutter build ipa \
            --release \
            --flavor prod \
            --dart-define=ENV=production \
            --obfuscate \
            --split-debug-info=build/debug-info \
            --build-number=${{ needs.extract-version.outputs.build_number }} \
            --export-options-plist=ios/ExportOptions.plist

      - name: Upload to App Store Connect
        env:
          APP_STORE_CONNECT_API_KEY_ID: ${{ secrets.APP_STORE_CONNECT_API_KEY_ID }}
          APP_STORE_CONNECT_ISSUER_ID: ${{ secrets.APP_STORE_CONNECT_ISSUER_ID }}
          APP_STORE_CONNECT_API_KEY_BASE64: ${{ secrets.APP_STORE_CONNECT_API_KEY_BASE64 }}
        run: |
          API_KEY_PATH=$RUNNER_TEMP/AuthKey.p8
          echo "$APP_STORE_CONNECT_API_KEY_BASE64" | base64 --decode > "$API_KEY_PATH"

          xcrun altool --upload-app \
            --type ios \
            --file build/ios/ipa/*.ipa \
            --apiKey "$APP_STORE_CONNECT_API_KEY_ID" \
            --apiIssuer "$APP_STORE_CONNECT_ISSUER_ID"

      - name: Clean up keychain
        if: always()
        run: security delete-keychain $RUNNER_TEMP/app-signing.keychain-db
```

## Secrets Management for Signing

### Required GitHub Secrets

Configure these secrets in your repository settings under **Settings > Secrets and variables > Actions**.

#### Android Signing Secrets

| Secret Name | Description | How to Generate |
|---|---|---|
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded release keystore | `base64 -i release-keystore.jks` |
| `ANDROID_KEY_ALIAS` | Key alias in the keystore | Chosen during keystore generation |
| `ANDROID_KEY_PASSWORD` | Password for the key | Chosen during keystore generation |
| `ANDROID_STORE_PASSWORD` | Password for the keystore file | Chosen during keystore generation |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Service account JSON for Play Store API | Google Cloud Console > Service Accounts |

#### iOS Signing Secrets

| Secret Name | Description | How to Generate |
|---|---|---|
| `IOS_P12_BASE64` | Base64-encoded .p12 distribution certificate | Export from Keychain Access, then `base64 -i cert.p12` |
| `IOS_P12_PASSWORD` | Password for the .p12 file | Set during export |
| `IOS_KEYCHAIN_PASSWORD` | Temporary keychain password for CI | Any strong random string |
| `IOS_PROVISIONING_PROFILE_BASE64` | Base64-encoded .mobileprovision file | `base64 -i profile.mobileprovision` |
| `APP_STORE_CONNECT_API_KEY_ID` | App Store Connect API key ID | App Store Connect > Users and Access > Keys |
| `APP_STORE_CONNECT_ISSUER_ID` | App Store Connect issuer ID | Same page as above |
| `APP_STORE_CONNECT_API_KEY_BASE64` | Base64-encoded .p8 API key file | `base64 -i AuthKey_XXXXXXXX.p8` |

### Encoding Secrets

```bash
# Encode the Android keystore
base64 -i android/app/release-keystore.jks | pbcopy

# Encode the iOS certificate
base64 -i distribution-certificate.p12 | pbcopy

# Encode the provisioning profile
base64 -i App_Distribution.mobileprovision | pbcopy

# Encode the App Store Connect API key
base64 -i AuthKey_XXXXXXXXXX.p8 | pbcopy
```

### ExportOptions.plist for iOS

Place this file at `ios/ExportOptions.plist`. Update the values to match your team and provisioning profile.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>method</key>
    <string>app-store</string>
    <key>teamID</key>
    <string>YOUR_TEAM_ID</string>
    <key>uploadBitcode</key>
    <false/>
    <key>uploadSymbols</key>
    <true/>
    <key>signingStyle</key>
    <string>manual</string>
    <key>provisioningProfiles</key>
    <dict>
        <key>com.example.myapp</key>
        <string>Your Distribution Profile Name</string>
    </dict>
</dict>
</plist>
```
