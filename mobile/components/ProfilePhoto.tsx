// Profile picture: the avatar button and its menu (view, change, remove),
// the full-screen viewer, and the preview shown before uploading.

import { useState } from 'react';
import {
  View,
  Text,
  Image,
  Modal,
  TouchableOpacity,
  ActivityIndicator,
  StyleSheet,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import AppPopup, { PopupMessage } from './AppPopup';
import { authFetch, errorMessage, readErrorMessage } from '../services/api';

const MAX_PHOTO_BYTES = 2 * 1024 * 1024;

type ProfilePhotoProps = {
  pictureUrl: string | null;
  onPictureChanged: (pictureUrl: string | null) => void;
  showNotice: (type: PopupMessage['type'], title: string, message: string) => void;
};

export default function ProfilePhoto({
  pictureUrl,
  onPictureChanged,
  showNotice,
}: ProfilePhotoProps) {
  const [uploadingPicture, setUploadingPicture] = useState(false);
  const [menuVisible, setMenuVisible] = useState(false);
  const [viewingPhoto, setViewingPhoto] = useState(false);
  const [confirmRemoveVisible, setConfirmRemoveVisible] = useState(false);
  // Picked and cropped, waiting for the runner to confirm the upload.
  const [selectedPhoto, setSelectedPhoto] =
    useState<ImagePicker.ImagePickerAsset | null>(null);

  const pickPhoto = async () => {
    if (uploadingPicture) return;

    try {
      const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (!permission.granted) {
        showNotice('warning', 'PERMISSION REQUIRED', 'Please allow access to your photos.');
        return;
      }

      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ['images'],
        allowsEditing: true,
        aspect: [1, 1],
        quality: 0.7,
      });

      if (result.canceled || !result.assets?.length) return;

      const image = result.assets[0];
      if (image.fileSize !== undefined && image.fileSize > MAX_PHOTO_BYTES) {
        showNotice('warning', 'IMAGE TOO LARGE', 'Please choose an image smaller than 2 MB.');
        return;
      }

      // Cropping only opens the preview. It does not upload.
      setSelectedPhoto(image);
    } catch (err) {
      showNotice('error', 'PHOTO SELECTION FAILED', errorMessage(err, 'Could not select photo.'));
    }
  };

  const uploadPhoto = async () => {
    if (!selectedPhoto || uploadingPicture) return;

    try {
      setUploadingPicture(true);

      const mimeType = selectedPhoto.mimeType || 'image/jpeg';
      const fileName =
        selectedPhoto.fileName ||
        (mimeType === 'image/png'
          ? 'profile.png'
          : mimeType === 'image/webp'
            ? 'profile.webp'
            : 'profile.jpg');

      // React Native uploads a file given as { uri, name, type }.
      const formData = new FormData();
      formData.append('file', {
        uri: selectedPhoto.uri,
        name: fileName,
        type: mimeType,
      } as any);

      const response = await authFetch('/api/runner/me/profile-picture', {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        throw new Error(
          await readErrorMessage(response, 'Unable to upload profile picture.')
        );
      }

      const data = await response.json();
      onPictureChanged(data.profilePictureUrl);
      setSelectedPhoto(null);
      showNotice('success', 'PHOTO UPDATED', 'Profile picture updated!');
    } catch (err) {
      showNotice('error', 'UPLOAD FAILED', errorMessage(err, 'Something went wrong.'));
    } finally {
      setUploadingPicture(false);
    }
  };

  const removePhoto = async () => {
    if (!pictureUrl || uploadingPicture) return;

    try {
      setUploadingPicture(true);

      const response = await authFetch('/api/runner/me/profile-picture', {
        method: 'DELETE',
      });

      if (!response.ok) {
        throw new Error(
          await readErrorMessage(response, 'Unable to remove profile picture.')
        );
      }

      onPictureChanged(null);
      showNotice('success', 'PHOTO REMOVED', 'Profile picture removed.');
    } catch (err) {
      showNotice('error', 'REMOVE FAILED', errorMessage(err, 'Something went wrong.'));
    } finally {
      setUploadingPicture(false);
    }
  };

  return (
    <>
      <TouchableOpacity
        style={styles.avatar}
        onPress={() => setMenuVisible(true)}
        disabled={uploadingPicture}
        accessibilityLabel="Change profile picture"
      >
        {uploadingPicture ? (
          <ActivityIndicator size="large" color="#C6FF35" />
        ) : pictureUrl ? (
          <Image source={{ uri: pictureUrl }} style={styles.profileImage} />
        ) : (
          <Ionicons name="person" size={40} color="#C6FF35" />
        )}

        {!uploadingPicture && (
          <View style={styles.cameraBadge}>
            <Ionicons name="camera" size={15} color="#0C0D0C" />
          </View>
        )}
      </TouchableOpacity>

      {/* Full-screen photo */}
      <Modal
        visible={viewingPhoto}
        transparent
        animationType="fade"
        onRequestClose={() => setViewingPhoto(false)}
      >
        <View style={styles.photoViewer}>
          <TouchableOpacity
            style={styles.closePhotoButton}
            onPress={() => setViewingPhoto(false)}
          >
            <Ionicons name="close" size={28} color="#FFFFFF" />
          </TouchableOpacity>

          {pictureUrl && (
            <Image
              source={{ uri: pictureUrl }}
              style={styles.fullProfileImage}
              resizeMode="contain"
            />
          )}
        </View>
      </Modal>

      {/* Confirm the cropped photo before uploading */}
      <Modal
        visible={selectedPhoto !== null}
        transparent
        animationType="fade"
        onRequestClose={() => {
          if (!uploadingPicture) setSelectedPhoto(null);
        }}
      >
        <View style={styles.previewOverlay}>
          <View style={styles.previewCard}>
            <Text style={styles.previewTitle}>PREVIEW PHOTO</Text>
            <Text style={styles.previewSubtitle}>
              Ready to update your profile picture?
            </Text>

            {selectedPhoto && (
              <Image
                source={{ uri: selectedPhoto.uri }}
                style={styles.previewImage}
                resizeMode="cover"
              />
            )}

            <TouchableOpacity
              style={styles.uploadPhotoButton}
              onPress={uploadPhoto}
              disabled={uploadingPicture}
            >
              {uploadingPicture ? (
                <ActivityIndicator color="#0C0D0C" />
              ) : (
                <Text style={styles.uploadPhotoText}>UPLOAD PHOTO</Text>
              )}
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.cancelPhotoButton}
              onPress={() => setSelectedPhoto(null)}
              disabled={uploadingPicture}
            >
              <Text style={styles.cancelPhotoText}>CANCEL</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

      {/* Profile picture options */}
      <Modal
        visible={menuVisible && !confirmRemoveVisible}
        transparent
        animationType="fade"
        onRequestClose={() => setMenuVisible(false)}
      >
        <View style={styles.menuOverlay}>
          <View style={styles.menuCard}>
            <View style={styles.menuHeader}>
              <Text style={styles.menuTitle}>PROFILE PICTURE</Text>

              <TouchableOpacity
                onPress={() => setMenuVisible(false)}
                accessibilityLabel="Close menu"
              >
                <Ionicons name="close" size={26} color="#FFFFFF" />
              </TouchableOpacity>
            </View>

            {pictureUrl && (
              <TouchableOpacity
                style={styles.menuOption}
                onPress={() => {
                  setMenuVisible(false);
                  setViewingPhoto(true);
                }}
              >
                <Ionicons name="eye-outline" size={22} color="#C6FF35" />
                <Text style={styles.menuOptionText}>View Photo</Text>
              </TouchableOpacity>
            )}

            <TouchableOpacity
              style={styles.menuOption}
              onPress={() => {
                setMenuVisible(false);
                pickPhoto();
              }}
            >
              <Ionicons name="image-outline" size={22} color="#C6FF35" />
              <Text style={styles.menuOptionText}>
                {pictureUrl ? 'Change Photo' : 'Upload Photo'}
              </Text>
            </TouchableOpacity>

            {pictureUrl && (
              <TouchableOpacity
                style={styles.menuOption}
                onPress={() => {
                  setMenuVisible(false);
                  setConfirmRemoveVisible(true);
                }}
              >
                <Ionicons name="trash-outline" size={22} color="#FF7777" />
                <Text style={[styles.menuOptionText, styles.removeText]}>
                  Remove Photo
                </Text>
              </TouchableOpacity>
            )}
          </View>
        </View>
      </Modal>

      <AppPopup
        visible={confirmRemoveVisible}
        type="confirm"
        title="REMOVE PROFILE PHOTO?"
        message="Are you sure you want to remove your profile picture?"
        confirmText="REMOVE"
        cancelText="CANCEL"
        loading={uploadingPicture}
        onClose={() => setConfirmRemoveVisible(false)}
        onConfirm={() => {
          setConfirmRemoveVisible(false);
          removePhoto();
        }}
      />
    </>
  );
}

