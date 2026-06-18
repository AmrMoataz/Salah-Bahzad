# Fastlane Automation for Flutter

## Fastlane Setup for Flutter

### Installation

```bash
# Install Fastlane via RubyGems (recommended)
gem install fastlane

# Or via Homebrew on macOS
brew install fastlane
```

### Initialize Fastlane for iOS

```bash
cd ios
fastlane init
```

### Initialize Fastlane for Android

```bash
cd android
fastlane init
```

### Project Structure

```
my_flutter_app/
  android/
    fastlane/
      Appfile
      Fastfile
      Matchfile       # (if using match)
      Pluginfile       # (if using plugins)
  ios/
    fastlane/
      Appfile
      Fastfile
      Matchfile
      Pluginfile
```

## iOS Fastfile

```ruby
# ios/fastlane/Fastfile

default_platform(:ios)

platform :ios do
  # ──────────────────────────────────────────────
  # Setup
  # ──────────────────────────────────────────────

  before_all do
    setup_ci if ENV["CI"]
  end

  # ──────────────────────────────────────────────
  # Testing
  # ──────────────────────────────────────────────

  desc "Run Flutter tests"
  lane :test do
    Dir.chdir("..") do
      sh("flutter", "pub", "get")
      sh("flutter", "analyze", "--fatal-infos")
      sh("flutter", "test", "--coverage")
    end
  end

  # ──────────────────────────────────────────────
  # Code Signing with match
  # ──────────────────────────────────────────────

  desc "Sync development certificates"
  lane :sync_dev_certs do
    match(
      type: "development",
      app_identifier: "com.example.myapp",
      readonly: is_ci
    )
  end

  desc "Sync App Store certificates"
  lane :sync_release_certs do
    match(
      type: "appstore",
      app_identifier: "com.example.myapp",
      readonly: is_ci
    )
  end

  # ──────────────────────────────────────────────
  # Build
  # ──────────────────────────────────────────────

  desc "Build iOS release IPA"
  lane :build do |options|
    flavor = options[:flavor] || "prod"
    env = options[:env] || "production"

    sync_release_certs

    Dir.chdir("..") do
      sh(
        "flutter", "build", "ipa",
        "--release",
        "--flavor", flavor,
        "--dart-define=ENV=#{env}",
        "--obfuscate",
        "--split-debug-info=build/debug-info",
        "--export-options-plist=ios/ExportOptions.plist"
      )
    end
  end

  # ──────────────────────────────────────────────
  # Deployment
  # ──────────────────────────────────────────────

  desc "Upload to TestFlight"
  lane :deploy_testflight do |options|
    build(flavor: options[:flavor], env: options[:env])

    api_key = app_store_connect_api_key(
      key_id: ENV["APP_STORE_CONNECT_API_KEY_ID"],
      issuer_id: ENV["APP_STORE_CONNECT_ISSUER_ID"],
      key_filepath: ENV["APP_STORE_CONNECT_API_KEY_PATH"],
      in_house: false
    )

    upload_to_testflight(
      api_key: api_key,
      ipa: Dir.glob("../build/ios/ipa/*.ipa").first,
      skip_waiting_for_build_processing: true,
      distribute_external: false,
      changelog: "Build #{lane_context[SharedValues::BUILD_NUMBER]} from CI"
    )
  end

  desc "Submit to App Store for review"
  lane :deploy_app_store do |options|
    build(flavor: "prod", env: "production")

    api_key = app_store_connect_api_key(
      key_id: ENV["APP_STORE_CONNECT_API_KEY_ID"],
      issuer_id: ENV["APP_STORE_CONNECT_ISSUER_ID"],
      key_filepath: ENV["APP_STORE_CONNECT_API_KEY_PATH"],
      in_house: false
    )

    deliver(
      api_key: api_key,
      ipa: Dir.glob("../build/ios/ipa/*.ipa").first,
      submit_for_review: true,
      automatic_release: false,
      force: true,
      precheck_include_in_app_purchases: false,
      submission_information: {
        add_id_info_uses_idfa: false
      }
    )
  end

  desc "Distribute via Firebase App Distribution"
  lane :deploy_firebase do |options|
    build(flavor: options[:flavor] || "staging", env: options[:env] || "staging")

    firebase_app_distribution(
      app: ENV["FIREBASE_APP_ID_IOS"],
      ipa_path: Dir.glob("../build/ios/ipa/*.ipa").first,
      groups: "qa-team, beta-testers",
      release_notes: "Build from branch #{`git rev-parse --abbrev-ref HEAD`.strip}"
    )
  end

  # ──────────────────────────────────────────────
  # Screenshots
  # ──────────────────────────────────────────────

  desc "Capture screenshots for App Store"
  lane :screenshots do
    snapshot(
      workspace: "Runner.xcworkspace",
      scheme: "prod",
      devices: [
        "iPhone 16 Pro Max",
        "iPhone 16",
        "iPad Pro (12.9-inch) (6th generation)"
      ],
      languages: ["en-US", "es-ES", "fr-FR"],
      output_directory: "./fastlane/screenshots",
      clear_previous_screenshots: true
    )
    frameit(silver: false)
  end
end
```

