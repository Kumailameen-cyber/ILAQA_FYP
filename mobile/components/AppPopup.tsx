
import React from 'react';
import {
  Modal,
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

type PopupType = 'success' | 'error' | 'warning' | 'confirm';

type AppPopupProps = {
  visible: boolean;
  type: PopupType;
  title: string;
  message: string;
  onClose: () => void;
  onConfirm?: () => void;
  confirmText?: string;
  cancelText?: string;
  loading?: boolean;
};

export default function AppPopup({
  visible,
  type,
  title,
  message,
  onClose,
  onConfirm,
  confirmText = 'CONFIRM',
  cancelText = 'CANCEL',
  loading = false,
}: AppPopupProps) {
  const isConfirm = type === 'confirm';

  const color =
    type === 'error'
      ? '#FF7777'
      : type === 'warning'
        ? '#FFCC66'
        : '#C6FF35';

  const iconName: React.ComponentProps<
    typeof Ionicons
  >['name'] =
    type === 'success'
      ? 'checkmark-circle-outline'
      : type === 'error'
        ? 'close-circle-outline'
        : type === 'warning'
          ? 'warning-outline'
          : 'help-circle-outline';

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={() => {
        if (!loading) onClose();
      }}
    >
      <View style={styles.overlay}>
        <View style={styles.card}>
          <Ionicons
            name={iconName}
            size={46}
            color={color}
          />

          <Text style={styles.title}>{title}</Text>
          <Text style={styles.message}>{message}</Text>

          <View style={styles.actions}>
            {isConfirm && (
              <TouchableOpacity
                style={styles.cancelButton}
                onPress={onClose}
                disabled={loading}
              >
                <Text style={styles.cancelText}>
                  {cancelText}
                </Text>
              </TouchableOpacity>
            )}

            <TouchableOpacity
              style={[
                styles.confirmButton,
                { backgroundColor: color },
                loading && { opacity: 0.6 },
              ]}
              onPress={isConfirm ? onConfirm : onClose}
              disabled={loading}
            >
              {loading ? (
                <ActivityIndicator color="#0C0D0C" />
              ) : (
                <Text style={styles.confirmText}>
                  {isConfirm ? confirmText : 'OK'}
                </Text>
              )}
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.85)',
    justifyContent: 'center',
    paddingHorizontal: 24,
  },
  card: {
    backgroundColor: '#1C1E1C',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: '#303330',
    padding: 26,
    alignItems: 'center',
  },
  title: {
    color: '#FFFFFF',
    fontSize: 20,
    fontWeight: '900',
    marginTop: 16,
    textAlign: 'center',
  },
  message: {
    color: '#A5A9A5',
    fontSize: 14,
    lineHeight: 22,
    textAlign: 'center',
    marginTop: 12,
    marginBottom: 26,
  },
  actions: {
    flexDirection: 'row',
    gap: 12,
    width: '100%',
  },
  cancelButton: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#444944',
    borderRadius: 10,
    paddingVertical: 15,
    alignItems: 'center',
  },
  cancelText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '800',
  },
  confirmButton: {
    flex: 1,
    borderRadius: 10,
    paddingVertical: 15,
    alignItems: 'center',
    justifyContent: 'center',
  },
  confirmText: {
    color: '#0C0D0C',
    fontSize: 13,
    fontWeight: '900',
  },
});