const styles = StyleSheet.create({
  avatar: {
    width: 90,
    height: 90,
    borderRadius: 45,
    backgroundColor: '#303830',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 15,
    position: 'relative',
  },
  profileImage: {
    width: 90,
    height: 90,
    borderRadius: 45,
  },
  cameraBadge: {
    position: 'absolute',
    bottom: 0,
    right: 0,
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: '#C6FF35',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 2,
    borderColor: '#1C1E1C',
  },
  photoViewer: {
    flex: 1,
    backgroundColor: '#0C0D0C',
    justifyContent: 'center',
    alignItems: 'center',
  },
  closePhotoButton: {
    position: 'absolute',
    top: 55,
    right: 25,
    zIndex: 10,
  },
  fullProfileImage: {
    width: '100%',
    height: '75%',
  },
  previewOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.85)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  previewCard: {
    width: '100%',
    backgroundColor: '#1A1C1A',
    borderRadius: 20,
    padding: 24,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#333833',
  },
  previewTitle: {
    color: '#C6FF35',
    fontSize: 20,
    fontWeight: '800',
    letterSpacing: 1,
    marginBottom: 8,
  },
  previewSubtitle: {
    color: '#AAAAAA',
    fontSize: 13,
    textAlign: 'center',
    marginBottom: 24,
  },
  previewImage: {
    width: 220,
    height: 220,
    borderRadius: 110,
    borderWidth: 3,
    borderColor: '#C6FF35',
    marginBottom: 28,
  },
  uploadPhotoButton: {
    backgroundColor: '#C6FF35',
    width: '100%',
    paddingVertical: 15,
    borderRadius: 12,
    alignItems: 'center',
    marginBottom: 12,
  },
  uploadPhotoText: {
    color: '#0C0D0C',
    fontSize: 15,
    fontWeight: '800',
  },
  cancelPhotoButton: {
    width: '100%',
    paddingVertical: 14,
    alignItems: 'center',
  },
  cancelPhotoText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '600',
  },
  menuOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.8)',
    justifyContent: 'center',
    padding: 24,
  },
  menuCard: {
    backgroundColor: '#1C1E1C',
    borderRadius: 20,
    padding: 24,
    borderWidth: 1,
    borderColor: '#303330',
  },
  menuHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 20,
  },
  menuTitle: {
    color: '#C6FF35',
    fontSize: 19,
    fontWeight: '800',
    letterSpacing: 1,
  },
  menuOption: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
    paddingVertical: 17,
    borderBottomWidth: 1,
    borderBottomColor: '#303330',
  },
  menuOptionText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '600',
  },
  removeText: {
    color: '#FF7777',
  },
});
