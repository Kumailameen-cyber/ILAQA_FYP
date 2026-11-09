// Forgot password, step 1: enter the account email to receive a reset code.

import { useState } from 'react';
import {
  Text,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
} from 'react-native';
import { MessagePopup, usePopup } from '../components/AppPopup';
import { passwordResetStyles } from '../components/authStyles';
import { postJson, TOO_MANY_REQUESTS_MESSAGE } from '../services/api';

type Props = {
  onOtpRequested: (email: string) => void;
  onBackToLogin: () => void;
};

export default function ForgotPasswordScreen({
  onOtpRequested,
  onBackToLogin,
}: Props) {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const { popup, showPopup, closePopup } = usePopup();

  const showError = (message: string) =>
    showPopup('error', 'UNABLE TO SEND CODE', message);

  const handleSendOtp = async () => {
    if (loading) return;

    const normalizedEmail = email.trim().toLowerCase();

    if (!normalizedEmail) {
      showError('Please enter your email address.');
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)) {
      showError('Please enter a valid email address.');
      return;
    }

    setLoading(true);
    closePopup();

    try {
      const { response, data } = await postJson('/api/auth/forgot-password', {
        email: normalizedEmail,
      });

      if (response.status === 429) {
        showError(TOO_MANY_REQUESTS_MESSAGE);
        return;
      }

      if (!response.ok) {
        showError(data?.message || 'Unable to send code. Please try again.');
        return;
      }

      onOtpRequested(normalizedEmail);
    } catch {
      showError('Unable to connect to the server. Please try again.');
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

        <Text style={styles.heading}>FORGOT PASSWORD?</Text>

        <Text style={styles.description}>
          Enter your registered email address. We'll send you a
          six-digit code to reset your password.
        </Text>

        <Text style={styles.label}>EMAIL ADDRESS</Text>

        <TextInput
          style={styles.input}
          placeholder="runner@example.com"
          placeholderTextColor="#777777"
          keyboardType="email-address"
          autoCapitalize="none"
          autoCorrect={false}
          value={email}
          onChangeText={setEmail}
          editable={!loading}
        />

        <TouchableOpacity
          style={[styles.button, loading && styles.disabledButton]}
          onPress={handleSendOtp}
          disabled={loading}
          activeOpacity={0.8}
        >
          {loading ? (
            <ActivityIndicator color="#0C0D0C" />
          ) : (
            <Text style={styles.buttonText}>SEND CODE →</Text>
          )}
        </TouchableOpacity>

        <TouchableOpacity
          onPress={onBackToLogin}
          disabled={loading}
          style={styles.backButton}
        >
          <Text style={styles.backText}>← BACK TO SIGN IN</Text>
        </TouchableOpacity>
      </ScrollView>

      <MessagePopup popup={popup} onClose={closePopup} />
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  ...passwordResetStyles,
  input: {
    backgroundColor: '#1B1D1B',
    borderColor: '#333633',
    borderWidth: 1,
    borderRadius: 12,
    paddingHorizontal: 16,
    paddingVertical: 16,
    color: '#FFFFFF',
    fontSize: 16,
    marginBottom: 20,
  },
});
