
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
import PasswordInput from '../components/PasswordInput';
import AppPopup from '../components/AppPopup';

import { API_URL } from '../config/api';

type RegisterScreenProps = {
  onGoToLogin: () => void;
  onRegistrationSuccess: (email: string) => void;
};

type PopupType = 'error' | 'warning' | 'success';

type PopupState = {
  type: PopupType;
  title: string;
  message: string;
};

export default function RegisterScreen({
  onGoToLogin,
  onRegistrationSuccess,
}: RegisterScreenProps) {
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const [popup, setPopup] = useState<PopupState | null>(null);

  // Reusable popup function
  const showPopup = (
    type: PopupType,
    title: string,
    message: string
  ) => {
    setPopup({ type, title, message });
  };

  const handleRegister = async () => {
    if (loading) return;

    // Check required fields
    if (
      !username.trim() ||
      !displayName.trim() ||
      !email.trim() ||
      !password
    ) {
      showPopup(
        'warning',
        'MISSING DETAILS',
        'Please fill in all fields.'
      );
      return;
    }

    // Username validation
    if (
      username.trim().length < 3 ||
      username.trim().length > 30
    ) {
      showPopup(
        'warning',
        'INVALID USERNAME',
        'Username must be 3–30 characters.'
      );
      return;
    }

    // Display name validation
    if (
      displayName.trim().length < 2 ||
      displayName.trim().length > 50
    ) {
      showPopup(
        'warning',
        'INVALID NAME',
        'Display name must be 2–50 characters.'
      );
      return;
    }

    // Password validation
    if (password.length < 8) {
      showPopup(
        'warning',
        'INVALID PASSWORD',
        'Password must be at least 8 characters.'
      );
      return;
    }

    setLoading(true);

    try {
      const response = await fetch(
        `${API_URL}/api/auth/register`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            username: username.trim(),
            displayName: displayName.trim(),
            email: email.trim(),
            password,
          }),
        }
      );

      const data = await response.json().catch(() => null);

      if (response.ok) {
        const registeredEmail = email.trim();

        // Clear registration fields
        setUsername('');
        setDisplayName('');
        setEmail('');
        setPassword('');

        // Navigate to email OTP verification
        onRegistrationSuccess(registeredEmail);
      } else {
        const errors = data?.errors
          ? Object.values(data.errors).flat().join('\n')
          : data?.message ||
            data?.title ||
            'Registration failed.';

        showPopup(
          'error',
          'REGISTRATION FAILED',
          String(errors)
        );
      }
    } catch {
      showPopup(
        'error',
        'CONNECTION ERROR',
        'Cannot connect to ILAQA backend. Check that Visual Studio is running.'
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
          <Text style={styles.title}>OWN YOUR</Text>

          <Text style={[styles.title, styles.accent]}>
            GROUND.
          </Text>

          <Text style={styles.subtitle}>
            Create your account. Your city is waiting.
          </Text>
        </View>

        {/* USERNAME */}
        <Text style={styles.label}>USERNAME</Text>

        <TextInput
          style={styles.input}
          placeholder="runner01"
          placeholderTextColor="#777"
          autoCapitalize="none"
          autoCorrect={false}
          value={username}
          onChangeText={setUsername}
          editable={!loading}
        />

        {/* DISPLAY NAME */}
        <Text style={styles.label}>DISPLAY NAME</Text>

        <TextInput
          style={styles.input}
          placeholder="Your name"
          placeholderTextColor="#777"
          value={displayName}
          onChangeText={setDisplayName}
          editable={!loading}
        />

        {/* EMAIL */}
        <Text style={styles.label}>EMAIL ADDRESS</Text>

        <TextInput
          style={styles.input}
          placeholder="you@example.com"
          placeholderTextColor="#777"
          keyboardType="email-address"
          autoCapitalize="none"
          autoCorrect={false}
          value={email}
          onChangeText={setEmail}
          editable={!loading}
        />

        {/* PASSWORD */}
        <Text style={styles.label}>PASSWORD</Text>

        <PasswordInput
          value={password}
          onChangeText={setPassword}
          placeholder="At least 8 characters"
          autoComplete="new-password"
          editable={!loading}
        />

        {/* REGISTER BUTTON */}
        <TouchableOpacity
          style={[
            styles.button,
            loading && styles.disabled,
          ]}
          onPress={handleRegister}
          disabled={loading}
        >
          {loading ? (
            <ActivityIndicator color="#101310" />
          ) : (
            <Text style={styles.buttonText}>
              CREATE ACCOUNT →
            </Text>
          )}
        </TouchableOpacity>

        {/* LOGIN LINK */}
        <Text style={styles.footer}>
          Already a runner?{' '}
          <Text
            style={styles.footerLink}
            onPress={onGoToLogin}
          >
            Sign in
          </Text>
        </Text>
      </ScrollView>

      {/* REUSABLE ILAQA POPUP */}
      <AppPopup
        visible={popup !== null}
        type={popup?.type ?? 'error'}
        title={popup?.title ?? ''}
        message={popup?.message ?? ''}
        onClose={() => setPopup(null)}
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
