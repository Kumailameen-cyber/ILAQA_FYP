// Forgot password, step 2: enter the six-digit reset code. A correct code
// returns a reset token for the New Password screen.

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
import { useOtpCode } from '../hooks/useOtpCode';
import { postJson, TOO_MANY_REQUESTS_MESSAGE } from '../services/api';

type Props = {
  email: string;
  onVerified: (resetToken: string) => void;
  onBack: () => void;
};

export default function VerifyResetOtpScreen({
  email,
  onVerified,
  onBack,
}: Props) {
  const { code, setCode, isCodeComplete, resendCountdown, restartCountdown } =
    useOtpCode();
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const { popup, showPopup, closePopup } = usePopup();

  const showError = (message: string) =>
    showPopup('error', 'VERIFICATION ERROR', message);

  const handleVerify = async () => {
    if (loading || resending) return;

    if (!isCodeComplete) {
      showError('Please enter a valid six-digit code.');
      return;
    }

    setLoading(true);
    closePopup();

    try {
      const { response, data } = await postJson('/api/auth/verify-reset-otp', {
        email,
        code,
      });

      if (response.status === 429) {
        showError(data?.message || TOO_MANY_REQUESTS_MESSAGE);
        return;
      }

      if (!response.ok) {
        showError(data?.message || 'Unable to verify code. Try again.');
        return;
      }

      if (
        data?.verified !== true ||
        typeof data?.resetToken !== 'string' ||
        !data.resetToken
      ) {
        showError('Verification was not confirmed. Please try again.');
        return;
      }

      // Pass the secure 15-minute reset token, not the six-digit OTP.
      onVerified(data.resetToken);
    } catch {
      showError('Unable to connect to the server.');
    } finally {
      setLoading(false);
    }
  };

  const handleResend = async () => {
    if (resending || loading || resendCountdown > 0) return;

    setResending(true);
    closePopup();

    try {
      const { response, data } = await postJson('/api/auth/forgot-password', {
        email,
      });

      if (response.status === 429) {
        showError(data?.message || TOO_MANY_REQUESTS_MESSAGE);
        return;
      }

      if (!response.ok) {
        showError(data?.message || 'Unable to request a new code.');
        return;
      }

      restartCountdown();
      // Keep the entered code: it may still be valid.
      showPopup(
        'success',
        'CODE REQUESTED',
        'If a password reset is available for this email, a code will be sent. Check your inbox.'
      );
    } catch {
      showError('Unable to connect to the server.');
    } finally {
      setResending(false);
    }
  };

  const busy = loading || resending;

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
        <Text style={styles.heading}>VERIFY YOUR CODE</Text>

        <Text style={styles.description}>
          Enter the six-digit password reset code sent to:
        </Text>
        <Text style={styles.email}>{email}</Text>

        <Text style={styles.otpHint}>
          If you recently requested a code for this email, your previous code
          may still be valid for up to 60 seconds. Check your inbox before
          requesting another code.
        </Text>

        <Text style={styles.label}>VERIFICATION CODE</Text>
        <TextInput
          style={styles.input}
          placeholder="000000"
          placeholderTextColor="#777777"
          keyboardType="number-pad"
          maxLength={6}
          value={code}
          onChangeText={setCode}
          editable={!busy}
          autoFocus
          textAlign="center"
          textContentType="oneTimeCode"
        />

        <TouchableOpacity
          style={[styles.button, busy && styles.disabledButton]}
          onPress={handleVerify}
          disabled={busy}
          activeOpacity={0.8}
        >
          {loading ? (
            <ActivityIndicator color="#0C0D0C" />
          ) : (
            <Text style={styles.buttonText}>VERIFY CODE →</Text>
          )}
        </TouchableOpacity>

        <TouchableOpacity
          style={[
            styles.resendButton,
            (busy || resendCountdown > 0) && styles.disabledButton,
          ]}
          onPress={handleResend}
          disabled={busy || resendCountdown > 0}
        >
          <Text style={styles.resendText}>
            {resending
              ? 'REQUESTING CODE...'
              : resendCountdown > 0
                ? `RESEND CODE IN ${resendCountdown}s`
                : 'RESEND CODE'}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.backButton}
          onPress={onBack}
          disabled={busy}
        >
          <Text style={styles.backText}>← CHANGE EMAIL</Text>
        </TouchableOpacity>
      </ScrollView>

      <MessagePopup popup={popup} onClose={closePopup} />
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  ...passwordResetStyles,
  heading: {
    color: '#FFFFFF',
    fontSize: 30,
    fontWeight: '900',
    marginBottom: 14,
  },
  description: {
    color: '#A0A0A0',
    fontSize: 15,
    lineHeight: 23,
  },
  email: {
    color: '#C6FF35',
    fontSize: 15,
    fontWeight: '700',
    marginTop: 8,
    marginBottom: 35,
  },
  otpHint: {
    color: '#A0A0A0',
    fontSize: 12,
    lineHeight: 19,
    marginTop: -18,
    marginBottom: 24,
  },
  input: {
    backgroundColor: '#1B1D1B',
    borderColor: '#333633',
    borderWidth: 1,
    borderRadius: 12,
    paddingVertical: 16,
    color: '#FFFFFF',
    fontSize: 28,
    fontWeight: '700',
    letterSpacing: 10,
    marginBottom: 20,
  },
  resendButton: {
    alignItems: 'center',
    marginTop: 28,
    padding: 10,
  },
  resendText: {
    color: '#C6FF35',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 1,
  },
  backButton: {
    alignItems: 'center',
    marginTop: 14,
    padding: 10,
  },
  backText: {
    color: '#A0A0A0',
    fontSize: 12,
    fontWeight: '700',
  },
});
