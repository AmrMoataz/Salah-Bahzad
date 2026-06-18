# App Store Submission for Flutter

## Flutter Flavors

Flavors let you produce multiple variants of your app (dev, staging, prod) from a single codebase with different app IDs, names, icons, and backend URLs.

### Android Flavors

Configure flavors in `android/app/build.gradle`:

```groovy
// android/app/build.gradle

android {
    // ...

    flavorDimensions "environment"

    productFlavors {
        dev {
            dimension "environment"
            applicationIdSuffix ".dev"
            versionNameSuffix "-dev"
            resValue "string", "app_name", "MyApp Dev"
        }
        staging {
            dimension "environment"
            applicationIdSuffix ".staging"
            versionNameSuffix "-staging"
            resValue "string", "app_name", "MyApp Staging"
        }
        prod {
            dimension "environment"
            resValue "string", "app_name", "MyApp"
        }
    }
}
```

### iOS Flavors (Schemes and Configurations)

In Xcode, create build configurations and schemes for each flavor:

1. Duplicate **Release** configuration three times: `Release-dev`, `Release-staging`, `Release-prod`.
2. Duplicate **Debug** configuration three times: `Debug-dev`, `Debug-staging`, `Debug-prod`.
3. Create three schemes: `dev`, `staging`, `prod`. Each scheme maps to its corresponding configurations.
4. Set different bundle identifiers per configuration in the Build Settings:

| Configuration | Bundle Identifier |
|---|---|
| Debug-dev / Release-dev | com.example.myapp.dev |
| Debug-staging / Release-staging | com.example.myapp.staging |
| Debug-prod / Release-prod | com.example.myapp |

### Building with Flavors

```bash
# Development
flutter run --flavor dev --dart-define=ENV=development

# Staging
flutter run --flavor staging --dart-define=ENV=staging

# Production release
flutter build appbundle --release --flavor prod --dart-define=ENV=production
flutter build ipa --release --flavor prod --dart-define=ENV=production
```

## Environment Configuration with --dart-define

Use `--dart-define` to inject environment-specific values at compile time. These values are tree-shaken and not visible in the compiled binary.

### Dart Configuration Class

```dart
// lib/config/app_config.dart

class AppConfig {
  static const String environment = String.fromEnvironment(
    'ENV',
    defaultValue: 'development',
  );

  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://api-dev.example.com',
  );

  static const bool enableAnalytics = bool.fromEnvironment(
    'ENABLE_ANALYTICS',
    defaultValue: false,
  );

  static const String sentryDsn = String.fromEnvironment(
    'SENTRY_DSN',
    defaultValue: '',
  );

  static bool get isProduction => environment == 'production';
  static bool get isStaging => environment == 'staging';
  static bool get isDevelopment => environment == 'development';
}
```

### Using a --dart-define-from-file

Create environment-specific JSON files to avoid long CLI commands:

```json
// config/dev.json
{
  "ENV": "development",
  "API_BASE_URL": "https://api-dev.example.com",
  "ENABLE_ANALYTICS": false,
  "SENTRY_DSN": ""
}
```

```json
// config/staging.json
{
  "ENV": "staging",
  "API_BASE_URL": "https://api-staging.example.com",
  "ENABLE_ANALYTICS": true,
  "SENTRY_DSN": "https://abc123@sentry.io/456"
}
```

```json
// config/prod.json
{
  "ENV": "production",
  "API_BASE_URL": "https://api.example.com",
  "ENABLE_ANALYTICS": true,
  "SENTRY_DSN": "https://def456@sentry.io/789"
}
```

```bash
# Build with environment file
flutter run --flavor dev --dart-define-from-file=config/dev.json
flutter build appbundle --release --flavor prod --dart-define-from-file=config/prod.json
```

## Build Number Versioning Strategy

A robust versioning strategy separates the human-readable version name from the machine-readable build number.

### Version Format

