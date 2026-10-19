
import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

type EditableProfileFieldProps = {
  value: string;
  placeholder: string;
  maxLength: number;
  multiline?: boolean;
  isName?: boolean;
  onSave: (newValue: string) => Promise<void>;
};

export default function EditableProfileField({
  value,
  placeholder,
  maxLength,
  multiline = false,
  isName = false,
  onSave,
}: EditableProfileFieldProps) {
  const [editing, setEditing] = useState(false);
  const [input, setInput] = useState(value);
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    const newValue = input.trim();

    if (isName && newValue.length < 2) {
      Alert.alert('Invalid name', 'Name must be at least 2 characters.');
      return;
    }

    if (newValue.length > maxLength) {
      Alert.alert('Too long', `Maximum ${maxLength} characters allowed.`);
      return;
    }

    if (newValue === value) {
      setEditing(false);
      return;
    }

    setSaving(true);

    try {
      await onSave(newValue);
      setEditing(false);
    } catch (error) {
      Alert.alert(
        'Update failed',
        error instanceof Error
          ? error.message
          : 'Something went wrong.'
      );
    } finally {
      setSaving(false);
    }
  };

  if (editing) {
    return (
      <View style={styles.editor}>
        <TextInput
          style={[
            styles.input,
            multiline && styles.multilineInput,
          ]}
          value={input}
          onChangeText={setInput}
          maxLength={maxLength}
          multiline={multiline}
          textAlignVertical={multiline ? 'top' : 'center'}
          placeholder={placeholder}
          placeholderTextColor="#777D77"
          editable={!saving}
          autoFocus
        />

        {multiline && (
          <Text style={styles.counter}>
            {input.length}/{maxLength}
          </Text>
        )}

        <View style={styles.actions}>
          <TouchableOpacity
            style={styles.saveButton}
            onPress={handleSave}
            disabled={saving}
          >
            {saving ? (
              <ActivityIndicator color="#0C0D0C" />
            ) : (
              <Text style={styles.saveText}>SAVE</Text>
            )}
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.cancelButton}
            onPress={() => setEditing(false)}
            disabled={saving}
          >
            <Text style={styles.cancelText}>CANCEL</Text>
          </TouchableOpacity>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.displayRow}>
      <Text
        style={[
          styles.displayText,
          isName ? styles.nameText : styles.bioText,
        ]}
        numberOfLines={isName ? 1 : undefined}
        adjustsFontSizeToFit={isName}
        minimumFontScale={0.65}
      >
        {value || placeholder}
      </Text>

      <TouchableOpacity
        style={styles.pencilButton}
        onPress={() => {
          setInput(value);
          setEditing(true);
        }}
        accessibilityLabel={`Edit ${isName ? 'display name' : 'bio'}`}
      >
        <Ionicons
          name="pencil"
          size={18}
          color="#C6FF35"
        />
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  displayRow: {
    width: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    position: 'relative',
    marginTop: 12,
  },
  displayText: {
    textAlign: 'center',
    maxWidth: '75%',
  },
  nameText: {
    color: '#FFFFFF',
    fontSize: 25,
    fontWeight: '900',
  },
  bioText: {
    color: '#999E99',
    fontSize: 14,
    lineHeight: 22,
  },
  pencilButton: {
    position: 'absolute',
    right: 0,
    padding: 8,
  },
  editor: {
    width: '100%',
    marginTop: 12,
  },
  input: {
    backgroundColor: '#121512',
    borderWidth: 1,
    borderColor: '#C6FF35',
    borderRadius: 8,
    padding: 14,
    color: '#FFFFFF',
    fontSize: 16,
  },
  multilineInput: {
    minHeight: 100,
  },
  counter: {
    color: '#777D77',
    fontSize: 12,
    textAlign: 'right',
    marginTop: 6,
  },
  actions: {
    flexDirection: 'row',
    gap: 10,
    marginTop: 12,
  },
  saveButton: {
    flex: 1,
    backgroundColor: '#C6FF35',
    borderRadius: 8,
    padding: 13,
    alignItems: 'center',
  },
  saveText: {
    color: '#0C0D0C',
    fontWeight: '900',
  },
  cancelButton: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#555555',
    borderRadius: 8,
    padding: 13,
    alignItems: 'center',
  },
  cancelText: {
    color: '#FFFFFF',
    fontWeight: '800',
  },
});
