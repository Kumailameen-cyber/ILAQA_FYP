
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

const API_URL = 'http://192.168.100.232:5246';

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
  const [error, setError] = useState('');

  const handleSendOtp = async () => {
    const normalizedEmail = email.trim().toLowerCase();

    if (!normalizedEmail) {
      setError('Please enter your email address.');
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalizedEmail)) {
      setError('Please enter a valid email address.');
      return;
    }

    setLoading(true);
    setError('');

    try {
      const response = await fetch(
        `${API_URL}/api/auth/forgot-password`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            email: normalizedEmail,
          }),
        }
      );

      
const data = await response.json().catch(() => null);

if (response.status === 429) {
  setError(
    'Too many requests. Please wait a minute before trying again.'
  );
  return;
}

if (!response.ok) {
  setError(
    data?.message || 'Unable to send code. Please try again.'
  );
  return;
}


      onOtpRequested(normalizedEmail);
    } catch {
      setError('Unable to connect to the server. Please try again.');
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
          onChangeText={(value) => {
            setEmail(value);
            setError('');
          }}
          editable={!loading}
        />

        {error ? (
          <Text style={styles.error}>{error}</Text>
        ) : null}

        <TouchableOpacity
          style={[
            styles.button,
            loading && styles.disabledButton,
          ]}
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
  error: {
    color: '#FF7474',
    fontSize: 13,
    marginBottom: 16,
  },
  button: {
    backgroundColor: '#C6FF35',
    paddingVertical: 18,
    borderRadius: 12,
    alignItems: 'center',
    marginTop: 8,
  },
  disabledButton: {
    opacity: 0.6,
  },
  buttonText: {
    color: '#0C0D0C',
    fontSize: 14,
    fontWeight: '900',
    letterSpacing: 1,
  },
  backButton: {
    alignItems: 'center',
    marginTop: 28,
    padding: 10,
  },
  backText: {
    color: '#C6FF35',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 1,
  },
});