## iOS Appfile

```ruby
# ios/fastlane/Appfile

app_identifier("com.example.myapp")
apple_id("developer@example.com")
itc_team_id("123456789")
team_id("ABCDEF1234")
```

## Android Fastfile

```ruby
# android/fastlane/Fastfile

default_platform(:android)

platform :android do
  # ──────────────────────────────────────────────
  # Testing
  # ──────────────────────────────────────────────

  desc "Run Flutter tests"
  lane :test do
    Dir.chdir("..") do
      sh("flutter", "pub", "get")
      sh("flutter", "analyze", "--fatal-infos")
      sh("flutter", "test", "--coverage")
    end
  end

  # ──────────────────────────────────────────────
  # Build
  # ──────────────────────────────────────────────

  desc "Build Android release App Bundle"
  lane :build do |options|
    flavor = options[:flavor] || "prod"
    env = options[:env] || "production"

    Dir.chdir("..") do
      sh(
        "flutter", "build", "appbundle",
        "--release",
        "--flavor", flavor,
        "--dart-define=ENV=#{env}",
        "--obfuscate",
        "--split-debug-info=build/debug-info"
      )
    end
  end

  desc "Build Android APK (for Firebase App Distribution)"
  lane :build_apk do |options|
    flavor = options[:flavor] || "staging"
    env = options[:env] || "staging"

    Dir.chdir("..") do
      sh(
        "flutter", "build", "apk",
        "--release",
        "--flavor", flavor,
        "--dart-define=ENV=#{env}"
      )
    end
  end

  # ──────────────────────────────────────────────
  # Deployment
  # ──────────────────────────────────────────────

  desc "Deploy to Play Store internal track"
  lane :deploy_internal do |options|
    build(flavor: options[:flavor], env: options[:env])

    supply(
      track: "internal",
      aab: Dir.glob("../build/app/outputs/bundle/prodRelease/*.aab").first,
      json_key: ENV["GOOGLE_PLAY_JSON_KEY_PATH"],
      skip_upload_metadata: true,
      skip_upload_changelogs: false,
      skip_upload_images: true,
      skip_upload_screenshots: true
    )
  end

  desc "Promote internal to beta track"
  lane :promote_to_beta do
    supply(
      track: "internal",
      track_promote_to: "beta",
      json_key: ENV["GOOGLE_PLAY_JSON_KEY_PATH"],
      skip_upload_aab: true,
      skip_upload_metadata: true,
      skip_upload_changelogs: false,
      skip_upload_images: true,
      skip_upload_screenshots: true
    )
  end

  desc "Promote beta to production"
  lane :promote_to_production do |options|
    rollout = options[:rollout] || 0.1  # 10% staged rollout by default

    supply(
      track: "beta",
      track_promote_to: "production",
      rollout: rollout.to_s,
      json_key: ENV["GOOGLE_PLAY_JSON_KEY_PATH"],
      skip_upload_aab: true,
      skip_upload_metadata: true,
      skip_upload_changelogs: false,
      skip_upload_images: true,
      skip_upload_screenshots: true
    )
  end

  desc "Full production release"
  lane :deploy_production do
    build(flavor: "prod", env: "production")

    supply(
      track: "production",
      aab: Dir.glob("../build/app/outputs/bundle/prodRelease/*.aab").first,
      json_key: ENV["GOOGLE_PLAY_JSON_KEY_PATH"],
      rollout: "1.0",
      skip_upload_metadata: false,
      skip_upload_changelogs: false,
      skip_upload_images: false,
      skip_upload_screenshots: false
    )
  end

  desc "Distribute via Firebase App Distribution"
  lane :deploy_firebase do |options|
    build_apk(flavor: options[:flavor] || "staging", env: options[:env] || "staging")

    firebase_app_distribution(
      app: ENV["FIREBASE_APP_ID_ANDROID"],
      android_artifact_type: "APK",
      android_artifact_path: Dir.glob("../build/app/outputs/flutter-apk/app-staging-release.apk").first,
      groups: "qa-team, beta-testers",
      release_notes: "Build from branch #{`git rev-parse --abbrev-ref HEAD`.strip}"
    )
  end

  # ──────────────────────────────────────────────
  # Screenshots
  # ──────────────────────────────────────────────

  desc "Capture screenshots for Play Store"
  lane :screenshots do
    screengrab(
      locales: ["en-US", "es-ES", "fr-FR"],
      clear_previous_screenshots: true,
      app_apk_path: Dir.glob("../build/app/outputs/flutter-apk/app-staging-debug.apk").first,
      tests_apk_path: Dir.glob("../build/app/outputs/apk/androidTest/staging/debug/*.apk").first,
      output_directory: "./fastlane/metadata/android/screenshots"
    )
  end
end
```

