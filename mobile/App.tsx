// App root: restores the login session and switches between the auth
// screens and the signed-in tabs (Home map, Profile).

import './services/backgroundLocationTask';
import { useEffect, useState } from 'react';
import {
  View,
  ActivityIndicator,
  StyleSheet,
  Alert,
  Text,
} from 'react-native';

import RegisterScreen from './screens/RegisterScreen';
import LoginScreen from './screens/LoginScreen';
import HomeScreen from './screens/HomeScreen';
import VerifyEmailScreen from './screens/VerifyEmailScreen';
import ForgotPasswordScreen from './screens/ForgotPasswordScreen';
import VerifyResetOtpScreen from './screens/VerifyResetOtpScreen';
import NewPasswordScreen from './screens/NewPasswordScreen';
import ProfileScreen from './screens/ProfileScreen';
import BottomTabBar, { MainTab } from './components/BottomTabBar';
import {
  Runner,
  clearTokens,
  logoutOnServer,
  restoreSession,
} from './services/session';

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
  const [activeTab, setActiveTab] = useState<MainTab>('home');
  const [runner, setRunner] = useState<Runner | null>(null);
  const [checkingSession, setCheckingSession] = useState(true);
  const [verificationEmail, setVerificationEmail] = useState('');
  const [resetEmail, setResetEmail] = useState('');
  const [resetToken, setResetToken] = useState('');

  useEffect(() => {
    const restore = async () => {
      try {
        const restoredRunner = await restoreSession();
        if (!restoredRunner) return;

        setRunner(restoredRunner);
        setScreen('home');
      } catch {
        // A temporary network error should not delete saved tokens.
        console.log('Unable to restore session.');
      } finally {
        setCheckingSession(false);
      }
    };

    restore();
  }, []);

  const handleLoginSuccess = (loggedInRunner: Runner) => {
    setRunner(loggedInRunner);
    setScreen('home');
    setActiveTab('home');
  };

  const handleLogout = async () => {
    try {
      await logoutOnServer();

      // Remove both tokens from the phone.
      await clearTokens();

      setRunner(null);
      setScreen('login');
      setActiveTab('home');
    } catch (error) {
      console.log('Logout failed:', error);

      Alert.alert(
        'Logout failed',
        'Could not complete logout. Check your connection and try again.'
      );
    }
  };

  const handleAccountDeleted = async () => {
    // The backend has already deleted the account.
    // No need to call the logout API.
    try {
      await clearTokens();
    } finally {
      // Clear the logged-in user and return to Login.
      setRunner(null);
      setActiveTab('home');
      setScreen('login');
    }
  };

  const clearPasswordReset = () => {
    setResetEmail('');
    setResetToken('');
  };

  if (checkingSession) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#C6FF35" />
      </View>
    );
  }

  if (screen === 'home' && runner) {
    return (
      <View style={styles.mainContainer}>
        <View style={styles.screenContainer}>
          {activeTab === 'home' ? (
            <HomeScreen
              runnerId={runner.id}
              displayName={runner.displayName}
              onLogout={handleLogout}
            />
          ) : activeTab === 'profile' ? (
            <ProfileScreen
              onDisplayNameUpdated={(newName) => {
                setRunner((current) =>
                  current ? { ...current, displayName: newName } : current
                );
              }}
              onAccountDeleted={handleAccountDeleted}
            />
          ) : (
            <View style={styles.comingSoonContainer}>
              <Text style={styles.comingSoonTitle}>
                {activeTab.toUpperCase()}
              </Text>
              <Text style={styles.comingSoonText}>
                This feature is coming soon.
              </Text>
            </View>
          )}
        </View>

        <BottomTabBar activeTab={activeTab} onTabChange={setActiveTab} />
      </View>
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
          setResetToken('');
          setScreen('verifyResetOtp');
        }}
        onBackToLogin={() => {
          clearPasswordReset();
          setScreen('login');
        }}
      />
    );
  }

  if (screen === 'verifyResetOtp') {
    return (
      <VerifyResetOtpScreen
        email={resetEmail}
        onVerified={(token) => {
          setResetToken(token);
          setScreen('newPassword');
        }}
        onBack={() => {
          setResetToken('');
          setScreen('forgotPassword');
        }}
      />
    );
  }

  if (screen === 'newPassword') {
    return (
      <NewPasswordScreen
        email={resetEmail}
        resetToken={resetToken}
        onResetSuccess={() => {
          clearPasswordReset();
          setScreen('login');
        }}
        onResetCancelled={() => {
          clearPasswordReset();
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
          clearPasswordReset();
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
  mainContainer: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },
  screenContainer: {
    flex: 1,
  },
  comingSoonContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#0C0D0C',
  },
  comingSoonTitle: {
    color: '#C6FF35',
    fontSize: 30,
    fontWeight: '900',
    letterSpacing: 2,
  },
  comingSoonText: {
    color: '#999E99',
    fontSize: 14,
    marginTop: 12,
  },
});
