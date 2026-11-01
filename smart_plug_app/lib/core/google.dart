import 'package:google_sign_in/google_sign_in.dart';
import 'api.dart';

/// Google Sign-In wrapper. Needs the Web OAuth client id at build time:
///   flutter build apk --dart-define=GOOGLE_CLIENT_ID=xxxx.apps.googleusercontent.com
/// (the same id must be listed in the API's Google__ClientIds setting).
class GoogleAuth {
  static const String clientId = String.fromEnvironment('GOOGLE_CLIENT_ID');

  /// Opens the Google account chooser and returns an ID token, or null if the user cancelled.
  static Future<String?> idToken() async {
    if (clientId.isEmpty) {
      throw ApiException('Google sign-in is not set up in this build');
    }
    final gs = GoogleSignIn(scopes: const ['email'], serverClientId: clientId);
    try {
      await gs.signOut(); // always show the account chooser
      final account = await gs.signIn();
      if (account == null) return null;
      final auth = await account.authentication;
      final token = auth.idToken;
      if (token == null) throw ApiException('Google did not return a sign-in token');
      return token;
    } on ApiException {
      rethrow;
    } catch (e) {
      // Keep the real reason (for example "ApiException: 10" = wrong SHA-1 / package) visible while setting up.
      var detail = e.toString().replaceAll('\n', ' ');
      if (detail.length > 140) detail = detail.substring(0, 140);
      throw ApiException('Google sign-in failed. Please try again. [$detail]');
    }
  }
}