## Android Appfile

```ruby
# android/fastlane/Appfile

json_key_file(ENV["GOOGLE_PLAY_JSON_KEY_PATH"])
package_name("com.example.myapp")
```

## match for iOS Certificate Management

`match` synchronizes certificates and provisioning profiles across a team using a shared Git repository or cloud storage.

### Matchfile

```ruby
# ios/fastlane/Matchfile

git_url("https://github.com/your-org/ios-certificates.git")
storage_mode("git")

type("appstore")
app_identifier("com.example.myapp")
team_id("ABCDEF1234")

# For multiple app identifiers (e.g., with extensions)
# app_identifier(["com.example.myapp", "com.example.myapp.widget"])
```

### Initial Setup

```bash
# Generate new certificates and profiles (run once, by one team member)
cd ios
fastlane match development
fastlane match appstore

# On CI or other machines, use readonly mode
fastlane match appstore --readonly
```

### Using match in CI

```ruby
  lane :sync_release_certs do
    match(
      type: "appstore",
      app_identifier: "com.example.myapp",
      git_url: "https://github.com/your-org/ios-certificates.git",
      readonly: is_ci,
      keychain_name: ENV["MATCH_KEYCHAIN_NAME"] || "login.keychain",
      keychain_password: ENV["MATCH_KEYCHAIN_PASSWORD"]
    )
  end
```

### Environment Variables for match on CI

| Variable | Description |
|---|---|
| `MATCH_PASSWORD` | Encryption password for the certificate repo |
| `MATCH_GIT_BASIC_AUTHORIZATION` | Base64-encoded `user:token` for Git access |
| `MATCH_KEYCHAIN_NAME` | Keychain name on CI |
| `MATCH_KEYCHAIN_PASSWORD` | Keychain password on CI |

## supply for Play Store Upload

`supply` uploads APKs/AABs, metadata, screenshots, and changelogs to the Google Play Store.

### Setting Up a Service Account

1. Open the Google Play Console.
2. Go to **Settings > API access**.
3. Link or create a Google Cloud project.
4. Create a new service account with **Service Account User** role.
5. Grant the service account access in the Play Console under **Users and permissions**.
6. Download the JSON key file.

### supply Usage

```ruby
# Upload AAB to internal track
supply(
  track: "internal",
  aab: "../build/app/outputs/bundle/prodRelease/app-prod-release.aab",
  json_key: "path/to/service-account-key.json",
  package_name: "com.example.myapp",
  skip_upload_metadata: true,
  skip_upload_images: true,
  skip_upload_screenshots: true
)

# Upload metadata and screenshots only (no binary)
supply(
  skip_upload_aab: true,
  skip_upload_apk: true,
  json_key: "path/to/service-account-key.json",
  metadata_path: "./fastlane/metadata/android"
)
```

### Play Store Metadata Directory Structure

```
android/fastlane/metadata/android/
  en-US/
    title.txt                    # Max 30 chars
    short_description.txt        # Max 80 chars
    full_description.txt         # Max 4000 chars
    changelogs/
      default.txt                # Release notes
    images/
      phoneScreenshots/
        1_home.png
        2_detail.png
      sevenInchScreenshots/
      tenInchScreenshots/
      icon.png                   # 512x512
      featureGraphic.png         # 1024x500
```

## deliver for App Store Upload

`deliver` uploads IPA files, metadata, and screenshots to App Store Connect.

