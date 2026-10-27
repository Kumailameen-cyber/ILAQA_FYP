import React, { useEffect, useState } from 'react';
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
  onVerified: (resetToken: string) => void;
  onBack: () => void;
};

type Popup = {
  type: 'error' | 'warning' | 'success';
  title: string;
  message: string;
};

export default function VerifyResetOtpScreen({
  email,
  onVerified,
  onBack,
}: Props) {
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const [resendCountdown, setResendCountdown] = useState(60);
  const [popup, setPopup] = useState<Popup | null>(null);

  const showError = (message: string) => {
    setPopup({ type: 'error', title: 'VERIFICATION ERROR', message });
  };

  const showSuccess = (message: string) => {
    setPopup({ type: 'success', title: 'CODE REQUESTED', message });
  };

  useEffect(() => {
    if (resendCountdown <= 0) return;

    const timer = setTimeout(() => {
      setResendCountdown((previous) => Math.max(0, previous - 1));
    }, 1000);

    return () => clearTimeout(timer);
  }, [resendCountdown]);

  const handleVerify = async () => {
    if (loading || resending) return;

    if (!/^[0-9]{6}$/.test(code)) {
      showError('Please enter a valid six-digit code.');
      return;
    }

    setLoading(true);
    setPopup(null);

    try {
      const response = await fetch(`${API_URL}/api/auth/verify-reset-otp`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, code }),
      });

      const data = await response.json().catch(() => null);

      if (response.status === 429) {
        showError(
          data?.message ||
            'Too many requests. Please wait a minute before trying again.'
        );
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

// Pass the secure 15-minute reset token,
// not the original six-digit OTP.
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
    setPopup(null);

    try {
      const response = await fetch(`${API_URL}/api/auth/forgot-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      });

      const data = await response.json().catch(() => null);

      if (response.status === 429) {
        showError(
          data?.message ||
            'Too many requests. Please wait a minute before trying again.'
        );
        return;
      }

      if (!response.ok) {
        showError(data?.message || 'Unable to request a new code.');
        return;
      }

      setResendCountdown(60);
      // Keep the entered code: it may still be valid.
      showSuccess(
        'If a password reset is available for this email, a code will be sent. Check your inbox.'
      );
    } catch {
      showError('Unable to connect to the server.');
    } finally {
      setResending(false);
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
          onChangeText={(value) => setCode(value.replace(/[^0-9]/g, ''))}
          editable={!loading && !resending}
          autoFocus
          textAlign="center"
          textContentType="oneTimeCode"
        />

        <TouchableOpacity
          style={[
            styles.button,
            (loading || resending) && styles.disabledButton,
          ]}
          onPress={handleVerify}
          disabled={loading || resending}
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
            (loading || resending || resendCountdown > 0) &&
              styles.disabledButton,
          ]}
          onPress={handleResend}
          disabled={loading || resending || resendCountdown > 0}
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
          disabled={loading || resending}
        >
          <Text style={styles.backText}>← CHANGE EMAIL</Text>
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
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },
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
  label: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 1.5,
    marginBottom: 10,
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
  otpHint: {
    color: '#A0A0A0',
    fontSize: 12,
    lineHeight: 19,
    marginTop: -18,
    marginBottom: 24,
  },
});
