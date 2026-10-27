import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
} from 'react-native';
import AppPopup from '../components/AppPopup';

import { API_URL } from '../config/api';

type Props = {
  email: string;
  resetToken: string;
  onResetSuccess: () => void;
  onBack: () => Promise<void>;
};

type Popup = {
  type: 'error' | 'warning' | 'success';
  title: string;
  message: string;
};

export default function NewPasswordScreen({
  email,
  resetToken,
  onResetSuccess,
  onBack,
}: Props) {
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [popup, setPopup] = useState<Popup | null>(null);

  const showError = (message: string, title = 'SOMETHING WENT WRONG') => {
    setPopup({ type: 'error', title, message });
  };

  const handleBackToLogin = async () => {
    if (loading || cancelling) return;

    setCancelling(true);
    setPopup(null);

    try {
      await onBack();
    } catch (error) {
      showError(
        error instanceof Error
          ? error.message
          : 'Unable to cancel password reset. Please try again.',
        'CANNOT GO BACK'
      );
    } finally {
      setCancelling(false);
    }
  };

  const handleResetPassword = async () => {
    if (loading || cancelling) return;

    if (!newPassword || !confirmPassword) {
      showError('Please fill in both password fields.', 'MISSING PASSWORD');
      return;
    }

    if (newPassword !== confirmPassword) {
      showError('Passwords do not match.', 'PASSWORD MISMATCH');
      return;
    }

    setLoading(true);
    setPopup(null);

    try {
      const response = await fetch(`${API_URL}/api/auth/reset-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, resetToken, newPassword }),
      });

      const data = await response.json().catch(() => null);

      if (response.status === 429) {
        showError(
          data?.message ||
            'Too many requests. Please wait a minute before trying again.',
          'TOO MANY REQUESTS'
        );
        return;
      }

      if (!response.ok) {
        const details = Array.isArray(data?.errors)
          ? data.errors.join('\n')
          : '';

        showError(
          details ||
            data?.message ||
            'Unable to reset password. Please try again.',
          'RESET FAILED'
        );
        return;
      }

      onResetSuccess();
    } catch {
      showError(
        'Unable to connect to the server. Please try again.',
        'CONNECTION ERROR'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <Text style={styles.brand}>ILAQA.</Text>

        <Text style={styles.heading}>NEW PASSWORD</Text>

        <Text style={styles.description}>
          Your verification code was accepted. Create a new password to secure
          your ILAQA account.
        </Text>

        <Text style={styles.label}>NEW PASSWORD</Text>
        <View style={styles.inputContainer}>
          <TextInput
            style={styles.passwordInput}
            placeholder="Enter new password"
            placeholderTextColor="#777777"
            secureTextEntry={!showPassword}
            autoCapitalize="none"
            autoCorrect={false}
            value={newPassword}
            onChangeText={setNewPassword}
            editable={!loading && !cancelling}
            autoComplete="new-password"
          />
          <TouchableOpacity
            onPress={() => setShowPassword((previous) => !previous)}
            style={styles.visibilityButton}
            disabled={loading || cancelling}
          >
            <Text style={styles.visibilityText}>
              {showPassword ? 'HIDE' : 'SHOW'}
            </Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.label}>CONFIRM PASSWORD</Text>
        <View style={styles.inputContainer}>
          <TextInput
            style={styles.passwordInput}
            placeholder="Confirm new password"
            placeholderTextColor="#777777"
            secureTextEntry={!showConfirmPassword}
            autoCapitalize="none"
            autoCorrect={false}
            value={confirmPassword}
            onChangeText={setConfirmPassword}
            editable={!loading && !cancelling}
            autoComplete="new-password"
          />
          <TouchableOpacity
            onPress={() => setShowConfirmPassword((previous) => !previous)}
            style={styles.visibilityButton}
            disabled={loading || cancelling}
          >
            <Text style={styles.visibilityText}>
              {showConfirmPassword ? 'HIDE' : 'SHOW'}
            </Text>
          </TouchableOpacity>
        </View>

        <Text style={styles.hint}>
          Use a strong password that meets the registration requirements.
        </Text>

        <TouchableOpacity
          style={[
            styles.button,
            (loading || cancelling) && styles.disabledButton,
          ]}
          onPress={handleResetPassword}
          disabled={loading || cancelling}
          activeOpacity={0.8}
        >
          {loading ? (
            <ActivityIndicator color="#0C0D0C" />
          ) : (
            <Text style={styles.buttonText}>RESET PASSWORD →</Text>
          )}
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.backButton}
          onPress={handleBackToLogin}
          disabled={loading || cancelling}
        >
          {cancelling ? (
            <ActivityIndicator color="#C6FF35" />
          ) : (
            <Text style={styles.backText}>← BACK TO LOGIN</Text>
          )}
        </TouchableOpacity>
      </ScrollView>

      <AppPopup
        visible={popup !== null}
        type={popup?.type ?? 'error'}
        title={popup?.title ?? ''}
        message={popup?.message ?? ''}
        onClose={() => setPopup(null)}
      />
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0C0D0C' },
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    paddingHorizontal: 28,
    paddingVertical: 48,
  },
  brand: {
    color: '#C6FF35',
    fontSize: 24,
    fontWeight: '900',
    letterSpacing: 2,
    marginBottom: 45,
  },
  heading: {
    color: '#FFFFFF',
    fontSize: 32,
    fontWeight: '900',
    letterSpacing: -1,
    marginBottom: 14,
  },
  description: {
    color: '#A0A0A0',
    fontSize: 15,
    lineHeight: 23,
    marginBottom: 35,
  },
  label: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 1.5,
    marginBottom: 10,
  },
  inputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#1B1D1B',
    borderColor: '#333633',
    borderWidth: 1,
    borderRadius: 12,
    marginBottom: 22,
  },
  passwordInput: {
    flex: 1,
    color: '#FFFFFF',
    fontSize: 16,
    paddingHorizontal: 16,
    paddingVertical: 16,
  },
  visibilityButton: { paddingHorizontal: 16, paddingVertical: 14 },
  visibilityText: { color: '#C6FF35', fontSize: 11, fontWeight: '800' },
  hint: {
    color: '#888888',
    fontSize: 12,
    lineHeight: 19,
    marginBottom: 20,
  },
  button: {
    backgroundColor: '#C6FF35',
    paddingVertical: 18,
    borderRadius: 12,
    alignItems: 'center',
    marginTop: 8,
  },
  disabledButton: { opacity: 0.6 },
  buttonText: {
    color: '#0C0D0C',
    fontSize: 14,
    fontWeight: '900',
    letterSpacing: 1,
  },
  backButton: { alignItems: 'center', marginTop: 28, padding: 10 },
  backText: {
    color: '#C6FF35',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 1,
  },
});