```
version: 2.5.0+142
         ^^^^^  ^^^
         |      |
         |      +-- Build number (versionCode on Android, CFBundleVersion on iOS)
         +--------- Version name (versionName on Android, CFBundleShortVersionString on iOS)
```

### Strategies for Build Numbers

| Strategy | Formula | Pros | Cons |
|---|---|---|---|
| CI run number | `github.run_number` | Simple, auto-incrementing | Resets if workflow is recreated |
| Timestamp-based | `date +%Y%m%d%H%M` | Always unique, sortable | Large numbers |
| Store-derived | `latest_store_build + 1` | Guaranteed to exceed previous | Requires API call |
| Git commit count | `git rev-list --count HEAD` | Reproducible | Can conflict on merge |

### Automated Versioning in pubspec.yaml

Do not manually update build numbers. Override them at build time:

```bash
# Override build number from CI
flutter build appbundle \
  --release \
  --flavor prod \
  --build-number=$CI_BUILD_NUMBER

# Override both version and build number
flutter build appbundle \
  --release \
  --flavor prod \
  --build-name=2.5.0 \
  --build-number=$CI_BUILD_NUMBER
```

### Codemagic: Auto-Increment from Store

```yaml
      - name: Compute build number
        script: |
          LATEST=$(google-play get-latest-build-number \
            --package-name "$PACKAGE_NAME" \
            --tracks internal)
          echo "BUILD_NUMBER=$((LATEST + 1))" >> "$CM_ENV"
```

## Android Signing

### Generate a Release Keystore

```bash
keytool -genkey -v \
  -keystore release-keystore.jks \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000 \
  -alias release \
  -storepass YOUR_STORE_PASSWORD \
  -keypass YOUR_KEY_PASSWORD \
  -dname "CN=Your Name, OU=Your Org, O=Your Company, L=City, S=State, C=US"
```

### key.properties File

Create `android/key.properties` (add this file to `.gitignore`):

```properties
# android/key.properties
storePassword=YOUR_STORE_PASSWORD
keyPassword=YOUR_KEY_PASSWORD
keyAlias=release
storeFile=../release-keystore.jks
```

### Reference key.properties in build.gradle

```groovy
// android/app/build.gradle

def keystoreProperties = new Properties()
def keystorePropertiesFile = rootProject.file('key.properties')
if (keystorePropertiesFile.exists()) {
    keystoreProperties.load(new FileInputStream(keystorePropertiesFile))
}

android {
    // ...

    signingConfigs {
        release {
            keyAlias keystoreProperties['keyAlias']
            keyPassword keystoreProperties['keyPassword']
            storeFile keystoreProperties['storeFile'] ? file(keystoreProperties['storeFile']) : null
            storePassword keystoreProperties['storePassword']
        }
    }

    buildTypes {
        release {
            signingConfig signingConfigs.release
            minifyEnabled true
            shrinkResources true
            proguardFiles getDefaultProguardFile('proguard-android-optimize.txt'), 'proguard-rules.pro'
        }
    }
}
```

### .gitignore Entries for Signing

```gitignore
# Android signing
android/key.properties
android/app/release-keystore.jks
*.jks
*.keystore
```

## iOS Signing

### Certificate Types

| Type | Usage |
|---|---|
| Apple Development | Debug builds on physical devices |
| Apple Distribution | App Store and TestFlight submissions |
| iOS Distribution | Older equivalent of Apple Distribution |

### Provisioning Profile Types

| Type | Usage |
|---|---|
| Development | Debug on registered devices |
| Ad Hoc | Install on specific registered devices (up to 100) |
| App Store | App Store and TestFlight |
| Enterprise | In-house distribution (requires Enterprise account) |

### Manual Certificate Setup

1. Create a Certificate Signing Request (CSR) in Keychain Access.
2. Upload the CSR to Apple Developer > Certificates.
3. Download and install the certificate.
4. Create a provisioning profile under Profiles, selecting the certificate and app ID.
5. Download and double-click the profile to install it.

