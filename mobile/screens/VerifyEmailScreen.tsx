
import React, { useState,useEffect  } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';

const API_URL = 'http://192.168.100.232:5246';

type Props = {
  email: string;
  onVerified: () => void;
  onBackToLogin: () => void;
  onRegisterAgain: () => void;
};

export default function VerifyEmailScreen({
  email,
  onVerified,
  onBackToLogin,    
  onRegisterAgain,
}: Props) {
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [resending, setResending] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
const [registrationExpired, setRegistrationExpired] = useState(false);
const [resendCountdown, setResendCountdown] = useState(60);


useEffect(() => {
  if (resendCountdown <= 0) return;

  const timer = setTimeout(() => {
    setResendCountdown((previous) =>
      Math.max(0, previous - 1)
    );
  }, 1000);

  return () => clearTimeout(timer);
}, [resendCountdown]);

const checkRegistrationExpired = (
  status: number,
  message?: string
) => {
  if (
    status === 410 ||
    (message ?? '')
      .toLowerCase()
      .includes('registration expired')
  ) {
    setRegistrationExpired(true);
    setCode('');
    setError('');
    setMessage('');
    return true;
  }

  return false;
};


  const handleVerify = async () => {
    if (loading) return;

    if (!/^[0-9]{6}$/.test(code)) {
      setError('Enter the six-digit code from your email.');
      return;
    }

    setError('');
    setMessage('');
    setLoading(true);

    try {
      const response = await fetch(
        `${API_URL}/api/auth/verify-email`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            email: email.trim(),
            code,
          }),
        }
      );

     
const data = await response.json().catch(() => null);

if (response.status === 429) {
  setError(
    data?.message ||
      'Too many requests. Please wait a minute before trying again.'
  );
  return;
}

if (!response.ok) {
  if (checkRegistrationExpired(response.status, data?.message)) {
    return;
  }

  setError(
    data?.message ||
      'Verification failed. Check your code.'
  );
  return;
}


      onVerified();
    } catch {
      setError(
        'Cannot connect to the server. Try again.'
      );
    } finally {
      setLoading(false);
    }
  };

  const handleResend = async () => {
    if (resending || loading || resendCountdown > 0) return;

    setCode('');
    setResendCountdown(60);
    setMessage('If verification is required, a new code will be sent. Check your inbox.');

    try {
      const response = await fetch(
        `${API_URL}/api/auth/resend-email-otp`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ email: email.trim() }),
        }
      );

      
     
const data = await response.json().catch(() => null);

if (response.status === 429) {
  setError(
    data?.message ||
      'Too many requests. Please wait a minute before trying again.'
  );
  return;
}

