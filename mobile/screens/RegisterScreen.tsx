// Create account: registration form; on success the runner goes on to
// verify their email.

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
import { MessagePopup, usePopup } from '../components/AppPopup';
import { signInStyles } from '../components/authStyles';
import { postJson } from '../services/api';

type RegisterScreenProps = {
  onGoToLogin: () => void;
  onRegistrationSuccess: (email: string) => void;
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
  const { popup, showPopup, closePopup } = usePopup();

  const handleRegister = async () => {
    if (loading) return;

    if (!username.trim() || !displayName.trim() || !email.trim() || !password) {
      showPopup('warning', 'MISSING DETAILS', 'Please fill in all fields.');
      return;
    }

    if (username.trim().length < 3 || username.trim().length > 30) {
      showPopup('warning', 'INVALID USERNAME', 'Username must be 3–30 characters.');
      return;
    }

    if (displayName.trim().length < 2 || displayName.trim().length > 50) {
      showPopup('warning', 'INVALID NAME', 'Display name must be 2–50 characters.');
      return;
    }

    if (password.length < 8) {
      showPopup('warning', 'INVALID PASSWORD', 'Password must be at least 8 characters.');
      return;
    }

    setLoading(true);

    try {
      const { response, data } = await postJson('/api/auth/register', {
        username: username.trim(),
        displayName: displayName.trim(),
        email: email.trim(),
        password,
      });

      if (response.ok) {
        const registeredEmail = email.trim();

        setUsername('');
        setDisplayName('');
        setEmail('');
        setPassword('');

        // Go to email OTP verification.
        onRegistrationSuccess(registeredEmail);
      } else {
        const errors = data?.errors
          ? Object.values(data.errors).flat().join('\n')
          : data?.message || data?.title || 'Registration failed.';

        showPopup('error', 'REGISTRATION FAILED', String(errors));
      }
    } catch {
      showPopup(
        'error',
        'CONNECTION ERROR',
        'Unable to connect to the server. Check your connection and try again.'
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
          <Text style={[styles.title, styles.accent]}>GROUND.</Text>
          <Text style={styles.subtitle}>
            Create your account. Your city is waiting.
          </Text>
        </View>

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

        <Text style={styles.label}>DISPLAY NAME</Text>
        <TextInput
          style={styles.input}
          placeholder="Your name"
          placeholderTextColor="#777"
          value={displayName}
          onChangeText={setDisplayName}
          editable={!loading}
        />

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

        <Text style={styles.label}>PASSWORD</Text>
        <PasswordInput
          value={password}
          onChangeText={setPassword}
          placeholder="At least 8 characters"
          autoComplete="new-password"
          editable={!loading}
        />

        <TouchableOpacity
          style={[styles.button, loading && styles.disabled]}
          onPress={handleRegister}
          disabled={loading}
        >
          {loading ? (
            <ActivityIndicator color="#101310" />
          ) : (
            <Text style={styles.buttonText}>CREATE ACCOUNT →</Text>
          )}
        </TouchableOpacity>

        <Text style={styles.footer}>
          Already a runner?{' '}
          <Text style={styles.footerLink} onPress={onGoToLogin}>
            Sign in
          </Text>
        </Text>
      </ScrollView>

      <MessagePopup popup={popup} onClose={closePopup} />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  ...signInStyles,
  subtitle: {
    color: '#999E99',
    fontSize: 14,
    marginTop: 15,
  },
});