### Automatic Signing with Fastlane match

```bash
# One-time setup: initialize match repository
cd ios
fastlane match init

# Generate certificates (run once by team lead)
fastlane match development
fastlane match appstore

# On other machines or CI (readonly mode)
fastlane match appstore --readonly
```

### ExportOptions.plist

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>method</key>
    <string>app-store</string>
    <key>teamID</key>
    <string>ABCDEF1234</string>
    <key>uploadBitcode</key>
    <false/>
    <key>uploadSymbols</key>
    <true/>
    <key>signingStyle</key>
    <string>manual</string>
    <key>provisioningProfiles</key>
    <dict>
        <key>com.example.myapp</key>
        <string>match AppStore com.example.myapp</string>
    </dict>
</dict>
</plist>
```

## Play Store Metadata and Screenshots

### Required Assets

| Asset | Specification |
|---|---|
| App icon | 512 x 512 px, PNG, 32-bit |
| Feature graphic | 1024 x 500 px, PNG or JPEG |
| Phone screenshots | Min 2, max 8. Between 320px and 3840px per side. |
| 7-inch tablet screenshots | Optional but recommended |
| 10-inch tablet screenshots | Optional but recommended |
| Short description | Max 80 characters |
| Full description | Max 4000 characters |

### Metadata Directory (for Fastlane supply)

```
android/fastlane/metadata/android/
  en-US/
    title.txt
    short_description.txt
    full_description.txt
    video.txt                    # YouTube URL (optional)
    changelogs/
      default.txt
      142.txt                    # Version-code-specific changelog
    images/
      icon.png
      featureGraphic.png
      phoneScreenshots/
        1_home.png
        2_search.png
        3_detail.png
        4_profile.png
      sevenInchScreenshots/
      tenInchScreenshots/
```

## App Store Connect Metadata

### Required Assets

| Asset | Specification |
|---|---|
| App icon | 1024 x 1024 px, PNG, no alpha, no rounded corners |
| 6.7" screenshots | 1290 x 2796 px (iPhone 16 Pro Max). Min 1, max 10. |
| 6.5" screenshots | 1284 x 2778 px or 1242 x 2688 px |
| 5.5" screenshots | 1242 x 2208 px |
| 12.9" iPad screenshots | 2048 x 2732 px |
| App name | Max 30 characters |
| Subtitle | Max 30 characters |
| Description | Max 4000 characters |
| Keywords | Max 100 characters, comma-separated |
| Promotional text | Max 170 characters |

### Metadata Directory (for Fastlane deliver)

```
ios/fastlane/metadata/
  en-US/
    name.txt
    subtitle.txt
    description.txt
    keywords.txt
    release_notes.txt
    promotional_text.txt
    support_url.txt
    marketing_url.txt
    privacy_url.txt
  review_information/
    demo_user.txt
    demo_password.txt
    notes.txt
ios/fastlane/screenshots/
  en-US/
    iPhone 16 Pro Max-1_home.png
    iPhone 16 Pro Max-2_search.png
    iPhone 16-1_home.png
    iPad Pro (12.9-inch) (6th generation)-1_home.png
```

## TestFlight and Internal Testing

### TestFlight Distribution

TestFlight allows up to 10,000 external testers and unlimited internal testers. Internal testers receive builds automatically; external testers require a beta review.

```bash
# Build and upload to TestFlight using Fastlane
cd ios && bundle exec fastlane deploy_testflight
```

```ruby
# Fastlane lane for TestFlight
upload_to_testflight(
  api_key: api_key,
  ipa: "../build/ios/ipa/MyApp.ipa",
  skip_waiting_for_build_processing: true,
  distribute_external: true,
  groups: ["External Beta"],
  changelog: "Bug fixes and performance improvements",
  beta_app_review_info: {
    contact_email: "beta@example.com",
    contact_first_name: "Jane",
    contact_last_name: "Doe",
    contact_phone: "+1-555-0100",
    demo_account_name: "demo@example.com",
    demo_account_password: "demo123"
  }
)
```

### Google Play Internal Testing

Internal testing tracks allow up to 100 testers and do not require a review.

```bash
# Upload to internal track using Fastlane
cd android && bundle exec fastlane deploy_internal
```

### Staged Rollout on Google Play

```ruby
# Start with 10% rollout
supply(
  track: "production",
  rollout: "0.1",
  aab: "../build/app/outputs/bundle/prodRelease/app-prod-release.aab",
  json_key: ENV["GOOGLE_PLAY_JSON_KEY_PATH"]
)

