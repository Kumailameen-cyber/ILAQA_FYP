
require('dotenv').config({ quiet: true });

module.exports = ({ config }) => {
  const googleMapsApiKey = process.env.GOOGLE_MAPS_API_KEY;

  if (!googleMapsApiKey) {
    throw new Error(
      'GOOGLE_MAPS_API_KEY is missing. Check your mobile/.env file.'
    );
  }

  const isDevelopment =
  process.env.APP_VARIANT === 'development' ||
  process.env.EAS_BUILD_PROFILE === 'development';

  return {
    ...config,

    // Different name for the Development APK.
    name: isDevelopment ? 'ILAQA DEV' : config.name,

    // Different package ID prevents replacing the Preview APK.
    android: {
      ...config.android,
      package: isDevelopment
        ? 'com.ilaqa.app.localtest'
        : 'com.ilaqa.app',
    },

    // Keep Google Maps working.
    plugins: [
      ...(config.plugins || []),

      [
        'react-native-maps',
        {
          androidGoogleMapsApiKey: googleMapsApiKey,
        },
      ],
    ],
  };
};
