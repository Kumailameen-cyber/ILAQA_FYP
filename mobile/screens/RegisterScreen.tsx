
import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  ScrollView,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';

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

  const handleRegister = async () => {
    if (loading) return;

    if (
      !username.trim() ||
      !displayName.trim() ||
      !email.trim() ||
      !password
    ) {
      Alert.alert('Missing details', 'Please fill in all fields.');
      return;
    }

    if (username.trim().length < 3 || username.trim().length > 30) {
      Alert.alert(
        'Invalid username',
        'Username must be 3–30 characters.'
      );
      return;
    }

    if (displayName.trim().length < 2 || displayName.trim().length > 50) {
      Alert.alert(
        'Invalid name',
        'Display name must be 2–50 characters.'
      );
      return;
    }

    if (password.length < 8) {
      Alert.alert(
        'Invalid password',
        'Password must be at least 8 characters.'
      );
      return;
    }

    setLoading(true);

    try {
      const response = await fetch(
        'http://192.168.100.232:5246/api/auth/register',
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

      const data = await response.json();

      if (response.ok) {
  const registeredEmail = email.trim();

  setUsername('');
  setDisplayName('');
  setEmail('');
  setPassword('');

  onRegistrationSuccess(registeredEmail);
} else {
        const errors = data.errors
          ? Object.values(data.errors).flat().join('\n')
          : data.message || data.title || 'Registration failed.';

        Alert.alert('Registration failed', String(errors));
      }
    } catch (error) {
      Alert.alert(
        'Connection error',
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

        <Text style={styles.label}>USERNAME</Text>
        <TextInput
          style={styles.input}
          placeholder="runner01"
          placeholderTextColor="#777"
          autoCapitalize="none"
          autoCorrect={false}
          value={username}
          onChangeText={setUsername}
        />

        <Text style={styles.label}>DISPLAY NAME</Text>
        <TextInput
          style={styles.input}
          placeholder="Your name"
          placeholderTextColor="#777"
          value={displayName}
          onChangeText={setDisplayName}
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
        />

        <Text style={styles.label}>PASSWORD</Text>
        <TextInput
          style={styles.input}
          placeholder="At least 8 characters"
          placeholderTextColor="#777"
          secureTextEntry
          value={password}
          onChangeText={setPassword}
          autoCapitalize="none"
autoCorrect={false}
        />

        <TouchableOpacity
          style={[
            styles.button,
            loading && { opacity: 0.6 },
          ]}
          onPress={handleRegister}
          disabled={loading}
        >
          {loading ? (
            <ActivityIndicator color="#101310" />
          ) : (
            <Text style={styles.buttonText}>
              CREATE ACCOUNT  →
            </Text>
          )}
        </TouchableOpacity>

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
