# Codemagic CI/CD for Flutter

## codemagic.yaml Configuration

Place this file at the root of your Flutter project. It defines workflows for both Android and iOS builds with automatic publishing.

```yaml
# codemagic.yaml
workflows:
  # ──────────────────────────────────────────────
  # Android Production Workflow
  # ──────────────────────────────────────────────
  android-production:
    name: Android Production
    max_build_duration: 30
    instance_type: mac_mini_m2
    environment:
      groups:
        - android_signing
        - google_play
        - firebase
      vars:
        PACKAGE_NAME: "com.example.myapp"
        FLUTTER_VERSION: "3.24.0"
      flutter: $FLUTTER_VERSION
      java: "17"
    triggering:
      events:
        - tag
      tag_patterns:
        - pattern: "v*"
          include: true
      cancel_previous_builds: true
    scripts:
      - name: Set up local.properties
        script: echo "flutter.sdk=$HOME/programs/flutter" > "$CM_BUILD_DIR/android/local.properties"

      - name: Set up key.properties
        script: |
          cat >> "$CM_BUILD_DIR/android/key.properties" <<EOF
          storePassword=$CM_KEYSTORE_PASSWORD
          keyPassword=$CM_KEY_PASSWORD
          keyAlias=$CM_KEY_ALIAS
          storeFile=$CM_KEYSTORE_PATH
          EOF

      - name: Install dependencies
        script: flutter pub get

      - name: Run static analysis
        script: flutter analyze --fatal-infos

      - name: Run tests
        script: flutter test --coverage
        test_report: build/app/reports/tests.json

      - name: Build release AAB
        script: |
          BUILD_NUMBER=$(($(google-play get-latest-build-number \
            --package-name "$PACKAGE_NAME" \
            --tracks internal) + 1))

          flutter build appbundle \
            --release \
            --flavor prod \
            --dart-define=ENV=production \
            --obfuscate \
            --split-debug-info=build/debug-info \
            --build-number=$BUILD_NUMBER
    artifacts:
      - build/app/outputs/bundle/prodRelease/**/*.aab
      - build/app/outputs/mapping/prodRelease/mapping.txt
      - build/debug-info/**
      - build/app/reports/tests.json
      - coverage/lcov.info
    publishing:
      google_play:
        credentials: $GCLOUD_SERVICE_ACCOUNT_CREDENTIALS
        track: internal
        submit_as_draft: false
      email:
        recipients:
          - team@example.com
        notify:
          success: true
          failure: true

  # ──────────────────────────────────────────────
  # iOS Production Workflow
  # ──────────────────────────────────────────────
  ios-production:
    name: iOS Production
    max_build_duration: 45
    instance_type: mac_mini_m2
    integrations:
      app_store_connect: My App Store Connect API Key
    environment:
      groups:
        - ios_signing
        - firebase
      vars:
        APP_BUNDLE_ID: "com.example.myapp"
        FLUTTER_VERSION: "3.24.0"
        XCODE_WORKSPACE: "ios/Runner.xcworkspace"
        XCODE_SCHEME: "prod"
      flutter: $FLUTTER_VERSION
      xcode: latest
      cocoapods: default
    triggering:
      events:
        - tag
      tag_patterns:
        - pattern: "v*"
          include: true
      cancel_previous_builds: true
    scripts:
      - name: Install dependencies
        script: flutter pub get

      - name: Install CocoaPods
        script: |
          cd ios
          pod install

      - name: Set up code signing
        script: |
          app-store-connect fetch-signing-files "$APP_BUNDLE_ID" \
            --type IOS_APP_STORE \
            --create
          keychain initialize
          keychain add-certificates

      - name: Set up Xcode signing
        script: xcode-project use-profiles

      - name: Run static analysis
        script: flutter analyze --fatal-infos

      - name: Run tests
        script: flutter test --coverage

      - name: Get latest build number
        script: |
          LATEST_BUILD=$(app-store-connect get-latest-testflight-build-number "$APP_BUNDLE_ID")
          echo "BUILD_NUMBER=$((LATEST_BUILD + 1))" >> "$CM_ENV"

      - name: Build release IPA
        script: |
          flutter build ipa \
            --release \
            --flavor prod \
            --dart-define=ENV=production \
            --obfuscate \
            --split-debug-info=build/debug-info \
            --build-number=$BUILD_NUMBER \
            --export-options-plist=/Users/builder/export_options.plist
    artifacts:
      - build/ios/ipa/**/*.ipa
      - build/debug-info/**
      - coverage/lcov.info
    publishing:
      app_store_connect:
        auth: integration
        submit_to_testflight: true
        expire_build_submitted_for_review: true
        beta_groups:
          - Internal Testers
      email:
        recipients:
          - team@example.com
        notify:
          success: true
          failure: true

  # ──────────────────────────────────────────────
  # Pull Request Validation Workflow
  # ──────────────────────────────────────────────
  pr-validation:
    name: PR Validation
    max_build_duration: 15
    instance_type: mac_mini_m2
    environment:
      vars:
        FLUTTER_VERSION: "3.24.0"
      flutter: $FLUTTER_VERSION
    triggering:
      events:
        - pull_request
      branch_patterns:
        - pattern: "main"
          include: true
          source: false
        - pattern: "develop"
          include: true
          source: false
      cancel_previous_builds: true
    scripts:
      - name: Install dependencies
        script: flutter pub get

      - name: Verify formatting
        script: dart format --output=none --set-exit-if-changed .

      - name: Run static analysis
        script: flutter analyze --fatal-infos

      - name: Run tests with coverage
        script: flutter test --coverage --test-randomize-ordering-seed=random

      - name: Check coverage threshold
        script: |
          sudo apt-get install -y lcov 2>/dev/null || true
          COVERAGE=$(lcov --summary coverage/lcov.info 2>&1 | grep "lines" | awk '{print $2}' | sed 's/%//')
          echo "Coverage: ${COVERAGE}%"
          if (( $(echo "$COVERAGE < 80" | bc -l) )); then
            echo "Coverage below 80% threshold"
            exit 1
          fi
    artifacts:
      - coverage/lcov.info
```