```ruby
deliver(
  api_key: api_key,
  ipa: "../build/ios/ipa/MyApp.ipa",
  submit_for_review: false,
  automatic_release: false,
  force: true,                     # Skip HTML preview verification
  skip_metadata: false,
  skip_screenshots: false,
  metadata_path: "./fastlane/metadata",
  screenshots_path: "./fastlane/screenshots",
  precheck_include_in_app_purchases: false,

  # App Store submission information
  submission_information: {
    add_id_info_uses_idfa: false,
    export_compliance_uses_encryption: false
  }
)
```

### App Store Metadata Directory Structure

```
ios/fastlane/metadata/
  en-US/
    name.txt                     # Max 30 chars
    subtitle.txt                 # Max 30 chars
    description.txt              # Max 4000 chars
    keywords.txt                 # Max 100 chars, comma-separated
    release_notes.txt
    promotional_text.txt         # Max 170 chars
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
    iPhone 16 Pro Max-2_detail.png
    iPad Pro (12.9-inch)-1_home.png
```

## Screengrab and Snapshot for Screenshots

### iOS Screenshots with snapshot

```ruby
# ios/fastlane/Snapfile
devices([
  "iPhone 16 Pro Max",
  "iPhone 16",
  "iPad Pro (12.9-inch) (6th generation)"
])

languages(["en-US", "es-ES", "fr-FR"])

scheme("prod")
output_directory("./fastlane/screenshots")
clear_previous_screenshots(true)
override_status_bar(true)
```

### Android Screenshots with screengrab

```ruby
# android/fastlane/Screengrabfile
locales(["en-US", "es-ES", "fr-FR"])
clear_previous_screenshots(true)
output_directory("./fastlane/metadata/android/screenshots")
app_apk_path("../build/app/outputs/flutter-apk/app-staging-debug.apk")
tests_apk_path("../build/app/outputs/apk/androidTest/staging/debug/app-staging-debug-androidTest.apk")
```

## Environment-Specific Lanes

Define a reusable build-and-deploy pattern parameterized by environment.

```ruby
# ios/fastlane/Fastfile

platform :ios do
  desc "Deploy to development environment"
  lane :deploy_dev do
    build(flavor: "dev", env: "development")
    firebase_app_distribution(
      app: ENV["FIREBASE_APP_ID_IOS_DEV"],
      ipa_path: Dir.glob("../build/ios/ipa/*.ipa").first,
      groups: "developers"
    )
  end

  desc "Deploy to staging environment"
  lane :deploy_staging do
    build(flavor: "staging", env: "staging")
    firebase_app_distribution(
      app: ENV["FIREBASE_APP_ID_IOS_STAGING"],
      ipa_path: Dir.glob("../build/ios/ipa/*.ipa").first,
      groups: "qa-team, beta-testers"
    )
  end

  desc "Deploy to production via TestFlight"
  lane :deploy_prod do
    build(flavor: "prod", env: "production")

    api_key = app_store_connect_api_key(
      key_id: ENV["APP_STORE_CONNECT_API_KEY_ID"],
      issuer_id: ENV["APP_STORE_CONNECT_ISSUER_ID"],
      key_filepath: ENV["APP_STORE_CONNECT_API_KEY_PATH"],
      in_house: false
    )

    upload_to_testflight(
      api_key: api_key,
      ipa: Dir.glob("../build/ios/ipa/*.ipa").first,
      skip_waiting_for_build_processing: true,
      beta_groups: ["Internal Testers"]
    )
  end
end
```

### Invoking Environment-Specific Lanes

```bash
# Development
cd ios && fastlane deploy_dev

# Staging
cd ios && fastlane deploy_staging

# Production
cd ios && fastlane deploy_prod

# Android equivalents
cd android && fastlane deploy_firebase flavor:staging env:staging
cd android && fastlane deploy_internal flavor:prod env:production
cd android && fastlane promote_to_production rollout:0.2
```

## Gemfile for Dependency Management

Pin Fastlane and plugin versions in a Gemfile to ensure reproducible builds.

```ruby
# ios/Gemfile (and android/Gemfile)

source "https://rubygems.org"

gem "fastlane", "~> 2.225"

plugins_path = File.join(File.dirname(__FILE__), "fastlane", "Pluginfile")
eval_gemfile(plugins_path) if File.exist?(plugins_path)
```

```ruby
# ios/fastlane/Pluginfile

gem "fastlane-plugin-firebase_app_distribution"
```

```bash
# Install dependencies
cd ios && bundle install
cd android && bundle install

# Run Fastlane through Bundler
cd ios && bundle exec fastlane deploy_testflight
```
