# Google ML Kit for Flutter

## Table of Contents

- [Setup](#setup)
- [InputImage from Different Sources](#inputimage-from-different-sources)
- [Text Recognition (OCR)](#text-recognition-ocr)
- [Face Detection](#face-detection)
- [Barcode Scanning](#barcode-scanning)
- [Image Labeling](#image-labeling)
- [Pose Detection](#pose-detection)
- [Camera Integration](#camera-integration)
- [Real-Time Frame Processing](#real-time-frame-processing)
- [Performance Tips](#performance-tips)

---

## Setup

### pubspec.yaml

Add only the ML Kit packages you actually need:

```yaml
dependencies:
  google_mlkit_text_recognition: ^0.14.0
  google_mlkit_face_detection: ^0.12.0
  google_mlkit_barcode_scanning: ^0.13.0
  google_mlkit_image_labeling: ^0.12.0
  google_mlkit_pose_detection: ^0.10.0
  camera: ^0.11.0           # For live camera feed
  image_picker: ^1.1.0      # For gallery/camera capture
```

### Android (`android/app/build.gradle`)

```groovy
android {
    defaultConfig {
        minSdk = 21
    }
}

dependencies {
    // ML Kit downloads models on first use by default.
    // To bundle models at install time, add:
    // implementation 'com.google.mlkit:text-recognition:16.0.0'
}
```

Add to `AndroidManifest.xml` for on-device model download:

```xml
<application>
    <meta-data
        android:name="com.google.mlkit.vision.DEPENDENCIES"
        android:value="ocr,face,barcode,label,pose" />
</application>
```

### iOS (`ios/Podfile`)

```ruby
platform :ios, '15.0'
```

In `Info.plist`, add camera and photo library permissions:

```xml
<key>NSCameraUsageDescription</key>
<string>Camera access is needed for scanning and detection.</string>
<key>NSPhotoLibraryUsageDescription</key>
<string>Photo library access is needed to select images for analysis.</string>
```

---

## InputImage from Different Sources

Every ML Kit detector accepts an `InputImage`. Here is how to create one from
the most common sources:

```dart
import 'dart:io';
import 'dart:typed_data';

import 'package:camera/camera.dart';
import 'package:flutter/foundation.dart';
import 'package:google_mlkit_commons/google_mlkit_commons.dart';
import 'package:image_picker/image_picker.dart';

/// Creates an [InputImage] from various sources.
abstract final class InputImageFactory {
  /// From a file path (e.g. after saving a photo).
  static InputImage fromFilePath(String path) {
    return InputImage.fromFilePath(path);
  }

  /// From a [File] object.
  static InputImage fromFile(File file) {
    return InputImage.fromFile(file);
  }

  /// From an [XFile] returned by the image_picker package.
  static InputImage fromXFile(XFile xFile) {
    return InputImage.fromFilePath(xFile.path);
  }

  /// From a [CameraImage] captured by the camera package.
  ///
  /// [rotation] should come from [CameraDescription.sensorOrientation].
  static InputImage? fromCameraImage(
    CameraImage cameraImage, {
    required InputImageRotation rotation,
  }) {
    final format = InputImageFormatValue.fromRawValue(
      cameraImage.format.raw as int,
    );
    if (format == null) return null;

    // Most Android cameras output NV21 with a single plane.
    // iOS cameras output BGRA8888 with a single plane.
    if (cameraImage.planes.isEmpty) return null;

    return InputImage.fromBytes(
      bytes: _concatenatePlanes(cameraImage.planes),
      metadata: InputImageMetadata(
        size: Size(
          cameraImage.width.toDouble(),
          cameraImage.height.toDouble(),
        ),
        rotation: rotation,
        format: format,
        bytesPerRow: cameraImage.planes.first.bytesPerRow,
      ),
    );
  }

  static Uint8List _concatenatePlanes(List<Plane> planes) {
    final allBytes = WriteBuffer();
    for (final plane in planes) {
      allBytes.putUint8List(plane.bytes);
    }
    return allBytes.done().buffer.asUint8List();
  }
}

/// Maps [CameraDescription.sensorOrientation] to [InputImageRotation].
InputImageRotation rotationFromSensorOrientation(int sensorOrientation) {
  return switch (sensorOrientation) {
    0   => InputImageRotation.rotation0deg,
    90  => InputImageRotation.rotation90deg,
    180 => InputImageRotation.rotation180deg,
    270 => InputImageRotation.rotation270deg,
    _   => InputImageRotation.rotation0deg,
  };
}
```

---

## Text Recognition (OCR)

```dart
import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';

/// Extracts structured text from an image.
///
/// Returns the full text string and a list of blocks with their
/// bounding rectangles for overlay rendering.
typedef OcrResult = ({
  String fullText,
  List<({String text, Rect boundingBox})> blocks,
});

final class TextRecognitionService {
  final TextRecognizer _recognizer = TextRecognizer(
    script: TextRecognitionScript.latin,
  );

  /// Recognizes text in the given [inputImage].
  Future<OcrResult> recognizeText(InputImage inputImage) async {
    final recognized = await _recognizer.processImage(inputImage);

    final blocks = <({String text, Rect boundingBox})>[];

    for (final block in recognized.blocks) {
      final box = block.boundingBox;
      blocks.add((text: block.text, boundingBox: box));
    }

    return (fullText: recognized.text, blocks: blocks);
  }

  /// For non-Latin scripts, create a recognizer with the appropriate script:
  /// - TextRecognitionScript.chinese
  /// - TextRecognitionScript.devanagari
  /// - TextRecognitionScript.japanese
  /// - TextRecognitionScript.korean

  Future<void> dispose() async {
    await _recognizer.close();
  }
}
```

### Usage

```dart
import 'dart:io';
import 'package:flutter/material.dart';

class OcrScreen extends StatefulWidget {
  const OcrScreen({super.key});

  @override
  State<OcrScreen> createState() => _OcrScreenState();
}

class _OcrScreenState extends State<OcrScreen> {
  final _service = TextRecognitionService();
  String _recognizedText = '';

  Future<void> _processImage(String imagePath) async {
    final inputImage = InputImageFactory.fromFilePath(imagePath);
    final result = await _service.recognizeText(inputImage);
    if (mounted) {
      setState(() => _recognizedText = result.fullText);
    }
  }

  @override
  void dispose() {
    _service.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('OCR Demo')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: SingleChildScrollView(
          child: SelectableText(
            _recognizedText.isEmpty
                ? 'Tap the button to scan text from an image.'
                : _recognizedText,
            semanticsLabel: 'Recognized text output',
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton(
        onPressed: () async {
          final picker = ImagePicker();
          final picked = await picker.pickImage(source: ImageSource.camera);
          if (picked != null) await _processImage(picked.path);
        },
        tooltip: 'Capture image for OCR',
        child: const Icon(Icons.camera_alt),
      ),
    );
  }
}
```

---

## Face Detection

```dart
import 'dart:ui';

import 'package:google_mlkit_face_detection/google_mlkit_face_detection.dart';

/// A simplified representation of a detected face.
typedef DetectedFace = ({
  Rect boundingBox,
  double? smilingProbability,
  double? leftEyeOpenProbability,
  double? rightEyeOpenProbability,
  double? headRotationY,
  double? headRotationZ,
  int? trackingId,
});

final class FaceDetectionService {
  final FaceDetector _detector = FaceDetector(
    options: FaceDetectorOptions(
      enableContours: true,
      enableLandmarks: true,
      enableClassification: true,
      enableTracking: true,
      performanceMode: FaceDetectorMode.fast,
      minFaceSize: 0.15,
    ),
  );

  Future<List<DetectedFace>> detectFaces(InputImage inputImage) async {
    final faces = await _detector.processImage(inputImage);

    return [
      for (final face in faces)
        (
          boundingBox: face.boundingBox,
          smilingProbability: face.smilingProbability,
          leftEyeOpenProbability: face.leftEyeOpenProbability,
          rightEyeOpenProbability: face.rightEyeOpenProbability,
          headRotationY: face.headEulerAngleY,
          headRotationZ: face.headEulerAngleZ,
          trackingId: face.trackingId,
        ),
    ];
  }

  Future<void> dispose() async {
    await _detector.close();
  }
}
```

### Face Detection Overlay Painter

```dart
import 'package:flutter/material.dart';

class FaceOverlayPainter extends CustomPainter {
  FaceOverlayPainter({
    required this.faces,
    required this.imageSize,
    required this.widgetSize,
  });

  final List<DetectedFace> faces;
  final Size imageSize;
  final Size widgetSize;

  @override
  void paint(Canvas canvas, Size size) {
    final scaleX = widgetSize.width / imageSize.width;
    final scaleY = widgetSize.height / imageSize.height;

    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2
      ..color = Colors.blueAccent;

    for (final face in faces) {
      final rect = Rect.fromLTRB(
        face.boundingBox.left * scaleX,
        face.boundingBox.top * scaleY,
        face.boundingBox.right * scaleX,
        face.boundingBox.bottom * scaleY,
      );
      canvas.drawRect(rect, paint);

      if (face.smilingProbability != null) {
        final label =
            'Smile: ${(face.smilingProbability! * 100).toStringAsFixed(0)}%';
        final textPainter = TextPainter(
          text: TextSpan(
            text: label,
            style: const TextStyle(
              color: Colors.white,
              fontSize: 10,
              backgroundColor: Colors.black54,
            ),
          ),
          textDirection: TextDirection.ltr,
        )..layout();
        textPainter.paint(canvas, rect.bottomLeft);
      }
    }
  }

  @override
  bool shouldRepaint(covariant FaceOverlayPainter oldDelegate) =>
      faces != oldDelegate.faces;
}
```

---

## Barcode Scanning

```dart
import 'dart:ui';

import 'package:google_mlkit_barcode_scanning/google_mlkit_barcode_scanning.dart';

/// Simplified barcode result.
typedef ScannedBarcode = ({
  String? displayValue,
  String? rawValue,
  BarcodeFormat format,
  BarcodeType type,
  Rect? boundingBox,
});

final class BarcodeScannerService {
  final BarcodeScanner _scanner = BarcodeScanner(
    formats: [
      BarcodeFormat.qrCode,
      BarcodeFormat.ean13,
      BarcodeFormat.ean8,
      BarcodeFormat.code128,
      BarcodeFormat.code39,
      BarcodeFormat.upcA,
      BarcodeFormat.upcE,
      BarcodeFormat.dataMatrix,
      BarcodeFormat.pdf417,
    ],
  );

  Future<List<ScannedBarcode>> scan(InputImage inputImage) async {
    final barcodes = await _scanner.processImage(inputImage);

    return [
      for (final barcode in barcodes)
        (
          displayValue: barcode.displayValue,
          rawValue: barcode.rawValue,
          format: barcode.format,
          type: barcode.type,
          boundingBox: barcode.boundingBox,
        ),
    ];
  }

  /// Extracts structured data from URL, Wi-Fi, contact, or email barcodes.
  Map<String, String> extractStructuredData(Barcode barcode) {
    return switch (barcode.type) {
      BarcodeType.url => {
        'url': barcode.value?.toString() ?? '',
      },
      BarcodeType.wifi => {
        'ssid': (barcode.value as BarcodeWifi?)?.ssid ?? '',
        'password': (barcode.value as BarcodeWifi?)?.password ?? '',
        'encryptionType':
            (barcode.value as BarcodeWifi?)?.encryptionType?.toString() ?? '',
      },
      BarcodeType.email => {
        'address': (barcode.value as BarcodeEmail?)?.address ?? '',
        'subject': (barcode.value as BarcodeEmail?)?.subject ?? '',
        'body': (barcode.value as BarcodeEmail?)?.body ?? '',
      },
      BarcodeType.contactInfo => {
        'name':
            (barcode.value as BarcodeContactInfo?)?.name?.formattedName ?? '',
        'organization':
            (barcode.value as BarcodeContactInfo?)?.organization ?? '',
      },
      _ => {'raw': barcode.rawValue ?? ''},
    };
  }

  Future<void> dispose() async {
    await _scanner.close();
  }
}
```

### Full-Screen Scanner Widget

```dart
import 'package:camera/camera.dart';
import 'package:flutter/material.dart';

class BarcodeScannerScreen extends StatefulWidget {
  const BarcodeScannerScreen({super.key, required this.onScanned});

  final ValueChanged<ScannedBarcode> onScanned;

  @override
  State<BarcodeScannerScreen> createState() => _BarcodeScannerScreenState();
}

class _BarcodeScannerScreenState extends State<BarcodeScannerScreen> {
  late final CameraController _camera;
  final _scanner = BarcodeScannerService();
  bool _isProcessing = false;
  bool _isCameraReady = false;

  @override
  void initState() {
    super.initState();
    _initCamera();
  }

  Future<void> _initCamera() async {
    final cameras = await availableCameras();
    final back = cameras.firstWhere(
      (c) => c.lensDirection == CameraLensDirection.back,
      orElse: () => cameras.first,
    );

    _camera = CameraController(
      back,
      ResolutionPreset.medium,
      enableAudio: false,
      imageFormatGroup: ImageFormatGroup.nv21,
    );

    await _camera.initialize();
    if (!mounted) return;

    setState(() => _isCameraReady = true);

    final rotation = rotationFromSensorOrientation(back.sensorOrientation);

    _camera.startImageStream((image) {
      if (_isProcessing) return;
      _isProcessing = true;
      _processFrame(image, rotation);
    });
  }

  Future<void> _processFrame(
    CameraImage image,
    InputImageRotation rotation,
  ) async {
    final inputImage = InputImageFactory.fromCameraImage(
      image,
      rotation: rotation,
    );
    if (inputImage == null) {
      _isProcessing = false;
      return;
    }

    final barcodes = await _scanner.scan(inputImage);

    if (barcodes.isNotEmpty && mounted) {
      await _camera.stopImageStream();
      widget.onScanned(barcodes.first);
    }

    _isProcessing = false;
  }

  @override
  void dispose() {
    _camera.dispose();
    _scanner.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (!_isCameraReady) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      body: Stack(
        fit: StackFit.expand,
        children: [
          CameraPreview(_camera),
          Center(
            child: Container(
              width: 260,
              height: 260,
              decoration: BoxDecoration(
                border: Border.all(color: Colors.white70, width: 2),
                borderRadius: BorderRadius.circular(12),
              ),
            ),
          ),
          const Positioned(
            bottom: 48,
            left: 0,
            right: 0,
            child: Text(
              'Point the camera at a barcode',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.white, fontSize: 16),
            ),
          ),
        ],
      ),
    );
  }
}
```

---

## Image Labeling

```dart
import 'package:google_mlkit_image_labeling/google_mlkit_image_labeling.dart';

typedef ImageLabel = ({String label, double confidence, int index});

final class ImageLabelingService {
  final ImageLabeler _labeler = ImageLabeler(
    options: ImageLabelerOptions(confidenceThreshold: 0.6),
  );

  Future<List<ImageLabel>> labelImage(InputImage inputImage) async {
    final labels = await _labeler.processImage(inputImage);

    return [
      for (final label in labels)
        (
          label: label.label,
          confidence: label.confidence,
          index: label.index,
        ),
    ];
  }

  Future<void> dispose() async {
    await _labeler.close();
  }
}
```

### Custom Model Labeling

Use a custom AutoML or TFLite model with ML Kit:

```dart
import 'package:google_mlkit_image_labeling/google_mlkit_image_labeling.dart';

final class CustomImageLabelingService {
  late final ImageLabeler _labeler;

  Future<void> initialize({
    required String modelPath,
    double confidenceThreshold = 0.5,
    int maxResultCount = 10,
  }) async {
    final localModel = LocalModel(path: modelPath);

    _labeler = ImageLabeler(
      options: CustomImageLabelerOptions(
        customModel: localModel,
        confidenceThreshold: confidenceThreshold,
        maxCount: maxResultCount,
      ),
    );
  }

  Future<List<ImageLabel>> labelImage(InputImage inputImage) async {
    final labels = await _labeler.processImage(inputImage);

    return [
      for (final label in labels)
        (
          label: label.label,
          confidence: label.confidence,
          index: label.index,
        ),
    ];
  }

  Future<void> dispose() async {
    await _labeler.close();
  }
}
```

---

## Pose Detection

```dart
import 'dart:ui';

import 'package:google_mlkit_pose_detection/google_mlkit_pose_detection.dart';

/// Simplified landmark with screen coordinates.
typedef PoseLandmark = ({
  PoseLandmarkType type,
  double x,
  double y,
  double z,
  double likelihood,
});

final class PoseDetectionService {
  final PoseDetector _detector = PoseDetector(
    options: PoseDetectorOptions(
      mode: PoseDetectionMode.stream,
      model: PoseDetectionModel.accurate,
    ),
  );

  Future<List<List<PoseLandmark>>> detectPoses(
    InputImage inputImage,
  ) async {
    final poses = await _detector.processImage(inputImage);

    return [
      for (final pose in poses)
        [
          for (final entry in pose.landmarks.entries)
            (
              type: entry.key,
              x: entry.value.x,
              y: entry.value.y,
              z: entry.value.z,
              likelihood: entry.value.likelihood,
            ),
        ],
    ];
  }

  Future<void> dispose() async {
    await _detector.close();
  }
}
```

### Pose Skeleton Painter

```dart
import 'package:flutter/material.dart';
import 'package:google_mlkit_pose_detection/google_mlkit_pose_detection.dart';

class PoseSkeletonPainter extends CustomPainter {
  PoseSkeletonPainter({
    required this.landmarks,
    required this.imageSize,
    required this.widgetSize,
  });

  final List<PoseLandmark> landmarks;
  final Size imageSize;
  final Size widgetSize;

  /// Connections between landmarks that form the skeleton.
  static const _connections = [
    (PoseLandmarkType.leftShoulder, PoseLandmarkType.rightShoulder),
    (PoseLandmarkType.leftShoulder, PoseLandmarkType.leftElbow),
    (PoseLandmarkType.leftElbow, PoseLandmarkType.leftWrist),
    (PoseLandmarkType.rightShoulder, PoseLandmarkType.rightElbow),
    (PoseLandmarkType.rightElbow, PoseLandmarkType.rightWrist),
    (PoseLandmarkType.leftShoulder, PoseLandmarkType.leftHip),
    (PoseLandmarkType.rightShoulder, PoseLandmarkType.rightHip),
    (PoseLandmarkType.leftHip, PoseLandmarkType.rightHip),
    (PoseLandmarkType.leftHip, PoseLandmarkType.leftKnee),
    (PoseLandmarkType.leftKnee, PoseLandmarkType.leftAnkle),
    (PoseLandmarkType.rightHip, PoseLandmarkType.rightKnee),
    (PoseLandmarkType.rightKnee, PoseLandmarkType.rightAnkle),
  ];

  @override
  void paint(Canvas canvas, Size size) {
    final scaleX = widgetSize.width / imageSize.width;
    final scaleY = widgetSize.height / imageSize.height;

    final landmarkMap = {
      for (final lm in landmarks) lm.type: Offset(lm.x * scaleX, lm.y * scaleY),
    };

    final dotPaint = Paint()
      ..color = Colors.redAccent
      ..style = PaintingStyle.fill;

    final linePaint = Paint()
      ..color = Colors.greenAccent
      ..strokeWidth = 2
      ..style = PaintingStyle.stroke;

    // Draw connections.
    for (final (start, end) in _connections) {
      final startPos = landmarkMap[start];
      final endPos = landmarkMap[end];
      if (startPos != null && endPos != null) {
        canvas.drawLine(startPos, endPos, linePaint);
      }
    }

    // Draw landmarks.
    for (final pos in landmarkMap.values) {
      canvas.drawCircle(pos, 4, dotPaint);
    }
  }

  @override
  bool shouldRepaint(covariant PoseSkeletonPainter oldDelegate) =>
      landmarks != oldDelegate.landmarks;
}
```

### Angle Calculation for Exercise Tracking

```dart
import 'dart:math' as math;

import 'package:google_mlkit_pose_detection/google_mlkit_pose_detection.dart';

/// Calculates the angle (in degrees) at [mid] formed by the line
/// from [start] to [mid] and the line from [mid] to [end].
///
/// Useful for counting reps in exercise apps (e.g. elbow angle for
/// bicep curls).
double calculateAngle(PoseLandmark start, PoseLandmark mid, PoseLandmark end) {
  final radians = math.atan2(end.y - mid.y, end.x - mid.x) -
      math.atan2(start.y - mid.y, start.x - mid.x);

  var angle = radians * (180 / math.pi);
  if (angle < 0) angle += 360;
  if (angle > 180) angle = 360 - angle;
  return angle;
}

/// Example: Calculate the right elbow angle from a list of landmarks.
double? rightElbowAngle(List<PoseLandmark> landmarks) {
  PoseLandmark? find(PoseLandmarkType type) {
    for (final lm in landmarks) {
      if (lm.type == type) return lm;
    }
    return null;
  }

  final shoulder = find(PoseLandmarkType.rightShoulder);
  final elbow = find(PoseLandmarkType.rightElbow);
  final wrist = find(PoseLandmarkType.rightWrist);

  if (shoulder == null || elbow == null || wrist == null) return null;

  return calculateAngle(shoulder, elbow, wrist);
}
```

---

## Camera Integration

### Unified Camera + ML Kit Controller

```dart
import 'dart:async';

import 'package:camera/camera.dart';
import 'package:flutter/widgets.dart';
import 'package:google_mlkit_commons/google_mlkit_commons.dart';

/// A reusable controller that connects the camera package to any ML Kit
/// detector. Subclass or compose with a specific detector.
final class CameraMlController {
  CameraMlController({
    required this.onInputImage,
    this.resolutionPreset = ResolutionPreset.medium,
    this.throttleMs = 66, // ~15 FPS
  });

  /// Called with each [InputImage] ready for processing.
  final Future<void> Function(InputImage image) onInputImage;
  final ResolutionPreset resolutionPreset;
  final int throttleMs;

  CameraController? _camera;
  bool _isProcessing = false;
  DateTime _lastFrameTime = DateTime.fromMillisecondsSinceEpoch(0);

  CameraController? get camera => _camera;
  bool get isInitialized => _camera?.value.isInitialized ?? false;

  Future<void> initialize() async {
    final cameras = await availableCameras();
    if (cameras.isEmpty) throw StateError('No cameras available.');

    final selected = cameras.firstWhere(
      (c) => c.lensDirection == CameraLensDirection.back,
      orElse: () => cameras.first,
    );

    _camera = CameraController(
      selected,
      resolutionPreset,
      enableAudio: false,
      imageFormatGroup: ImageFormatGroup.nv21,
    );

    await _camera!.initialize();

    final rotation =
        rotationFromSensorOrientation(selected.sensorOrientation);

    _camera!.startImageStream((cameraImage) {
      final now = DateTime.now();
      if (_isProcessing ||
          now.difference(_lastFrameTime).inMilliseconds < throttleMs) {
        return;
      }
      _lastFrameTime = now;
      _isProcessing = true;
      _processCameraImage(cameraImage, rotation);
    });
  }

  Future<void> _processCameraImage(
    CameraImage cameraImage,
    InputImageRotation rotation,
  ) async {
    try {
      final inputImage = InputImageFactory.fromCameraImage(
        cameraImage,
        rotation: rotation,
      );
      if (inputImage != null) {
        await onInputImage(inputImage);
      }
    } finally {
      _isProcessing = false;
    }
  }

  Future<void> dispose() async {
    await _camera?.stopImageStream();
    await _camera?.dispose();
  }
}
```

---

## Real-Time Frame Processing

### Example: Live Text Scanner Widget

```dart
import 'package:camera/camera.dart';
import 'package:flutter/material.dart';

class LiveTextScanner extends StatefulWidget {
  const LiveTextScanner({super.key});

  @override
  State<LiveTextScanner> createState() => _LiveTextScannerState();
}

class _LiveTextScannerState extends State<LiveTextScanner> {
  late final CameraMlController _mlCamera;
  final _ocrService = TextRecognitionService();
  String _lastText = '';

  @override
  void initState() {
    super.initState();
    _mlCamera = CameraMlController(
      onInputImage: _onImage,
      throttleMs: 200, // OCR is heavier; throttle to ~5 FPS.
    );
    _mlCamera.initialize().then((_) {
      if (mounted) setState(() {});
    });
  }

  Future<void> _onImage(InputImage image) async {
    final result = await _ocrService.recognizeText(image);
    if (mounted && result.fullText.isNotEmpty) {
      setState(() => _lastText = result.fullText);
    }
  }

  @override
  void dispose() {
    _mlCamera.dispose();
    _ocrService.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final camera = _mlCamera.camera;

    if (camera == null || !camera.value.isInitialized) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      body: Stack(
        fit: StackFit.expand,
        children: [
          CameraPreview(camera),
          Positioned(
            bottom: 0,
            left: 0,
            right: 0,
            child: Container(
              color: Colors.black54,
              padding: const EdgeInsets.all(16),
              constraints: const BoxConstraints(maxHeight: 200),
              child: SingleChildScrollView(
                child: SelectableText(
                  _lastText,
                  style: const TextStyle(color: Colors.white, fontSize: 14),
                  semanticsLabel: 'Live recognized text',
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
```

---

## Performance Tips

### Frame Throttling

Processing every camera frame is wasteful. Use a time-based gate:

```dart
/// A simple frame throttle that ensures at most [maxFps] frames per
/// second are processed.
final class FrameThrottle {
  FrameThrottle({this.maxFps = 15});

  final int maxFps;
  DateTime _lastFrame = DateTime.fromMillisecondsSinceEpoch(0);

  bool shouldProcess() {
    final now = DateTime.now();
    final minInterval = Duration(milliseconds: (1000 / maxFps).round());
    if (now.difference(_lastFrame) < minInterval) return false;
    _lastFrame = now;
    return true;
  }
}
```

### Resolution Selection

| Use Case           | Resolution       | Rationale                          |
| ------------------ | ---------------- | ---------------------------------- |
| Barcode scanning   | `medium` (480p)  | Barcodes need contrast, not detail |
| Text recognition   | `high` (720p)    | Small text benefits from more px   |
| Face detection     | `medium` (480p)  | Face features are large            |
| Pose detection     | `medium` (480p)  | Full body needs wide view          |
| Image labeling     | `low` (240p)     | Global features, not fine detail   |

### Resource Lifecycle

Always close detectors when they are no longer needed:

```dart
/// Mixin that automatically disposes ML Kit detectors when the widget
/// is removed from the tree.
mixin MlKitDisposable<T extends StatefulWidget> on State<T> {
  final _disposables = <Future<void> Function()>[];

  void registerDisposable(Future<void> Function() dispose) {
    _disposables.add(dispose);
  }

  @override
  void dispose() {
    for (final fn in _disposables) {
      fn();
    }
    super.dispose();
  }
}
```

### Avoid Concurrent Detector Calls

ML Kit detectors are not thread-safe. Always gate with a boolean flag or use a
`Completer`-based queue:

```dart
import 'dart:async';

/// Ensures only one ML Kit call is in flight at a time.
/// Additional calls while busy are silently dropped.
final class SingleFlightGuard {
  bool _busy = false;

  Future<T?> run<T>(Future<T> Function() task) async {
    if (_busy) return null;
    _busy = true;
    try {
      return await task();
    } finally {
      _busy = false;
    }
  }
}
```
