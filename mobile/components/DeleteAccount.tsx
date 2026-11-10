// Delete account: the button, the password prompt, the final confirmation,
// and the DELETE request.

import { useState } from 'react';
import {
  View,
  Text,
  Modal,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import AppPopup, { PopupMessage } from './AppPopup';
import PasswordInput from './PasswordInput';
import { authFetch, errorMessage, readErrorMessage } from '../services/api';

type DeleteAccountProps = {
  onAccountDeleted: () => Promise<void>;
  showNotice: (type: PopupMessage['type'], title: string, message: string) => void;
};

export default function DeleteAccount({
  onAccountDeleted,
  showNotice,
}: DeleteAccountProps) {
  const [modalVisible, setModalVisible] = useState(false);
  const [confirmVisible, setConfirmVisible] = useState(false);
  const [password, setPassword] = useState('');
  const [deleting, setDeleting] = useState(false);

  const canDelete = password.trim().length > 0 && !deleting;

  const closeModal = () => {
    setModalVisible(false);
    setPassword('');
  };

  const deleteMyAccount = async () => {
    if (deleting || !password.trim()) return;

    setDeleting(true);

    try {
      const response = await authFetch('/api/runner/me', {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password }),
      });

      if (!response.ok) {
        throw new Error(
          await readErrorMessage(response, 'Unable to delete account.')
        );
      }

      // Backend confirmed that the account was deleted.
      closeModal();

      // Clear local login tokens and return to Login.
      await onAccountDeleted();
    } catch (err) {
      showNotice(
        'error',
        'ACCOUNT DELETION FAILED',
        errorMessage(err, 'Something went wrong.')
      );
    } finally {
      setDeleting(false);
    }
  };

  return (
    <>
      <TouchableOpacity
        style={styles.deleteAccountButton}
        onPress={() => setModalVisible(true)}
      >
        <Ionicons name="trash-outline" size={20} color="#FF7777" />
        <Text style={styles.deleteAccountButtonText}>DELETE ACCOUNT</Text>
      </TouchableOpacity>

      {/* Ask for the password */}
      <Modal
        visible={modalVisible && !confirmVisible}
        transparent
        animationType="fade"
        onRequestClose={() => {
          if (!deleting) closeModal();
        }}
      >
        <View style={styles.deleteOverlay}>
          <View style={styles.deleteCard}>
            <View style={styles.deleteHeader}>
              <Text style={styles.deleteTitle}>DELETE ACCOUNT</Text>

              <TouchableOpacity disabled={deleting} onPress={closeModal}>
                <Ionicons name="close" size={26} color="#FFFFFF" />
              </TouchableOpacity>
            </View>

            <Text style={styles.deleteWarning}>
              This action is permanent. Your account and associated
              data will be deleted and cannot be recovered.
            </Text>

            <Text style={styles.deleteLabel}>
              ENTER YOUR PASSWORD TO CONFIRM
            </Text>

            <PasswordInput
              value={password}
              onChangeText={setPassword}
              placeholder="Current password"
              autoComplete="current-password"
              editable={!deleting}
            />

            <TouchableOpacity
              style={[styles.confirmDeleteButton, !canDelete && styles.disabled]}
              onPress={() => setConfirmVisible(true)}
              disabled={!canDelete}
            >
              {deleting ? (
                <ActivityIndicator color="#FFFFFF" />
              ) : (
                <Text style={styles.confirmDeleteText}>DELETE PERMANENTLY</Text>
              )}
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

      <AppPopup
        visible={confirmVisible}
        type="confirm"
        title="DELETE ACCOUNT?"
        message="This action is permanent. Your account and associated data cannot be recovered."
        confirmText="DELETE"
        cancelText="CANCEL"
        loading={deleting}
        onClose={() => setConfirmVisible(false)}
        onConfirm={() => {
          setConfirmVisible(false);
          deleteMyAccount();
        }}
      />
    </>
  );
}

const styles = StyleSheet.create({
  deleteAccountButton: {
    marginTop: 28,
    borderWidth: 1,
    borderColor: '#FF7777',
    borderRadius: 12,
    paddingVertical: 16,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    gap: 10,
  },
  deleteAccountButtonText: {
    color: '#FF7777',
    fontSize: 14,
    fontWeight: '800',
    letterSpacing: 1,
  },
  deleteOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.85)',
    justifyContent: 'center',
    padding: 24,
  },
  deleteCard: {
    backgroundColor: '#1C1E1C',
    borderRadius: 20,
    padding: 24,
    borderWidth: 1,
    borderColor: '#303330',
  },
  deleteHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 20,
  },
  deleteTitle: {
    color: '#FF7777',
    fontSize: 19,
    fontWeight: '800',
    letterSpacing: 1,
  },
  deleteWarning: {
    color: '#CCCCCC',
    fontSize: 14,
    lineHeight: 22,
    marginBottom: 24,
  },
  deleteLabel: {
    color: '#999E99',
    fontSize: 12,
    fontWeight: '800',
    marginBottom: 10,
  },
  confirmDeleteButton: {
    backgroundColor: '#B42323',
    borderRadius: 12,
    paddingVertical: 16,
    alignItems: 'center',
  },
  confirmDeleteText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '800',
    letterSpacing: 1,
  },
  disabled: {
    opacity: 0.5,
  },
});
