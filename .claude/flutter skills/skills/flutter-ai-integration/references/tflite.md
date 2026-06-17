# On-Device ML with TFLite Flutter

## Table of Contents

- [Setup](#setup)
- [Loading a TFLite Model](#loading-a-tflite-model)
- [Input/Output Tensor Handling](#inputoutput-tensor-handling)
- [Image Pre-Processing](#image-pre-processing)
- [Image Classification Example](#image-classification-example)
- [Object Detection Example](#object-detection-example)
- [Post-Processing Results](#post-processing-results)
- [Performance Optimization](#performance-optimization)
- [Model Conversion Basics](#model-conversion-basics)
- [Custom Model Deployment](#custom-model-deployment)

---

## Setup

### pubspec.yaml

```yaml
dependencies:
  tflite_flutter: ^0.11.0
  image: ^4.3.0        # For image decoding / resizing
  path_provider: ^2.1.0 # For locating app directories
```

### Android

In `android/app/build.gradle`:

```groovy
android {
    defaultConfig {
        minSdk = 26  // Required for GPU delegate
        ndk {
            abiFilters 'armeabi-v7a', 'arm64-v8a', 'x86_64'
        }
    }

    asBuild {
        noCompress 'tflite'
    }
}
```

### iOS

In `ios/Podfile`:

```ruby
platform :ios, '15.0'

post_install do |installer|
  installer.pods_project.targets.each do |target|
    target.build_configurations.each do |config|
      config.build_settings['EXCLUDED_ARCHS[sdk=iphonesimulator*]'] = 'i386'
    end
  end
end
```

### Asset Registration

Place model files in `assets/models/` and register them:

```yaml
flutter:
  assets:
    - assets/models/
    - assets/labels/
```

---

## Loading a TFLite Model

```dart
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/services.dart';
import 'package:path_provider/path_provider.dart';
import 'package:tflite_flutter/tflite_flutter.dart';

/// Manages the lifecycle of a TFLite interpreter.
///
/// Call [initialize] before running inference, and [dispose] when done.
final class TFLiteModelRunner {
  Interpreter? _interpreter;

  bool get isReady => _interpreter != null;

  /// Loads a .tflite model from the Flutter asset bundle.
  ///
  /// [assetPath] is relative to the assets directory,
  /// e.g. `models/mobilenet_v2.tflite`.
  Future<void> initialize(
    String assetPath, {
    int numThreads = 2,
    bool useGpuDelegate = false,
  }) async {
    final modelFile = await _copyAssetToFile(assetPath);

    final options = InterpreterOptions()..threads = numThreads;

    if (useGpuDelegate) {
      if (Platform.isAndroid) {
        options.addDelegate(GpuDelegateV2());
      } else if (Platform.isIOS) {
        options.addDelegate(GpuDelegate());
      }
    }

    _interpreter = Interpreter.fromFile(modelFile, options: options);
  }

  /// Runs inference with the loaded model.
  ///
  /// [input] and [output] must match the model's expected tensor shapes.
  void run(Object input, Object output) {
    final interpreter = _interpreter;
    if (interpreter == null) {
      throw StateError('Interpreter not initialized. Call initialize() first.');
    }
    interpreter.run(input, output);
  }

  /// Runs inference on models with multiple inputs/outputs.
  void runForMultipleInputs(
    List<Object> inputs,
    Map<int, Object> outputs,
  ) {
    final interpreter = _interpreter;
    if (interpreter == null) {
      throw StateError('Interpreter not initialized. Call initialize() first.');
    }
    interpreter.runForMultipleInputs(inputs, outputs);
  }

  /// Returns tensor metadata for inspection.
  ({List<int> inputShape, List<int> outputShape}) get tensorShapes {
    final interpreter = _interpreter;
    if (interpreter == null) {
      throw StateError('Interpreter not initialized.');
    }
    return (
      inputShape: interpreter.getInputTensor(0).shape,
      outputShape: interpreter.getOutputTensor(0).shape,
    );
  }

  void dispose() {
    _interpreter?.close();
    _interpreter = null;
  }

  /// Copies a bundled asset to a temporary file so the interpreter can
  /// memory-map it.
  Future<File> _copyAssetToFile(String assetPath) async {
    final byteData = await rootBundle.load('assets/$assetPath');
    final tempDir = await getTemporaryDirectory();
    final fileName = assetPath.split('/').last;
    final file = File('${tempDir.path}/$fileName');

    if (!file.existsSync()) {
      await file.writeAsBytes(
        byteData.buffer.asUint8List(),
        flush: true,
      );
    }
    return file;
  }
}
```

---

## Input/Output Tensor Handling

```dart
import 'dart:typed_data';

import 'package:tflite_flutter/tflite_flutter.dart';

/// Inspects every input and output tensor of a loaded interpreter.
void inspectTensors(Interpreter interpreter) {
  final inputCount = interpreter.getInputTensors().length;
  final outputCount = interpreter.getOutputTensors().length;

  for (var i = 0; i < inputCount; i++) {
    final tensor = interpreter.getInputTensor(i);
    print('Input[$i]: name=${tensor.name}, '
        'shape=${tensor.shape}, '
        'type=${tensor.type}');
  }

  for (var i = 0; i < outputCount; i++) {
    final tensor = interpreter.getOutputTensor(i);
    print('Output[$i]: name=${tensor.name}, '
        'shape=${tensor.shape}, '
        'type=${tensor.type}');
  }
}

/// Allocates correctly typed output buffers based on the model's output
/// tensor metadata.
///
/// Supports Float32 and UInt8 quantized outputs.
Map<int, Object> allocateOutputBuffers(Interpreter interpreter) {
  final outputs = <int, Object>{};

  for (var i = 0; i < interpreter.getOutputTensors().length; i++) {
    final tensor = interpreter.getOutputTensor(i);
    final shape = tensor.shape;

    switch (tensor.type) {
      case TensorType.float32:
        outputs[i] = _createNestedList<double>(shape, 0.0);
      case TensorType.uint8:
        outputs[i] = _createNestedList<int>(shape, 0);
      case _:
        throw UnsupportedError(
          'Tensor type ${tensor.type} is not yet handled.',
        );
    }
  }

  return outputs;
}

/// Recursively builds a nested [List] matching [shape] filled with
/// [fillValue].
Object _createNestedList<T>(List<int> shape, T fillValue) {
  if (shape.length == 1) {
    return List<T>.filled(shape[0], fillValue);
  }
  return List.generate(
    shape[0],
    (_) => _createNestedList<T>(shape.sublist(1), fillValue),
  );
}
```

---

## Image Pre-Processing

```dart
import 'dart:typed_data';

import 'package:image/image.dart' as img;

/// Holds the normalized pixel data ready for model input together with
/// the original image dimensions for coordinate mapping.
typedef PreProcessedImage = ({
  Float32List pixels,
  int originalWidth,
  int originalHeight,
});

/// Resizes and normalizes an image for a TFLite model that expects
/// `[1, height, width, 3]` float32 input in the range `[0, 1]`.
///
/// [imageBytes] is the raw file bytes (PNG, JPEG, etc.).
/// [targetWidth] and [targetHeight] must match the model's input tensor.
PreProcessedImage preProcessImage(
  Uint8List imageBytes, {
  required int targetWidth,
  required int targetHeight,
}) {
  final original = img.decodeImage(imageBytes);
  if (original == null) {
    throw ArgumentError('Unable to decode the provided image bytes.');
  }

  final resized = img.copyResize(
    original,
    width: targetWidth,
    height: targetHeight,
    interpolation: img.Interpolation.linear,
  );

  final pixelCount = targetWidth * targetHeight;
  final float32Pixels = Float32List(pixelCount * 3);

  var index = 0;
  for (var y = 0; y < targetHeight; y++) {
    for (var x = 0; x < targetWidth; x++) {
      final pixel = resized.getPixel(x, y);
      float32Pixels[index++] = pixel.r / 255.0;
      float32Pixels[index++] = pixel.g / 255.0;
      float32Pixels[index++] = pixel.b / 255.0;
    }
  }

  return (
    pixels: float32Pixels,
    originalWidth: original.width,
    originalHeight: original.height,
  );
}

/// Reshapes a flat [Float32List] into the 4-D list structure that
/// [Interpreter.run] expects: `[1, height, width, channels]`.
List<List<List<List<double>>>> reshapeToModelInput(
  Float32List flat, {
  required int height,
  required int width,
  int channels = 3,
}) {
  var offset = 0;
  return [
    List.generate(height, (y) {
      return List.generate(width, (x) {
        return List.generate(channels, (_) => flat[offset++]);
      });
    }),
  ];
}
```

---

## Image Classification Example

```dart
import 'dart:typed_data';

import 'package:flutter/services.dart';

// Assumes TFLiteModelRunner, preProcessImage, reshapeToModelInput are
// available from earlier sections.

/// A single classification result.
typedef ClassificationResult = ({String label, double confidence});

/// Classifies an image using a MobileNet-style model that outputs a
/// probability vector over N classes.
///
/// Returns the top [topK] results sorted by descending confidence.
final class ImageClassifier {
  final TFLiteModelRunner _runner = TFLiteModelRunner();
  late final List<String> _labels;

  static const int _inputSize = 224;

  Future<void> load({
    String modelAsset = 'models/mobilenet_v2.tflite',
    String labelsAsset = 'assets/labels/labels.txt',
  }) async {
    await _runner.initialize(modelAsset, numThreads: 4);
    final labelsRaw = await rootBundle.loadString(labelsAsset);
    _labels = labelsRaw
        .split('\n')
        .map((l) => l.trim())
        .where((l) => l.isNotEmpty)
        .toList(growable: false);
  }

  List<ClassificationResult> classify(
    Uint8List imageBytes, {
    int topK = 5,
  }) {
    final processed = preProcessImage(
      imageBytes,
      targetWidth: _inputSize,
      targetHeight: _inputSize,
    );

    final input = reshapeToModelInput(
      processed.pixels,
      height: _inputSize,
      width: _inputSize,
    );

    // Output shape: [1, numClasses]
    final output = List.filled(1, List.filled(_labels.length, 0.0));

    _runner.run(input, output);

    final probabilities = output[0];

    final indexed = [
      for (var i = 0; i < probabilities.length; i++)
        (label: _labels[i], confidence: probabilities[i]),
    ]..sort((a, b) => b.confidence.compareTo(a.confidence));

    return indexed.take(topK).toList(growable: false);
  }

  void dispose() => _runner.dispose();
}
```

### Usage in a Widget

```dart
import 'dart:typed_data';

import 'package:flutter/material.dart';

class ClassificationScreen extends StatefulWidget {
  const ClassificationScreen({super.key});

  @override
  State<ClassificationScreen> createState() => _ClassificationScreenState();
}

class _ClassificationScreenState extends State<ClassificationScreen> {
  final _classifier = ImageClassifier();
  List<ClassificationResult>? _results;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _initClassifier();
  }

  Future<void> _initClassifier() async {
    await _classifier.load();
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _classifyFromAsset(String assetPath) async {
    final bytes = await rootBundle.load(assetPath);
    final results = _classifier.classify(bytes.buffer.asUint8List());
    if (mounted) setState(() => _results = results);
  }

  @override
  void dispose() {
    _classifier.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Image Classifier')),
      body: Column(
        children: [
          ElevatedButton(
            onPressed: () => _classifyFromAsset('assets/images/sample.jpg'),
            child: const Text('Classify Sample Image'),
          ),
          if (_results != null)
            Expanded(
              child: ListView.builder(
                itemCount: _results!.length,
                itemBuilder: (context, index) {
                  final (:label, :confidence) = _results![index];
                  return ListTile(
                    title: Text(label),
                    trailing: Text(
                      '${(confidence * 100).toStringAsFixed(1)}%',
                    ),
                    leading: SizedBox(
                      width: 100,
                      child: LinearProgressIndicator(
                        value: confidence,
                        semanticsLabel: '$label confidence',
                      ),
                    ),
                  );
                },
              ),
            ),
        ],
      ),
    );
  }
}
```

---

## Object Detection Example

```dart
import 'dart:typed_data';

import 'package:flutter/services.dart';

/// Represents a detected object with its bounding box and metadata.
typedef Detection = ({
  String label,
  double confidence,
  double top,
  double left,
  double bottom,
  double right,
});

/// Runs object detection using a SSD MobileNet-style TFLite model.
///
/// Expected model outputs (4 tensors):
///   0: Bounding boxes  [1, numDetections, 4]  (top, left, bottom, right)
///   1: Class indices    [1, numDetections]
///   2: Scores           [1, numDetections]
///   3: Number of detections [1]
final class ObjectDetector {
  final TFLiteModelRunner _runner = TFLiteModelRunner();
  late final List<String> _labels;

  static const int _inputSize = 300;
  static const int _maxDetections = 10;

  Future<void> load({
    String modelAsset = 'models/ssd_mobilenet.tflite',
    String labelsAsset = 'assets/labels/coco_labels.txt',
  }) async {
    await _runner.initialize(modelAsset, numThreads: 4, useGpuDelegate: true);
    final raw = await rootBundle.loadString(labelsAsset);
    _labels = raw
        .split('\n')
        .map((l) => l.trim())
        .where((l) => l.isNotEmpty)
        .toList(growable: false);
  }

  List<Detection> detect(
    Uint8List imageBytes, {
    double confidenceThreshold = 0.5,
  }) {
    final processed = preProcessImage(
      imageBytes,
      targetWidth: _inputSize,
      targetHeight: _inputSize,
    );

    final input = reshapeToModelInput(
      processed.pixels,
      height: _inputSize,
      width: _inputSize,
    );

    // Allocate output buffers matching the model's 4 output tensors.
    final boxes = List.generate(
      1,
      (_) => List.generate(_maxDetections, (_) => List.filled(4, 0.0)),
    );
    final classes = List.generate(1, (_) => List.filled(_maxDetections, 0.0));
    final scores = List.generate(1, (_) => List.filled(_maxDetections, 0.0));
    final numDetections = List.filled(1, 0.0);

    final outputs = <int, Object>{
      0: boxes,
      1: classes,
      2: scores,
      3: numDetections,
    };

    _runner.runForMultipleInputs([input], outputs);

    final count = numDetections[0].toInt();
    final detections = <Detection>[];

    for (var i = 0; i < count; i++) {
      final score = scores[0][i];
      if (score < confidenceThreshold) continue;

      final classIndex = classes[0][i].toInt();
      final label =
          classIndex < _labels.length ? _labels[classIndex] : 'Unknown';

      detections.add((
        label: label,
        confidence: score,
        top: boxes[0][i][0],
        left: boxes[0][i][1],
        bottom: boxes[0][i][2],
        right: boxes[0][i][3],
      ));
    }

    detections.sort((a, b) => b.confidence.compareTo(a.confidence));
    return detections;
  }

  void dispose() => _runner.dispose();
}
```

### Painting Bounding Boxes

```dart
import 'package:flutter/material.dart';

class DetectionOverlay extends CustomPainter {
  DetectionOverlay({required this.detections, required this.imageSize});

  final List<Detection> detections;
  final Size imageSize;

  @override
  void paint(Canvas canvas, Size size) {
    final scaleX = size.width / imageSize.width;
    final scaleY = size.height / imageSize.height;

    final boxPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2.0
      ..color = Colors.greenAccent;

    final textStyle = const TextStyle(
      color: Colors.white,
      fontSize: 12,
      backgroundColor: Colors.black54,
    );

    for (final (:label, :confidence, :top, :left, :bottom, :right)
        in detections) {
      final rect = Rect.fromLTRB(
        left * imageSize.width * scaleX,
        top * imageSize.height * scaleY,
        right * imageSize.width * scaleX,
        bottom * imageSize.height * scaleY,
      );

      canvas.drawRect(rect, boxPaint);

      final textSpan = TextSpan(
        text: '$label ${(confidence * 100).toStringAsFixed(0)}%',
        style: textStyle,
      );
      final textPainter = TextPainter(
        text: textSpan,
        textDirection: TextDirection.ltr,
      )..layout();

      textPainter.paint(canvas, rect.topLeft);
    }
  }

  @override
  bool shouldRepaint(covariant DetectionOverlay oldDelegate) =>
      detections != oldDelegate.detections;
}
```

---

## Post-Processing Results

```dart
/// Applies Non-Maximum Suppression to remove overlapping detections
/// for the same class.
///
/// [iouThreshold] controls how much overlap is tolerated (0.0 - 1.0).
List<Detection> nonMaxSuppression(
  List<Detection> detections, {
  double iouThreshold = 0.45,
}) {
  if (detections.isEmpty) return const [];

  final sorted = [...detections]
    ..sort((a, b) => b.confidence.compareTo(a.confidence));

  final kept = <Detection>[];

  while (sorted.isNotEmpty) {
    final best = sorted.removeAt(0);
    kept.add(best);

    sorted.removeWhere((other) {
      if (other.label != best.label) return false;
      return _iou(best, other) > iouThreshold;
    });
  }

  return kept;
}

/// Computes Intersection over Union for two bounding boxes.
double _iou(Detection a, Detection b) {
  final interLeft = a.left > b.left ? a.left : b.left;
  final interTop = a.top > b.top ? a.top : b.top;
  final interRight = a.right < b.right ? a.right : b.right;
  final interBottom = a.bottom < b.bottom ? a.bottom : b.bottom;

  final interWidth = (interRight - interLeft).clamp(0.0, double.infinity);
  final interHeight = (interBottom - interTop).clamp(0.0, double.infinity);
  final interArea = interWidth * interHeight;

  final areaA = (a.right - a.left) * (a.bottom - a.top);
  final areaB = (b.right - b.left) * (b.bottom - b.top);
  final unionArea = areaA + areaB - interArea;

  if (unionArea <= 0) return 0.0;
  return interArea / unionArea;
}

/// Applies softmax to a list of raw logits, returning probabilities
/// that sum to 1.
List<double> softmax(List<double> logits) {
  final maxLogit = logits.reduce((a, b) => a > b ? a : b);
  final exps = logits.map((l) => _exp(l - maxLogit)).toList(growable: false);
  final sumExps = exps.reduce((a, b) => a + b);
  return exps.map((e) => e / sumExps).toList(growable: false);
}

/// Dart's `exp` from dart:math, inlined to avoid an extra import in
/// snippet context.
double _exp(double x) {
  // Use dart:math in production; inlined here for self-containedness.
  return 2.718281828459045 * x.sign == 0 ? 1.0 : _pow(2.718281828459045, x);
}

// In production, replace with: import 'dart:math' show exp;
// and call exp(x) directly. The above is illustrative only.
```

> **Note:** In real projects, import `dart:math` and call `exp()` directly
> rather than re-implementing it.

```dart
import 'dart:math' as math;

List<double> softmaxProduction(List<double> logits) {
  final maxLogit = logits.reduce(math.max);
  final exps = [for (final l in logits) math.exp(l - maxLogit)];
  final sum = exps.reduce((a, b) => a + b);
  return [for (final e in exps) e / sum];
}
```

---

## Performance Optimization

### GPU Delegate

```dart
import 'dart:io';

import 'package:tflite_flutter/tflite_flutter.dart';

/// Creates an interpreter with the optimal delegate for the current
/// platform.
Future<Interpreter> createOptimizedInterpreter(
  String modelPath, {
  int threads = 4,
}) async {
  final options = InterpreterOptions()..threads = threads;

  try {
    if (Platform.isAndroid) {
      // GpuDelegateV2 supports Android API 26+.
      options.addDelegate(GpuDelegateV2(
        options: GpuDelegateOptionsV2(
          isPrecisionLossAllowed: true, // FP16 for speed
          inferencePreference: TfLiteGpuInferenceUsage.fastSingleAnswer,
          inferencePriority1: TfLiteGpuInferencePriority.minLatency,
          inferencePriority2: TfLiteGpuInferencePriority.auto,
          inferencePriority3: TfLiteGpuInferencePriority.auto,
        ),
      ));
    } else if (Platform.isIOS) {
      // Metal delegate for iOS.
      options.addDelegate(GpuDelegate(
        options: GpuDelegateOptions(
          allowPrecisionLoss: true,
          waitType: TFLGpuDelegateWaitType.passive,
        ),
      ));
    }
  } on Object {
    // GPU delegate not available; fall back to CPU.
  }

  return Interpreter.fromFile(File(modelPath), options: options);
}
```

### Thread Tuning

| Device Tier    | Recommended Threads | Notes                              |
| -------------- | ------------------- | ---------------------------------- |
| Low-end        | 2                   | Avoid starving the UI thread       |
| Mid-range      | 4                   | Good balance                       |
| High-end       | 4-6                 | Diminishing returns past 6         |
| With GPU deleg. | 1-2 CPU             | GPU handles the heavy lifting      |

### Isolate-Based Inference

Run inference on a separate isolate to keep the UI thread at 60 FPS:

```dart
import 'dart:isolate';

import 'package:flutter/services.dart';

/// Message sent from the main isolate to the inference isolate.
typedef InferenceRequest = ({
  String modelPath,
  Float32List inputData,
  List<int> inputShape,
  List<int> outputShape,
});

/// Runs TFLite inference in a background isolate.
///
/// Returns the raw output as a Float32List.
Future<Float32List> runInferenceInIsolate(InferenceRequest request) async {
  return Isolate.run(() {
    final interpreter = Interpreter.fromFile(File(request.modelPath));

    final input = request.inputData.reshape(request.inputShape);
    final output = Float32List(
      request.outputShape.reduce((a, b) => a * b),
    ).reshape(request.outputShape);

    interpreter.run(input, output);
    interpreter.close();

    return Float32List.fromList(
      (output as List).expand<double>((row) {
        if (row is List) return row.cast<double>();
        return [row as double];
      }).toList(),
    );
  });
}
```

---

## Model Conversion Basics

Converting a TensorFlow / Keras model to TFLite (run in a Python environment):

```python
import tensorflow as tf

# 1. Standard conversion
converter = tf.lite.TFLiteConverter.from_saved_model("saved_model_dir")
tflite_model = converter.convert()

with open("model.tflite", "wb") as f:
    f.write(tflite_model)

# 2. Float16 quantization (smaller model, GPU-friendly)
converter.optimizations = [tf.lite.Optimize.DEFAULT]
converter.target_spec.supported_types = [tf.float16]
tflite_fp16 = converter.convert()

with open("model_fp16.tflite", "wb") as f:
    f.write(tflite_fp16)

# 3. Full integer quantization (smallest, CPU-friendly)
def representative_dataset():
    for _ in range(100):
        yield [tf.random.normal([1, 224, 224, 3])]

converter.optimizations = [tf.lite.Optimize.DEFAULT]
converter.representative_dataset = representative_dataset
converter.target_spec.supported_ops = [
    tf.lite.OpsSet.TFLITE_BUILTINS_INT8,
]
converter.inference_input_type = tf.uint8
converter.inference_output_type = tf.uint8
tflite_int8 = converter.convert()

with open("model_int8.tflite", "wb") as f:
    f.write(tflite_int8)
```

### Quantization Trade-Offs

| Quantization | Model Size | Speed     | Accuracy Loss | Best For            |
| ------------ | ---------- | --------- | ------------- | ------------------- |
| None (FP32)  | Largest    | Baseline  | None          | Development/testing  |
| Float16      | ~50%       | Faster    | Negligible    | GPU delegate         |
| Dynamic      | ~25%       | Faster    | Small         | General deployment   |
| Full INT8    | ~25%       | Fastest   | Moderate      | Low-end devices      |

---

## Custom Model Deployment

### On-Demand Model Download

For models too large to bundle with the app (>10 MB):

```dart
import 'dart:io';

import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';
import 'package:crypto/crypto.dart';

/// Downloads a model from [url] and caches it locally.
///
/// [expectedSha256] is used to verify integrity. Pass `null` to skip
/// verification (not recommended for production).
Future<File> downloadModel({
  required String url,
  required String fileName,
  String? expectedSha256,
  void Function(double progress)? onProgress,
}) async {
  final dir = await getApplicationDocumentsDirectory();
  final modelFile = File('${dir.path}/models/$fileName');

  if (modelFile.existsSync()) {
    if (expectedSha256 != null) {
      final bytes = await modelFile.readAsBytes();
      final digest = sha256.convert(bytes).toString();
      if (digest == expectedSha256) return modelFile;
      // Hash mismatch -- re-download.
    } else {
      return modelFile;
    }
  }

  await modelFile.parent.create(recursive: true);

  final request = http.Request('GET', Uri.parse(url));
  final response = await http.Client().send(request);

  if (response.statusCode != 200) {
    throw HttpException(
      'Failed to download model: ${response.statusCode}',
      uri: Uri.parse(url),
    );
  }

  final totalBytes = response.contentLength ?? -1;
  var receivedBytes = 0;
  final sink = modelFile.openWrite();

  await for (final chunk in response.stream) {
    sink.add(chunk);
    receivedBytes += chunk.length;
    if (totalBytes > 0 && onProgress != null) {
      onProgress(receivedBytes / totalBytes);
    }
  }

  await sink.close();

  if (expectedSha256 != null) {
    final bytes = await modelFile.readAsBytes();
    final digest = sha256.convert(bytes).toString();
    if (digest != expectedSha256) {
      await modelFile.delete();
      throw StateError('Downloaded model hash mismatch.');
    }
  }

  return modelFile;
}
```

### Model Versioning Strategy

```dart
/// Tracks model versions and handles migrations.
final class ModelRegistry {
  ModelRegistry({required this.baseUrl});

  final String baseUrl;

  /// Registry of available models with their metadata.
  static const models = {
    'image_classifier': (
      version: 3,
      fileName: 'classifier_v3.tflite',
      sha256: 'abc123...', // Replace with real hash
      sizeBytes: 4500000,
    ),
    'object_detector': (
      version: 2,
      fileName: 'detector_v2.tflite',
      sha256: 'def456...', // Replace with real hash
      sizeBytes: 8200000,
    ),
  };

  /// Ensures the latest version of [modelKey] is available locally.
  Future<File> ensureModel(
    String modelKey, {
    void Function(double)? onProgress,
  }) async {
    final meta = models[modelKey];
    if (meta == null) throw ArgumentError('Unknown model: $modelKey');

    return downloadModel(
      url: '$baseUrl/${meta.fileName}',
      fileName: meta.fileName,
      expectedSha256: meta.sha256,
      onProgress: onProgress,
    );
  }
}
```
