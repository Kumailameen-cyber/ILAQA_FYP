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
import PasswordInput from '../components/PasswordInput';
import AppPopup from '../components/AppPopup';
import { API_URL } from '../config/api';
type Runner = {
  id: string;
  username: string;
  displayName: string;
};

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
  const [needsVerification, setNeedsVerification] =
    useState(false);

  const [verificationEmail, setVerificationEmail] =
    useState('');

  const resetVerification = () => {
    setNeedsVerification(false);
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
      showLoginError(
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

      const data = await response.json().catch(() => null);
if (response.status === 429) {
  showLoginError(
    'Too many attempts. Please wait a minute before trying again.'
  );
  return;
}
      // ACCOUNT EXISTS BUT EMAIL IS NOT VERIFIED
      if (!response.ok) {
        if (
          data?.requiresEmailVerification === true &&
          typeof data?.email === 'string' &&
          data.email.length > 0
        ) {
          setVerificationEmail(data.email);
          setNeedsVerification(true);
          setError('');
          return;
        }

        showLoginError(
          data?.message ||
            'Invalid email, username, or password.'
        );

        return;
      }

      // CHECK LOGIN RESPONSE
      if (
        !data?.accessToken ||
        !data?.refreshToken ||
        !data?.user
      ) {
        showLoginError(
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
      
      resetVerification();

      // OPEN HOME SCREEN
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
    showLoginError(
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

    
const data = await response.json().catch(() => null);

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
  showLoginError(
    data?.message || 'Unable to send verification code.'
  );
  return;
}

    setPassword('');
    

    onVerifyEmail(verificationEmail);
  } catch {
    showLoginError(
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

        
<PasswordInput
  value={password}
  onChangeText={(value) => {
    setPassword(value);
    resetVerification();
  }}
  placeholder="Enter your password"
  autoComplete="password"
/>


        {/* FORGOT PASSWORD LINK - ADD HERE */}
        <TouchableOpacity
          style={styles.forgotPasswordButton}
          onPress={onForgotPassword}
          disabled={loading}
        >
          <Text style={styles.forgotPasswordText}>
            Forgot Password?
          </Text>
        </TouchableOpacity>


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