# Increase to 50%
supply(
  track: "production",
  rollout: "0.5",
  json_key: ENV["GOOGLE_PLAY_JSON_KEY_PATH"],
  skip_upload_aab: true
)

# Full rollout
supply(
  track: "production",
  rollout: "1.0",
  json_key: ENV["GOOGLE_PLAY_JSON_KEY_PATH"],
  skip_upload_aab: true
)
```

## Firebase App Distribution for Beta Testing

Firebase App Distribution provides a fast way to distribute pre-release builds to trusted testers without the overhead of TestFlight reviews or Play Store tracks.

### Setup

```bash
# Install the Firebase CLI
npm install -g firebase-tools

# Authenticate
firebase login

# Generate a CI token
firebase login:ci
# Save the output token as FIREBASE_TOKEN in your CI secrets

# Install the Fastlane plugin
cd ios && fastlane add_plugin firebase_app_distribution
cd android && fastlane add_plugin firebase_app_distribution
```

### Direct CLI Distribution

```bash
# Distribute Android APK
firebase appdistribution:distribute \
  build/app/outputs/flutter-apk/app-staging-release.apk \
  --app "1:123456789:android:abc123def456" \
  --groups "qa-team,beta-testers" \
  --release-notes "Version 2.5.0 build 142: Fixed login bug, improved performance"

# Distribute iOS IPA
firebase appdistribution:distribute \
  build/ios/ipa/MyApp.ipa \
  --app "1:123456789:ios:abc123def456" \
  --groups "qa-team,beta-testers" \
  --release-notes "Version 2.5.0 build 142: Fixed login bug, improved performance"
```

### Fastlane Integration

```ruby
firebase_app_distribution(
  app: ENV["FIREBASE_APP_ID_ANDROID"],
  android_artifact_type: "APK",
  android_artifact_path: "../build/app/outputs/flutter-apk/app-staging-release.apk",
  groups: "qa-team, beta-testers",
  release_notes_file: "../release-notes.txt",
  firebase_cli_token: ENV["FIREBASE_TOKEN"]
)
```

### Managing Tester Groups

```bash
# Add testers
firebase appdistribution:testers:add \
  --project my-project \
  user1@example.com user2@example.com

# Create a group
firebase appdistribution:group:create beta-testers \
  --project my-project

# Add testers to a group
firebase appdistribution:testers:add \
  --project my-project \
  --group-alias beta-testers \
  user1@example.com user2@example.com
```

## Common Rejection Reasons and Prevention

### App Store Rejections

| Rejection Reason | Prevention |
|---|---|
| **Crashes and bugs** | Run the full test suite in CI. Test on multiple real devices. Include crash reporting (Crashlytics). |
| **Broken links** | Verify all URLs in the app and metadata before submission. |
| **Incomplete information** | Fill in all required metadata fields. Provide demo credentials in review notes if the app requires login. |
| **Misleading description** | Ensure the description and screenshots accurately represent the app. |
| **Privacy policy missing** | Add a valid privacy policy URL in App Store Connect and inside the app. |
| **Insufficient permissions justification** | Add `NSCameraUsageDescription`, `NSLocationWhenInUseUsageDescription`, etc. in `Info.plist` with clear, user-facing explanations. |
| **Login required without demo account** | Always provide a demo account in review notes. |
| **Third-party payment links** | Do not include links to external payment systems for digital goods. |
| **Missing IPv6 support** | Test on IPv6-only networks. Flutter apps generally support this by default. |
| **Performance (app too slow)** | Profile with DevTools. Optimize images. Use lazy loading. |

### iOS Info.plist Usage Description Keys

Add clear, human-readable descriptions for every permission your app uses:

```xml
<!-- ios/Runner/Info.plist -->
<key>NSCameraUsageDescription</key>
<string>This app uses the camera to scan barcodes and take profile photos.</string>

