// Forgot password, step 3: choose a new password with the reset token, or
// cancel the reset and go back to Sign In.

import { useState } from 'react';
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
import { MessagePopup, usePopup } from '../components/AppPopup';
import { passwordResetStyles } from '../components/authStyles';
import {
  errorMessage,
  postJson,
  TOO_MANY_REQUESTS_MESSAGE,
} from '../services/api';

type Props = {
  email: string;
  resetToken: string;
  onResetSuccess: () => void;
  onResetCancelled: () => void;
};

export default function NewPasswordScreen({
  email,
  resetToken,
  onResetSuccess,
  onResetCancelled,
}: Props) {
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const { popup, showPopup, closePopup } = usePopup();

  const busy = loading || cancelling;

  const showError = (message: string, title = 'SOMETHING WENT WRONG') =>
    showPopup('error', title, message);

  // Tells the server to drop the reset token, then returns to Sign In.
  const handleBackToLogin = async () => {
    if (busy) return;

    setCancelling(true);
    closePopup();

    try {
      const { response, data } = await postJson(
        '/api/auth/cancel-password-reset',
        { email, resetToken }
      );

      if (!response.ok) {
        if (response.status === 429) {
          throw new Error(TOO_MANY_REQUESTS_MESSAGE);
        }
        throw new Error(data?.message || 'Password reset cancellation failed.');
      }

      onResetCancelled();
    } catch (error) {
      showError(
        errorMessage(error, 'Unable to cancel password reset. Please try again.'),
        'CANNOT GO BACK'
      );
    } finally {
      setCancelling(false);
    }
  };

  const handleResetPassword = async () => {
    if (busy) return;

    if (!newPassword || !confirmPassword) {
      showError('Please fill in both password fields.', 'MISSING PASSWORD');
      return;
    }

    if (newPassword !== confirmPassword) {
      showError('Passwords do not match.', 'PASSWORD MISMATCH');
      return;
    }

    setLoading(true);
    closePopup();

    try {
      const { response, data } = await postJson('/api/auth/reset-password', {
        email,
        resetToken,
        newPassword,
      });

      if (response.status === 429) {
        showError(data?.message || TOO_MANY_REQUESTS_MESSAGE, 'TOO MANY REQUESTS');
        return;
      }

      if (!response.ok) {
        const details = Array.isArray(data?.errors) ? data.errors.join('\n') : '';
        showError(
          details || data?.message || 'Unable to reset password. Please try again.',
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
        <NewPasswordField
          placeholder="Enter new password"
          value={newPassword}
          onChangeText={setNewPassword}
          disabled={busy}
        />

        <Text style={styles.label}>CONFIRM PASSWORD</Text>
        <NewPasswordField
          placeholder="Confirm new password"
          value={confirmPassword}
          onChangeText={setConfirmPassword}
          disabled={busy}
        />

        <Text style={styles.hint}>
          Use a strong password that meets the registration requirements.
        </Text>

        <TouchableOpacity
          style={[styles.button, busy && styles.disabledButton]}
          onPress={handleResetPassword}
          disabled={busy}
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
          disabled={busy}
        >
          {cancelling ? (
            <ActivityIndicator color="#C6FF35" />
          ) : (
            <Text style={styles.backText}>← BACK TO LOGIN</Text>
          )}
        </TouchableOpacity>
      </ScrollView>

      <MessagePopup popup={popup} onClose={closePopup} />
    </KeyboardAvoidingView>
  );
}

// Password box with its own SHOW/HIDE toggle.
function NewPasswordField({
  placeholder,
  value,
  onChangeText,
  disabled,
}: {
  placeholder: string;
  value: string;
  onChangeText: (text: string) => void;
  disabled: boolean;
}) {
  const [visible, setVisible] = useState(false);

  return (
    <View style={styles.inputContainer}>
      <TextInput
        style={styles.passwordInput}
        placeholder={placeholder}
        placeholderTextColor="#777777"
        secureTextEntry={!visible}
        autoCapitalize="none"
        autoCorrect={false}
        value={value}
        onChangeText={onChangeText}
        editable={!disabled}
        autoComplete="new-password"
      />
      <TouchableOpacity
        onPress={() => setVisible((previous) => !previous)}
        style={styles.visibilityButton}
        disabled={disabled}
      >
        <Text style={styles.visibilityText}>{visible ? 'HIDE' : 'SHOW'}</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  ...passwordResetStyles,
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
});
