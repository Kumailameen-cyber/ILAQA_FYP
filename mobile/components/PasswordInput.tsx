
import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
} from 'react-native';

type PasswordInputProps = {
  value: string;
  onChangeText: (text: string) => void;
  placeholder?: string;
  editable?: boolean;
  autoComplete?: 'password' | 'new-password' | 'current-password';
  onBlur?: () => void;
};

export default function PasswordInput({
  value,
  onChangeText,
  placeholder = 'Enter your password',
  editable = true,
  autoComplete,
  onBlur,
}: PasswordInputProps) {
  const [showPassword, setShowPassword] = useState(false);

  return (
    <View style={styles.container}>
      <TextInput
        style={styles.input}
        placeholder={placeholder}
        placeholderTextColor="#777"
        secureTextEntry={!showPassword}
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete={autoComplete}
        value={value}
        onChangeText={onChangeText}
        editable={editable}
        onBlur={onBlur}
      />

      <TouchableOpacity
        style={styles.eyeButton}
        onPress={() => setShowPassword((current) => !current)}
        disabled={!editable}
        accessibilityRole="button"
        accessibilityLabel={
          showPassword ? 'Hide password' : 'Show password'
        }
      >
        <Text style={styles.eyeText}>
          {showPassword ? 'HIDE' : 'SHOW'}
        </Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#1C1E1C',
    borderWidth: 1,
    borderColor: '#303330',
    borderRadius: 8,
    height: 54,
    marginBottom: 22,
    flexDirection: 'row',
    alignItems: 'center',
  },
  input: {
    flex: 1,
    color: '#FFFFFF',
    paddingHorizontal: 16,
    fontSize: 15,
    height: '100%',
  },
  eyeButton: {
    paddingHorizontal: 16,
    height: '100%',
    justifyContent: 'center',
  },
  eyeText: {
    color: '#C6FF35',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 1,
  },
});
