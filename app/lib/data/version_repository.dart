import 'package:dio/dio.dart';

import '../core/net/api_client.dart';
import '../core/net/api_exception.dart';
import '../core/net/app_config.dart';
import '../core/platform/app_platform.dart';
import 'dtos/app_version_status.dart';

/// Contract §F.1 — `GET /api/app/version-status` (anonymous endpoint).
abstract interface class VersionRepository {
  Future<AppVersionStatusDto> checkStatus();
}

final class RemoteVersionRepository implements VersionRepository {
  const RemoteVersionRepository(this._client, this._config, this._platform);

  final ApiClient _client;
  final AppConfig _config;
  final AppPlatform _platform;

  @override
  Future<AppVersionStatusDto> checkStatus() async {
    try {
      final Response<Map<String, dynamic>> resp =
          await _client.dio.get<Map<String, dynamic>>(
        '/api/app/version-status',
        queryParameters: <String, dynamic>{
          'platform': _platform.target.wireName,
          'version': _config.appVersion,
        },
      );
      return AppVersionStatusDto.fromJson(resp.data!);
    } on DioException catch (e) {
      throw ApiException.fromDio(e);
    }
  }
}
