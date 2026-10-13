
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
import ForgotPasswordScreen from './screens/ForgotPasswordScreen';
import VerifyResetOtpScreen from './screens/VerifyResetOtpScreen';
import NewPasswordScreen from './screens/NewPasswordScreen';
const API_URL = 'http://192.168.100.232:5246';

type Runner = {
  id: string;
  username: string;
  displayName: string;
};

type Screen =
  | 'register'
  | 'login'
  | 'home'
  | 'verifyEmail'
  | 'forgotPassword'
  | 'verifyResetOtp'
  | 'newPassword';

export default function App() {
  const [screen, setScreen] = useState<Screen>('login');
  const [runner, setRunner] = useState<Runner | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);
const [verificationEmail, setVerificationEmail] = useState('');
const [resetEmail, setResetEmail] = useState('');
const [resetCode, setResetCode] = useState('');
  useEffect(() => {
    const restoreSession = async () => {
      try {
        const refreshToken =
          await SecureStore.getItemAsync('refreshToken');

        if (!refreshToken) return;

        // Verify the saved session with the backend.
        // A saved token alone does not prove the session is valid.
       
const requestRefresh = () =>
  fetch(`${API_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ refreshToken }),
  });

let response = await requestRefresh();

if (response.status === 429 || response.status >= 500) {
  console.log('Temporary refresh failure. Retrying once...');

  await new Promise<void>((resolve) => {
    setTimeout(resolve, 5000);
  });

  response = await requestRefresh();
}

        
if (response.status === 429) {
  console.log(
    'Session refresh rate-limited. Saved tokens preserved.'
  );
  return;
}


if (!response.ok) {
  if (response.status === 400 || response.status === 401) {
    await SecureStore.deleteItemAsync('accessToken');
    await SecureStore.deleteItemAsync('refreshToken');
  } else {
    console.log(
      'Temporary session refresh failure. Saved tokens preserved.'
    );
  }

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
      onRegisterAgain={() => {
        setVerificationEmail('');
        setScreen('register');
      }}
    />
  );
}

if (screen === 'forgotPassword') {
  return (
    <ForgotPasswordScreen
      onOtpRequested={(email) => {
        setResetEmail(email);
        setResetCode('');
        setScreen('verifyResetOtp');
      }}
      onBackToLogin={() => {
        setResetEmail('');
        setResetCode('');
        setScreen('login');
      }}
    />
  );
}

if (screen === 'verifyResetOtp') {
  return (
    <VerifyResetOtpScreen
      email={resetEmail}
      onVerified={(code) => {
        setResetCode(code);
        setScreen('newPassword');
      }}
      onBack={() => {
        setResetCode('');
        setScreen('forgotPassword');
      }}
    />
  );
}




if (screen === 'newPassword') {
  return (
    <NewPasswordScreen
      email={resetEmail}
      code={resetCode}
      onResetSuccess={() => {
        setResetEmail('');
        setResetCode('');
        setScreen('login');
      }}
      onBack={async () => {
        const response = await fetch(
          `${API_URL}/api/auth/cancel-password-reset`,
          {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({
              email: resetEmail,
              code: resetCode,
            }),
          }
        );

       
if (!response.ok) {
  const data = await response.json().catch(() => null);

  if (response.status === 429) {
    throw new Error(
      'Too many requests. Please wait a minute before trying again.'
    );
  }

  throw new Error(
    data?.message || 'Password reset cancellation failed.'
  );
}


        setResetEmail('');
        setResetCode('');
        setScreen('login');
      }}
    />
  );
}





if (screen === 'login') {
  return (
    <LoginScreen
      onGoToRegister={() => setScreen('register')}
      onLoginSuccess={handleLoginSuccess}
      onForgotPassword={() => {
        setResetEmail('');
        setResetCode('');
        setScreen('forgotPassword');
      }}
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
