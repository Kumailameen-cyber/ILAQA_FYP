
import React, { useState } from 'react';
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
import * as SecureStore from 'expo-secure-store';

const API_URL = 'http://192.168.100.232:5246';

type Runner = {
  id: string;
  username: string;
  displayName: string;
};

type LoginScreenProps = {
  onGoToRegister: () => void;
  onLoginSuccess: (runner: Runner) => void;
  onVerifyEmail: (email: string) => void;
};

export default function LoginScreen({
  onGoToRegister,
  onLoginSuccess,
  onVerifyEmail,
}: LoginScreenProps) {
  const [emailOrUsername, setEmailOrUsername] = useState('');
  const [password, setPassword] = useState('');

  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const [error, setError] = useState('');

  const [needsVerification, setNeedsVerification] =
    useState(false);

  const [verificationEmail, setVerificationEmail] =
    useState('');

  const resetVerification = () => {
    setNeedsVerification(false);
    setVerificationEmail('');
    setError('');
  };

  const handleLogin = async () => {
    if (loading) return;

    const identifier = emailOrUsername.trim();

    if (!identifier || !password) {
      setError(
        'Please enter your email or username and password.'
      );
      return;
    }

    setLoading(true);
    setError('');
    setNeedsVerification(false);
    setVerificationEmail('');

    try {
      const response = await fetch(
        `${API_URL}/api/auth/login`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            emailOrUsername: identifier,
            password,
          }),
        }
      );

      const data = await response.json();

      // ACCOUNT EXISTS BUT EMAIL IS NOT VERIFIED
      if (!response.ok) {
        if (
          data.requiresEmailVerification === true &&
          typeof data.email === 'string' &&
          data.email.length > 0
        ) {
          setVerificationEmail(data.email);
          setNeedsVerification(true);
          setError('');
          return;
        }

        setError(
          data.message ||
            'Invalid email, username, or password.'
        );

        return;
      }

      // CHECK LOGIN RESPONSE
      if (
        !data.accessToken ||
        !data.refreshToken ||
        !data.user
      ) {
        setError(
          'The server returned incomplete login details.'
        );
        return;
      }

      // SAVE TOKENS SECURELY
      await SecureStore.setItemAsync(
        'accessToken',
        data.accessToken
      );

      await SecureStore.setItemAsync(
        'refreshToken',
        data.refreshToken
      );

      // PREPARE RUNNER DATA
      const loggedInRunner: Runner = {
        id: String(data.user.id),
        username: String(data.user.username ?? ''),
        displayName: String(
          data.user.displayName ??
            data.user.username ??
            'Runner'
        ),
      };

      setPassword('');
      setShowPassword(false);
      resetVerification();

      // OPEN HOME SCREEN
      onLoginSuccess(loggedInRunner);
    } catch {
      setError(
        'Unable to connect to the server. Check your connection and try again.'
      );
    } finally {
      setLoading(false);
    }
  };

 
