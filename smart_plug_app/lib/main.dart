import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'core/api.dart';
import 'core/i18n.dart';
import 'core/push.dart';
import 'core/theme.dart';
import 'screens/splash.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setSystemUIOverlayStyle(const SystemUiOverlayStyle(
    statusBarColor: Colors.transparent,
    statusBarIconBrightness: Brightness.light,
    systemNavigationBarColor: C.bg,
    systemNavigationBarIconBrightness: Brightness.light,
  ));
  await Api.i.init();
  await Lang.i.init();
  Push.init().then((_) => Push.registerForUser()); // phone notifications (does not delay start-up)
  runApp(const SmartPlugApp());
}

class SmartPlugApp extends StatelessWidget {
  const SmartPlugApp({super.key});
  @override
  Widget build(BuildContext context) => AnimatedBuilder(
        animation: Lang.i,
        builder: (_, __) => MaterialApp(
          title: 'Smart Plug',
          navigatorKey: Push.navKey,
          debugShowCheckedModeBanner: false,
          theme: buildTheme(),
          locale: Lang.i.locale,
          supportedLocales: const [Locale('en'), Locale('ar')],
          localizationsDelegates: const [
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          // Keyed by language: switching language rebuilds every screen from scratch (then returns to Settings).
          builder: (context, child) => KeyedSubtree(key: ValueKey(Lang.i.code), child: child ?? const SizedBox.shrink()),
          home: const SplashScreen(),
        ),
      );
}