if (!response.ok) {
  if (checkRegistrationExpired(response.status, data?.message)) {
    return;
  }

  setError(
    data?.message || 'Unable to resend code.'
  );
  return;
}


      setCode('');
      setMessage(
        'If verification is required, a new code will be sent. Check your inbox.'
      );
    } catch {
      setError('Connection failed. Try again.');
    } finally {
      setResending(false);
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

      {registrationExpired ? (
        <View style={styles.expiredContainer}>
          <Text style={styles.expiredIcon}>⌛</Text>

          <Text style={styles.expiredTitle}>
            REGISTRATION{'\n'}
            <Text style={styles.accent}>EXPIRED.</Text>
          </Text>

          <Text style={styles.expiredDescription}>
            Your 24-hour email verification period has ended.
            Please register again to create your ILAQA account.
          </Text>

          <TouchableOpacity
            style={styles.button}
            onPress={onRegisterAgain}
          >
            <Text style={styles.buttonText}>
              REGISTER AGAIN →
            </Text>
          </TouchableOpacity>

          <TouchableOpacity onPress={onBackToLogin}>
            <Text style={styles.backText}>
              ← Back to Sign In
            </Text>
          </TouchableOpacity>
        </View>
      ) : (
        <>
          <View style={styles.heading}>
            <Text style={styles.title}>VERIFY YOUR</Text>
            <Text style={[styles.title, styles.accent]}>
              EMAIL.
            </Text>

            <Text style={styles.subtitle}>
              Enter the six-digit verification code sent to:
            </Text>

            <Text style={styles.email}>{email}</Text>
          </View>

          <Text style={styles.label}>
            VERIFICATION CODE
          </Text>

          <TextInput
            style={styles.input}
            placeholder="000000"
            placeholderTextColor="#777"
            keyboardType="number-pad"
            maxLength={6}
            autoCorrect={false}
            value={code}
            onChangeText={(value) => {
              setCode(value.replace(/[^0-9]/g, ''));
              setError('');
            }}
            textContentType="oneTimeCode"
          />

          <Text style={styles.hint}>
            Your code expires after 60 seconds.
          </Text>

          {error ? (
            <Text style={styles.error}>{error}</Text>
          ) : null}

          {message ? (
            <Text style={styles.success}>{message}</Text>
          ) : null}

          <TouchableOpacity
            style={[
              styles.button,
              loading && styles.disabled,
            ]}
            onPress={handleVerify}
            disabled={loading}
          >
            {loading ? (
              <ActivityIndicator color="#101310" />
            ) : (
              <Text style={styles.buttonText}>
                VERIFY EMAIL →
              </Text>
            )}
          </TouchableOpacity>

         
<TouchableOpacity
  style={[
    styles.resendButton,
    (loading || resending || resendCountdown > 0) &&
      styles.disabled,
  ]}
  onPress={handleResend}
  disabled={loading || resending || resendCountdown > 0}
>
  <Text style={styles.resendText}>
    {resending
      ? 'SENDING...'
      : resendCountdown > 0
        ? `RESEND CODE IN ${resendCountdown}s`
        : "DIDN'T RECEIVE THE CODE? RESEND"}
  </Text>
</TouchableOpacity>


          <TouchableOpacity onPress={onBackToLogin}>
            <Text style={styles.backText}>
              ← Back to Sign In
            </Text>
          </TouchableOpacity>
        </>
      )}
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
    marginBottom: 35,
  },
  title: {
    color: '#FFFFFF',
    fontSize: 39,
    fontWeight: '900',
    letterSpacing: -1.5,
    lineHeight: 46,
  },
  accent: {
    color: '#C6FF35',
  },
  subtitle: {
    color: '#999E99',
    fontSize: 14,
    marginTop: 18,
    lineHeight: 22,
  },
  email: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
    marginTop: 8,
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
    borderColor: '#C6FF35',
    borderRadius: 8,
    height: 64,
    paddingHorizontal: 16,
    fontSize: 27,
    fontWeight: '700',
    letterSpacing: 9,
    textAlign: 'center',
  },
  hint: {
    color: '#999E99',
    fontSize: 12,
    marginTop: 12,
  },
  error: {
    color: '#FF7777',
    fontSize: 13,
    marginTop: 18,
  },
  success: {
    color: '#C6FF35',
    fontSize: 13,
    marginTop: 18,
    lineHeight: 20,
  },
  button: {
    backgroundColor: '#C6FF35',
    height: 56,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 30,
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
  resendButton: {
    marginTop: 30,
    alignItems: 'center',
  },
  resendText: {
    color: '#C6FF35',
    fontWeight: '800',
    fontSize: 12,
    letterSpacing: 0.5,
  },
  backText: {
    color: '#999E99',
    textAlign: 'center',
    marginTop: 35,
    fontSize: 13,
  },


  
expiredContainer: {
  flex: 1,
  justifyContent: 'center',
  paddingTop: 100,
},
expiredIcon: {
  fontSize: 42,
  marginBottom: 24,
},
expiredTitle: {
  color: '#FFFFFF',
  fontSize: 39,
  fontWeight: '900',
  letterSpacing: -1.5,
  lineHeight: 46,
},
expiredDescription: {
  color: '#999E99',
  fontSize: 14,
  lineHeight: 23,
  marginTop: 22,
  marginBottom: 18,
},

});