const handleVerifyEmail = async () => {
  if (!verificationEmail) {
    setError(
      'Verification email is missing. Please sign in again.'
    );
    return;
  }

  if (loading) return;

  setLoading(true);
  setError('');

  try {
    const response = await fetch(
      `${API_URL}/api/auth/resend-email-otp`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          email: verificationEmail,
        }),
      }
    );

    const data = await response.json();

    // A recent OTP may already have been sent.
    // The backend enforces a 60-second cooldown.
    if (!response.ok && response.status !== 429) {
      setError(
        data.message ||
          'Unable to send verification code.'
      );
      return;
    }

    setPassword('');
    setShowPassword(false);

    onVerifyEmail(verificationEmail);
  } catch {
    setError(
      'Unable to connect to the server. Try again.'
    );
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

          <Text style={[styles.title, styles.accent]}>
            BACK.
          </Text>

          <Text style={styles.subtitle}>
            Your ground is waiting. Sign in to continue.
          </Text>
        </View>

        {/* EMAIL OR USERNAME */}

        <Text style={styles.label}>
          EMAIL OR USERNAME
        </Text>

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

        {/* PASSWORD */}

        <Text style={styles.label}>
          PASSWORD
        </Text>

        <View style={styles.passwordContainer}>
          <TextInput
            style={styles.passwordInput}
            placeholder="Enter your password"
            placeholderTextColor="#777"
            secureTextEntry={!showPassword}
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="password"
            value={password}
            onChangeText={(value) => {
              setPassword(value);
              resetVerification();
            }}
          />

          <TouchableOpacity
            style={styles.eyeButton}
            onPress={() =>
              setShowPassword(!showPassword)
            }
            accessibilityRole="button"
            accessibilityLabel={
              showPassword
                ? 'Hide password'
                : 'Show password'
            }
          >
            <Text style={styles.eyeText}>
              {showPassword ? 'HIDE' : 'SHOW'}
            </Text>
          </TouchableOpacity>
        </View>

        {/* INLINE ERROR MESSAGE */}

        {error ? (
          <View style={styles.errorBox}>
            <Text style={styles.errorText}>
              {error}
            </Text>
          </View>
        ) : null}

        {/* EMAIL VERIFICATION REQUIRED */}

        {needsVerification && verificationEmail ? (
          <View style={styles.verificationBox}>
            <Text style={styles.verificationTitle}>
              EMAIL NOT VERIFIED
            </Text>

            <Text style={styles.verificationDescription}>
              Your account is waiting for email
              verification. Enter the OTP sent to:
            </Text>

            <Text style={styles.verificationEmail}>
              {verificationEmail}
            </Text>

            <TouchableOpacity
  style={[
    styles.verifyButton,
    loading && styles.disabled,
  ]}
  onPress={handleVerifyEmail}
  disabled={loading}
>
  {loading ? (
    <ActivityIndicator color="#101310" />
  ) : (
    <Text style={styles.verifyButtonText}>
      VERIFY EMAIL →
    </Text>
  )}
</TouchableOpacity>
          </View>
        ) : null}

        {/* SIGN IN BUTTON */}

        <TouchableOpacity
          style={[
            styles.button,
            loading && styles.disabled,
          ]}
          onPress={handleLogin}
          disabled={loading}
        >
          {loading ? (
            <ActivityIndicator color="#101310" />
          ) : (
            <Text style={styles.buttonText}>
              SIGN IN →
            </Text>
          )}
        </TouchableOpacity>

        {/* REGISTER LINK */}

        <Text style={styles.footer}>
          New to ILAQA?{' '}
          <Text
            style={styles.footerLink}
            onPress={onGoToRegister}
          >
            Create an account
          </Text>
        </Text>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },

  content: {
    flexGrow: 1,
    paddingHorizontal: 28,
    paddingTop: 55,
    paddingBottom: 40,
  },

  logo: {
    color: '#C6FF35',
    fontSize: 25,
    fontWeight: '900',
    letterSpacing: 2,
  },

  heading: {
    marginTop: 65,
    marginBottom: 40,
  },

  title: {
    color: '#FFFFFF',
    fontSize: 44,
    fontWeight: '900',
    letterSpacing: -1.5,
    lineHeight: 48,
  },

  accent: {
    color: '#C6FF35',
  },

  subtitle: {
    color: '#999E99',
    fontSize: 14,
    marginTop: 15,
    lineHeight: 21,
  },

  label: {
    color: '#A5A9A5',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginBottom: 9,
  },

  input: {
    backgroundColor: '#1C1E1C',
    color: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#303330',
    borderRadius: 8,
    height: 54,
    paddingHorizontal: 16,
    fontSize: 15,
    marginBottom: 22,
  },

  passwordContainer: {
    backgroundColor: '#1C1E1C',
    borderWidth: 1,
    borderColor: '#303330',
    borderRadius: 8,
    height: 54,
    marginBottom: 22,
    flexDirection: 'row',
    alignItems: 'center',
  },

  passwordInput: {
    flex: 1,
    color: '#FFFFFF',
    paddingHorizontal: 16,
    fontSize: 15,
    height: '100%',
  },

  eyeButton: {
    paddingHorizontal: 16,
    height: '100%',
    justifyContent: 'center',
  },

  eyeText: {
    color: '#C6FF35',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 1,
  },

  errorBox: {
    backgroundColor: '#291717',
    borderColor: '#B64B4B',
    borderWidth: 1,
    borderRadius: 8,
    padding: 14,
    marginBottom: 18,
  },

  errorText: {
    color: '#FF8585',
    fontSize: 13,
    lineHeight: 20,
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

  button: {
    backgroundColor: '#C6FF35',
    height: 56,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 14,
  },

  disabled: {
    opacity: 0.6,
  },

  buttonText: {
    color: '#101310',
    fontWeight: '900',
    fontSize: 14,
    letterSpacing: 1,
  },

  footer: {
    color: '#A5A9A5',
    textAlign: 'center',
    marginTop: 30,
    fontSize: 13,
  },

  footerLink: {
    color: '#C6FF35',
    fontWeight: '700',
  },
});