<key>NSPhotoLibraryUsageDescription</key>
<string>This app accesses your photo library to let you choose a profile picture.</string>

<key>NSLocationWhenInUseUsageDescription</key>
<string>This app uses your location to show nearby stores and delivery options.</string>

<key>NSMicrophoneUsageDescription</key>
<string>This app uses the microphone for voice messages.</string>

<key>NSFaceIDUsageDescription</key>
<string>This app uses Face ID for secure authentication.</string>
```

### Google Play Rejections

| Rejection Reason | Prevention |
|---|---|
| **Policy violation: data safety** | Complete the Data Safety section accurately in the Play Console. |
| **Target API level too low** | Set `targetSdkVersion` to the latest required level (currently 34). |
| **Missing privacy policy** | Link a privacy policy in the Play Console store listing and inside the app. |
| **Deceptive behavior** | Do not request unnecessary permissions. Clearly describe all data collection. |
| **Broken functionality** | Test on multiple screen sizes and Android API levels. |
| **Inappropriate content rating** | Complete the content rating questionnaire honestly. |
| **Missing ads disclosure** | If using ads, declare it in the store listing and use the appropriate content rating. |

### Pre-Submission Checklist

```
Android:
  [ ] targetSdkVersion meets current Play Store requirement
  [ ] Signed with release keystore
  [ ] ProGuard / R8 enabled for release builds
  [ ] App Bundle (not APK) for Play Store submission
  [ ] Data Safety section completed in Play Console
  [ ] Privacy policy URL added to store listing
  [ ] Content rating questionnaire completed
  [ ] All screenshots match actual app UI
  [ ] Release notes written for the current version
  [ ] Tested on API levels 28 through 34

iOS:
  [ ] Minimum deployment target set appropriately
  [ ] Signed with Distribution certificate and App Store profile
  [ ] All usage description keys present in Info.plist
  [ ] Privacy policy URL added in App Store Connect
  [ ] Demo account provided in review notes
  [ ] Screenshots provided for all required device sizes
  [ ] App Category selected
  [ ] Age rating questionnaire completed
  [ ] Export compliance information provided
  [ ] Tested on latest iOS version and at least one prior version
```

### Release Build Flags

Always use these flags for production release builds:

```bash
flutter build appbundle \
  --release \
  --flavor prod \
  --dart-define-from-file=config/prod.json \
  --obfuscate \
  --split-debug-info=build/debug-info \
  --build-number=$BUILD_NUMBER

flutter build ipa \
  --release \
  --flavor prod \
  --dart-define-from-file=config/prod.json \
  --obfuscate \
  --split-debug-info=build/debug-info \
  --build-number=$BUILD_NUMBER \
  --export-options-plist=ios/ExportOptions.plist
```

| Flag | Purpose |
|---|---|
| `--release` | Compile in release mode with optimizations |
| `--obfuscate` | Obfuscate Dart code to protect intellectual property |
| `--split-debug-info` | Extract debug symbols for deobfuscating stack traces (upload to Crashlytics) |
| `--flavor` | Select the build flavor (dev, staging, prod) |
| `--dart-define-from-file` | Inject compile-time environment variables |
| `--build-number` | Override the build number for store submission |
| `--export-options-plist` | iOS-only: specify signing and export configuration |
