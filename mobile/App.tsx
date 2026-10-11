
import React, { useEffect, useState } from 'react';
import {
  View,
  ActivityIndicator,
  StyleSheet,
  Alert,
} from 'react-native';
import * as SecureStore from 'expo-secure-store';

import RegisterScreen from './screens/RegisterScreen';
import LoginScreen from './screens/LoginScreen';
import HomeScreen from './screens/HomeScreen';
import VerifyEmailScreen from './screens/VerifyEmailScreen';
const API_URL = 'http://192.168.100.232:5246';

type Runner = {
  id: string;
  username: string;
  displayName: string;
};

type Screen = 'register' | 'login' | 'home'| 'verifyEmail';

export default function App() {
  const [screen, setScreen] = useState<Screen>('register');
  const [runner, setRunner] = useState<Runner | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);
const [verificationEmail, setVerificationEmail] = useState('');
  useEffect(() => {
    const restoreSession = async () => {
      try {
        const refreshToken =
          await SecureStore.getItemAsync('refreshToken');

        if (!refreshToken) return;

        // Verify the saved session with the backend.
        // A saved token alone does not prove the session is valid.
        const response = await fetch(
          `${API_URL}/api/auth/refresh`,
          {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({ refreshToken }),
          }
        );

        if (!response.ok) {
          await SecureStore.deleteItemAsync('accessToken');
          await SecureStore.deleteItemAsync('refreshToken');
          return;
        }

        const data = await response.json();

        if (!data.accessToken || !data.refreshToken) {
          return;
        }

        await SecureStore.setItemAsync(
          'accessToken',
          data.accessToken
        );

        await SecureStore.setItemAsync(
          'refreshToken',
          data.refreshToken
        );

        // Fetch the current runner's profile using the JWT.
        const profileResponse = await fetch(
          `${API_URL}/api/runner/me`,
          {
            headers: {
              Authorization: `Bearer ${data.accessToken}`,
            },
          }
        );

        if (!profileResponse.ok) return;

        const profile = await profileResponse.json();

        const restoredRunner: Runner = {
          id: String(profile.id ?? profile.userId ?? ''),
          username: String(profile.username ?? ''),
          displayName: String(
            profile.displayName ??
            profile.username ??
            'Runner'
          ),
        };

        setRunner(restoredRunner);
        setScreen('home');
      } catch (error) {
        // A temporary network error should not delete saved tokens.
        console.log('Unable to restore session.');
      } finally {
        setCheckingSession(false);
      }
    };

    restoreSession();
  }, []);

  const handleLoginSuccess = (loggedInRunner: Runner) => {
    setRunner(loggedInRunner);
    setScreen('home');
  };

 
const handleLogout = async () => {
  try {
    const refreshToken =
      await SecureStore.getItemAsync('refreshToken');

    if (refreshToken) {
      const response = await fetch(
        `${API_URL}/api/auth/logout`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            refreshToken,
          }),
        }
      );

      if (!response.ok) {
        const errorData = await response.json().catch(() => null);

        // If the session is already invalid, local logout is safe.
        if (response.status !== 401) {
          throw new Error(
            errorData?.message || 'Server logout failed.'
          );
        }
      }
    }

    // Remove both tokens from the phone.
    await SecureStore.deleteItemAsync('accessToken');
    await SecureStore.deleteItemAsync('refreshToken');

    setRunner(null);
    setScreen('login');
  } catch (error) {
    console.log('Logout failed:', error);

    Alert.alert(
      'Logout failed',
      'Could not complete logout. Check your connection and try again.'
    );
  }
};


  if (checkingSession) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator
          size="large"
          color="#C6FF35"
        />
      </View>
    );
  }

  if (screen === 'home' && runner) {
    return (
      <HomeScreen
        displayName={runner.displayName}
        onLogout={handleLogout}
      />
    );
  }
if (screen === 'verifyEmail') {
  return (
    <VerifyEmailScreen
      email={verificationEmail}
      onVerified={() => {
        setVerificationEmail('');
        setScreen('login');
      }}
      onBackToLogin={() => setScreen('login')}
    />
  );
}
  if (screen === 'login') {
    return (
      <LoginScreen
        onGoToRegister={() => setScreen('register')}
        onLoginSuccess={handleLoginSuccess}
        onVerifyEmail={(email) => {
    setVerificationEmail(email);
    setScreen('verifyEmail');
  }}
      />
    );
  }

 
  return (
    <RegisterScreen
      onGoToLogin={() => setScreen('login')}
      onRegistrationSuccess={(email) => {
        setVerificationEmail(email);
        setScreen('verifyEmail');
      }}
    />
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    flex: 1,
    backgroundColor: '#0C0D0C',
    justifyContent: 'center',
    alignItems: 'center',
  },
});
