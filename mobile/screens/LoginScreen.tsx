// Sign in: email/username + password login, and the "email not verified"
// prompt that sends the runner to email verification.

import { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  ScrollView,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import PasswordInput from '../components/PasswordInput';
import AppPopup from '../components/AppPopup';
import { signInStyles } from '../components/authStyles';
import { postJson } from '../services/api';
import { Runner, saveTokens } from '../services/session';

type LoginScreenProps = {
  onGoToRegister: () => void;
  onLoginSuccess: (runner: Runner) => void;
  onVerifyEmail: (email: string) => void;
  onForgotPassword: () => void;
};

export default function LoginScreen({
  onGoToRegister,
  onLoginSuccess,
  onVerifyEmail,
  onForgotPassword,
}: LoginScreenProps) {
  const [emailOrUsername, setEmailOrUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const [error, setError] = useState('');
  const [errorPopupVisible, setErrorPopupVisible] = useState(false);

  // Set when the account exists but its email is not verified yet.
  const [verificationEmail, setVerificationEmail] = useState('');

  const resetVerification = () => {
    setVerificationEmail('');
    setError('');
  };

  const showLoginError = (message: string) => {
    setError(message);
    setErrorPopupVisible(true);
  };

  const handleLogin = async () => {
    if (loading) return;

    const identifier = emailOrUsername.trim();

    if (!identifier || !password) {
      showLoginError('Please enter your email or username and password.');
      return;
    }

    setLoading(true);
    resetVerification();

    try {
      const { response, data } = await postJson('/api/auth/login', {
        emailOrUsername: identifier,
        password,
      });

      if (response.status === 429) {
        showLoginError(
          'Too many attempts. Please wait a minute before trying again.'
        );
        return;
      }

      if (!response.ok) {
        // Account exists but the email is not verified.
        if (
          data?.requiresEmailVerification === true &&
          typeof data?.email === 'string' &&
          data.email.length > 0
        ) {
          setVerificationEmail(data.email);
          setError('');
          return;
        }

        showLoginError(data?.message || 'Invalid email, username, or password.');
        return;
      }

      if (!data?.accessToken || !data?.refreshToken || !data?.user) {
        showLoginError('The server returned incomplete login details.');
        return;
      }

      await saveTokens(data.accessToken, data.refreshToken);

      const loggedInRunner: Runner = {
        id: String(data.user.id),
        username: String(data.user.username ?? ''),
        displayName: String(
          data.user.displayName ?? data.user.username ?? 'Runner'
        ),
      };

      setPassword('');
      resetVerification();
      onLoginSuccess(loggedInRunner);
    } catch {
      showLoginError(
        'Unable to connect to the server. Check your connection and try again.'
      );
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyEmail = async () => {
    if (!verificationEmail) {
      showLoginError('Verification email is missing. Please sign in again.');
      return;
    }

    if (loading) return;

    setLoading(true);
    setError('');

    try {
      const { response, data } = await postJson('/api/auth/resend-email-otp', {
        email: verificationEmail,
      });

      // Distinguish the OTP cooldown from the API rate limit.
      if (response.status === 429) {
        if (data?.message?.includes('wait 60 seconds')) {
          // OTP was already sent recently. Use the existing code.
          setPassword('');
          onVerifyEmail(verificationEmail);
          return;
        }

        showLoginError(
          'Too many requests. Please wait a minute before trying again.'
        );
        return;
      }

      if (!response.ok) {
        showLoginError(data?.message || 'Unable to send verification code.');
        return;
      }

      setPassword('');
      onVerifyEmail(verificationEmail);
    } catch {
      showLoginError('Unable to connect to the server. Try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar style="light" />

      <ScrollView
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <Text style={styles.logo}>ILAQA®</Text>

        <View style={styles.heading}>
          <Text style={styles.title}>WELCOME</Text>
          <Text style={[styles.title, styles.accent]}>BACK.</Text>
          <Text style={styles.subtitle}>
            Your ground is waiting. Sign in to continue.
          </Text>
        </View>

        <Text style={styles.label}>EMAIL OR USERNAME</Text>
        <TextInput
          style={styles.input}
          placeholder="Email or username"
          placeholderTextColor="#777"
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="username"
          value={emailOrUsername}
          onChangeText={(value) => {
            setEmailOrUsername(value);
            resetVerification();
          }}
        />

        <Text style={styles.label}>PASSWORD</Text>
        <PasswordInput
          value={password}
          onChangeText={(value) => {
            setPassword(value);
            resetVerification();
          }}
          placeholder="Enter your password"
          autoComplete="password"
        />

        <TouchableOpacity
          style={styles.forgotPasswordButton}
          onPress={onForgotPassword}
          disabled={loading}
        >
          <Text style={styles.forgotPasswordText}>Forgot Password?</Text>
        </TouchableOpacity>

        {verificationEmail ? (
          <View style={styles.verificationBox}>
            <Text style={styles.verificationTitle}>EMAIL NOT VERIFIED</Text>

            <Text style={styles.verificationDescription}>
              Your account is waiting for email
              verification. Enter the OTP sent to:
            </Text>

            <Text style={styles.verificationEmail}>{verificationEmail}</Text>

            <TouchableOpacity
              style={[styles.verifyButton, loading && styles.disabled]}
              onPress={handleVerifyEmail}
              disabled={loading}
            >
              {loading ? (
                <ActivityIndicator color="#101310" />
              ) : (
                <Text style={styles.verifyButtonText}>VERIFY EMAIL →</Text>
              )}
            </TouchableOpacity>
          </View>
        ) : null}

        <TouchableOpacity
          style={[styles.button, loading && styles.disabled]}
          onPress={handleLogin}
          disabled={loading}
        >
          {loading ? (
            <ActivityIndicator color="#101310" />
          ) : (
            <Text style={styles.buttonText}>SIGN IN →</Text>
          )}
        </TouchableOpacity>

        <Text style={styles.footer}>
          New to ILAQA?{' '}
          <Text style={styles.footerLink} onPress={onGoToRegister}>
            Create an account
          </Text>
        </Text>
      </ScrollView>

      <AppPopup
        visible={errorPopupVisible}
        type="error"
        title="SIGN IN FAILED"
        message={error}
        onClose={() => setErrorPopupVisible(false)}
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  ...signInStyles,
  subtitle: {
    color: '#999E99',
    fontSize: 14,
    marginTop: 15,
    lineHeight: 21,
  },
  verificationBox: {
    backgroundColor: '#1C1E1C',
    borderColor: '#C6FF35',
    borderWidth: 1,
    borderRadius: 8,
    padding: 18,
    marginBottom: 18,
  },
  verificationTitle: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '900',
    marginBottom: 9,
  },
  verificationDescription: {
    color: '#A5A9A5',
    fontSize: 13,
    lineHeight: 20,
  },
  verificationEmail: {
    color: '#C6FF35',
    fontSize: 13,
    fontWeight: '700',
    marginTop: 10,
    marginBottom: 18,
  },
  verifyButton: {
    backgroundColor: '#C6FF35',
    borderRadius: 7,
    padding: 14,
    alignItems: 'center',
  },
  verifyButtonText: {
    color: '#0C0D0C',
    fontWeight: '900',
    fontSize: 12,
    letterSpacing: 0.5,
  },
  forgotPasswordButton: {
    alignSelf: 'flex-end',
    marginTop: -8,
    marginBottom: 24,
    paddingVertical: 8,
  },
  forgotPasswordText: {
    color: '#C6FF35',
    fontSize: 13,
    fontWeight: '700',
  },
});