## Environment Variables and Secrets

Codemagic manages secrets through **environment variable groups**. Define groups in the Codemagic UI under **Teams > Settings > Environment variables** or directly in the YAML.

### Variable Groups

```yaml
    environment:
      groups:
        - android_signing   # CM_KEYSTORE, CM_KEYSTORE_PASSWORD, CM_KEY_ALIAS, CM_KEY_PASSWORD
        - ios_signing       # Managed automatically with App Store Connect integration
        - google_play       # GCLOUD_SERVICE_ACCOUNT_CREDENTIALS
        - firebase          # FIREBASE_TOKEN, FIREBASE_APP_ID_ANDROID, FIREBASE_APP_ID_IOS
        - slack             # SLACK_WEBHOOK_URL
```

### Predefined Codemagic Variables

| Variable | Description |
|---|---|
| `CM_BUILD_DIR` | Path to the project root |
| `CM_BUILD_ID` | Unique build ID |
| `CM_BRANCH` | Current branch name |
| `CM_TAG` | Tag that triggered the build |
| `CM_COMMIT` | Full commit SHA |
| `CM_REPO_SLUG` | Repository slug (org/repo) |
| `CM_KEYSTORE_PATH` | Path to the decoded Android keystore |
| `CM_ENV` | File to write env vars that persist between scripts |

### Persisting Variables Between Scripts

```yaml
      - name: Compute build number
        script: |
          BUILD_NUMBER=$(( $(date +%s) / 60 ))
          echo "BUILD_NUMBER=$BUILD_NUMBER" >> "$CM_ENV"

      - name: Use the build number
        script: |
          echo "Building with number: $BUILD_NUMBER"
          flutter build appbundle --build-number=$BUILD_NUMBER
```

## Build Triggers

### Branch-Based Triggers

```yaml
    triggering:
      events:
        - push
      branch_patterns:
        - pattern: "main"
          include: true
          source: true
        - pattern: "release/*"
          include: true
          source: true
        - pattern: "feature/*"
          include: false
          source: true
```

### Tag-Based Triggers

```yaml
    triggering:
      events:
        - tag
      tag_patterns:
        - pattern: "v*"
          include: true
        - pattern: "*-beta"
          include: true
```

### Pull Request Triggers

```yaml
    triggering:
      events:
        - pull_request
      branch_patterns:
        - pattern: "main"
          include: true
          source: false    # false = target branch
        - pattern: "develop"
          include: true
          source: false
      cancel_previous_builds: true
```

## iOS Signing with App Store Connect Integration

Codemagic can automatically manage iOS signing through the App Store Connect integration.

```yaml
  ios-production:
    integrations:
      app_store_connect: My API Key Name
    environment:
      vars:
        APP_BUNDLE_ID: "com.example.myapp"
    scripts:
      - name: Fetch signing files
        script: |
          app-store-connect fetch-signing-files "$APP_BUNDLE_ID" \
            --type IOS_APP_STORE \
            --create

      - name: Initialize keychain
        script: |
          keychain initialize

      - name: Add certificates to keychain
        script: |
          keychain add-certificates

      - name: Configure Xcode project signing
        script: |
          xcode-project use-profiles
```

### Manual Signing (Without Integration)

```yaml
    environment:
      groups:
        - ios_manual_signing
      # Group contains:
      #   CM_CERTIFICATE: base64-encoded .p12 certificate
      #   CM_CERTIFICATE_PASSWORD: certificate password
      #   CM_PROVISIONING_PROFILE: base64-encoded .mobileprovision
    scripts:
      - name: Set up manual signing
        script: |
          CERT_PATH=$CM_BUILD_DIR/ios/certificate.p12
          PROFILE_PATH=$CM_BUILD_DIR/ios/profile.mobileprovision

          echo "$CM_CERTIFICATE" | base64 --decode > "$CERT_PATH"
          echo "$CM_PROVISIONING_PROFILE" | base64 --decode > "$PROFILE_PATH"

          keychain initialize
          keychain add-certificates --certificate "$CERT_PATH" --certificate-password "$CM_CERTIFICATE_PASSWORD"

          PROFILE_UUID=$(/usr/libexec/PlistBuddy -c "Print UUID" /dev/stdin <<< $(security cms -D -i "$PROFILE_PATH"))
          mkdir -p ~/Library/MobileDevice/Provisioning\ Profiles
          cp "$PROFILE_PATH" ~/Library/MobileDevice/Provisioning\ Profiles/"$PROFILE_UUID.mobileprovision"
```

