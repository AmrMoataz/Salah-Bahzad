import 'package:connectivity_plus/connectivity_plus.dart';

abstract class ConnectivityChecker {
  Future<List<ConnectivityResult>> check();
  Stream<List<ConnectivityResult>> get onChange;
}

class LiveConnectivityChecker implements ConnectivityChecker {
  final Connectivity _connectivity = Connectivity();

  @override
  Future<List<ConnectivityResult>> check() => _connectivity.checkConnectivity();

  @override
  Stream<List<ConnectivityResult>> get onChange =>
      _connectivity.onConnectivityChanged;
}