## Android Signing

```yaml
    environment:
      groups:
        - android_signing
      # Group contains:
      #   CM_KEYSTORE: base64-encoded keystore file (Codemagic auto-decodes to CM_KEYSTORE_PATH)
      #   CM_KEYSTORE_PASSWORD: keystore password
      #   CM_KEY_ALIAS: key alias
      #   CM_KEY_PASSWORD: key password
    scripts:
      - name: Create key.properties
        script: |
          cat > "$CM_BUILD_DIR/android/key.properties" <<EOF
          storePassword=$CM_KEYSTORE_PASSWORD
          keyPassword=$CM_KEY_PASSWORD
          keyAlias=$CM_KEY_ALIAS
          storeFile=$CM_KEYSTORE_PATH
          EOF
```

## Auto-Publish to App Store and Play Store

### Play Store Publishing

```yaml
    publishing:
      google_play:
        credentials: $GCLOUD_SERVICE_ACCOUNT_CREDENTIALS
        track: internal          # internal | alpha | beta | production
        submit_as_draft: false
        changes_not_sent_for_review: false
        in_app_update_priority: 3  # 0-5, for in-app updates
```

### App Store Publishing

```yaml
    publishing:
      app_store_connect:
        auth: integration                        # Uses the configured API key integration
        submit_to_testflight: true
        expire_build_submitted_for_review: true  # Cancel previous review submissions
        beta_groups:
          - Internal Testers
          - External Beta
        submit_to_app_store: false               # Set true for automatic App Store submission
```

## Firebase App Distribution Integration

Distribute builds to testers before store submission.

```yaml
    environment:
      groups:
        - firebase
      # Group contains:
      #   FIREBASE_TOKEN: CI token from `firebase login:ci`
      #   FIREBASE_APP_ID_ANDROID: Firebase app ID (1:123456789:android:abc123)
      #   FIREBASE_APP_ID_IOS: Firebase app ID (1:123456789:ios:abc123)
    scripts:
      - name: Build debug APK for distribution
        script: |
          flutter build apk \
            --flavor staging \
            --dart-define=ENV=staging \
            --build-number=$BUILD_NUMBER

      - name: Distribute Android to Firebase
        script: |
          firebase appdistribution:distribute \
            build/app/outputs/flutter-apk/app-staging-release.apk \
            --app "$FIREBASE_APP_ID_ANDROID" \
            --token "$FIREBASE_TOKEN" \
            --groups "qa-team,beta-testers" \
            --release-notes "Build $BUILD_NUMBER from branch $CM_BRANCH"

      - name: Distribute iOS to Firebase
        script: |
          firebase appdistribution:distribute \
            build/ios/ipa/*.ipa \
            --app "$FIREBASE_APP_ID_IOS" \
            --token "$FIREBASE_TOKEN" \
            --groups "qa-team,beta-testers" \
            --release-notes "Build $BUILD_NUMBER from branch $CM_BRANCH"
```

## Post-Build Scripts and Notifications

### Slack Notification

```yaml
    publishing:
      scripts:
        - name: Notify Slack
          script: |
            if [ "$CM_BUILD_STEP_STATUS" = "success" ]; then
              EMOJI=":white_check_mark:"
              STATUS="succeeded"
            else
              EMOJI=":x:"
              STATUS="failed"
            fi

            curl -X POST "$SLACK_WEBHOOK_URL" \
              -H "Content-Type: application/json" \
              -d "{
                \"blocks\": [
                  {
                    \"type\": \"section\",
                    \"text\": {
                      \"type\": \"mrkdwn\",
                      \"text\": \"$EMOJI *Build $STATUS*\n*App:* $CM_REPO_SLUG\n*Branch:* $CM_BRANCH\n*Commit:* \`${CM_COMMIT:0:8}\`\n*Build:* <https://codemagic.io/app/$CM_PROJECT_ID/build/$CM_BUILD_ID|View Build>\"
                    }
                  }
                ]
              }"
```

### Upload Debug Symbols to Crashlytics

```yaml
      - name: Upload debug symbols to Crashlytics
        script: |
          if [ -d "build/debug-info" ]; then
            firebase crashlytics:symbols:upload \
              --app="$FIREBASE_APP_ID_ANDROID" \
              build/debug-info/
          fi
```

### Clean Up and Artifact Management

```yaml
    artifacts:
      - build/app/outputs/bundle/prodRelease/**/*.aab
      - build/app/outputs/flutter-apk/**/*.apk
      - build/ios/ipa/**/*.ipa
      - build/debug-info/**
      - coverage/lcov.info
      - build/app/outputs/mapping/**/mapping.txt
      - flutter_drive.log
```
